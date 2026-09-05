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
    /// Diagnoses the project settings required for Volumetric Light Beam in URP and
    /// applies SD or HD beam components to fixture target lights.
    /// VLB is intentionally accessed through reflection so this package remains usable
    /// in projects where VLB is not installed.
    /// </summary>
    internal sealed class VlbUrpSetupWindow : EditorWindow
    {
        private const string MenuPath = "Art-Net/VLB/URP Setup";
        private const string VlbConfigTypeName = "VLB.Config";
        private const string VlbSdTypeName = "VLB.VolumetricLightBeamSD";
        private const string VlbHdTypeName = "VLB.VolumetricLightBeamHD";
        private const string VlbCookieHdTypeName = "VLB.VolumetricCookieHD";
        private const int UrpRenderPipelineEnumValue = 1;
        private const int DepthPrimingDisabledEnumValue = 0;
        private const int CopyDepthAfterOpaquesEnumValue = 0;

        private Vector2 _scrollPosition;
        private ProjectStatus _status;
        private readonly List<ScanTarget> _scanTargets = new() { new ScanTarget() };
        private BeamMode _beamMode = BeamMode.SD;

        [MenuItem(MenuPath)]
        private static void Open()
        {
            var window = GetWindow<VlbUrpSetupWindow>(true, "Art-Net VLB URP Setup", true);
            window.minSize = new Vector2(560f, 440f);
            window.Refresh();
            window.Show();
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void OnFocus()
        {
            Refresh();
        }

        private void OnGUI()
        {
            _status ??= InspectProject();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            EditorGUILayout.LabelField("Art-Net VLB / URP Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Checks the URP settings required by Volumetric Light Beam (VLB). " +
                "This window does not make VLB a required dependency of the Art-Net package.",
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
            EditorGUILayout.LabelField("Project settings", EditorStyles.boldLabel);
            DrawProjectActions();
            DrawStatusSummary("Project settings overview", GetProjectSettingsSummary());
            DrawStatusRow("VLB installed", _status.vlbInstalled ? StatusLevel.Success : StatusLevel.Error,
                _status.vlbInstalled ? "VLB SD or HD API was found." : "Install Volumetric Light Beam before configuring VLB.");

            if (_status.vlbInstalled)
            {
                var configIsReady = _status.vlbConfigFound && _status.vlbConfigIsUrp;
                var configDetail = !_status.vlbConfigFound
                    ? "VLB Config asset was not found."
                    : _status.vlbConfigIsUrp ? "Render Pipeline is URP." : "Render Pipeline should be URP.";
                if (configIsReady)
                    DrawStatusRow("VLB Config: URP", StatusLevel.Success, configDetail);
                else
                    DrawStatusRowWithFix("VLB Config: URP", StatusLevel.Warning, configDetail, FixVlbConfig);
            }

            if (_status.urpAssets.Count == 0)
            {
                DrawStatusRow("URP Asset", StatusLevel.Error, "No URP Asset is assigned in Graphics Settings or the Quality Levels.");
                return;
            }

            foreach (var asset in _status.urpAssets)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.ObjectField(asset.label, asset.asset, typeof(RenderPipelineAsset), false);
                DrawStatusRow("Depth Texture", asset.depthTextureEnabled ? StatusLevel.Success : StatusLevel.Warning,
                    asset.depthTextureEnabled ? "Enabled." : "Should be enabled.");

                if (asset.rendererData == null)
                {
                    DrawStatusRow("Renderer Data", StatusLevel.Error, "Renderer Data could not be resolved from this URP Asset.");
                }
                else
                {
                    DrawStatusRow("Depth Priming", asset.depthPrimingDisabled ? StatusLevel.Success : StatusLevel.Warning,
                        asset.depthPrimingDisabled ? "Disabled." : "Should be Disabled.");
                    DrawStatusRow("Depth Texture Mode", asset.copyDepthAfterOpaques ? StatusLevel.Success : StatusLevel.Warning,
                        asset.copyDepthAfterOpaques ? "After Opaques." : "Should be After Opaques.");
                }

                EditorGUILayout.EndVertical();
            }
        }

        private void DrawScanTargets()
        {
            EditorGUILayout.LabelField("Setup Targets", EditorStyles.boldLabel);
            if (_scanTargets.Count == 0)
                _scanTargets.Add(new ScanTarget());

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
                    var resolvedPrefab = ResolvePrefabAsset(rootObject);
                    if (resolvedPrefab != null)
                        target.prefabAsset = resolvedPrefab;
                }

                target.prefabAsset = (GameObject)EditorGUILayout.ObjectField("Prefab Asset", target.prefabAsset, typeof(GameObject), false);
                var issue = GetScanTargetIssue(target, out var prefabAsset);
                if (issue != null)
                {
                    EditorGUILayout.HelpBox(issue, MessageType.Warning);
                }
                else
                {
                    prefabPaths.Add(AssetDatabase.GetAssetPath(prefabAsset));
                    EditorGUILayout.HelpBox($"Will inspect: {AssetDatabase.GetAssetPath(prefabAsset)}", MessageType.None);
                }
                EditorGUILayout.EndVertical();
            }

            prefabPaths = prefabPaths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (!_status.vlbInstalled)
            {
                EditorGUILayout.HelpBox("VLB is not installed, so scan targets cannot be checked or updated.", MessageType.Warning);
                return;
            }

            foreach (var prefabStatus in InspectPrefabs(prefabPaths, _beamMode))
                DrawPrefabStatus(prefabStatus);

            EditorGUILayout.Space(3f);
            if (GUILayout.Button("Add Setup Target"))
                _scanTargets.Add(new ScanTarget());
            _beamMode = (BeamMode)EditorGUILayout.EnumPopup("Beam Mode", _beamMode);

            DrawScanTargetActions(prefabPaths);
        }

        private void DrawPrefabStatus(PrefabStatus prefabStatus)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.ObjectField(prefabStatus.prefab, typeof(GameObject), false);
            if (prefabStatus.fixtureCount == 0)
            {
                DrawStatusRow("DmxFixtureComponent", StatusLevel.Warning, "No fixture component was found.");
            }
            else if (prefabStatus.needsSetupTargetLights.Count == 0 && prefabStatus.needsModeUpdateTargetLights.Count == 0 &&
                     prefabStatus.needsUrpPresetFixtureCount == 0)
            {
                DrawStatusRow("Target Light VLB", StatusLevel.Success,
                    $"All {prefabStatus.targetLightCount} target light(s) use VLB {_beamMode}.");
            }
            else
            {
                DrawStatusRow("Target Light VLB", StatusLevel.Warning,
                    $"{prefabStatus.readyTargetLightCount} ready, {prefabStatus.needsSetupTargetLights.Count} need setup, {prefabStatus.needsModeUpdateTargetLights.Count} need mode update, {prefabStatus.needsUrpPresetFixtureCount} need URP preset.");
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawProjectActions()
        {
            using (new EditorGUI.DisabledScope(!_status.vlbInstalled || !_status.HasFixableProjectSettings))
            {
                if (GUILayout.Button("Fix All Project Settings", GUILayout.Height(24f)))
                {
                    ApplyRecommendedProjectSettings();
                }
            }

            EditorGUILayout.HelpBox(
                "Fix All creates/configures VLB Config, enables URP Asset Depth Texture, and updates Universal Renderer depth settings.",
                MessageType.None);
        }

        private void DrawScanTargetActions(IReadOnlyCollection<string> prefabPaths)
        {
            var typesAvailable = TryGetRequiredBeamTypes(_beamMode, out var typeError);
            var prefabStatuses = typesAvailable ? InspectPrefabs(prefabPaths, _beamMode) : new List<PrefabStatus>();
            var needsSetup = prefabStatuses.Sum(status => status.needsSetupTargetLights.Count);
            var needsUpdate = prefabStatuses.Sum(status => status.needsModeUpdateTargetLights.Count);
            var needsUrpPreset = prefabStatuses.Sum(status => status.needsUrpPresetFixtureCount);
            using (new EditorGUI.DisabledScope(!_status.vlbInstalled || !typesAvailable || prefabPaths.Count == 0 || needsSetup + needsUpdate + needsUrpPreset == 0))
            {
                if (GUILayout.Button($"Apply VLB {_beamMode} to Target Lights", GUILayout.Height(24f)))
                {
                    ApplyBeamModeToScanTargets(prefabPaths, needsSetup, needsUpdate, needsUrpPreset);
                }
            }

            EditorGUILayout.HelpBox(
                typesAvailable
                    ? $"Applies VLB {_beamMode} and the URP VLB preset only to the Light objects referenced by DmxFixtureComponent. HD also adds VolumetricCookieHD."
                    : typeError,
                MessageType.None);
        }

        private void ApplyRecommendedProjectSettings()
        {
            var targets = _status.urpAssets.Count(asset => !asset.IsConfigured);
            var changes = new List<string>();
            if (!_status.vlbConfigFound)
                changes.Add("Create VLB Config asset and set Render Pipeline to URP");
            else if (!_status.vlbConfigIsUrp)
                changes.Add("Set VLB Config Render Pipeline to URP");
            if (targets > 0)
                changes.Add($"Configure {targets} URP Asset(s): Depth Texture ON, Depth Priming Disabled, Depth Texture Mode After Opaques");

            if (changes.Count == 0)
                return;

            if (!EditorUtility.DisplayDialog("Fix VLB / URP settings", string.Join("\n\n", changes), "Fix All", "Cancel"))
                return;

            var config = _status.vlbConfig;
            if (config == null && !TryCreateVlbConfig(out config, out var configError))
            {
                EditorUtility.DisplayDialog("VLB Config setup failed", configError, "OK");
                Refresh();
                return;
            }
            if (config != null && GetVlbConfigPipelineValue(config) != UrpRenderPipelineEnumValue)
                SetVlbConfigToUrp(config);

            foreach (var asset in _status.urpAssets)
                ConfigureUrpAsset(asset);

            AssetDatabase.SaveAssets();
            Refresh();
        }

        private void FixVlbConfig()
        {
            var action = _status.vlbConfigFound
                ? "Set the VLB Config Render Pipeline to URP and refresh VLB shaders?"
                : "Create the VLB Config asset, set its Render Pipeline to URP, and refresh VLB shaders?";
            if (!EditorUtility.DisplayDialog("Fix VLB Config", action, "Fix", "Cancel"))
                return;

            var config = _status.vlbConfig;
            if (config == null && !TryCreateVlbConfig(out config, out var error))
            {
                EditorUtility.DisplayDialog("VLB Config setup failed", error, "OK");
                Refresh();
                return;
            }

            SetVlbConfigToUrp(config);
            AssetDatabase.SaveAssets();
            Refresh();
        }

        private void ApplyBeamModeToScanTargets(IEnumerable<string> prefabPaths, int needsSetup, int needsUpdate, int needsUrpPreset)
        {
            if (!TryGetRequiredBeamTypes(_beamMode, out var error))
            {
                EditorUtility.DisplayDialog("VLB component not available", error, "OK");
                return;
            }

            var paths = prefabPaths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (needsSetup + needsUpdate + needsUrpPreset == 0)
                return;

            if (!EditorUtility.DisplayDialog(
                    $"Apply VLB {_beamMode} to Target Lights",
                    $"Prefab(s): {paths.Count}\nAdd VLB {_beamMode}: {needsSetup}\nSwitch mode or repair Cookie: {needsUpdate}\nApply URP VLB preset: {needsUrpPreset}\n\n" +
                    "Target Lights will be made consistent with the selected Beam Mode and URP VLB preset.",
                    "Apply", "Cancel"))
                return;

            var changedPrefabCount = 0;
            var changedLightCount = 0;
            foreach (var path in paths)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var changed = false;
                    Undo.RegisterFullObjectHierarchyUndo(root, $"Apply VLB {_beamMode} Beam Setup");
                    var processedLights = new HashSet<int>();
                    foreach (var fixture in root.GetComponentsInChildren<DmxFixtureComponent>(true))
                    {
                        var fixtureChanged = ApplyUrpVlbOverrides(fixture);
                        changed |= fixtureChanged;
                        foreach (var light in GetTargetLights(fixture))
                        {
                            if (light == null || !processedLights.Add(light.GetInstanceID()))
                                continue;

                            var beamChanged = ApplyBeamMode(light.gameObject, _beamMode);
                            var presetChanged = ApplyUrpBeamPreset(light.gameObject, _beamMode);
                            if (beamChanged || presetChanged)
                            {
                                changed = true;
                                changedLightCount++;
                            }
                        }
                    }

                    if (changed)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        changedPrefabCount++;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("VLB Target Light setup", $"Updated {changedPrefabCount} Prefab(s) and {changedLightCount} Target Light(s).", "OK");
            Refresh();
        }

        private void Refresh()
        {
            _status = InspectProject();
            Repaint();
        }

        private static ProjectStatus InspectProject()
        {
            var status = new ProjectStatus
            {
                vlbSdType = FindType(VlbSdTypeName),
                vlbHdType = FindType(VlbHdTypeName),
                vlbCookieHdType = FindType(VlbCookieHdTypeName),
                vlbConfig = FindVlbConfig(),
            };

            status.vlbInstalled = status.vlbSdType != null || status.vlbHdType != null;
            status.vlbConfigFound = status.vlbConfig != null;
            status.vlbConfigIsUrp = status.vlbConfigFound && GetVlbConfigPipelineValue(status.vlbConfig) == UrpRenderPipelineEnumValue;
            status.urpAssets = FindUsedUrpAssets();
            return status;
        }

        private static List<UrpAssetStatus> FindUsedUrpAssets()
        {
            var assets = new Dictionary<int, UrpAssetStatus>();
            AddUrpAsset(GraphicsSettings.defaultRenderPipeline, "Graphics Settings", assets);

            for (var qualityIndex = 0; qualityIndex < QualitySettings.names.Length; qualityIndex++)
            {
                var pipeline = QualitySettings.GetRenderPipelineAssetAt(qualityIndex);
                AddUrpAsset(pipeline, $"Quality: {QualitySettings.names[qualityIndex]}", assets);
            }

            return assets.Values.OrderBy(status => status.label, StringComparer.Ordinal).ToList();
        }

        private static void AddUrpAsset(RenderPipelineAsset asset, string source, IDictionary<int, UrpAssetStatus> assets)
        {
            if (asset == null || !IsUrpAsset(asset))
                return;

            if (assets.TryGetValue(asset.GetInstanceID(), out var existing))
            {
                existing.label = $"{existing.label}, {source}";
                return;
            }

            var serialized = new SerializedObject(asset);
            var depthTexture = serialized.FindProperty("m_RequireDepthTexture");
            var rendererList = serialized.FindProperty("m_RendererDataList");
            var rendererData = rendererList != null && rendererList.arraySize > 0
                ? rendererList.GetArrayElementAtIndex(0).objectReferenceValue
                : null;

            var status = new UrpAssetStatus
            {
                asset = asset,
                label = source,
                depthTextureEnabled = depthTexture != null && depthTexture.boolValue,
                rendererData = rendererData,
            };

            if (rendererData != null)
            {
                var rendererSerialized = new SerializedObject(rendererData);
                var depthPriming = rendererSerialized.FindProperty("m_DepthPrimingMode");
                var copyDepth = rendererSerialized.FindProperty("m_CopyDepthMode");
                status.depthPrimingDisabled = depthPriming != null && depthPriming.enumValueIndex == DepthPrimingDisabledEnumValue;
                status.copyDepthAfterOpaques = copyDepth != null && copyDepth.enumValueIndex == CopyDepthAfterOpaquesEnumValue;
            }

            assets.Add(asset.GetInstanceID(), status);
        }

        private static void ConfigureUrpAsset(UrpAssetStatus status)
        {
            var serialized = new SerializedObject(status.asset);
            var depthTexture = serialized.FindProperty("m_RequireDepthTexture");
            if (depthTexture != null)
            {
                depthTexture.boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(status.asset);
            }

            if (status.rendererData == null)
                return;

            var rendererSerialized = new SerializedObject(status.rendererData);
            var depthPriming = rendererSerialized.FindProperty("m_DepthPrimingMode");
            var copyDepth = rendererSerialized.FindProperty("m_CopyDepthMode");
            var changed = false;
            if (depthPriming != null)
            {
                depthPriming.enumValueIndex = DepthPrimingDisabledEnumValue;
                changed = true;
            }
            if (copyDepth != null)
            {
                copyDepth.enumValueIndex = CopyDepthAfterOpaquesEnumValue;
                changed = true;
            }
            if (changed)
            {
                rendererSerialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(status.rendererData);
            }
        }

        private static bool IsUrpAsset(RenderPipelineAsset asset)
        {
            return asset.GetType().FullName?.Contains("UniversalRenderPipelineAsset", StringComparison.Ordinal) == true;
        }

        private static ScriptableObject FindVlbConfig()
        {
            var type = FindType(VlbConfigTypeName);
            if (type == null)
                return null;

            foreach (var guid in AssetDatabase.FindAssets($"t:{type.Name}"))
            {
                var asset = AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guid), type) as ScriptableObject;
                if (asset != null && asset.GetType() == type)
                    return asset;
            }

            return Resources.FindObjectsOfTypeAll(type).OfType<ScriptableObject>().FirstOrDefault();
        }

        private static bool TryCreateVlbConfig(out ScriptableObject config, out string error)
        {
            config = FindVlbConfig();
            error = null;
            if (config != null)
                return true;

            var type = FindType(VlbConfigTypeName);
            var instanceProperty = type?.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public);
            if (instanceProperty == null)
            {
                error = "The installed VLB version does not expose Config.Instance, so its Config asset could not be created automatically.";
                return false;
            }

            try
            {
                config = instanceProperty.GetValue(null) as ScriptableObject;
                if (config != null)
                    return true;
            }
            catch (TargetInvocationException exception)
            {
                error = $"VLB could not create its Config asset: {exception.InnerException?.Message ?? exception.Message}";
                return false;
            }
            catch (Exception exception)
            {
                error = $"VLB could not create its Config asset: {exception.Message}";
                return false;
            }

            error = "VLB did not return a Config asset. Open VLB Config once and retry.";
            return false;
        }

        private static int GetVlbConfigPipelineValue(ScriptableObject config)
        {
            var serialized = new SerializedObject(config);
            var property = serialized.FindProperty("m_RenderPipeline");
            return property == null ? -1 : property.enumValueIndex;
        }

        private static void SetVlbConfigToUrp(ScriptableObject config)
        {
            var serialized = new SerializedObject(config);
            var property = serialized.FindProperty("m_RenderPipeline");
            if (property == null)
                return;

            property.enumValueIndex = UrpRenderPipelineEnumValue;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);

            // VLB owns its shader generation and render-pipeline define symbols. Invoke
            // those public editor APIs when present, without creating a hard dependency.
            var configType = config.GetType();
            configType.GetMethod("SetScriptingDefineSymbolsForCurrentRenderPipeline", BindingFlags.Instance | BindingFlags.Public)
                ?.Invoke(config, null);

            var refreshMethod = configType.GetMethod("RefreshShaders", BindingFlags.Instance | BindingFlags.Public);
            if (refreshMethod?.GetParameters().Length == 1)
            {
                var flagsType = refreshMethod.GetParameters()[0].ParameterType;
                if (flagsType.IsEnum)
                {
                    try
                    {
                        refreshMethod.Invoke(config, new[] { Enum.Parse(flagsType, "All") });
                    }
                    catch (ArgumentException)
                    {
                        // Keep the config change even when an older VLB version does not expose an All flag.
                    }
                }
            }
        }

        private static bool TryGetRequiredBeamTypes(BeamMode mode, out string error)
        {
            error = null;
            var beamType = FindType(mode == BeamMode.SD ? VlbSdTypeName : VlbHdTypeName);
            if (beamType == null || !typeof(Component).IsAssignableFrom(beamType))
            {
                error = mode == BeamMode.SD
                    ? "VolumetricLightBeamSD could not be resolved. Check that VLB has finished compiling."
                    : "VolumetricLightBeamHD could not be resolved. Check that VLB has finished compiling.";
                return false;
            }

            if (mode != BeamMode.HD)
                return true;

            var cookieType = FindType(VlbCookieHdTypeName);
            if (cookieType != null && typeof(Component).IsAssignableFrom(cookieType))
                return true;

            error = "VolumetricCookieHD could not be resolved. HD setup requires the VLB Cookie HD API.";
            return false;
        }

        private static bool ApplyBeamMode(GameObject gameObject, BeamMode mode)
        {
            var sdType = FindType(VlbSdTypeName);
            var hdType = FindType(VlbHdTypeName);
            var cookieType = FindType(VlbCookieHdTypeName);
            var changed = false;

            if (mode == BeamMode.SD)
            {
                changed |= RemoveComponentIfPresent(gameObject, hdType);
                changed |= RemoveComponentIfPresent(gameObject, cookieType);
                changed |= AddComponentIfMissing(gameObject, sdType);
            }
            else
            {
                changed |= RemoveComponentIfPresent(gameObject, sdType);
                changed |= AddComponentIfMissing(gameObject, hdType);
                changed |= AddComponentIfMissing(gameObject, cookieType);
            }

            return changed;
        }

        private static bool ApplyUrpVlbOverrides(DmxFixtureComponent fixture)
        {
            if (fixture == null || HasUrpVlbOverrides(fixture))
                return false;

            fixture.ApplyVlbPipelineDefaults();
            EditorUtility.SetDirty(fixture);
            return true;
        }

        private static bool HasUrpVlbOverrides(DmxFixtureComponent fixture)
        {
            var serialized = new SerializedObject(fixture);
            var overrideHd = serialized.FindProperty("overrideVlbHdIntensityMultiplier");
            var hdMultiplier = serialized.FindProperty("vlbHdIntensityMultiplier");
            var overrideSd = serialized.FindProperty("overrideVlbSdIntensityMultiplier");
            var sdMultiplier = serialized.FindProperty("vlbSdIntensityMultiplier");
            return overrideHd != null && hdMultiplier != null && overrideSd != null && sdMultiplier != null &&
                   overrideHd.boolValue && overrideSd.boolValue &&
                   Mathf.Approximately(hdMultiplier.floatValue, 0.01f) &&
                   Mathf.Approximately(sdMultiplier.floatValue, 0.01f);
        }

        private static bool ApplyUrpBeamPreset(GameObject gameObject, BeamMode mode)
        {
            var beamType = FindType(mode == BeamMode.SD ? VlbSdTypeName : VlbHdTypeName);
            var beam = beamType == null ? null : gameObject.GetComponent(beamType);
            if (beam == null)
                return false;

            var changed = false;
            changed |= TrySetVlbMember(beam, "colorFromLight", true);
            changed |= TrySetVlbMember(beam, "intensityMultiplier", 0.01f);

            if (mode == BeamMode.SD)
            {
                changed |= TrySetVlbMember(beam, "intensityFromLight", true);
                changed |= TrySetVlbMember(beam, "spotAngleFromLight", true);
                changed |= TrySetVlbMember(beam, "spotAngleMultiplier", 1.3f);
                changed |= TrySetVlbMember(beam, "coneRadiusStart", 0.01f);
                changed |= TrySetVlbMember(beam, "fallOffEndFromLight", true);
                changed |= TrySetVlbMember(beam, "depthBlendDistance", 2f);
                changed |= TrySetVlbMember(beam, "cameraClippingDistance", 0.5f);
            }
            else
            {
                changed |= TrySetVlbMember(beam, "useIntensityFromAttachedLightSpot", true);
                changed |= TrySetVlbMember(beam, "useSpotAngleFromAttachedLightSpot", true);
                changed |= TrySetVlbMember(beam, "useFallOffEndFromAttachedLightSpot", true);
                InvokeVlbMethod(beam, "AssignPropertiesFromAttachedSpotLight");
                InvokeVlbMethod(beam, "UpdateAfterManualPropertyChange");
            }

            if (changed)
                EditorUtility.SetDirty(beam);
            return changed;
        }

        private static bool TrySetVlbMember(Component component, string name, object value)
        {
            var type = component.GetType();
            var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            if (property != null && property.CanWrite && property.PropertyType == value.GetType())
            {
                var current = property.GetValue(component);
                if (Equals(current, value))
                    return false;

                property.SetValue(component, value);
                return true;
            }

            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public);
            if (field == null || field.FieldType != value.GetType())
                return false;

            var fieldValue = field.GetValue(component);
            if (Equals(fieldValue, value))
                return false;

            field.SetValue(component, value);
            return true;
        }

        private static void InvokeVlbMethod(Component component, string name)
        {
            component.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null)
                ?.Invoke(component, null);
        }

        private static bool AddComponentIfMissing(GameObject gameObject, Type componentType)
        {
            if (componentType == null || gameObject.GetComponent(componentType) != null)
                return false;

            Undo.AddComponent(gameObject, componentType);
            return true;
        }

        private static bool RemoveComponentIfPresent(GameObject gameObject, Type componentType)
        {
            var component = componentType == null ? null : gameObject.GetComponent(componentType);
            if (component == null)
                return false;

            Undo.DestroyObjectImmediate(component);
            return true;
        }

        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName, false);
                if (type != null)
                    return type;
            }
            return null;
        }

        private static GameObject ResolvePrefabAsset(GameObject rootObject)
        {
            if (rootObject == null)
                return null;

            var directPath = AssetDatabase.GetAssetPath(rootObject);
            if (!string.IsNullOrEmpty(directPath) && directPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                return AssetDatabase.LoadAssetAtPath<GameObject>(directPath);

            var instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(rootObject);
            if (instanceRoot == null)
                return null;

            var prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instanceRoot);
            return string.IsNullOrEmpty(prefabPath) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        private static string GetScanTargetIssue(ScanTarget target, out GameObject prefabAsset)
        {
            prefabAsset = null;
            var rootPrefabAsset = ResolvePrefabAsset(target.rootObject);
            var directPrefabAsset = target.prefabAsset;
            if (directPrefabAsset != null)
            {
                var path = AssetDatabase.GetAssetPath(directPrefabAsset);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    return "Prefab Asset must reference a Prefab asset in the Project window.";
            }

            if (target.rootObject != null && rootPrefabAsset == null)
                return "Root Object must be a Prefab instance or a Prefab asset.";
            if (rootPrefabAsset != null && directPrefabAsset != null &&
                !string.Equals(AssetDatabase.GetAssetPath(rootPrefabAsset), AssetDatabase.GetAssetPath(directPrefabAsset), StringComparison.OrdinalIgnoreCase))
                return "Root Object and Prefab Asset refer to different Prefabs. This target will not be processed.";

            prefabAsset = directPrefabAsset ?? rootPrefabAsset;
            return prefabAsset == null ? "Specify either Root Object or Prefab Asset." : null;
        }

        private static List<PrefabStatus> InspectPrefabs(IEnumerable<string> prefabPaths, BeamMode mode)
        {
            var statuses = new List<PrefabStatus>();
            foreach (var path in prefabPaths)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var fixtures = root.GetComponentsInChildren<DmxFixtureComponent>(true);
                    var status = new PrefabStatus
                    {
                        prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path),
                        fixtureCount = fixtures.Length,
                    };
                    var processedLights = new HashSet<int>();
                    foreach (var fixture in fixtures)
                    {
                        if (!HasUrpVlbOverrides(fixture))
                            status.needsUrpPresetFixtureCount++;

                        foreach (var light in GetTargetLights(fixture))
                        {
                            if (light == null || !processedLights.Add(light.GetInstanceID()))
                                continue;

                            status.targetLightCount++;
                            if (IsBeamModeConfigured(light.gameObject, mode))
                                status.readyTargetLightCount++;
                            else if (HasAnyBeamComponent(light.gameObject))
                                status.needsModeUpdateTargetLights.Add(light.name);
                            else
                                status.needsSetupTargetLights.Add(light.name);
                        }
                    }
                    statuses.Add(status);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            return statuses;
        }

        private static IEnumerable<Light> GetTargetLights(DmxFixtureComponent fixture)
        {
            var lights = new HashSet<Light>();
            if (fixture.targetLight != null)
                lights.Add(fixture.targetLight);
            if (fixture.targetLights != null)
            {
                foreach (var light in fixture.targetLights)
                {
                    if (light != null)
                        lights.Add(light);
                }
            }
            return lights;
        }

        private static bool IsBeamModeConfigured(GameObject gameObject, BeamMode mode)
        {
            var sdType = FindType(VlbSdTypeName);
            var hdType = FindType(VlbHdTypeName);
            var cookieType = FindType(VlbCookieHdTypeName);
            var hasSd = sdType != null && gameObject.GetComponent(sdType) != null;
            var hasHd = hdType != null && gameObject.GetComponent(hdType) != null;
            var hasCookie = cookieType != null && gameObject.GetComponent(cookieType) != null;
            return mode == BeamMode.SD ? hasSd && !hasHd && !hasCookie : hasHd && !hasSd && hasCookie;
        }

        private static bool HasAnyBeamComponent(GameObject gameObject)
        {
            var sdType = FindType(VlbSdTypeName);
            var hdType = FindType(VlbHdTypeName);
            var cookieType = FindType(VlbCookieHdTypeName);
            return (sdType != null && gameObject.GetComponent(sdType) != null) ||
                   (hdType != null && gameObject.GetComponent(hdType) != null) ||
                   (cookieType != null && gameObject.GetComponent(cookieType) != null);
        }

        private StatusSummary GetProjectSettingsSummary()
        {
            var summary = new StatusSummary();
            summary.Add(_status.vlbInstalled ? StatusLevel.Success : StatusLevel.Error);
            if (_status.vlbInstalled)
                summary.Add(_status.vlbConfigFound && _status.vlbConfigIsUrp ? StatusLevel.Success : StatusLevel.Warning);

            if (_status.urpAssets.Count == 0)
            {
                summary.Add(StatusLevel.Error);
                return summary;
            }

            foreach (var asset in _status.urpAssets)
            {
                summary.Add(asset.depthTextureEnabled ? StatusLevel.Success : StatusLevel.Warning);
                if (asset.rendererData == null)
                {
                    summary.Add(StatusLevel.Error);
                    continue;
                }

                summary.Add(asset.depthPrimingDisabled ? StatusLevel.Success : StatusLevel.Warning);
                summary.Add(asset.copyDepthAfterOpaques ? StatusLevel.Success : StatusLevel.Warning);
            }

            return summary;
        }

        private static void DrawStatusSummary(string title, StatusSummary summary)
        {
            var detail = summary.ErrorCount > 0
                ? $"{summary.ErrorCount} error(s), {summary.WarningCount} setting(s) need attention."
                : summary.WarningCount > 0
                    ? $"{summary.SuccessCount} check(s) passed, {summary.WarningCount} setting(s) need attention."
                    : $"All {summary.SuccessCount} check(s) passed. No action is required.";
            DrawStatusRow(title, summary.OverallLevel, detail);
        }

        private static void DrawStatusRow(string title, StatusLevel level, string detail)
        {
            var rect = EditorGUILayout.GetControlRect(false, 46f);
            DrawStatusRowContent(rect, title, level, detail);
        }

        private static void DrawStatusRowWithFix(string title, StatusLevel level, string detail, Action fixAction)
        {
            var rect = EditorGUILayout.GetControlRect(false, 46f);
            var contentRect = new Rect(rect.x, rect.y, rect.width - 74f, rect.height);
            DrawStatusRowContent(contentRect, title, level, detail);
            if (GUI.Button(new Rect(rect.xMax - 66f, rect.y + 11f, 58f, 24f), "Fix"))
                fixAction?.Invoke();
        }

        private static void DrawStatusRowContent(Rect rect, string title, StatusLevel level, string detail)
        {
            EditorGUI.DrawRect(rect, GetStatusBackgroundColor(level));

            var iconRect = new Rect(rect.x + 7f, rect.y + 10f, 22f, 22f);
            var titleRect = new Rect(rect.x + 36f, rect.y + 5f, rect.width - 43f, 18f);
            var detailRect = new Rect(rect.x + 36f, rect.y + 23f, rect.width - 43f, 18f);
            GUI.Label(iconRect, EditorGUIUtility.IconContent(GetStatusIconName(level)));
            EditorGUI.LabelField(titleRect, title, EditorStyles.boldLabel);
            EditorGUI.LabelField(detailRect, detail, EditorStyles.miniLabel);
        }

        private static Color GetStatusBackgroundColor(StatusLevel level)
        {
            return level switch
            {
                StatusLevel.Success => new Color(0.08f, 0.24f, 0.14f, 0.55f),
                StatusLevel.Warning => new Color(0.32f, 0.23f, 0.06f, 0.55f),
                _ => new Color(0.32f, 0.09f, 0.09f, 0.55f)
            };
        }

        private static string GetStatusIconName(StatusLevel level)
        {
            return level switch
            {
                StatusLevel.Success => "TestPassed",
                StatusLevel.Warning => "console.warnicon",
                _ => "console.erroricon"
            };
        }

        private enum StatusLevel
        {
            Success,
            Warning,
            Error
        }

        private enum BeamMode
        {
            SD,
            HD
        }

        private sealed class StatusSummary
        {
            public int SuccessCount { get; private set; }
            public int WarningCount { get; private set; }
            public int ErrorCount { get; private set; }
            public StatusLevel OverallLevel => ErrorCount > 0 ? StatusLevel.Error : WarningCount > 0 ? StatusLevel.Warning : StatusLevel.Success;

            public void Add(StatusLevel level)
            {
                switch (level)
                {
                    case StatusLevel.Success:
                        SuccessCount++;
                        break;
                    case StatusLevel.Warning:
                        WarningCount++;
                        break;
                    default:
                        ErrorCount++;
                        break;
                }
            }
        }

        private sealed class ProjectStatus
        {
            public Type vlbSdType;
            public Type vlbHdType;
            public Type vlbCookieHdType;
            public bool vlbInstalled;
            public ScriptableObject vlbConfig;
            public bool vlbConfigFound;
            public bool vlbConfigIsUrp;
            public List<UrpAssetStatus> urpAssets = new();
            public bool HasFixableProjectSettings =>
                !vlbConfigFound || !vlbConfigIsUrp || urpAssets.Any(asset => !asset.IsConfigured);
        }

        private sealed class UrpAssetStatus
        {
            public RenderPipelineAsset asset;
            public string label;
            public bool depthTextureEnabled;
            public UnityEngine.Object rendererData;
            public bool depthPrimingDisabled;
            public bool copyDepthAfterOpaques;
            public bool IsConfigured => depthTextureEnabled && rendererData != null && depthPrimingDisabled && copyDepthAfterOpaques;
        }

        private sealed class PrefabStatus
        {
            public GameObject prefab;
            public int fixtureCount;
            public int targetLightCount;
            public int readyTargetLightCount;
            public int needsUrpPresetFixtureCount;
            public List<string> needsSetupTargetLights = new();
            public List<string> needsModeUpdateTargetLights = new();
        }

        private sealed class ScanTarget
        {
            public GameObject rootObject;
            public GameObject prefabAsset;
        }
    }
}
