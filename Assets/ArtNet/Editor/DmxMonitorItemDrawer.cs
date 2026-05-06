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
    [CustomPropertyDrawer(typeof(DmxFixtureComponent.DmxMonitorItem))]
    public class DmxMonitorItemDrawer : PropertyDrawer
    {
        private const float Gap = 2f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var useElementSchema = property.FindPropertyRelative("useElementSchema");
            bool isElement = useElementSchema != null && useElementSchema.boolValue;

            label = new GUIContent(GetFoldoutLabel(property, isElement), label.tooltip);

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

            DrawProperty(ref line, property.FindPropertyRelative("relativeCh"));
            DrawProperty(ref line, property.FindPropertyRelative("absoluteCh"));
            DrawProperty(ref line, property.FindPropertyRelative("value"));

            EditorGUI.EndDisabledGroup();
            EditorGUI.indentLevel--;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded) return height;

            var useElementSchema = property.FindPropertyRelative("useElementSchema");
            bool isElement = useElementSchema != null && useElementSchema.boolValue;
            int lineCount = isElement ? 7 : 4;

            return height + (EditorGUIUtility.singleLineHeight + Gap) * lineCount;
        }

        private static void DrawProperty(ref Rect line, SerializedProperty property)
        {
            line.height = EditorGUI.GetPropertyHeight(property, includeChildren: true);
            EditorGUI.PropertyField(line, property, includeChildren: true);
            line.y += line.height + Gap;
        }

        private static string GetFoldoutLabel(SerializedProperty property, bool isElement)
        {
            var relativeCh = property.FindPropertyRelative("relativeCh");
            if (relativeCh != null && relativeCh.intValue > 0)
                return $"CH{relativeCh.intValue}";

            if (isElement)
            {
                var attribute = property.FindPropertyRelative("attribute");
                var instance = property.FindPropertyRelative("instance");
                var role = property.FindPropertyRelative("role");
                var byteRole = property.FindPropertyRelative("byteRole");

                string attributeName = attribute != null ? attribute.enumDisplayNames[attribute.enumValueIndex] : "Element";
                string roleName = role != null ? role.enumDisplayNames[role.enumValueIndex] : "Value";
                string byteRoleName = byteRole != null ? byteRole.enumDisplayNames[byteRole.enumValueIndex] : "Single";
                int instanceValue = instance != null ? Mathf.Max(1, instance.intValue) : 1;
                return $"{attributeName}{instanceValue}.{roleName}.{byteRoleName}";
            }

            var function = property.FindPropertyRelative("function");
            return function != null ? function.enumDisplayNames[function.enumValueIndex] : property.displayName;
        }
    }
}
