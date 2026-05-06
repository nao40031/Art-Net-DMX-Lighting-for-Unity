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
                    EditorGUILayout.PropertyField(range.FindPropertyRelative("type"));
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
            range.isExpanded = true;
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
