/*!
 * Copyright (c) 2026 Oshino
 *
 * Released under the MIT license.
 * see https://opensource.org/licenses/MIT
 */

using ArtNet.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArtNet.Editor
{
    [CustomEditor(typeof(DmxFixtureComponent))]
    [CanEditMultipleObjects]
    public sealed class DmxFixtureComponentEditor : UnityEditor.Editor
    {
        private static int _sourceFocusFixtureId;
        private static string _sourceFocusPropertyPath;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var property = serializedObject.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                if (ShouldDrawIrisBefore(property.propertyPath))
                    DrawIrisSection();

                if (property.propertyPath == "panTiltSpeedMinDegPerSec")
                    DrawPanTiltSpeedResolution();

                if (property.propertyPath == "lensMaterialBindings")
                {
                    enterChildren = false;
                    continue;
                }

                // Iris is rendered explicitly below Prism so its visual order is stable,
                // independent of partial-class field serialization order.
                if (IsIrisProperty(property.propertyPath))
                {
                    enterChildren = false;
                    continue;
                }

                using (new EditorGUI.DisabledScope(property.propertyPath == "m_Script"))
                {
                    if (property.propertyPath == "prismGoboScaleVlb")
                        EditorGUILayout.Slider(property, 0.1f, 3f, new GUIContent(
                            "Prism Gobo Scale (VLB)",
                            "VLBのプリズム時に、各ゴボ投影の外形サイズをSpot Angleで調整します。1で通常のSpot Lightに合わせた基準サイズです。\nAdjusts the outline size of each gobo projection by Spot Angle when using a VLB prism. A value of 1 matches the standard Spot Light size."));
                    else
                        EditorGUILayout.PropertyField(property, CreateDisplayContent(property), true);
                }

                if (property.propertyPath == "vlbHdrpExposureWeight")
                    DrawVlbDefaultsButton();

                if (property.propertyPath == "goboLensHotspotStrength")
                    DrawGoboLensMaterialSetup();

                if (property.propertyPath == "auxiliaryLightShadows")
                    DrawOptionalPrismShadowWarning(property);

                enterChildren = false;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawPanTiltSpeedResolution()
        {
            if (targets.Length != 1 || target is not DmxFixtureComponent fixture)
            {
                EditorGUILayout.HelpBox("Pan/Tilt Speed Resolution is available when one fixture is selected.", MessageType.Info);
                return;
            }

            var resolution = fixture.GetPanTiltSpeedResolution();
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Pan/Tilt Speed Resolution", EditorStyles.boldLabel);

            switch (resolution.mode)
            {
                case DmxFixtureComponent.PanTiltSpeedMode.ContinuousDmx:
                    EditorGUILayout.LabelField("Speed Mode", "Continuous DMX");
                    EditorGUILayout.LabelField("Active Setting", resolution.isRuntimeValue ? $"DMX {resolution.currentDmxValue}" : "DMX Input (0-255)");
                    EditorGUILayout.LabelField("Applied Speed", resolution.isRuntimeValue
                        ? $"{resolution.appliedSpeedDegPerSec:0.##} deg/sec"
                        : $"{fixture.panTiltSpeedMinDegPerSec:0.##} - {fixture.panTiltSpeedMaxDegPerSec:0.##} deg/sec");
                    EditorGUILayout.LabelField("Speed Range Status", "Applied (DMX Interpolation)");
                    DrawControlSource(fixture, resolution, $"{GetFixtureAndModeLabel(fixture)} / Ch {resolution.controlRelativeChannel} / Pan Tilt Speed");
                    DrawSpeedRangeSource(fixture);
                    break;

                case DmxFixtureComponent.PanTiltSpeedMode.Preset:
                    string presetName = GetPresetName(resolution.activePresetRange);
                    EditorGUILayout.LabelField("Speed Mode", "Preset");
                    EditorGUILayout.LabelField("Active Setting", presetName);
                    EditorGUILayout.LabelField("Applied Speed", $"{resolution.appliedSpeedDegPerSec:0.##} deg/sec");
                    EditorGUILayout.LabelField("Speed Range Status", "Applied (Preset)");
                    string dmxValue = resolution.activePresetRange != null
                        ? $" / DMX {resolution.activePresetRange.dmxMin}"
                        : string.Empty;
                    DrawControlSource(fixture, resolution, $"{GetFixtureAndModeLabel(fixture)} / Ch {resolution.controlRelativeChannel} / {presetName}{dmxValue}");
                    DrawSpeedRangeSource(fixture);
                    break;

                case DmxFixtureComponent.PanTiltSpeedMode.AutoSmoothing:
                    EditorGUILayout.LabelField("Speed Mode", "Auto Smoothing");
                    EditorGUILayout.LabelField("Applied Setting", $"Pan/Tilt Smoothing = {fixture.panTiltSmoothing:0.##}");
                    EditorGUILayout.LabelField("Applied Speed", "—");
                    EditorGUILayout.LabelField("Speed Range Status", "Not Applied");
                    EditorGUILayout.LabelField("Source", "Dmx Fixture Component / Pan/Tilt Smoothing");
                    if (GUILayout.Button("Open Source Setting"))
                        FocusComponentSetting(fixture, "panTiltSmoothing");
                    break;

                default:
                    EditorGUILayout.LabelField("Speed Mode", "Unresolved");
                    EditorGUILayout.LabelField("Source", "Fixture Definition / Mode");
                    if (GUILayout.Button("Open Source Setting"))
                        FocusComponentSetting(fixture, "fixture");
                    break;
            }

            if (_sourceFocusFixtureId == fixture.GetInstanceID())
            {
                EditorGUILayout.HelpBox($"Source setting: {_sourceFocusPropertyPath}", MessageType.Info);
                _sourceFocusFixtureId = 0;
                _sourceFocusPropertyPath = null;
            }

            EditorGUILayout.Space(4f);
        }

        private static void DrawControlSource(DmxFixtureComponent fixture, DmxFixtureComponent.PanTiltSpeedResolution resolution, string label)
        {
            EditorGUILayout.LabelField("Control Source", label);
            if (GUILayout.Button("Open Control Source"))
                FixtureDefinitionSourceNavigator.Open(fixture.fixture, fixture.mode, resolution.controlRelativeChannel, resolution.activePresetRange);
        }

        private static void DrawSpeedRangeSource(DmxFixtureComponent fixture)
        {
            EditorGUILayout.LabelField("Speed Range Source", $"Dmx Fixture Component / Min {fixture.panTiltSpeedMinDegPerSec:0.##} / Max {fixture.panTiltSpeedMaxDegPerSec:0.##} deg/sec");
            if (GUILayout.Button("Open Speed Range Settings"))
                FocusComponentSetting(fixture, "panTiltSpeedMinDegPerSec");
        }

        private static string GetFixtureAndModeLabel(DmxFixtureComponent fixture)
        {
            if (fixture.fixture == null) return "(No Fixture)";
            string modeName = "(No Mode)";
            if (fixture.fixture.modes != null && fixture.fixture.modes.Count > 0)
            {
                int modeIndex = Mathf.Clamp(fixture.mode, 0, fixture.fixture.modes.Count - 1);
                modeName = fixture.fixture.modes[modeIndex]?.modeName;
                if (string.IsNullOrWhiteSpace(modeName)) modeName = $"Mode {modeIndex}";
            }

            return $"{fixture.fixture.GetDisplayLabel()} / {modeName}";
        }

        private static string GetPresetName(FixtureChannelRange range)
        {
            return range?.type switch
            {
                FixtureRangeType.PanTiltSpeedFast => "Fast",
                FixtureRangeType.PanTiltSpeedSmooth => "Smooth",
                FixtureRangeType.PanTiltSpeedStandard => "Standard",
                _ => "Standard"
            };
        }

        private static void FocusComponentSetting(DmxFixtureComponent fixture, string propertyPath)
        {
            _sourceFocusFixtureId = fixture.GetInstanceID();
            _sourceFocusPropertyPath = propertyPath;
            Selection.activeObject = fixture;
            EditorGUIUtility.PingObject(fixture);
            ActiveEditorTracker.sharedTracker.ForceRebuild();
        }

        private void DrawOptionalPrismShadowWarning(SerializedProperty auxiliaryLightShadows)
        {
            var enablePrism = serializedObject.FindProperty("enablePrism");
            bool prismCanUseAuxiliaryShadows = enablePrism != null &&
                                                (enablePrism.boolValue || enablePrism.hasMultipleDifferentValues) &&
                                                (auxiliaryLightShadows.boolValue || auxiliaryLightShadows.hasMultipleDifferentValues);
            if (!prismCanUseAuxiliaryShadows ||
                !VlbUrpSetupWindow.TryGetOptionalPrismShadowIssue(out string details))
                return;

            EditorGUILayout.HelpBox(
                "プリズム補助ライトの影が有効ですが、影を表示するためのURP設定が不足しています。\n" +
                details + "\n" +
                "Art-Net > VLB > URP Setup の Optional Prism Shadow Settings で確認・修正してください。\n\n" +
                "Prism auxiliary-light shadows are enabled, but the required URP settings are incomplete.\n" +
                details + "\n" +
                "Review and fix Optional Prism Shadow Settings in Art-Net > VLB > URP Setup.",
                MessageType.Warning);

            if (GUILayout.Button("Open VLB URP Setup"))
                VlbUrpSetupWindow.OpenWindow(focusOptionalPrismShadows: true);
        }

        private static bool ShouldDrawIrisBefore(string propertyPath)
        {
            return propertyPath == "lightResponseMode";
        }

        private static bool IsIrisProperty(string propertyPath)
        {
            return propertyPath == "syncIrisToDmx" || propertyPath == "irisProfile" || propertyPath == "irisInstance";
        }

        private void DrawIrisSection()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Iris (all light / beam modes)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("syncIrisToDmx"),
                new GUIContent("Sync Iris To DMX", "DMXでアイリスを制御します。\nControls the iris from DMX."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("irisProfile"),
                new GUIContent("Iris Profile", "Fixture DefinitionのIris Profileを上書きします。\nOverrides the Iris Profile from the Fixture Definition."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("irisInstance"),
                new GUIContent("Iris Instance", "複数のIris属性を使う場合の番号です。\nInstance number when multiple Iris attributes are defined."));
            EditorGUILayout.HelpBox(
                "IrisはFixture DefinitionのIris ProfileとChannel Elementsで設定します（このコンポーネントで上書き可能）。\n" +
                "Zoomと異なりゴボの倍率を変えず外周を遮ります。VLB HDはCookie合成、SDは円形ビームの幅で再現します。\n\n" +
                "Configure Iris in the Fixture Definition with an Iris Profile and Channel Elements (the component override above is optional).\n" +
                "Unlike Zoom, Iris masks the outer area without changing gobo magnification. VLB HD uses Cookie composition; SD approximates it with beam width.",
                MessageType.Info);
        }

        private static GUIContent CreateDisplayContent(SerializedProperty property)
        {
            string displayName = property.displayName
                .Replace("Dmx", "DMX")
                .Replace("Hdrp", "HDRP")
                .Replace("Vlb", "VLB")
                .Replace("Urp", "URP")
                .Replace("Hd", "HD")
                .Replace("Sd", "SD");
            return new GUIContent(displayName, property.tooltip);
        }

        private void DrawGoboLensMaterialSetup()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Gobo Lens Material Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Setup Gobo Lens Material は、現在のRender Pipeline（HDRP / URP）を一度だけ判定し、登録済みの各Rendererスロットへ対応する共有マテリアルを割り当てます。再生中の差し替えは行いません。\n" +
                "\n" +
                "Setup Gobo Lens Material detects the current Render Pipeline (HDRP / URP) once and assigns the corresponding shared material to each registered Renderer slot. Materials are not replaced during Play Mode.",
                MessageType.Info);

            var bindings = serializedObject.FindProperty("lensMaterialBindings");
            DrawLensMaterialBindings(bindings);

            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                if (GUILayout.Button("Setup Gobo Lens Material"))
                {
                    serializedObject.ApplyModifiedProperties();
                    SetupGoboLensMaterialForTargets();
                    serializedObject.Update();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Validate Gobo Lens Setup"))
                    ValidateGoboLensSetupForTargets();

                using (new EditorGUI.DisabledScope(Application.isPlaying))
                {
                    if (GUILayout.Button("Restore Original Materials"))
                    {
                        serializedObject.ApplyModifiedProperties();
                        RestoreOriginalMaterialsForTargets();
                        serializedObject.Update();
                    }
                }
            }
        }

        private static void DrawLensMaterialBindings(SerializedProperty bindings)
        {
            bindings.isExpanded = EditorGUILayout.Foldout(bindings.isExpanded, "Lens Material Bindings", true);
            if (!bindings.isExpanded)
                return;

            EditorGUI.indentLevel++;
            for (int i = 0; i < bindings.arraySize; i++)
            {
                var binding = bindings.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField($"Binding {i}", EditorStyles.miniBoldLabel);
                    EditorGUILayout.PropertyField(binding.FindPropertyRelative("renderer"));
                    EditorGUILayout.PropertyField(binding.FindPropertyRelative("materialSlot"));
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.PropertyField(binding.FindPropertyRelative("originalMaterial"), new GUIContent("Original Material"));
                        EditorGUILayout.PropertyField(binding.FindPropertyRelative("goboLensMaterial"), new GUIContent("Assigned Gobo Lens Material"));
                    }
                    if (GUILayout.Button("Remove Binding"))
                    {
                        bindings.DeleteArrayElementAtIndex(i);
                        break;
                    }
                }
            }

            if (GUILayout.Button("Add Lens Material Binding"))
            {
                int index = bindings.arraySize;
                bindings.InsertArrayElementAtIndex(index);
                var binding = bindings.GetArrayElementAtIndex(index);
                binding.FindPropertyRelative("renderer").objectReferenceValue = null;
                binding.FindPropertyRelative("materialSlot").intValue = 0;
                binding.FindPropertyRelative("originalMaterial").objectReferenceValue = null;
                binding.FindPropertyRelative("goboLensMaterial").objectReferenceValue = null;
            }
            EditorGUI.indentLevel--;
        }

        private static Material FindGoboLensMaterialForCurrentPipeline(out string expectedShaderName)
        {
            var pipeline = GraphicsSettings.currentRenderPipeline != null
                ? GraphicsSettings.currentRenderPipeline
                : GraphicsSettings.defaultRenderPipeline;
            string pipelineName = pipeline != null ? pipeline.GetType().FullName : string.Empty;
            bool isHdrp = pipelineName != null && pipelineName.Contains("HighDefinition");
            bool isUrp = pipelineName != null && pipelineName.Contains("Universal");

            if (!isHdrp && !isUrp)
            {
                expectedShaderName = null;
                return null;
            }

            string materialName = isHdrp ? "GoboLensSurface_HDRP" : "GoboLensSurface_URP";
            expectedShaderName = isHdrp ? "ArtNet/HDRP/Gobo Lens Surface" : "ArtNet/URP/Gobo Lens Surface";
            string[] guids = AssetDatabase.FindAssets($"{materialName} t:Material");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material != null && material.shader != null && material.shader.name == expectedShaderName)
                    return material;
            }

            return null;
        }

        private void SetupGoboLensMaterialForTargets()
        {
            var material = FindGoboLensMaterialForCurrentPipeline(out string expectedShaderName);
            if (material == null)
            {
                Debug.LogError("Gobo Lens Material Setup: 現在のRender Pipelineに対応する共有マテリアルが見つかりません。HDRPまたはURPを有効にし、UPMパッケージを再importしてください。\nNo shared material compatible with the current Render Pipeline was found. Enable HDRP or URP and reimport the UPM package.");
                return;
            }

            int configured = 0;
            for (int targetIndex = 0; targetIndex < targets.Length; targetIndex++)
            {
                var targetObject = new SerializedObject(targets[targetIndex]);
                var bindings = targetObject.FindProperty("lensMaterialBindings");
                if (bindings == null) continue;
                MigrateLegacyLensRenderersToBindings(targetObject, bindings);

                for (int i = 0; i < bindings.arraySize; i++)
                {
                    var binding = bindings.GetArrayElementAtIndex(i);
                    var renderer = binding.FindPropertyRelative("renderer").objectReferenceValue as Renderer;
                    int slot = binding.FindPropertyRelative("materialSlot").intValue;
                    if (renderer == null || slot < 0 || renderer.sharedMaterials == null || slot >= renderer.sharedMaterials.Length)
                    {
                        Debug.LogWarning($"Gobo Lens Material Setup: {targets[targetIndex].name} のBinding {i} はRendererまたはMaterial Slotが無効です。\nBinding {i} on {targets[targetIndex].name} has an invalid Renderer or Material Slot.", targets[targetIndex]);
                        continue;
                    }

                    Undo.RecordObject(renderer, "Setup Gobo Lens Material");
                    var materials = renderer.sharedMaterials;
                    var original = binding.FindPropertyRelative("originalMaterial");
                    if (original.objectReferenceValue == null) original.objectReferenceValue = materials[slot];
                    materials[slot] = material;
                    renderer.sharedMaterials = materials;
                    binding.FindPropertyRelative("goboLensMaterial").objectReferenceValue = material;
                    EditorUtility.SetDirty(renderer);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                    configured++;
                }

                bindings.serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(targets[targetIndex]);
                PrefabUtility.RecordPrefabInstancePropertyModifications(targets[targetIndex]);
            }
            Debug.Log($"Gobo Lens Material Setup: {configured} slot(s) に {expectedShaderName} を割り当てました。\nAssigned {expectedShaderName} to {configured} slot(s).");
        }

        private static void MigrateLegacyLensRenderersToBindings(SerializedObject targetObject, SerializedProperty bindings)
        {
            if (bindings.arraySize > 0) return;
            AddRendererBinding(targetObject.FindProperty("lensRenderer"), bindings);
            var extraRenderers = targetObject.FindProperty("extraLensRenderers");
            if (extraRenderers == null || !extraRenderers.isArray) return;
            for (int i = 0; i < extraRenderers.arraySize; i++) AddRendererBinding(extraRenderers.GetArrayElementAtIndex(i), bindings);
        }

        private static void AddRendererBinding(SerializedProperty rendererProperty, SerializedProperty bindings)
        {
            var renderer = rendererProperty != null ? rendererProperty.objectReferenceValue as Renderer : null;
            if (renderer == null) return;
            int index = bindings.arraySize;
            bindings.InsertArrayElementAtIndex(index);
            var binding = bindings.GetArrayElementAtIndex(index);
            binding.FindPropertyRelative("renderer").objectReferenceValue = renderer;
            binding.FindPropertyRelative("materialSlot").intValue = 0;
        }

        private void RestoreOriginalMaterialsForTargets()
        {
            int restored = 0;
            for (int targetIndex = 0; targetIndex < targets.Length; targetIndex++)
            {
                var bindings = new SerializedObject(targets[targetIndex]).FindProperty("lensMaterialBindings");
                if (bindings == null) continue;
                for (int i = 0; i < bindings.arraySize; i++)
                {
                    var binding = bindings.GetArrayElementAtIndex(i);
                    var renderer = binding.FindPropertyRelative("renderer").objectReferenceValue as Renderer;
                    var original = binding.FindPropertyRelative("originalMaterial").objectReferenceValue as Material;
                    int slot = binding.FindPropertyRelative("materialSlot").intValue;
                    if (renderer == null || original == null || slot < 0 || renderer.sharedMaterials == null || slot >= renderer.sharedMaterials.Length) continue;
                    Undo.RecordObject(renderer, "Restore Gobo Lens Material");
                    var materials = renderer.sharedMaterials;
                    materials[slot] = original;
                    renderer.sharedMaterials = materials;
                    binding.FindPropertyRelative("goboLensMaterial").objectReferenceValue = null;
                    EditorUtility.SetDirty(renderer);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                    restored++;
                }
                bindings.serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(targets[targetIndex]);
                PrefabUtility.RecordPrefabInstancePropertyModifications(targets[targetIndex]);
            }
            Debug.Log($"Gobo Lens Material Setup: {restored} slot(s) を元のマテリアルへ戻しました。\nRestored {restored} slot(s) to their original materials.");
        }

        private void ValidateGoboLensSetupForTargets()
        {
            var expectedMaterial = FindGoboLensMaterialForCurrentPipeline(out string expectedShaderName);
            if (expectedMaterial == null)
            {
                Debug.LogError("Gobo Lens Material Setup: 現在のRender Pipelineに対応する共有マテリアルが見つかりません。\nNo shared material compatible with the current Render Pipeline was found.");
                return;
            }

            int valid = 0;
            int invalid = 0;
            for (int targetIndex = 0; targetIndex < targets.Length; targetIndex++)
            {
                var bindings = new SerializedObject(targets[targetIndex]).FindProperty("lensMaterialBindings");
                if (bindings == null || bindings.arraySize == 0)
                {
                    invalid++;
                    Debug.LogWarning($"Gobo Lens Material Setup: {targets[targetIndex].name} にLens Material Bindingがありません。\n{targets[targetIndex].name} has no Lens Material Binding.", targets[targetIndex]);
                    continue;
                }
                for (int i = 0; i < bindings.arraySize; i++)
                {
                    var binding = bindings.GetArrayElementAtIndex(i);
                    var renderer = binding.FindPropertyRelative("renderer").objectReferenceValue as Renderer;
                    int slot = binding.FindPropertyRelative("materialSlot").intValue;
                    bool isValid = renderer != null && slot >= 0 && renderer.sharedMaterials != null && slot < renderer.sharedMaterials.Length && renderer.sharedMaterials[slot] == expectedMaterial;
                    if (isValid) valid++;
                    else
                    {
                        invalid++;
                        Debug.LogWarning($"Gobo Lens Material Setup: {targets[targetIndex].name} のBinding {i} は {expectedShaderName} を参照していません。\nBinding {i} on {targets[targetIndex].name} does not reference {expectedShaderName}.", targets[targetIndex]);
                    }
                }
            }
            Debug.Log($"Gobo Lens Material Setup: 検証完了。Valid: {valid}, Invalid: {invalid}。\nValidation complete. Valid: {valid}, Invalid: {invalid}.");
        }

        private void DrawVlbDefaultsButton()
        {
            EditorGUILayout.Space(2f);
            if (GUILayout.Button("Apply VLB Pipeline Defaults"))
            {
                serializedObject.ApplyModifiedProperties();
                ApplyVlbPipelineDefaultsToTargets();
                serializedObject.Update();
            }
        }

        private void ApplyVlbPipelineDefaultsToTargets()
        {
            for (int i = 0; i < targets.Length; i++)
            {
                var fixture = targets[i] as DmxFixtureComponent;
                if (fixture == null)
                    continue;

                Undo.RecordObject(fixture, "Apply VLB Pipeline Defaults");
                fixture.ApplyVlbPipelineDefaults();
                EditorUtility.SetDirty(fixture);
                PrefabUtility.RecordPrefabInstancePropertyModifications(fixture);
            }
        }
    }

    internal static class FixtureDefinitionSourceNavigator
    {
        private static FixtureDefinition _definition;
        private static int _modeIndex = -1;
        private static int _relativeChannel;
        private static FixtureChannelRange _range;

        internal static void Open(FixtureDefinition definition, int modeIndex, int relativeChannel, FixtureChannelRange range)
        {
            if (definition == null) return;

            _definition = definition;
            _modeIndex = modeIndex;
            _relativeChannel = relativeChannel;
            _range = range;
            Selection.activeObject = definition;
            EditorGUIUtility.PingObject(definition);
            ActiveEditorTracker.sharedTracker.ForceRebuild();
        }

        internal static bool TryGetTarget(FixtureDefinition definition, out int modeIndex, out int relativeChannel, out FixtureChannelRange range)
        {
            modeIndex = _modeIndex;
            relativeChannel = _relativeChannel;
            range = _range;
            return definition != null && definition == _definition && modeIndex >= 0 && relativeChannel > 0;
        }
    }
}
