using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ArtNet.Editor
{
    /// <summary>
    /// Resolves VLB's standard High raymarching preset without a compile-time VLB dependency.
    /// </summary>
    internal static class VlbRaymarchingQualitySetup
    {
        private const string ConfigTypeName = "VLB.Config";
        private const string BeamHdTypeName = "VLB.VolumetricLightBeamHD";
        private const string QualityTypeName = "VLB.RaymarchingQuality";
        private const string PresetName = "High";
        private const int PresetStepCount = 20;
        private const int PresetUniqueId = 3;

        internal readonly struct Status
        {
            public readonly bool configFound;
            public readonly bool highAvailable;
            public readonly bool highUsesStandardId;

            public Status(bool configFound, bool highAvailable, bool highUsesStandardId)
            {
                this.configFound = configFound;
                this.highAvailable = highAvailable;
                this.highUsesStandardId = highUsesStandardId;
            }

            public bool IsReady => configFound && highAvailable && highUsesStandardId;
        }

        public static Status InspectStatus()
        {
            var config = FindConfig();
            if (config == null) return new Status(false, false, false);

            var quality = FindQuality(config);
            var id = quality?.GetType().GetProperty("uniqueID")?.GetValue(quality);
            return id is int qualityId
                ? new Status(true, true, qualityId == PresetUniqueId)
                : new Status(true, false, false);
        }

        public static bool EnsureHigh(out string error)
        {
            error = null;
            var config = FindConfig();
            if (config == null)
            {
                error = "VLB Config could not be found or created. Open the VLB Config asset once and retry.";
                return false;
            }

            var existingQuality = FindQuality(config);
            if (existingQuality != null)
            {
                var existingId = existingQuality.GetType().GetProperty("uniqueID")?.GetValue(existingQuality);
                if (existingId is int id && id == PresetUniqueId)
                    return true;

                error = $"VLB Config already contains High (20) with ID {existingId}. Standard High must use ID {PresetUniqueId}.";
                return false;
            }

            if (IsQualityIdInUse(config, PresetUniqueId))
            {
                error = $"VLB Config already uses ID {PresetUniqueId} for another raymarching quality. Change that quality manually before adding standard High (20).";
                return false;
            }

            var qualityType = FindType(QualityTypeName);
            var newMethod = qualityType?.GetMethod("New", BindingFlags.Static | BindingFlags.Public, null,
                new[] { typeof(string), typeof(int), typeof(int) }, null);
            var addMethod = config.GetType().GetMethod("AddRaymarchingQuality", BindingFlags.Instance | BindingFlags.Public);
            if (newMethod == null || addMethod == null)
            {
                error = "This VLB version does not expose the raymarching quality editor API.";
                return false;
            }

            try
            {
                var quality = newMethod.Invoke(null, new object[] { PresetName, PresetUniqueId, PresetStepCount });
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

        public static bool FixHigh(out string error)
        {
            if (!EnsureHigh(out error)) return false;

            var config = FindConfig();
            var quality = config == null ? null : FindQuality(config);
            var id = quality?.GetType().GetProperty("uniqueID")?.GetValue(quality);
            if (!(id is int qualityId))
            {
                error = "High (20) could not be resolved from the VLB Config asset.";
                return false;
            }
            if (qualityId != PresetUniqueId)
            {
                error = $"High (20) must use standard ID {PresetUniqueId}, but VLB Config uses ID {qualityId}.";
                return false;
            }

            error = null;
            return true;
        }

        public static bool IsHighApplied(GameObject gameObject)
        {
            if (gameObject == null || !TryGetHighQualityId(out var qualityId)) return false;
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

            if (!TryGetHighQualityId(out var qualityId)) return false;
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

        public static bool TryGetHighQualityId(out int qualityId)
        {
            qualityId = default;
            var config = FindConfig();
            var quality = config == null ? null : FindQuality(config);
            var id = quality?.GetType().GetProperty("uniqueID")?.GetValue(quality);
            if (!(id is int value) || value != PresetUniqueId) return false;
            qualityId = value;
            return true;
        }

        private static bool IsQualityIdInUse(ScriptableObject config, int qualityId)
        {
            var type = config.GetType();
            var countProperty = type.GetProperty("raymarchingQualitiesCount", BindingFlags.Instance | BindingFlags.Public);
            var getMethod = type.GetMethod("GetRaymarchingQualityForIndex", BindingFlags.Instance | BindingFlags.Public);
            if (countProperty == null || getMethod == null) return false;

            var count = (int)countProperty.GetValue(config);
            for (var i = 0; i < count; i++)
            {
                var quality = getMethod.Invoke(config, new object[] { i });
                var id = quality?.GetType().GetProperty("uniqueID")?.GetValue(quality);
                if (id is int value && value == qualityId)
                    return true;
            }
            return false;
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
