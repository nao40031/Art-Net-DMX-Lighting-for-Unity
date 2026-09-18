/*!
 * Copyright (c) 2026 Oshino
 *
 * Released under the MIT license.
 * see https://opensource.org/licenses/MIT
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ArtNet.Runtime
{
    internal sealed class VlbBeamAdapter
    {
        // VLB HD Cookie scale is expressed in its own projection space. A value of
        // 1.0 matches the URP Spot Light cone; 0.5 made the gobo projection too small.
        private const float CookieScaleToMatchUnitySpotLight = 1.0f;

        private readonly List<Entry> _entries = new();
        private readonly HashSet<Light> _knownLights = new();
        private bool _warnedMissingVlb;

        public struct Overrides
        {
            public bool overrideHdIntensityMultiplier;
            public float hdIntensityMultiplier;
            public bool overrideSdIntensityMultiplier;
            public float sdIntensityMultiplier;
            public bool overrideHdrpExposureWeight;
            public float hdrpExposureWeight;
        }

        private sealed class Entry
        {
            public Light light;
            public Component hd;
            public Component sd;
            public Component cookieHd;
            public bool hasCookieHdBaseline;
            public bool cookieHdEnabled;
            public object cookieHdTexture;
            public object cookieHdChannel;
            public float cookieHdContribution;
            public float cookieHdRotation;
            public Vector2 cookieHdTranslation;
            public Vector2 cookieHdScale;
            public bool irisHdApplied;
            public bool irisSdApplied;
            public bool irisSdOriginalFromLight;
            public float irisSdOriginalAngle;
            public float irisSdOriginalRadius;
        }

        public static bool IsVlbAvailable => VlbReflection.IsAvailable;

        public static Component GetHd(GameObject gameObject)
        {
            return VlbReflection.GetComponent(gameObject, VlbReflection.HdType);
        }

        public static Component GetCookieHd(GameObject gameObject)
        {
            return VlbReflection.GetComponent(gameObject, VlbReflection.CookieHdType);
        }

        public static Component EnsureHd(GameObject gameObject)
        {
            return VlbReflection.EnsureComponent(gameObject, VlbReflection.HdType);
        }

        public static Component EnsureCookieHd(GameObject gameObject)
        {
            return VlbReflection.EnsureComponent(gameObject, VlbReflection.CookieHdType);
        }

        public static Vector2 GetCookieScale(Component cookieHd)
        {
            return VlbReflection.GetVector2(cookieHd, "scale", Vector2.one);
        }

        public static void Disable(Component component)
        {
            if (component is Behaviour behaviour)
                behaviour.enabled = false;
        }

        public static void ApplyPrismHd(Component hd, Light light, Component template, Overrides overrides)
        {
            if (hd == null)
                return;

            if (hd is Behaviour behaviour)
                behaviour.enabled = true;

            if (template != null && template != hd)
                VlbReflection.CopyHdProps(hd, template);

            VlbReflection.SetBool(hd, "useIntensityFromAttachedLightSpot", true);
            VlbReflection.SetBool(hd, "useSpotAngleFromAttachedLightSpot", true);
            VlbReflection.SetBool(hd, "useFallOffEndFromAttachedLightSpot", true);
            VlbReflection.SetBool(hd, "colorFromLight", true);

            if (overrides.overrideHdIntensityMultiplier)
                VlbReflection.SetFloat(hd, "intensityMultiplier", Mathf.Max(0f, overrides.hdIntensityMultiplier));
            if (overrides.overrideHdrpExposureWeight)
                VlbReflection.SetFloat(hd, "hdrpExposureWeight", Mathf.Clamp01(overrides.hdrpExposureWeight));

            VlbReflection.Invoke(hd, "AssignPropertiesFromAttachedSpotLight");
            VlbReflection.Invoke(hd, "UpdateAfterManualPropertyChange");
        }

        public static void ApplyPrismCookieHd(Component cookieHd, bool hasCookie, Texture cookie, float contribution, Vector2 scale)
        {
            if (cookieHd == null)
                return;

            if (cookieHd is Behaviour behaviour)
                behaviour.enabled = hasCookie;

            VlbReflection.SetObject(cookieHd, "cookieTexture", hasCookie ? cookie : null);
            if (hasCookie)
                VlbReflection.SetEnum(cookieHd, "channel", "RGBA", 3);
            VlbReflection.SetFloat(cookieHd, "contribution", hasCookie ? Mathf.Clamp01(contribution) : 0f);
            VlbReflection.SetFloat(cookieHd, "rotation", 0f);
            VlbReflection.SetVector2(cookieHd, "translation", Vector2.zero);
            VlbReflection.SetVector2(cookieHd, "scale", scale);
        }

        public void Apply(IReadOnlyList<Light> lights, FixtureRenderState state, bool syncCookie, Overrides overrides, UnityEngine.Object logContext)
        {
            SyncEntries(lights);

            bool applied = false;
            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (entry.light == null)
                    continue;

                RefreshComponents(entry);

                if (entry.hd != null)
                {
                    ApplyHd(entry, state, syncCookie, overrides);
                    applied = true;
                    continue;
                }

                if (entry.sd != null)
                {
                    ApplySd(entry, state, overrides);
                    applied = true;
                }
            }

            if (!applied && !_warnedMissingVlb)
            {
                Debug.LogWarning("[DMX Fixture Component] BeamRenderMode is Volumetric Light Beam, but no VolumetricLightBeamHD/SD component was found on target lights. If VLB is not installed, this mode is ignored.", logContext);
                _warnedMissingVlb = true;
            }
        }

        public void Disable()
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                Disable(entry.hd);
                RestoreSdIris(entry);
                Disable(entry.sd);
                Disable(entry.cookieHd);
            }
        }

        public void Disable(IReadOnlyList<Light> lights)
        {
            SyncEntries(lights);
            for (int i = 0; i < _entries.Count; i++)
                RefreshComponents(_entries[i]);

            Disable();
        }

        private void SyncEntries(IReadOnlyList<Light> lights)
        {
            if (lights == null)
            {
                RestoreIrisEntries();
                _entries.Clear();
                _knownLights.Clear();
                return;
            }

            bool changed = lights.Count != _knownLights.Count;
            if (!changed)
            {
                for (int i = 0; i < lights.Count; i++)
                {
                    var light = lights[i];
                    if (light != null && !_knownLights.Contains(light))
                    {
                        changed = true;
                        break;
                    }
                }
            }

            if (!changed)
                return;

            RestoreIrisEntries();
            _entries.Clear();
            _knownLights.Clear();
            for (int i = 0; i < lights.Count; i++)
            {
                var light = lights[i];
                if (light == null || !_knownLights.Add(light))
                    continue;

                _entries.Add(new Entry
                {
                    light = light,
                });
            }

            _warnedMissingVlb = false;
        }

        private void RestoreIrisEntries()
        {
            foreach (var entry in _entries)
            {
                RestoreSdIris(entry);
                if (entry.irisHdApplied) RestoreCookieHdBaseline(entry);
            }
        }

        private static void RefreshComponents(Entry entry)
        {
            if (entry.light == null)
                return;

            if (entry.hd == null)
                entry.hd = GetHd(entry.light.gameObject);
            if (entry.sd == null)
                entry.sd = VlbReflection.GetComponent(entry.light.gameObject, VlbReflection.SdType);
            if (entry.cookieHd == null)
                entry.cookieHd = GetCookieHd(entry.light.gameObject);

            if (entry.cookieHd != null && !entry.hasCookieHdBaseline)
            {
                entry.cookieHdEnabled = entry.cookieHd is Behaviour behaviour && behaviour.enabled;
                entry.cookieHdTexture = VlbReflection.GetObject(entry.cookieHd, "cookieTexture");
                entry.cookieHdChannel = VlbReflection.GetObject(entry.cookieHd, "channel");
                entry.cookieHdContribution = VlbReflection.GetFloat(entry.cookieHd, "contribution", 0f);
                entry.cookieHdRotation = VlbReflection.GetFloat(entry.cookieHd, "rotation", 0f);
                entry.cookieHdTranslation = VlbReflection.GetVector2(entry.cookieHd, "translation", Vector2.zero);
                entry.cookieHdScale = VlbReflection.GetVector2(entry.cookieHd, "scale", Vector2.one);
                entry.hasCookieHdBaseline = true;
            }
        }

        private static void ApplyHd(Entry entry, FixtureRenderState state, bool syncCookie, Overrides overrides)
        {
            ApplyPrismHd(entry.hd, entry.light, null, overrides);
            Disable(entry.sd);

            if (state.irisEnabled && entry.cookieHd == null)
            {
                entry.cookieHd = EnsureCookieHd(entry.light.gameObject);
                RefreshComponents(entry);
            }

            if (entry.cookieHd == null)
                return;

            if (state.irisEnabled)
            {
                // The Light already carries the post-gobo iris cookie, in the light's projection space.
                entry.irisHdApplied = true;
                ApplyPrismCookieHd(entry.cookieHd, entry.light.cookie != null, entry.light.cookie, 1f, Vector2.one);
                return;
            }

            entry.irisHdApplied = false;

            if (!syncCookie)
            {
                RestoreCookieHdBaseline(entry);
                return;
            }

            bool hasCookie = syncCookie && state.goboEnabled && state.goboTexture != null;
            float prismGoboScale = state.vlbPrismGoboScale > 0f
                ? Mathf.Clamp(state.vlbPrismGoboScale, 0.1f, 3f)
                : 1f;
            float visualScale = state.prismEnabled ? prismGoboScale : 1f;
            float cookieScale = CookieScaleToMatchUnitySpotLight / visualScale;
            ApplyPrismCookieHd(entry.cookieHd, hasCookie, state.goboTexture, state.lightDimmer01, hasCookie ? Vector2.one * cookieScale : Vector2.one);
            if (hasCookie)
                VlbReflection.SetFloat(entry.cookieHd, "rotation", state.syncLightGoboRotationToDmx ? state.goboRotationDeg : 0f);
            if (hasCookie)
                VlbReflection.SetVector2(entry.cookieHd, "translation", state.syncLightGoboRotationToDmx ? state.goboOffsetUv : Vector2.zero);
        }

        private static void RestoreCookieHdBaseline(Entry entry)
        {
            if (entry.cookieHd == null || !entry.hasCookieHdBaseline)
                return;

            if (entry.cookieHd is Behaviour behaviour)
                behaviour.enabled = entry.cookieHdEnabled;
            VlbReflection.SetObject(entry.cookieHd, "cookieTexture", entry.cookieHdTexture);
            VlbReflection.SetObject(entry.cookieHd, "channel", entry.cookieHdChannel);
            VlbReflection.SetFloat(entry.cookieHd, "contribution", entry.cookieHdContribution);
            VlbReflection.SetFloat(entry.cookieHd, "rotation", entry.cookieHdRotation);
            VlbReflection.SetVector2(entry.cookieHd, "translation", entry.cookieHdTranslation);
            VlbReflection.SetVector2(entry.cookieHd, "scale", entry.cookieHdScale);
            VlbReflection.Invoke(entry.cookieHd, "UpdateAfterManualPropertyChange");
        }

        private static void ApplySd(Entry entry, FixtureRenderState state, Overrides overrides)
        {
            if (entry.sd is Behaviour behaviour)
                behaviour.enabled = true;
            Disable(entry.cookieHd);

            // SD uses the native VLB SD property names. The HD-specific
            // use*FromAttachedLightSpot properties are not the SD API and therefore
            // do not make SD intensity follow the DMX-driven Light.
            VlbReflection.SetBool(entry.sd, "colorFromLight", true);
            VlbReflection.SetBool(entry.sd, "intensityFromLight", true);
            VlbReflection.SetBool(entry.sd, "spotAngleFromLight", true);
            VlbReflection.SetBool(entry.sd, "fallOffEndFromLight", true);
            VlbReflection.SetObject(entry.sd, "color", entry.light.color);

            if (overrides.overrideSdIntensityMultiplier)
                VlbReflection.SetFloat(entry.sd, "intensityMultiplier", Mathf.Max(0f, overrides.sdIntensityMultiplier));

            // Keep the runtime values in sync after changing the SD properties above.
            VlbReflection.Invoke(entry.sd, "UpdateAfterManualPropertyChange");
            // SD has no cookie support. Its native homogeneous beam can still represent the
            // circular aperture geometrically, without changing the Light or projected gobo.
            if (state.irisEnabled)
            {
                if (!entry.irisSdApplied)
                {
                    entry.irisSdOriginalFromLight = VlbReflection.GetObject(entry.sd, "spotAngleFromLight") is bool fromLight && fromLight;
                    entry.irisSdOriginalAngle = VlbReflection.GetFloat(entry.sd, "spotAngle", entry.light.spotAngle);
                    entry.irisSdOriginalRadius = VlbReflection.GetFloat(entry.sd, "coneRadiusStart", 0f);
                    entry.irisSdApplied = true;
                }
                VlbReflection.SetBool(entry.sd, "spotAngleFromLight", false);
                float baseAngle = entry.irisSdOriginalFromLight
                    ? entry.light.spotAngle * VlbReflection.GetFloat(entry.sd, "spotAngleMultiplier", 1f)
                    : entry.irisSdOriginalAngle;
                float angle = 2f * Mathf.Atan(Mathf.Tan(baseAngle * Mathf.Deg2Rad * 0.5f) * state.irisShape.x) * Mathf.Rad2Deg;
                VlbReflection.SetFloat(entry.sd, "spotAngle", Mathf.Max(0.1f, angle));
                VlbReflection.SetFloat(entry.sd, "coneRadiusStart", entry.irisSdOriginalRadius * state.irisShape.x);
                if (state.irisShape.x <= 0.000001f && entry.sd is Behaviour closedBeam) closedBeam.enabled = false;
            }
            else if (entry.irisSdApplied)
                RestoreSdIris(entry);
            VlbReflection.Invoke(entry.sd, "UpdateAfterManualPropertyChange");
        }

        private static void RestoreSdIris(Entry entry)
        {
            if (!entry.irisSdApplied) return;
            VlbReflection.SetBool(entry.sd, "spotAngleFromLight", entry.irisSdOriginalFromLight);
            VlbReflection.SetFloat(entry.sd, "spotAngle", entry.irisSdOriginalAngle);
            VlbReflection.SetFloat(entry.sd, "coneRadiusStart", entry.irisSdOriginalRadius);
            VlbReflection.Invoke(entry.sd, "AssignPropertiesFromAttachedSpotLight");
            entry.irisSdApplied = false;
        }

        private static class VlbReflection
        {
            public static readonly Type HdType = FindType("VLB.VolumetricLightBeamHD");
            public static readonly Type SdType = FindType("VLB.VolumetricLightBeamSD");
            public static readonly Type CookieHdType = FindType("VLB.VolumetricCookieHD");
            private static readonly Type BeamPropsType = FindType("VLB.BeamProps");

            private static readonly Dictionary<Type, Dictionary<string, MemberInfo>> MemberCache = new();
            private static readonly Dictionary<Type, Dictionary<string, MethodInfo>> MethodCache = new();
            private static readonly object HdCopyMask = CreateHdCopyMask();

            public static bool IsAvailable => HdType != null || SdType != null;

            public static Component GetComponent(GameObject gameObject, Type type)
            {
                return gameObject != null && type != null
                    ? gameObject.GetComponent(type)
                    : null;
            }

            public static Component EnsureComponent(GameObject gameObject, Type type)
            {
                if (gameObject == null || type == null)
                    return null;

                return gameObject.GetComponent(type) ?? gameObject.AddComponent(type);
            }

            public static void CopyHdProps(Component target, Component template)
            {
                if (target == null || template == null || HdCopyMask == null)
                    return;

                var method = GetMethod(target.GetType(), "CopyPropsFrom", template.GetType(), BeamPropsType);
                method?.Invoke(target, new[] { template, HdCopyMask });
            }

            public static void Invoke(Component component, string methodName)
            {
                if (component == null)
                    return;

                var method = GetMethod(component.GetType(), methodName);
                method?.Invoke(component, null);
            }

            public static void SetBool(Component component, string name, bool value)
            {
                SetValue(component, name, value);
            }

            public static void SetFloat(Component component, string name, float value)
            {
                SetValue(component, name, value);
            }

            public static void SetVector2(Component component, string name, Vector2 value)
            {
                SetValue(component, name, value);
            }

            public static void SetObject(Component component, string name, object value)
            {
                SetValue(component, name, value);
            }

            public static void SetEnum(Component component, string name, string enumName, int fallbackValue)
            {
                if (component == null)
                    return;

                var member = GetMember(component.GetType(), name);
                Type valueType = GetMemberType(member);
                if (valueType == null)
                    return;

                object value = valueType.IsEnum
                    ? Enum.Parse(valueType, enumName)
                    : fallbackValue;
                SetValue(component, member, value);
            }

            public static Vector2 GetVector2(Component component, string name, Vector2 fallback)
            {
                if (component == null)
                    return fallback;

                var member = GetMember(component.GetType(), name);
                object value = GetValue(component, member);
                return value is Vector2 vector ? vector : fallback;
            }

            public static float GetFloat(Component component, string name, float fallback)
            {
                if (component == null)
                    return fallback;

                object value = GetValue(component, GetMember(component.GetType(), name));
                return value is float number ? number : fallback;
            }

            public static object GetObject(Component component, string name)
            {
                return component == null
                    ? null
                    : GetValue(component, GetMember(component.GetType(), name));
            }

            private static void SetValue(Component component, string name, object value)
            {
                if (component == null)
                    return;

                SetValue(component, GetMember(component.GetType(), name), value);
            }

            private static void SetValue(Component component, MemberInfo member, object value)
            {
                switch (member)
                {
                    case PropertyInfo property when property.CanWrite:
                        property.SetValue(component, value);
                        break;
                    case FieldInfo field:
                        field.SetValue(component, value);
                        break;
                }
            }

            private static object GetValue(Component component, MemberInfo member)
            {
                return member switch
                {
                    PropertyInfo property when property.CanRead => property.GetValue(component),
                    FieldInfo field => field.GetValue(component),
                    _ => null
                };
            }

            private static Type GetMemberType(MemberInfo member)
            {
                return member switch
                {
                    PropertyInfo property => property.PropertyType,
                    FieldInfo field => field.FieldType,
                    _ => null
                };
            }

            private static MemberInfo GetMember(Type type, string name)
            {
                if (type == null)
                    return null;

                if (!MemberCache.TryGetValue(type, out var members))
                {
                    members = new Dictionary<string, MemberInfo>();
                    MemberCache[type] = members;
                }

                if (members.TryGetValue(name, out var cached))
                    return cached;

                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var member = (MemberInfo)type.GetProperty(name, flags) ?? type.GetField(name, flags);
                members[name] = member;
                return member;
            }

            private static MethodInfo GetMethod(Type type, string name, params Type[] parameterTypes)
            {
                if (type == null)
                    return null;

                if (!MethodCache.TryGetValue(type, out var methods))
                {
                    methods = new Dictionary<string, MethodInfo>();
                    MethodCache[type] = methods;
                }

                string key = parameterTypes == null || parameterTypes.Length == 0
                    ? name
                    : name + "(" + string.Join(",", Array.ConvertAll(parameterTypes, t => t?.FullName ?? string.Empty)) + ")";
                if (methods.TryGetValue(key, out var cached))
                    return cached;

                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                MethodInfo method = parameterTypes == null || parameterTypes.Length == 0
                    ? type.GetMethod(name, flags, null, Type.EmptyTypes, null)
                    : type.GetMethod(name, flags, null, parameterTypes, null);
                methods[key] = method;
                return method;
            }

            private static Type FindType(string fullName)
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < assemblies.Length; i++)
                {
                    var type = assemblies[i].GetType(fullName, false);
                    if (type != null)
                        return type;
                }

                return null;
            }

            private static object CreateHdCopyMask()
            {
                if (BeamPropsType == null)
                    return null;

                const string names = "Color, BlendingMode, Intensity, SideSoftness, SpotShape, FallOffAttenuation, Noise3D";
                try
                {
                    return Enum.Parse(BeamPropsType, names);
                }
                catch
                {
                    return null;
                }
            }
        }
    }
}
