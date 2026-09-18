using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ArtNet.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArtNet.Editor
{
    /// <summary>
    /// Configures Volumetric Light Beam for HDRP without taking a compile-time
    /// dependency on VLB or HDRP assemblies.
    /// </summary>
    internal sealed class VlbHdrpSetupWindow : EditorWindow
    {
        private const string VlbConfigTypeName = "VLB.Config";
        private const string VlbSdTypeName = "VLB.VolumetricLightBeamSD";
        private const string VlbHdTypeName = "VLB.VolumetricLightBeamHD";
        private const string VlbCookieHdTypeName = "VLB.VolumetricCookieHD";
        private const int HdrpRenderPipelineEnumValue = 2;
        private const int VlbBeamRenderModeEnumValue = 2;
        private const string MenuPath = "Art-Net/VLB/HDRP Setup";

        private Vector2 _scrollPosition;
        private ProjectStatus _status;
        private readonly List<ScanTarget> _scanTargets = new() { new ScanTarget() };
        private BeamMode _beamMode = BeamMode.HD;

        [MenuItem(MenuPath)]
        private static void Open()
        {
            var window = GetWindow<VlbHdrpSetupWindow>(true, "Art-Net VLB HDRP Setup", true);
            window.minSize = new Vector2(560f, 440f);
            window.Refresh();
            window.Show();
        }

        private void OnEnable() => Refresh();
        private void OnFocus() => Refresh();

        private void OnGUI()
        {
            _status ??= InspectProject();
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            EditorGUILayout.LabelField("Art-Net VLB / HDRP Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Checks the HDRP configuration used by Volumetric Light Beam (VLB) and configures only the VLB target Lights referenced by DMX Fixture Component. " +
                "HDRP native Volumetrics and Volume Profiles are not changed because they are not required by VLB.",
                MessageType.Info);
            if (GUILayout.Button("Refresh All Checks", GUILayout.Height(24f)))
                Refresh();

            EditorGUILayout.Space(8f);
            DrawProjectSettings();
            EditorGUILayout.Space(8f);
            DrawScanTargets();
            EditorGUILayout.EndScrollView();
        }

        private void DrawProjectSettings()
        {
            EditorGUILayout.LabelField("Project Settings", EditorStyles.boldLabel);
            DrawStatusSummary("Project settings overview", GetProjectSettingsSummary());
            DrawStatusRow("VLB installed", _status.vlbInstalled ? StatusLevel.Success : StatusLevel.Error,
                _status.vlbInstalled ? "VLB SD or HD API was found." : "Install Volumetric Light Beam before configuring VLB.");

            if (_status.vlbInstalled)
            {
                var ready = _status.vlbConfigFound && _status.vlbConfigIsHdrp;
                var detail = !_status.vlbConfigFound ? "VLB Config asset was not found."
                    : ready ? "Render Pipeline is HDRP." : "Render Pipeline should be HDRP.";
                if (ready)
                    DrawStatusRow("VLB Config: HDRP", StatusLevel.Success, detail);
                else
                    DrawStatusRowWithFix("VLB Config: HDRP", StatusLevel.Warning, detail, FixVlbConfig);
            }

            if (_status.hdrpAssets.Count == 0)
                DrawStatusRow("HDRP Asset", StatusLevel.Error, "No HDRP Asset is assigned in Graphics Settings or the Quality Levels.");
            else
            {
                foreach (var asset in _status.hdrpAssets)
                {
                    EditorGUILayout.ObjectField(asset.label, asset.asset, typeof(RenderPipelineAsset), false);
                    DrawStatusRow("HDRP Asset", StatusLevel.Success, "HDRP Render Pipeline Asset is assigned.");
                }
            }

            EditorGUILayout.HelpBox(
                "VLB renders its own beam geometry. HDRP Volumetric Fog, the Volume Profile, and Light Volumetric Dimmer affect HDRP's native volumetric lighting separately, so this tool leaves them under scene-art direction control.",
                MessageType.None);
        }

        private void DrawScanTargets()
        {
            EditorGUILayout.LabelField("Setup Targets", EditorStyles.boldLabel);
            var prefabPaths = new List<string>();
            for (var index = 0; index < _scanTargets.Count; index++)
            {
                var target = _scanTargets[index];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Setup Target {index + 1}", EditorStyles.boldLabel);
                if (GUILayout.Button("Remove", GUILayout.Width(64f)))
                {
                    _scanTargets.RemoveAt(index);
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUI.BeginChangeCheck();
                var rootObject = (GameObject)EditorGUILayout.ObjectField("Root Object", target.rootObject, typeof(GameObject), true);
                if (EditorGUI.EndChangeCheck())
                {
                    target.rootObject = rootObject;
                    var resolved = ResolvePrefabAsset(rootObject);
                    if (resolved != null) target.prefabAsset = resolved;
                }
                target.prefabAsset = (GameObject)EditorGUILayout.ObjectField("Prefab Asset", target.prefabAsset, typeof(GameObject), false);
                var issue = GetScanTargetIssue(target, out var prefabAsset);
                if (issue != null) EditorGUILayout.HelpBox(issue, MessageType.Warning);
                else
                {
                    prefabPaths.Add(AssetDatabase.GetAssetPath(prefabAsset));
                    EditorGUILayout.HelpBox($"Will inspect: {AssetDatabase.GetAssetPath(prefabAsset)}", MessageType.None);
                }
                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("Add Setup Target")) _scanTargets.Add(new ScanTarget());
            _beamMode = (BeamMode)EditorGUILayout.EnumPopup("Beam Mode", _beamMode);
            prefabPaths = prefabPaths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            if (!_status.vlbInstalled)
            {
                EditorGUILayout.HelpBox("VLB is not installed, so scan targets cannot be checked or updated.", MessageType.Warning);
                return;
            }

            var prefabStatuses = InspectPrefabs(prefabPaths, _beamMode);
            foreach (var prefabStatus in prefabStatuses) DrawPrefabStatus(prefabStatus);
            DrawScanTargetActions(prefabPaths, prefabStatuses);
        }

        private void DrawPrefabStatus(PrefabStatus status)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.ObjectField(status.prefab, typeof(GameObject), false);
            if (status.fixtureCount == 0)
                DrawStatusRow("DMX Fixture Component", StatusLevel.Warning, "No fixture component was found.");
            else if (status.nonSpotTargetLightCount > 0)
                DrawStatusRow("Target Light Type", StatusLevel.Warning, $"{status.nonSpotTargetLightCount} non-Spot target Light(s) will be skipped. VLB requires Spot Lights.");

            var pending = status.needsSetupTargetLights.Count + status.needsModeUpdateTargetLights.Count +
                          status.needsHdrpPresetFixtureCount + status.needsVlbRenderModeFixtureCount;
            DrawStatusRow("Target Light VLB", pending == 0 ? StatusLevel.Success : StatusLevel.Warning,
                pending == 0 ? $"All {status.targetLightCount} Spot target Light(s) use VLB {_beamMode}."
                    : $"{status.readyTargetLightCount} ready, {status.needsSetupTargetLights.Count} need setup, {status.needsModeUpdateTargetLights.Count} need mode update, {status.needsHdrpPresetFixtureCount} need HDRP defaults, {status.needsVlbRenderModeFixtureCount} need automatic VLB render mode.");
            EditorGUILayout.EndVertical();
        }

        private void DrawScanTargetActions(IReadOnlyCollection<string> paths, IReadOnlyCollection<PrefabStatus> statuses)
        {
            var typesAvailable = TryGetRequiredBeamTypes(_beamMode, out var typeError);
            var needSetup = statuses.Sum(x => x.needsSetupTargetLights.Count);
            var needUpdate = statuses.Sum(x => x.needsModeUpdateTargetLights.Count);
            var needDefaults = statuses.Sum(x => x.needsHdrpPresetFixtureCount);
            var needRenderMode = statuses.Sum(x => x.needsVlbRenderModeFixtureCount);
            using (new EditorGUI.DisabledScope(!typesAvailable || paths.Count == 0 || needSetup + needUpdate + needDefaults + needRenderMode == 0))
            {
                if (GUILayout.Button($"Apply VLB {_beamMode} to Target Lights", GUILayout.Height(24f)))
                    ApplyBeamModeToScanTargets(paths, needSetup, needUpdate, needDefaults, needRenderMode);
            }
            EditorGUILayout.HelpBox(typesAvailable
                ? $"Applies VLB {_beamMode} and HDRP-safe Art-Net VLB defaults only to Spot Lights referenced by DMX Fixture Component. HD also adds VolumetricCookieHD."
                : typeError, MessageType.None);
        }

        private void FixVlbConfig()
        {
            var action = _status.vlbConfigFound
                ? "Set the VLB Config Render Pipeline to HDRP and refresh VLB shaders?"
                : "Create the VLB Config asset, set its Render Pipeline to HDRP, and refresh VLB shaders?";
            if (!EditorUtility.DisplayDialog("Fix VLB Config", action, "Fix", "Cancel")) return;
            var config = _status.vlbConfig;
            if (config == null && !TryCreateVlbConfig(out config, out var error))
            {
                EditorUtility.DisplayDialog("VLB Config setup failed", error, "OK");
                Refresh();
                return;
            }
            SetVlbConfigToHdrp(config);
            AssetDatabase.SaveAssets();
            Refresh();
        }

        private void ApplyBeamModeToScanTargets(IEnumerable<string> prefabPaths, int needsSetup, int needsUpdate, int needsDefaults, int needsRenderMode)
        {
            if (!TryGetRequiredBeamTypes(_beamMode, out var error))
            {
                EditorUtility.DisplayDialog("VLB component not available", error, "OK");
                return;
            }
            var paths = prefabPaths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (!EditorUtility.DisplayDialog($"Apply VLB {_beamMode} to Target Lights",
                    $"Prefab(s): {paths.Count}\nAdd VLB {_beamMode}: {needsSetup}\nSwitch mode or repair Cookie: {needsUpdate}\nApply HDRP defaults: {needsDefaults}\nSet Beam Render Mode to VLB: {needsRenderMode}\n\nOnly Spot Lights referenced by DMX Fixture Component will be changed.",
                    "Apply", "Cancel")) return;

            var changedPrefabs = 0;
            var changedLights = 0;
            foreach (var path in paths)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var changed = false;
                    Undo.RegisterFullObjectHierarchyUndo(root, $"Apply VLB {_beamMode} HDRP Setup");
                    var processedLights = new HashSet<int>();
                    foreach (var fixture in root.GetComponentsInChildren<DmxFixtureComponent>(true))
                    {
                        changed |= ApplyHdrpVlbDefaults(fixture);
                        changed |= ApplyVlbRenderMode(fixture);
                        foreach (var light in GetTargetLights(fixture))
                        {
                            if (light == null || light.type != LightType.Spot || !processedLights.Add(light.GetInstanceID())) continue;
                            var beamChanged = ApplyBeamMode(light.gameObject, _beamMode);
                            var presetChanged = ApplyHdrpBeamPreset(light.gameObject, _beamMode);
                            if (beamChanged || presetChanged) { changed = true; changedLights++; }
                        }
                    }
                    if (changed) { PrefabUtility.SaveAsPrefabAsset(root, path); changedPrefabs++; }
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("VLB HDRP target Light setup", $"Updated {changedPrefabs} Prefab(s) and {changedLights} Target Light(s).", "OK");
            Refresh();
        }

        private void Refresh() { _status = InspectProject(); Repaint(); }

        private static ProjectStatus InspectProject()
        {
            var status = new ProjectStatus
            {
                vlbSdType = FindType(VlbSdTypeName), vlbHdType = FindType(VlbHdTypeName),
                vlbCookieHdType = FindType(VlbCookieHdTypeName), vlbConfig = FindVlbConfig(), hdrpAssets = FindUsedHdrpAssets()
            };
            status.vlbInstalled = status.vlbSdType != null || status.vlbHdType != null;
            status.vlbConfigFound = status.vlbConfig != null;
            status.vlbConfigIsHdrp = status.vlbConfigFound && GetVlbConfigPipelineValue(status.vlbConfig) == HdrpRenderPipelineEnumValue;
            return status;
        }

        private static List<PipelineAssetStatus> FindUsedHdrpAssets()
        {
            var assets = new Dictionary<int, PipelineAssetStatus>();
            AddHdrpAsset(GraphicsSettings.defaultRenderPipeline, "Graphics Settings", assets);
            for (var i = 0; i < QualitySettings.names.Length; i++)
                AddHdrpAsset(QualitySettings.GetRenderPipelineAssetAt(i), $"Quality: {QualitySettings.names[i]}", assets);
            return assets.Values.OrderBy(x => x.label, StringComparer.Ordinal).ToList();
        }

        private static void AddHdrpAsset(RenderPipelineAsset asset, string source, IDictionary<int, PipelineAssetStatus> assets)
        {
            if (asset == null || !IsHdrpAsset(asset)) return;
            if (assets.TryGetValue(asset.GetInstanceID(), out var existing)) { existing.label = $"{existing.label}, {source}"; return; }
            assets.Add(asset.GetInstanceID(), new PipelineAssetStatus { asset = asset, label = source });
        }

        private static bool IsHdrpAsset(RenderPipelineAsset asset) =>
            asset.GetType().FullName?.Contains("HDRenderPipelineAsset", StringComparison.Ordinal) == true ||
            asset.GetType().FullName?.Contains("HighDefinitionRenderPipelineAsset", StringComparison.Ordinal) == true;

        private static ScriptableObject FindVlbConfig()
        {
            var type = FindType(VlbConfigTypeName);
            if (type == null) return null;
            foreach (var guid in AssetDatabase.FindAssets($"t:{type.Name}"))
            {
                var asset = AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guid), type) as ScriptableObject;
                if (asset != null && asset.GetType() == type) return asset;
            }
            return Resources.FindObjectsOfTypeAll(type).OfType<ScriptableObject>().FirstOrDefault();
        }

        private static bool TryCreateVlbConfig(out ScriptableObject config, out string error)
        {
            config = FindVlbConfig(); error = null;
            if (config != null) return true;
            var instance = FindType(VlbConfigTypeName)?.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public);
            if (instance == null) { error = "The installed VLB version does not expose Config.Instance, so its Config asset could not be created automatically."; return false; }
            try { config = instance.GetValue(null) as ScriptableObject; }
            catch (TargetInvocationException exception) { error = $"VLB could not create its Config asset: {exception.InnerException?.Message ?? exception.Message}"; return false; }
            catch (Exception exception) { error = $"VLB could not create its Config asset: {exception.Message}"; return false; }
            if (config != null) return true;
            error = "VLB did not return a Config asset. Open VLB Config once and retry.";
            return false;
        }

        private static int GetVlbConfigPipelineValue(ScriptableObject config) => new SerializedObject(config).FindProperty("m_RenderPipeline")?.enumValueIndex ?? -1;

        private static void SetVlbConfigToHdrp(ScriptableObject config)
        {
            var property = new SerializedObject(config).FindProperty("m_RenderPipeline");
            if (property == null) return;
            property.enumValueIndex = HdrpRenderPipelineEnumValue;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            var type = config.GetType();
            type.GetMethod("SetScriptingDefineSymbolsForCurrentRenderPipeline", BindingFlags.Instance | BindingFlags.Public)?.Invoke(config, null);
            var refresh = type.GetMethod("RefreshShaders", BindingFlags.Instance | BindingFlags.Public);
            if (refresh?.GetParameters().Length == 1 && refresh.GetParameters()[0].ParameterType.IsEnum)
                try { refresh.Invoke(config, new[] { Enum.Parse(refresh.GetParameters()[0].ParameterType, "All") }); } catch (ArgumentException) { }
        }

        private static bool TryGetRequiredBeamTypes(BeamMode mode, out string error)
        {
            var type = FindType(mode == BeamMode.SD ? VlbSdTypeName : VlbHdTypeName);
            if (type == null || !typeof(Component).IsAssignableFrom(type))
            {
                error = mode == BeamMode.SD ? "VolumetricLightBeamSD could not be resolved. Check that VLB has finished compiling." : "VolumetricLightBeamHD could not be resolved. Check that VLB has finished compiling.";
                return false;
            }
            if (mode == BeamMode.SD) { error = null; return true; }
            var cookie = FindType(VlbCookieHdTypeName);
            error = cookie == null || !typeof(Component).IsAssignableFrom(cookie) ? "VolumetricCookieHD could not be resolved. HD setup requires the VLB Cookie HD API." : null;
            return error == null;
        }

        private static bool ApplyBeamMode(GameObject gameObject, BeamMode mode)
        {
            var sd = FindType(VlbSdTypeName); var hd = FindType(VlbHdTypeName); var cookie = FindType(VlbCookieHdTypeName);
            return mode == BeamMode.SD
                ? RemoveComponentIfPresent(gameObject, hd) | RemoveComponentIfPresent(gameObject, cookie) | AddComponentIfMissing(gameObject, sd)
                : RemoveComponentIfPresent(gameObject, sd) | AddComponentIfMissing(gameObject, hd) | AddComponentIfMissing(gameObject, cookie);
        }

        private static bool ApplyHdrpVlbDefaults(DmxFixtureComponent fixture)
        {
            if (fixture == null || HasHdrpVlbOverrides(fixture)) return false;
            fixture.ApplyVlbPipelineDefaults();
            EditorUtility.SetDirty(fixture);
            return true;
        }

        private static bool HasHdrpVlbOverrides(DmxFixtureComponent fixture)
        {
            var serialized = new SerializedObject(fixture);
            var hdOverride = serialized.FindProperty("overrideVlbHdIntensityMultiplier");
            var hdValue = serialized.FindProperty("vlbHdIntensityMultiplier");
            var sdOverride = serialized.FindProperty("overrideVlbSdIntensityMultiplier");
            var sdValue = serialized.FindProperty("vlbSdIntensityMultiplier");
            var exposureOverride = serialized.FindProperty("overrideVlbHdrpExposureWeight");
            var exposure = serialized.FindProperty("vlbHdrpExposureWeight");
            return hdOverride != null && hdValue != null && sdOverride != null && sdValue != null && exposureOverride != null && exposure != null &&
                   hdOverride.boolValue && sdOverride.boolValue && exposureOverride.boolValue && Mathf.Approximately(hdValue.floatValue, 0.00001f) &&
                   Mathf.Approximately(sdValue.floatValue, 0.01f) && Mathf.Approximately(exposure.floatValue, 0f);
        }

        private static bool ApplyHdrpBeamPreset(GameObject gameObject, BeamMode mode)
        {
            var type = FindType(mode == BeamMode.SD ? VlbSdTypeName : VlbHdTypeName);
            var beam = type == null ? null : gameObject.GetComponent(type);
            if (beam == null) return false;
            var changed = TrySetVlbMember(beam, "colorFromLight", true);
            changed |= TrySetVlbMember(beam, "intensityMultiplier", mode == BeamMode.HD ? 0.00001f : 0.01f);
            if (mode == BeamMode.SD)
            {
                changed |= TrySetVlbMember(beam, "intensityFromLight", true);
                changed |= TrySetVlbMember(beam, "spotAngleFromLight", true);
                changed |= TrySetVlbMember(beam, "spotAngleMultiplier", 1.3f);
                changed |= TrySetVlbMember(beam, "coneRadiusStart", 0.01f);
                changed |= TrySetVlbMember(beam, "fallOffEndFromLight", true);
            }
            else
            {
                changed |= TrySetVlbMember(beam, "hdrpExposureWeight", 0f);
                changed |= TrySetVlbMember(beam, "useIntensityFromAttachedLightSpot", true);
                changed |= TrySetVlbMember(beam, "useSpotAngleFromAttachedLightSpot", true);
                changed |= TrySetVlbMember(beam, "useFallOffEndFromAttachedLightSpot", true);
                InvokeVlbMethod(beam, "AssignPropertiesFromAttachedSpotLight");
                InvokeVlbMethod(beam, "UpdateAfterManualPropertyChange");
            }
            if (changed) EditorUtility.SetDirty(beam);
            InvokeVlbMethod(beam, "GenerateGeometry");
            return changed;
        }

        private static bool HasVlbRenderMode(DmxFixtureComponent fixture)
        {
            if (fixture == null) return false;
            var property = new SerializedObject(fixture).FindProperty("beamRenderMode");
            return property != null && property.enumValueIndex == VlbBeamRenderModeEnumValue;
        }

        private static bool ApplyVlbRenderMode(DmxFixtureComponent fixture)
        {
            if (fixture == null) return false;
            var serialized = new SerializedObject(fixture);
            var property = serialized.FindProperty("beamRenderMode");
            if (property == null || property.enumValueIndex == VlbBeamRenderModeEnumValue) return false;
            property.enumValueIndex = VlbBeamRenderModeEnumValue;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(fixture);
            return true;
        }

        private static bool TrySetVlbMember(Component component, string name, object value)
        {
            var type = component.GetType();
            var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            if (property != null && property.CanWrite && property.PropertyType == value.GetType())
            {
                if (Equals(property.GetValue(component), value)) return false;
                property.SetValue(component, value); return true;
            }
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public);
            if (field == null || field.FieldType != value.GetType() || Equals(field.GetValue(component), value)) return false;
            field.SetValue(component, value); return true;
        }

        private static void InvokeVlbMethod(Component component, string name) =>
            component.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null)?.Invoke(component, null);
        private static bool AddComponentIfMissing(GameObject gameObject, Type type) { if (type == null || gameObject.GetComponent(type) != null) return false; Undo.AddComponent(gameObject, type); return true; }
        private static bool RemoveComponentIfPresent(GameObject gameObject, Type type) { var component = type == null ? null : gameObject.GetComponent(type); if (component == null) return false; Undo.DestroyObjectImmediate(component); return true; }
        private static Type FindType(string fullName) => AppDomain.CurrentDomain.GetAssemblies().Select(x => x.GetType(fullName, false)).FirstOrDefault(x => x != null);

        private static GameObject ResolvePrefabAsset(GameObject root)
        {
            if (root == null) return null;
            var path = AssetDatabase.GetAssetPath(root);
            if (!string.IsNullOrEmpty(path) && path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(root);
            path = instanceRoot == null ? null : PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instanceRoot);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static string GetScanTargetIssue(ScanTarget target, out GameObject prefab)
        {
            prefab = null; var rootPrefab = ResolvePrefabAsset(target.rootObject); var direct = target.prefabAsset;
            if (direct != null && !AssetDatabase.GetAssetPath(direct).EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) return "Prefab Asset must reference a Prefab asset in the Project window.";
            if (target.rootObject != null && rootPrefab == null) return "Root Object must be a Prefab instance or a Prefab asset.";
            if (rootPrefab != null && direct != null && !string.Equals(AssetDatabase.GetAssetPath(rootPrefab), AssetDatabase.GetAssetPath(direct), StringComparison.OrdinalIgnoreCase)) return "Root Object and Prefab Asset refer to different Prefabs. This target will not be processed.";
            prefab = direct ?? rootPrefab;
            return prefab == null ? "Specify either Root Object or Prefab Asset." : null;
        }

        private static List<PrefabStatus> InspectPrefabs(IEnumerable<string> paths, BeamMode mode)
        {
            var statuses = new List<PrefabStatus>();
            foreach (var path in paths)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var status = new PrefabStatus { prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path) };
                    var processed = new HashSet<int>(); var fixtures = root.GetComponentsInChildren<DmxFixtureComponent>(true); status.fixtureCount = fixtures.Length;
                    foreach (var fixture in fixtures)
                    {
                        if (!HasHdrpVlbOverrides(fixture)) status.needsHdrpPresetFixtureCount++;
                        if (!HasVlbRenderMode(fixture)) status.needsVlbRenderModeFixtureCount++;
                        foreach (var light in GetTargetLights(fixture))
                        {
                            if (light == null || !processed.Add(light.GetInstanceID())) continue;
                            if (light.type != LightType.Spot) { status.nonSpotTargetLightCount++; continue; }
                            status.targetLightCount++;
                            if (IsBeamModeConfigured(light.gameObject, mode)) status.readyTargetLightCount++;
                            else if (HasAnyBeamComponent(light.gameObject)) status.needsModeUpdateTargetLights.Add(light.name);
                            else status.needsSetupTargetLights.Add(light.name);
                        }
                    }
                    statuses.Add(status);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            return statuses;
        }

        private static IEnumerable<Light> GetTargetLights(DmxFixtureComponent fixture)
        {
            var lights = new HashSet<Light>();
            if (fixture.targetLight != null) lights.Add(fixture.targetLight);
            if (fixture.targetLights != null) foreach (var light in fixture.targetLights) if (light != null) lights.Add(light);
            return lights;
        }

        private static bool IsBeamModeConfigured(GameObject gameObject, BeamMode mode)
        {
            var sd = FindType(VlbSdTypeName); var hd = FindType(VlbHdTypeName); var cookie = FindType(VlbCookieHdTypeName);
            var hasSd = sd != null && gameObject.GetComponent(sd) != null; var hasHd = hd != null && gameObject.GetComponent(hd) != null; var hasCookie = cookie != null && gameObject.GetComponent(cookie) != null;
            return mode == BeamMode.SD ? hasSd && !hasHd && !hasCookie : hasHd && !hasSd && hasCookie;
        }
        private static bool HasAnyBeamComponent(GameObject gameObject)
        {
            var sd = FindType(VlbSdTypeName);
            var hd = FindType(VlbHdTypeName);
            var cookie = FindType(VlbCookieHdTypeName);
            return (sd != null && gameObject.GetComponent(sd) != null) ||
                   (hd != null && gameObject.GetComponent(hd) != null) ||
                   (cookie != null && gameObject.GetComponent(cookie) != null);
        }

        private StatusSummary GetProjectSettingsSummary()
        {
            var summary = new StatusSummary(); summary.Add(_status.vlbInstalled ? StatusLevel.Success : StatusLevel.Error);
            if (_status.vlbInstalled) summary.Add(_status.vlbConfigFound && _status.vlbConfigIsHdrp ? StatusLevel.Success : StatusLevel.Warning);
            summary.Add(_status.hdrpAssets.Count > 0 ? StatusLevel.Success : StatusLevel.Error); return summary;
        }
        private static void DrawStatusSummary(string title, StatusSummary summary) => DrawStatusRow(title, summary.OverallLevel, summary.ErrorCount > 0 ? $"{summary.ErrorCount} error(s), {summary.WarningCount} setting(s) need attention." : summary.WarningCount > 0 ? $"{summary.SuccessCount} check(s) passed, {summary.WarningCount} setting(s) need attention." : $"All {summary.SuccessCount} check(s) passed. No action is required.");
        private static void DrawStatusRow(string title, StatusLevel level, string detail) { var rect = EditorGUILayout.GetControlRect(false, 46f); DrawStatusRowContent(rect, title, level, detail); }
        private static void DrawStatusRowWithFix(string title, StatusLevel level, string detail, Action fix) { var rect = EditorGUILayout.GetControlRect(false, 46f); DrawStatusRowContent(new Rect(rect.x, rect.y, rect.width - 74f, rect.height), title, level, detail); if (GUI.Button(new Rect(rect.xMax - 66f, rect.y + 11f, 58f, 24f), "Fix")) fix?.Invoke(); }
        private static void DrawStatusRowContent(Rect rect, string title, StatusLevel level, string detail)
        {
            EditorGUI.DrawRect(rect, level == StatusLevel.Success ? new Color(0.08f, 0.24f, 0.14f, 0.55f) : level == StatusLevel.Warning ? new Color(0.32f, 0.23f, 0.06f, 0.55f) : new Color(0.32f, 0.09f, 0.09f, 0.55f));
            var icon = level == StatusLevel.Success ? "TestPassed" : level == StatusLevel.Warning ? "console.warnicon" : "console.erroricon";
            GUI.Label(new Rect(rect.x + 7f, rect.y + 10f, 22f, 22f), EditorGUIUtility.IconContent(icon));
            EditorGUI.LabelField(new Rect(rect.x + 36f, rect.y + 5f, rect.width - 43f, 18f), title, EditorStyles.boldLabel);
            EditorGUI.LabelField(new Rect(rect.x + 36f, rect.y + 23f, rect.width - 43f, 18f), detail, EditorStyles.miniLabel);
        }

        private enum BeamMode { SD, HD }
        private enum StatusLevel { Success, Warning, Error }
        private sealed class StatusSummary { public int SuccessCount { get; private set; } public int WarningCount { get; private set; } public int ErrorCount { get; private set; } public StatusLevel OverallLevel => ErrorCount > 0 ? StatusLevel.Error : WarningCount > 0 ? StatusLevel.Warning : StatusLevel.Success; public void Add(StatusLevel level) { if (level == StatusLevel.Success) SuccessCount++; else if (level == StatusLevel.Warning) WarningCount++; else ErrorCount++; } }
        private sealed class ProjectStatus { public Type vlbSdType; public Type vlbHdType; public Type vlbCookieHdType; public bool vlbInstalled; public ScriptableObject vlbConfig; public bool vlbConfigFound; public bool vlbConfigIsHdrp; public List<PipelineAssetStatus> hdrpAssets = new(); }
        private sealed class PipelineAssetStatus { public RenderPipelineAsset asset; public string label; }
        private sealed class PrefabStatus { public GameObject prefab; public int fixtureCount; public int targetLightCount; public int readyTargetLightCount; public int nonSpotTargetLightCount; public int needsHdrpPresetFixtureCount; public int needsVlbRenderModeFixtureCount; public List<string> needsSetupTargetLights = new(); public List<string> needsModeUpdateTargetLights = new(); }
        private sealed class ScanTarget { public GameObject rootObject; public GameObject prefabAsset; }
    }
}
