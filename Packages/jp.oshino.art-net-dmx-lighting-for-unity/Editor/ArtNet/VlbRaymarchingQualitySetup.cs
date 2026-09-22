using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ArtNet.Editor
{
    /// <summary>
    /// Creates the shared VLB HD raymarching preset without a compile-time VLB dependency.
    /// </summary>
    internal static class VlbRaymarchingQualitySetup
    {
        private const string ConfigTypeName = "VLB.Config";
        private const string BeamHdTypeName = "VLB.VolumetricLightBeamHD";
        private const string QualityTypeName = "VLB.RaymarchingQuality";
        private const string PresetName = "Very High";
        private const int PresetStepCount = 40;

        internal readonly struct Status
        {
            public readonly bool configFound;
            public readonly bool veryHighAvailable;
            public readonly bool veryHighIsDefault;

            public Status(bool configFound, bool veryHighAvailable, bool veryHighIsDefault)
            {
                this.configFound = configFound;
                this.veryHighAvailable = veryHighAvailable;
                this.veryHighIsDefault = veryHighIsDefault;
            }

            public bool IsReady => configFound && veryHighAvailable && veryHighIsDefault;
        }

        public static Status InspectStatus()
        {
            var config = FindConfig();
            if (config == null) return new Status(false, false, false);

            var quality = FindQuality(config);
            var id = quality?.GetType().GetProperty("uniqueID")?.GetValue(quality);
            if (!(id is int qualityId)) return new Status(true, false, false);

            var serializedConfig = new SerializedObject(config);
            var defaultQuality = serializedConfig.FindProperty("m_DefaultRaymarchingQualityUniqueID");
            return new Status(true, true, defaultQuality != null && defaultQuality.intValue == qualityId);
        }

        public static bool EnsureVeryHigh(out string error)
        {
            error = null;
            var config = FindConfig();
            if (config == null)
            {
                error = "VLB Config could not be found or created. Open the VLB Config asset once and retry.";
                return false;
            }

            if (FindQuality(config) != null)
                return true;

            var qualityType = FindType(QualityTypeName);
            var newMethod = qualityType?.GetMethod("New", BindingFlags.Static | BindingFlags.Public, null,
                new[] { typeof(string), typeof(int), typeof(int) }, null);
            var addMethod = config.GetType().GetMethod("AddRaymarchingQuality", BindingFlags.Instance | BindingFlags.Public);
            if (newMethod == null || addMethod == null)
            {
                error = "This VLB version does not expose the raymarching quality editor API.";
                return false;
            }

            var forcedId = 4;
            var countProperty = config.GetType().GetProperty("raymarchingQualitiesCount", BindingFlags.Instance | BindingFlags.Public);
            var getMethod = config.GetType().GetMethod("GetRaymarchingQualityForIndex", BindingFlags.Instance | BindingFlags.Public);
            if (countProperty != null && getMethod != null)
            {
                var count = (int)countProperty.GetValue(config);
                for (var i = 0; i < count; i++)
                {
                    var quality = getMethod.Invoke(config, new object[] { i });
                    var id = quality?.GetType().GetProperty("uniqueID")?.GetValue(quality);
                    if (id is int value) forcedId = Math.Max(forcedId, value + 1);
                }
            }

            try
            {
                var quality = newMethod.Invoke(null, new object[] { PresetName, forcedId, PresetStepCount });
                addMethod.Invoke(config, new[] { quality });
                EditorUtility.SetDirty(config);
                RefreshShaders(config);
                AssetDatabase.SaveAssets();
                return true;
            }
            catch (TargetInvocationException exception)
            {
                error = exception.InnerException?.Message ?? exception.Message;
                return false;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        public static bool FixVeryHighDefault(out string error)
        {
            if (!EnsureVeryHigh(out error)) return false;

            var config = FindConfig();
            var quality = config == null ? null : FindQuality(config);
            var id = quality?.GetType().GetProperty("uniqueID")?.GetValue(quality);
            if (!(id is int qualityId))
            {
                error = "Very High (40) could not be resolved from the VLB Config asset.";
                return false;
            }

            var serializedConfig = new SerializedObject(config);
            var defaultQuality = serializedConfig.FindProperty("m_DefaultRaymarchingQualityUniqueID");
            if (defaultQuality == null)
            {
                error = "This VLB Config version does not expose its default raymarching quality setting.";
                return false;
            }

            serializedConfig.Update();
            if (defaultQuality.intValue != qualityId)
            {
                defaultQuality.intValue = qualityId;
                serializedConfig.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
            }

            error = null;
            return true;
        }

        public static bool IsVeryHighApplied(GameObject gameObject)
        {
            if (gameObject == null || !TryGetVeryHighQualityId(out var qualityId)) return false;
            return IsQualityApplied(gameObject, qualityId);
        }

        public static bool IsQualityApplied(GameObject gameObject, int qualityId)
        {
            if (gameObject == null) return false;
            var beamType = FindType(BeamHdTypeName);
            var beam = beamType == null ? null : gameObject.GetComponent(beamType);
            var property = beamType?.GetProperty("raymarchingQualityID", BindingFlags.Instance | BindingFlags.Public);
            return beam != null && property != null && property.PropertyType == typeof(int) && (int)property.GetValue(beam) == qualityId;
        }

        public static bool ApplyToGameObject(GameObject gameObject)
        {
            if (gameObject == null) return false;
            var beamType = FindType(BeamHdTypeName);
            var beam = beamType == null ? null : gameObject.GetComponent(beamType);
            if (beam == null) return false;

            if (!TryGetVeryHighQualityId(out var qualityId)) return false;
            return ApplyQualityToGameObject(gameObject, qualityId);
        }

        public static bool ApplyQualityToGameObject(GameObject gameObject, int qualityId)
        {
            if (gameObject == null) return false;
            var beamType = FindType(BeamHdTypeName);
            var beam = beamType == null ? null : gameObject.GetComponent(beamType);
            return beam != null && ApplyQualityToBeam(beam, beamType, qualityId);
        }

        public static bool ApplyToChildren(GameObject root, int qualityId)
        {
            if (root == null) return false;
            var beamType = FindType(BeamHdTypeName);
            if (beamType == null) return false;
            var changed = false;
            foreach (var component in root.GetComponentsInChildren(beamType, true))
                changed |= ApplyQualityToBeam(component, beamType, qualityId);
            return changed;
        }

        private static bool ApplyQualityToBeam(Component beam, Type beamType, int qualityId)
        {
            var property = beamType.GetProperty("raymarchingQualityID", BindingFlags.Instance | BindingFlags.Public);
            if (property == null || property.PropertyType != typeof(int)) return false;
            var current = (int)property.GetValue(beam);
            if (current == qualityId) return false;
            property.SetValue(beam, qualityId);
            EditorUtility.SetDirty(beam);
            return true;
        }

        public static bool ApplyToChildren(GameObject root)
        {
            if (root == null) return false;
            var beamType = FindType(BeamHdTypeName);
            if (beamType == null) return false;
            var changed = false;
            foreach (var component in root.GetComponentsInChildren(beamType, true))
                changed |= ApplyToGameObject(component.gameObject);
            return changed;
        }

        public static bool TryGetVeryHighQualityId(out int qualityId)
        {
            qualityId = default;
            var config = FindConfig();
            var quality = config == null ? null : FindQuality(config);
            var id = quality?.GetType().GetProperty("uniqueID")?.GetValue(quality);
            if (!(id is int value)) return false;
            qualityId = value;
            return true;
        }

        private static ScriptableObject FindConfig()
        {
            var type = FindType(ConfigTypeName);
            if (type == null) return null;
            foreach (var guid in AssetDatabase.FindAssets($"t:{type.Name}"))
            {
                var asset = AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guid), type) as ScriptableObject;
                if (asset != null && asset.GetType() == type) return asset;
            }
            var instance = type.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public);
            try { return instance?.GetValue(null) as ScriptableObject; }
            catch { return null; }
        }

        private static object FindQuality(ScriptableObject config)
        {
            var type = config.GetType();
            var countProperty = type.GetProperty("raymarchingQualitiesCount", BindingFlags.Instance | BindingFlags.Public);
            var getMethod = type.GetMethod("GetRaymarchingQualityForIndex", BindingFlags.Instance | BindingFlags.Public);
            if (countProperty == null || getMethod == null) return null;
            var count = (int)countProperty.GetValue(config);
            for (var i = 0; i < count; i++)
            {
                var quality = getMethod.Invoke(config, new object[] { i });
                if (quality == null) continue;
                var name = quality.GetType().GetField("name")?.GetValue(quality) as string;
                var steps = quality.GetType().GetField("stepCount")?.GetValue(quality);
                if (string.Equals(name, PresetName, StringComparison.Ordinal) && steps is int value && value == PresetStepCount)
                    return quality;
            }
            return null;
        }

        private static void RefreshShaders(ScriptableObject config)
        {
            var method = config.GetType().GetMethod("RefreshShaders", BindingFlags.Instance | BindingFlags.Public);
            if (method == null || method.GetParameters().Length != 1 || !method.GetParameters()[0].ParameterType.IsEnum) return;
            try { method.Invoke(config, new[] { Enum.Parse(method.GetParameters()[0].ParameterType, "All") }); }
            catch (ArgumentException) { }
        }

        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }
            return null;
        }
    }
}
