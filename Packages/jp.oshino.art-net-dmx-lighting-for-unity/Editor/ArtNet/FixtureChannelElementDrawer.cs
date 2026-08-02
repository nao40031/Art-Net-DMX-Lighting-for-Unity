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
            SerializedProperty byteRoleProperty = property.FindPropertyRelative("byteRole");
            DrawProperty(ref line, byteRoleProperty);

            var ranges = property.FindPropertyRelative("ranges");
            DrawRanges(ref line, ranges, property);

            EditorGUI.indentLevel--;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded) return height;

            height += (EditorGUIUtility.singleLineHeight + Gap) * 4f;
            height += GetRangesHeight(property.FindPropertyRelative("ranges"));
            return height;
        }

        private static void DrawProperty(ref Rect line, SerializedProperty property)
        {
            line.height = EditorGUI.GetPropertyHeight(property, includeChildren: true);
            EditorGUI.PropertyField(line, property, includeChildren: true);
            line.y += line.height + Gap;
        }

        private static void DrawRanges(ref Rect line, SerializedProperty ranges, SerializedProperty element)
        {
            line.height = EditorGUIUtility.singleLineHeight;

            Rect foldoutRect = new Rect(line.x, line.y, line.width - 84f, line.height);
            Rect addRect = new Rect(line.xMax - 80f, line.y, 80f, line.height);

            ranges.isExpanded = EditorGUI.Foldout(foldoutRect, ranges.isExpanded, $"Ranges ({ranges.arraySize})", true);
            if (GUI.Button(addRect, "Add"))
                AddRange(ranges, ranges.arraySize, InferContext(element));

            line.y += line.height + Gap;

            if (!ranges.isExpanded)
                return;

            EditorGUI.indentLevel++;
            for (int i = 0; i < ranges.arraySize; i++)
            {
                SerializedProperty range = ranges.GetArrayElementAtIndex(i);
                Rect box = new Rect(line.x, line.y, line.width, GetRangeHeight(range));
                GUI.Box(box, GUIContent.none, EditorStyles.helpBox);

                Rect inner = new Rect(box.x + 4f, box.y + 2f, box.width - 8f, EditorGUIUtility.singleLineHeight);
                if (DrawRange(ref inner, range, ranges, i, element))
                {
                    line.y += box.height + Gap;
                    break;
                }

                line.y += box.height + Gap;
            }
            EditorGUI.indentLevel--;
        }

        private static bool DrawRange(ref Rect line, SerializedProperty range, SerializedProperty ranges, int index, SerializedProperty element)
        {
            Rect foldoutRect = new Rect(line.x, line.y, line.width - 52f, line.height);
            Rect addRect = new Rect(line.xMax - 48f, line.y, 22f, line.height);
            Rect removeRect = new Rect(line.xMax - 24f, line.y, 22f, line.height);

            range.isExpanded = EditorGUI.Foldout(foldoutRect, range.isExpanded, GetRangeLabel(range, index), true);
            if (GUI.Button(addRect, "+"))
            {
                AddRange(ranges, index + 1, InferContext(element));
                return true;
            }

            if (GUI.Button(removeRect, "-"))
            {
                ranges.DeleteArrayElementAtIndex(index);
                return true;
            }

            line.y += line.height + Gap;
            if (!range.isExpanded)
                return false;

            EditorGUI.indentLevel++;
            DrawProperty(ref line, range.FindPropertyRelative("name"));
            DrawRangeScaleHint(ref line, element);
            DrawProperty(ref line, range.FindPropertyRelative("dmxMin"));
            DrawProperty(ref line, range.FindPropertyRelative("dmxMax"));
            SerializedProperty typeProperty = range.FindPropertyRelative("type");
            DrawProperty(ref line, typeProperty);
            ApplyNotUsedPresetIfNeeded(range, typeProperty);
            DrawProperty(ref line, range.FindPropertyRelative("mappingContext"));
            DrawMappingPreset(ref line, range);
            EditorGUI.indentLevel--;

            return false;
        }

        private static void DrawMappingPreset(ref Rect line, SerializedProperty range)
        {
            SerializedProperty contextProp = range.FindPropertyRelative("mappingContext");
            SerializedProperty presetProp = range.FindPropertyRelative("mappingPreset");
            SerializedProperty normalizedFrom = range.FindPropertyRelative("normalizedFrom");
            SerializedProperty normalizedTo = range.FindPropertyRelative("normalizedTo");

            var context = (NormalizedMappingContext)Mathf.Clamp(contextProp.enumValueIndex, 0, (int)NormalizedMappingContext.IndexPosition);
            string[] labels = GetPresetLabels(context);
            int presetIndex = Mathf.Clamp(presetProp.enumValueIndex, 0, labels.Length - 1);

            line.height = EditorGUIUtility.singleLineHeight;
            int nextPresetIndex = EditorGUI.Popup(line, GetPresetTitle(context).text, presetIndex, labels);
            if (nextPresetIndex != presetProp.enumValueIndex)
                presetProp.enumValueIndex = nextPresetIndex;

            var preset = (NormalizedMappingPreset)Mathf.Clamp(presetProp.enumValueIndex, 0, (int)NormalizedMappingPreset.NotUsed);
            if (preset != NormalizedMappingPreset.Custom && preset != NormalizedMappingPreset.NotUsed)
                ApplyPresetToNormalizedFields(preset, normalizedFrom, normalizedTo);

            line.y += line.height + Gap;

            if (preset == NormalizedMappingPreset.Custom)
            {
                DrawProperty(ref line, normalizedFrom);
                DrawProperty(ref line, normalizedTo);
            }
            else if (preset == NormalizedMappingPreset.NotUsed)
            {
                line.height = EditorGUIUtility.singleLineHeight;
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUI.TextField(line, "Normalized Mapping", "Not used by runtime");
                }
                line.y += line.height + Gap;
            }
            else
            {
                line.height = EditorGUIUtility.singleLineHeight;
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUI.TextField(line, "Normalized Mapping", $"{normalizedFrom.floatValue:0.###} -> {normalizedTo.floatValue:0.###}");
                }
                line.y += line.height + Gap;
            }

            line.height = EditorGUIUtility.singleLineHeight;
            EditorGUI.LabelField(line, "Meaning Preview", GetMeaningPreview(context, preset));
            line.y += line.height + Gap;
        }

        private static float GetRangesHeight(SerializedProperty ranges)
        {
            float height = EditorGUIUtility.singleLineHeight + Gap;
            if (!ranges.isExpanded)
                return height;

            for (int i = 0; i < ranges.arraySize; i++)
                height += GetRangeHeight(ranges.GetArrayElementAtIndex(i)) + Gap;

            return height;
        }

        private static float GetRangeHeight(SerializedProperty range)
        {
            float line = EditorGUIUtility.singleLineHeight + Gap;
            float height = line + 4f;
            if (!range.isExpanded)
                return height;

            int lines = 9;
            var preset = (NormalizedMappingPreset)Mathf.Clamp(range.FindPropertyRelative("mappingPreset").enumValueIndex, 0, (int)NormalizedMappingPreset.NotUsed);
            if (preset == NormalizedMappingPreset.Custom)
                lines += 1;

            return height + (line * lines);
        }

        private static void AddRange(SerializedProperty ranges, int index, NormalizedMappingContext context)
        {
            ranges.InsertArrayElementAtIndex(index);
            SerializedProperty range = ranges.GetArrayElementAtIndex(index);

            range.FindPropertyRelative("name").stringValue = "Range";
            range.FindPropertyRelative("dmxMin").intValue = 0;
            range.FindPropertyRelative("dmxMax").intValue = 255;
            range.FindPropertyRelative("type").enumValueIndex = (int)FixtureRangeType.None;
            range.FindPropertyRelative("mappingContext").enumValueIndex = (int)context;
            range.FindPropertyRelative("mappingPreset").enumValueIndex = (int)NormalizedMappingPreset.Normal;
            range.FindPropertyRelative("normalizedFrom").floatValue = 0f;
            range.FindPropertyRelative("normalizedTo").floatValue = 1f;
            range.isExpanded = true;
        }

        private static void DrawRangeScaleHint(ref Rect line, SerializedProperty element)
        {
            var byteRole = (FixtureByteRole)element.FindPropertyRelative("byteRole").enumValueIndex;
            string hint = byteRole switch
            {
                FixtureByteRole.Single => "Range values use 0-255",
                FixtureByteRole.Coarse => "Range values use 0-65535 when paired with Fine",
                FixtureByteRole.Fine => "Usually define ranges on the Coarse channel",
                _ => string.Empty
            };

            line.height = EditorGUIUtility.singleLineHeight;
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUI.TextField(line, "Range Value Scale", hint);
            }
            line.y += line.height + Gap;
        }

        private static NormalizedMappingContext InferContext(SerializedProperty element)
        {
            var attribute = (FixtureAttribute)element.FindPropertyRelative("attribute").enumValueIndex;
            var role = (FixtureChannelRole)element.FindPropertyRelative("role").enumValueIndex;

            return attribute switch
            {
                FixtureAttribute.Zoom => NormalizedMappingContext.Zoom,
                FixtureAttribute.Focus => NormalizedMappingContext.Focus,
                FixtureAttribute.Iris => NormalizedMappingContext.Iris,
                FixtureAttribute.Frost => NormalizedMappingContext.Frost,
                FixtureAttribute.GoboWheel when role == FixtureChannelRole.Rotation || role == FixtureChannelRole.PositionOrRotation || role == FixtureChannelRole.Speed || role == FixtureChannelRole.SpeedDirection => NormalizedMappingContext.RotationSpeed,
                FixtureAttribute.AnimationWheel when role == FixtureChannelRole.Rotation || role == FixtureChannelRole.PositionOrRotation || role == FixtureChannelRole.Speed || role == FixtureChannelRole.SpeedDirection => NormalizedMappingContext.RotationSpeed,
                _ => NormalizedMappingContext.Generic
            };
        }

        private static string GetRangeLabel(SerializedProperty range, int index)
        {
            string name = range.FindPropertyRelative("name").stringValue;
            int min = range.FindPropertyRelative("dmxMin").intValue;
            int max = range.FindPropertyRelative("dmxMax").intValue;
            var type = (FixtureRangeType)range.FindPropertyRelative("type").enumValueIndex;

            if (string.IsNullOrWhiteSpace(name))
                name = $"Range {index}";

            return $"{name} ({min}-{max}, {type})";
        }

        private static GUIContent GetPresetTitle(NormalizedMappingContext context)
        {
            return context switch
            {
                NormalizedMappingContext.Zoom => new GUIContent("Zoom Direction"),
                NormalizedMappingContext.Focus => new GUIContent("Focus Direction"),
                NormalizedMappingContext.Iris => new GUIContent("Iris Direction"),
                NormalizedMappingContext.Frost => new GUIContent("Frost Direction"),
                NormalizedMappingContext.RotationSpeed => new GUIContent("Speed Direction"),
                NormalizedMappingContext.IndexPosition => new GUIContent("Index Direction"),
                _ => new GUIContent("Mapping Preset")
            };
        }

        private static string[] GetPresetLabels(NormalizedMappingContext context)
        {
            return context switch
            {
                NormalizedMappingContext.Zoom => new[] { "DMX Low = Narrow, High = Wide", "DMX Low = Wide, High = Narrow", "Custom", "Not Used / N/A" },
                NormalizedMappingContext.Focus => new[] { "DMX Low = Near, High = Far", "DMX Low = Far, High = Near", "Custom", "Not Used / N/A" },
                NormalizedMappingContext.Iris => new[] { "DMX Low = Closed, High = Open", "DMX Low = Open, High = Closed", "Custom", "Not Used / N/A" },
                NormalizedMappingContext.Frost => new[] { "DMX Low = No Frost, High = Full Frost", "DMX Low = Full Frost, High = No Frost", "Custom", "Not Used / N/A" },
                NormalizedMappingContext.RotationSpeed => new[] { "DMX Low = Slow, High = Fast", "DMX Low = Fast, High = Slow", "Custom", "Not Used / N/A" },
                NormalizedMappingContext.IndexPosition => new[] { "DMX Low = 0 deg, High = 360 deg", "DMX Low = 360 deg, High = 0 deg", "Custom", "Not Used / N/A" },
                _ => new[] { "Normal 0 -> 1", "Inverted 1 -> 0", "Custom", "Not Used / N/A" }
            };
        }

        private static string GetMeaningPreview(NormalizedMappingContext context, NormalizedMappingPreset preset)
        {
            if (preset == NormalizedMappingPreset.NotUsed)
                return "Range state only; no normalized value mapping";

            if (preset == NormalizedMappingPreset.Custom)
                return "Uses Normalized From / To";

            return GetPresetLabels(context)[(int)preset];
        }

        private static void ApplyPresetToNormalizedFields(NormalizedMappingPreset preset, SerializedProperty normalizedFrom, SerializedProperty normalizedTo)
        {
            if (preset == NormalizedMappingPreset.NotUsed)
                return;

            if (preset == NormalizedMappingPreset.Inverted)
            {
                normalizedFrom.floatValue = 1f;
                normalizedTo.floatValue = 0f;
            }
            else
            {
                normalizedFrom.floatValue = 0f;
                normalizedTo.floatValue = 1f;
            }
        }

        private static void ApplyNotUsedPresetIfNeeded(SerializedProperty range, SerializedProperty typeProperty)
        {
            var type = (FixtureRangeType)typeProperty.enumValueIndex;
            if (type != FixtureRangeType.NoFunction)
                return;

            range.FindPropertyRelative("mappingPreset").enumValueIndex = (int)NormalizedMappingPreset.NotUsed;
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
