/*!
 * Copyright (c) 2026 Oshino
 *
 * Released under the MIT license.
 * see https://opensource.org/licenses/MIT
 */

using ArtNet.Runtime;
using UnityEditor;
using UnityEngine;

namespace ArtNet.Editor
{
    [CustomEditor(typeof(DmxFixtureComponent))]
    [CanEditMultipleObjects]
    public sealed class DmxFixtureComponentEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var property = serializedObject.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                using (new EditorGUI.DisabledScope(property.propertyPath == "m_Script"))
                {
                    if (property.propertyPath == "prismGoboScaleVlb")
                        EditorGUILayout.Slider(property, 0.1f, 3f, new GUIContent(
                            "Prism Gobo Scale (VLB)",
                            "VLBのプリズム時に、各ゴボ投影の外形サイズをSpot Angleで調整します。1で通常のSpot Lightに合わせた基準サイズです。"));
                    else
                        EditorGUILayout.PropertyField(property, true);
                }

                if (property.propertyPath == "vlbHdrpExposureWeight")
                    DrawVlbDefaultsButton();

                enterChildren = false;
            }

            serializedObject.ApplyModifiedProperties();
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
}
