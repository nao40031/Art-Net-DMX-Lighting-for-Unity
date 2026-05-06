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
    [CustomPropertyDrawer(typeof(FixtureChannelElement))]
    public class FixtureChannelElementDrawer : PropertyDrawer
    {
        private const float Gap = 2f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            label = ToChannelLabel(label);

            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, label, true);
            if (!property.isExpanded) return;

            EditorGUI.indentLevel++;

            line.y += EditorGUIUtility.singleLineHeight + Gap;
            DrawProperty(ref line, property.FindPropertyRelative("attribute"));
            DrawProperty(ref line, property.FindPropertyRelative("instance"));
            DrawProperty(ref line, property.FindPropertyRelative("role"));
            DrawProperty(ref line, property.FindPropertyRelative("byteRole"));

            var ranges = property.FindPropertyRelative("ranges");
            float rangesHeight = EditorGUI.GetPropertyHeight(ranges, includeChildren: true);
            line.height = rangesHeight;
            EditorGUI.PropertyField(line, ranges, includeChildren: true);

            EditorGUI.indentLevel--;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded) return height;

            height += (EditorGUIUtility.singleLineHeight + Gap) * 4f;
            height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("ranges"), includeChildren: true);
            return height;
        }

        private static void DrawProperty(ref Rect line, SerializedProperty property)
        {
            line.height = EditorGUI.GetPropertyHeight(property, includeChildren: true);
            EditorGUI.PropertyField(line, property, includeChildren: true);
            line.y += line.height + Gap;
        }

        private static GUIContent ToChannelLabel(GUIContent label)
        {
            const string prefix = "Element ";
            if (label != null && label.text != null && label.text.StartsWith(prefix))
            {
                string indexText = label.text.Substring(prefix.Length);
                if (int.TryParse(indexText, out int index))
                    return new GUIContent($"CH{index + 1}", label.tooltip);
            }

            return label;
        }
    }
}
