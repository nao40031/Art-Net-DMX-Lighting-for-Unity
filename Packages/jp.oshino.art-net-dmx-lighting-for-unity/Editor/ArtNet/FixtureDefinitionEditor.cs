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
    [CustomEditor(typeof(FixtureDefinition))]
    public sealed class FixtureDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var definition = target as FixtureDefinition;
            bool hasHighlightedSource = FixtureDefinitionSourceNavigator.TryGetTarget(definition, out int modeIndex, out int relativeChannel, out var range, out int rangeIndex);
            if (hasHighlightedSource)
            {
                ExpandSourceProperties(modeIndex, relativeChannel, range);
                string modeName = GetModeName(definition, modeIndex);
                string rangeName = string.IsNullOrWhiteSpace(range?.name) ? range?.type.ToString() : range.name;
                DrawSourceHighlight(
                    $"Control Source: {modeName} / Ch {relativeChannel}" +
                    (string.IsNullOrWhiteSpace(rangeName) ? string.Empty : $" / {rangeName}"));
            }

            DrawDefinitionProperties(hasHighlightedSource, modeIndex, relativeChannel, rangeIndex);
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawDefinitionProperties(bool hasHighlightedSource, int highlightedModeIndex, int highlightedRelativeChannel, int highlightedRangeIndex)
        {
            var property = serializedObject.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                if (property.propertyPath == "modes" && hasHighlightedSource)
                {
                    DrawModesWithHighlightedSource(property, highlightedModeIndex, highlightedRelativeChannel, highlightedRangeIndex);
                    enterChildren = false;
                    continue;
                }

                using (new EditorGUI.DisabledScope(property.propertyPath == "m_Script"))
                    EditorGUILayout.PropertyField(property, true);
                enterChildren = false;
            }
        }

        private static void DrawModesWithHighlightedSource(SerializedProperty modes, int highlightedModeIndex, int highlightedRelativeChannel, int highlightedRangeIndex)
        {
            modes.isExpanded = EditorGUILayout.Foldout(modes.isExpanded, modes.displayName, true);
            if (!modes.isExpanded) return;

            using (new EditorGUI.IndentLevelScope())
            {
                int nextSize = Mathf.Max(0, EditorGUILayout.IntField("Size", modes.arraySize));
                if (nextSize != modes.arraySize)
                    modes.arraySize = nextSize;

                for (int i = 0; i < modes.arraySize; i++)
                {
                    var mode = modes.GetArrayElementAtIndex(i);
                    if (i != highlightedModeIndex)
                    {
                        EditorGUILayout.PropertyField(mode, true);
                        continue;
                    }

                    mode.isExpanded = EditorGUILayout.Foldout(mode.isExpanded, $"Element {i}", true);
                    if (!mode.isExpanded) continue;

                    using (new EditorGUI.IndentLevelScope())
                    {
                        EditorGUILayout.PropertyField(mode.FindPropertyRelative("modeName"), true);
                        EditorGUILayout.PropertyField(mode.FindPropertyRelative("channelCount"), true);
                        EditorGUILayout.PropertyField(mode.FindPropertyRelative("channels"), true);
                        DrawElementsWithHighlightedSource(mode.FindPropertyRelative("elements"), highlightedRelativeChannel, highlightedRangeIndex);
                        EditorGUILayout.PropertyField(mode.FindPropertyRelative("wheelBindings"), true);
                    }
                }
            }
        }

        private static void DrawElementsWithHighlightedSource(SerializedProperty elements, int highlightedRelativeChannel, int highlightedRangeIndex)
        {
            if (elements == null) return;

            elements.isExpanded = EditorGUILayout.Foldout(elements.isExpanded, elements.displayName, true);
            if (!elements.isExpanded) return;

            using (new EditorGUI.IndentLevelScope())
            {
                int nextSize = Mathf.Max(0, EditorGUILayout.IntField("Size", elements.arraySize));
                if (nextSize != elements.arraySize)
                    elements.arraySize = nextSize;

                for (int i = 0; i < elements.arraySize; i++)
                {
                    var element = elements.GetArrayElementAtIndex(i);
                    if (i == highlightedRelativeChannel - 1)
                        DrawElementWithHighlightedRange(element, highlightedRangeIndex);
                    else
                        EditorGUILayout.PropertyField(element, true);
                }
            }
        }

        private static void DrawElementWithHighlightedRange(SerializedProperty element, int highlightedRangeIndex)
        {
            Rect header = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
            EditorGUI.DrawRect(header, new Color(0.18f, 0.55f, 0.9f, 0.25f));
            element.isExpanded = EditorGUI.Foldout(header, element.isExpanded, element.displayName, true);
            if (!element.isExpanded) return;

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(element.FindPropertyRelative("attribute"), true);
                EditorGUILayout.PropertyField(element.FindPropertyRelative("instance"), true);
                EditorGUILayout.PropertyField(element.FindPropertyRelative("role"), true);
                EditorGUILayout.PropertyField(element.FindPropertyRelative("byteRole"), true);
                DrawRangesWithHighlightedSource(element.FindPropertyRelative("ranges"), highlightedRangeIndex);
            }
        }

        private static void DrawRangesWithHighlightedSource(SerializedProperty ranges, int highlightedRangeIndex)
        {
            if (ranges == null) return;

            ranges.isExpanded = EditorGUILayout.Foldout(ranges.isExpanded, ranges.displayName, true);
            if (!ranges.isExpanded) return;

            using (new EditorGUI.IndentLevelScope())
            {
                int nextSize = Mathf.Max(0, EditorGUILayout.IntField("Size", ranges.arraySize));
                if (nextSize != ranges.arraySize)
                    ranges.arraySize = nextSize;

                for (int i = 0; i < ranges.arraySize; i++)
                {
                    var range = ranges.GetArrayElementAtIndex(i);
                    if (i == highlightedRangeIndex)
                        DrawHighlightedProperty(range);
                    else
                        EditorGUILayout.PropertyField(range, true);
                }
            }
        }

        private static void DrawHighlightedProperty(SerializedProperty property)
        {
            float height = EditorGUI.GetPropertyHeight(property, true);
            Rect rect = EditorGUILayout.GetControlRect(true, height);
            EditorGUI.DrawRect(rect, new Color(0.18f, 0.55f, 0.9f, 0.25f));
            EditorGUI.PropertyField(rect, property, true);
        }

        private static void DrawSourceHighlight(string text)
        {
            var content = new GUIContent(text);
            float width = Mathf.Max(100f, EditorGUIUtility.currentViewWidth - 36f);
            float height = Mathf.Max(EditorGUIUtility.singleLineHeight, EditorStyles.wordWrappedLabel.CalcHeight(content, width));
            Rect rect = EditorGUILayout.GetControlRect(true, height + 8f);
            EditorGUI.DrawRect(rect, new Color(0.18f, 0.55f, 0.9f, 0.25f));
            rect.xMin += 6f;
            rect.xMax -= 6f;
            rect.yMin += 4f;
            EditorGUI.LabelField(rect, content, EditorStyles.wordWrappedLabel);
        }

        private void ExpandSourceProperties(int modeIndex, int relativeChannel, FixtureChannelRange sourceRange)
        {
            var modes = serializedObject.FindProperty("modes");
            if (modes == null || modeIndex < 0 || modeIndex >= modes.arraySize) return;

            modes.isExpanded = true;
            var mode = modes.GetArrayElementAtIndex(modeIndex);
            mode.isExpanded = true;
            var elements = mode.FindPropertyRelative("elements");
            if (elements == null || relativeChannel <= 0 || relativeChannel > elements.arraySize) return;

            elements.isExpanded = true;
            var element = elements.GetArrayElementAtIndex(relativeChannel - 1);
            element.isExpanded = true;
            var ranges = element.FindPropertyRelative("ranges");
            if (ranges == null || sourceRange == null) return;

            ranges.isExpanded = true;
            for (int i = 0; i < ranges.arraySize; i++)
            {
                var range = ranges.GetArrayElementAtIndex(i);
                // FixtureChannelRange is an inline serializable class, so Unity does not expose an object reference here.
                // Match its serialized values instead of relying on object identity.
                var type = range.FindPropertyRelative("type");
                var min = range.FindPropertyRelative("dmxMin");
                var max = range.FindPropertyRelative("dmxMax");
                if (type != null && min != null && max != null &&
                    type.enumValueIndex == (int)sourceRange.type && min.intValue == sourceRange.dmxMin && max.intValue == sourceRange.dmxMax)
                {
                    range.isExpanded = true;
                    break;
                }
            }
        }

        private static string GetModeName(FixtureDefinition definition, int modeIndex)
        {
            if (definition?.modes == null || modeIndex < 0 || modeIndex >= definition.modes.Count)
                return "(No Mode)";

            string name = definition.modes[modeIndex]?.modeName;
            return string.IsNullOrWhiteSpace(name) ? $"Mode {modeIndex}" : name;
        }
    }
}
