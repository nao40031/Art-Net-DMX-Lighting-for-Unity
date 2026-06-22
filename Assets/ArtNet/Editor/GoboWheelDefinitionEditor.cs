/*!
 * Copyright (c) 2026 Oshino
 *
 * Released under the MIT license.
 * see https://opensource.org/licenses/MIT
 */

using ArtNet.Runtime;
using UnityEditor;
using UnityEngine;

namespace ArtNet.EditorTools
{
    [CustomEditor(typeof(GoboWheelDefinition))]
    public class GoboWheelDefinitionEditor : UnityEditor.Editor
    {
        private SerializedProperty _slots;

        private void OnEnable()
        {
            _slots = serializedObject.FindProperty("slots");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Slots", EditorStyles.boldLabel);

            for (int i = 0; i < _slots.arraySize; i++)
            {
                SerializedProperty slot = _slots.GetArrayElementAtIndex(i);
                DrawSlot(slot, i);
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Add Slot"))
            {
                AddSlot(_slots.arraySize);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawSlot(SerializedProperty slot, int index)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            slot.isExpanded = EditorGUILayout.Foldout(slot.isExpanded, GetSlotLabel(slot, index), true);

            if (GUILayout.Button("+", GUILayout.Width(24f)))
            {
                AddSlot(index + 1);
            }

            if (GUILayout.Button("-", GUILayout.Width(24f)))
            {
                _slots.DeleteArrayElementAtIndex(index);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.EndHorizontal();

            if (slot.isExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(slot.FindPropertyRelative("name"));
                EditorGUILayout.PropertyField(slot.FindPropertyRelative("dmxMin"));
                EditorGUILayout.PropertyField(slot.FindPropertyRelative("dmxMax"));
                EditorGUILayout.PropertyField(slot.FindPropertyRelative("texture"));
                EditorGUILayout.PropertyField(slot.FindPropertyRelative("isOpen"));
                EditorGUILayout.PropertyField(slot.FindPropertyRelative("rotationOffsetDeg"));
                DrawAdditionalRanges(slot.FindPropertyRelative("additionalRanges"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawAdditionalRanges(SerializedProperty ranges)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Additional Ranges", EditorStyles.boldLabel);

            for (int i = 0; i < ranges.arraySize; i++)
            {
                SerializedProperty range = ranges.GetArrayElementAtIndex(i);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();

                range.isExpanded = EditorGUILayout.Foldout(range.isExpanded, GetRangeLabel(range, i), true);

                if (GUILayout.Button("+", GUILayout.Width(24f)))
                {
                    AddRange(ranges, i + 1);
                }

                if (GUILayout.Button("-", GUILayout.Width(24f)))
                {
                    ranges.DeleteArrayElementAtIndex(i);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    return;
                }

                EditorGUILayout.EndHorizontal();

                if (range.isExpanded)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(range.FindPropertyRelative("name"));
                    EditorGUILayout.PropertyField(range.FindPropertyRelative("dmxMin"));
                    EditorGUILayout.PropertyField(range.FindPropertyRelative("dmxMax"));

                    SerializedProperty type = range.FindPropertyRelative("type");
                    GoboRangeType previousType = (GoboRangeType)type.enumValueIndex;
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(type);
                    if (EditorGUI.EndChangeCheck() &&
                        previousType != GoboRangeType.Shake &&
                        (GoboRangeType)type.enumValueIndex == GoboRangeType.Shake)
                    {
                        ResetShakeProfile(range.FindPropertyRelative("shake"));
                    }

                    if ((GoboRangeType)type.enumValueIndex == GoboRangeType.Shake)
                        DrawShakeSettings(range.FindPropertyRelative("shake"));

                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("Add Range"))
            {
                AddRange(ranges, ranges.arraySize);
            }
        }

        private void AddSlot(int index)
        {
            _slots.InsertArrayElementAtIndex(index);
            SerializedProperty slot = _slots.GetArrayElementAtIndex(index);

            slot.FindPropertyRelative("name").stringValue = $"Gobo {_slots.arraySize - 1}";
            slot.FindPropertyRelative("dmxMin").intValue = 0;
            slot.FindPropertyRelative("dmxMax").intValue = 0;
            slot.FindPropertyRelative("texture").objectReferenceValue = null;
            slot.FindPropertyRelative("isOpen").boolValue = false;
            slot.FindPropertyRelative("rotationOffsetDeg").floatValue = 0f;
            slot.FindPropertyRelative("additionalRanges").arraySize = 0;
            slot.isExpanded = true;
        }

        private static void AddRange(SerializedProperty ranges, int index)
        {
            ranges.InsertArrayElementAtIndex(index);
            SerializedProperty range = ranges.GetArrayElementAtIndex(index);

            range.FindPropertyRelative("name").stringValue = "Range";
            range.FindPropertyRelative("dmxMin").intValue = 0;
            range.FindPropertyRelative("dmxMax").intValue = 0;
            range.FindPropertyRelative("type").enumValueIndex = (int)GoboRangeType.Select;
            ResetShakeProfile(range.FindPropertyRelative("shake"));
            range.isExpanded = true;
        }

        private static void DrawShakeSettings(SerializedProperty shake)
        {
            if (shake == null)
                return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Shake Settings", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(shake.FindPropertyRelative("enabled"));
            EditorGUILayout.PropertyField(shake.FindPropertyRelative("speedMinHz"));
            EditorGUILayout.PropertyField(shake.FindPropertyRelative("speedMaxHz"));
            EditorGUILayout.PropertyField(shake.FindPropertyRelative("motionMode"));
            EditorGUILayout.PropertyField(shake.FindPropertyRelative("positionAxis"));
            EditorGUILayout.PropertyField(shake.FindPropertyRelative("affectBeam"));
            EditorGUILayout.PropertyField(shake.FindPropertyRelative("applyWithPrism"));
            EditorGUILayout.PropertyField(shake.FindPropertyRelative("applyWithZoom"));
            EditorGUILayout.PropertyField(shake.FindPropertyRelative("allowDuringGoboRotation"));
            EditorGUILayout.PropertyField(shake.FindPropertyRelative("amplitudeMinDeg"));
            EditorGUILayout.PropertyField(shake.FindPropertyRelative("amplitudeMaxDeg"));
            EditorGUILayout.PropertyField(shake.FindPropertyRelative("positionAmplitudeMin"));
            EditorGUILayout.PropertyField(shake.FindPropertyRelative("positionAmplitudeMax"));
            EditorGUILayout.PropertyField(shake.FindPropertyRelative("beamAngleAmplitudeMinDeg"));
            EditorGUILayout.PropertyField(shake.FindPropertyRelative("beamAngleAmplitudeMaxDeg"));
            EditorGUILayout.PropertyField(shake.FindPropertyRelative("zoomShakeScaleMin"));
            EditorGUILayout.PropertyField(shake.FindPropertyRelative("zoomShakeScaleMax"));
            EditorGUILayout.PropertyField(shake.FindPropertyRelative("amplitudeMapping"));
            EditorGUI.indentLevel--;
        }

        private static void ResetShakeProfile(SerializedProperty shake)
        {
            if (shake == null)
                return;

            shake.FindPropertyRelative("enabled").boolValue = true;
            shake.FindPropertyRelative("speedMinHz").floatValue = 0.4f;
            shake.FindPropertyRelative("speedMaxHz").floatValue = 10f;
            shake.FindPropertyRelative("motionMode").enumValueIndex = (int)GoboShakeMotionMode.Rotation;
            shake.FindPropertyRelative("positionAxis").enumValueIndex = (int)GoboShakePositionAxis.Horizontal;
            shake.FindPropertyRelative("affectBeam").boolValue = true;
            shake.FindPropertyRelative("applyWithPrism").boolValue = true;
            shake.FindPropertyRelative("applyWithZoom").boolValue = true;
            shake.FindPropertyRelative("allowDuringGoboRotation").boolValue = true;
            shake.FindPropertyRelative("amplitudeMinDeg").floatValue = 10f;
            shake.FindPropertyRelative("amplitudeMaxDeg").floatValue = 360f;
            shake.FindPropertyRelative("positionAmplitudeMin").floatValue = 0.015f;
            shake.FindPropertyRelative("positionAmplitudeMax").floatValue = 0.06f;
            shake.FindPropertyRelative("beamAngleAmplitudeMinDeg").floatValue = 0.2f;
            shake.FindPropertyRelative("beamAngleAmplitudeMaxDeg").floatValue = 1f;
            shake.FindPropertyRelative("zoomShakeScaleMin").floatValue = 0.5f;
            shake.FindPropertyRelative("zoomShakeScaleMax").floatValue = 1.5f;
            shake.FindPropertyRelative("amplitudeMapping").enumValueIndex = (int)GoboShakeAmplitudeMapping.SlowLargeFastSmall;
        }

        private static string GetSlotLabel(SerializedProperty slot, int index)
        {
            string name = slot.FindPropertyRelative("name").stringValue;
            int min = slot.FindPropertyRelative("dmxMin").intValue;
            int max = slot.FindPropertyRelative("dmxMax").intValue;

            if (string.IsNullOrWhiteSpace(name))
                name = $"Slot {index}";

            return $"{name} ({min}-{max})";
        }

        private static string GetRangeLabel(SerializedProperty range, int index)
        {
            string name = range.FindPropertyRelative("name").stringValue;
            int min = range.FindPropertyRelative("dmxMin").intValue;
            int max = range.FindPropertyRelative("dmxMax").intValue;
            int typeIndex = range.FindPropertyRelative("type").enumValueIndex;
            string type = ((GoboRangeType)typeIndex).ToString();

            if (string.IsNullOrWhiteSpace(name))
                name = $"Range {index}";

            return $"{name} ({min}-{max}, {type})";
        }
    }
}
