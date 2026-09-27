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
    [CustomPropertyDrawer(typeof(DmxFixtureComponent.ResolvedItem))]
    public class DmxResolvedItemDrawer : PropertyDrawer
    {
        private const float Gap = 2f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            bool isElement = property.FindPropertyRelative("useElementSchema").boolValue;
            label = new GUIContent(GetFoldoutLabel(property), label.tooltip);

            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, label, true);
            if (!property.isExpanded) return;

            EditorGUI.indentLevel++;
            EditorGUI.BeginDisabledGroup(true);

            line.y += EditorGUIUtility.singleLineHeight + Gap;

            if (isElement)
            {
                DrawProperty(ref line, property.FindPropertyRelative("attribute"));
                DrawProperty(ref line, property.FindPropertyRelative("instance"));
                DrawProperty(ref line, property.FindPropertyRelative("role"));
                DrawProperty(ref line, property.FindPropertyRelative("byteRole"));
            }
            else
            {
                DrawProperty(ref line, property.FindPropertyRelative("function"));
            }

            DrawProperty(ref line, property.FindPropertyRelative("relativeChannel"));
            DrawProperty(ref line, property.FindPropertyRelative("absoluteChannel"));

            EditorGUI.EndDisabledGroup();
            EditorGUI.indentLevel--;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded) return height;

            bool isElement = property.FindPropertyRelative("useElementSchema").boolValue;
            int lineCount = isElement ? 6 : 3;
            return height + (EditorGUIUtility.singleLineHeight + Gap) * lineCount;
        }

        private static void DrawProperty(ref Rect line, SerializedProperty property)
        {
            line.height = EditorGUI.GetPropertyHeight(property, includeChildren: true);
            EditorGUI.PropertyField(line, property, includeChildren: true);
            line.y += line.height + Gap;
        }

        private static string GetFoldoutLabel(SerializedProperty property)
        {
            var relativeCh = property.FindPropertyRelative("relativeChannel");
            if (relativeCh != null && relativeCh.intValue > 0)
                return $"CH{relativeCh.intValue}";

            var label = property.FindPropertyRelative("label");
            if (label != null && !string.IsNullOrWhiteSpace(label.stringValue))
                return label.stringValue;

            return property.displayName;
        }
    }
}
