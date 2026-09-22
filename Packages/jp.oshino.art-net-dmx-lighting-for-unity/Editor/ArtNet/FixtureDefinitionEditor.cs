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
            if (FixtureDefinitionSourceNavigator.TryGetTarget(definition, out int modeIndex, out int relativeChannel, out var range))
            {
                ExpandSourceProperties(modeIndex, relativeChannel, range);
                string modeName = GetModeName(definition, modeIndex);
                string rangeName = string.IsNullOrWhiteSpace(range?.name) ? range?.type.ToString() : range.name;
                EditorGUILayout.HelpBox(
                    $"Opened source setting: {modeName} / Ch {relativeChannel}" +
                    (string.IsNullOrWhiteSpace(rangeName) ? string.Empty : $" / {rangeName}"),
                    MessageType.Info);
            }

            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();
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
