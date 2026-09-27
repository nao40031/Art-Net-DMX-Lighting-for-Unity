// Assets/Editor/MagicQ/MagicQCsvExporter.cs
// Export Unity transforms to a calibration-sheet CSV (MagicQ columns + Unity columns)
// - Position: optional Invert Z for MagicQ
// - Rotation: calibrated rule-based conversion (based on your measured pairs)
// - Output format matches your attached CSV columns

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class MagicQCsvExporter
{
    private const string MenuPath = "Art-Net/MagicQ/Export MagicQ Calibration CSV...";

    // Prefs
    private const string PrefUnitsScale = "MagicQCsvExporter.UnitsScale";
    private const string PrefInvertZ = "MagicQCsvExporter.InvertZ";
    private const string PrefIncludeInactive = "MagicQCsvExporter.IncludeInactive";
    private const string PrefSortMode = "MagicQCsvExporter.SortMode";
    private const string PrefRotationSpace = "MagicQCsvExporter.RotationSpace";
    private const string PrefRuleTolerance = "MagicQCsvExporter.RuleTolerance";

    // Defaults for string columns (used if component fields not found)
    private const string PrefDefaultName = "MagicQCsvExporter.DefaultName";
    private const string PrefDefaultManufacturer = "MagicQCsvExporter.DefaultManufacturer";
    private const string PrefDefaultModel = "MagicQCsvExporter.DefaultModel";
    private const string PrefDefaultMode = "MagicQCsvExporter.DefaultMode";

    private enum SortMode
    {
        ByName = 0,
        ByHeadNoThenName = 1,
        ByHierarchyOrder = 2,
    }

    private enum RotationSpace
    {
        World = 0,
        Local = 1,
    }

    [MenuItem(MenuPath)]
    public static void ExportMenu()
    {
        var selected = Selection.gameObjects;
        if (selected == null || selected.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "MagicQ Calibration CSV Export",
                "Hierarchyで出力したい灯体（または親）を選択してから実行してください。\n" +
                "選択したオブジェクト配下のTransformを走査してCSVを作ります。\n\n" +
                "Select the fixtures (or their parent) to export in the Hierarchy before running this command.\n" +
                "A CSV file will be created by scanning the Transforms under the selected objects.",
                "OK");
            return;
        }

        OptionsWindow.Show(selected);
    }

    private sealed class OptionsWindow : EditorWindow
    {
        private GameObject[] _roots;

        private float _unitsScale;
        private bool _invertZ;
        private bool _includeInactive;
        private SortMode _sortMode;
        private RotationSpace _rotSpace;
        private float _ruleToleranceDeg;

        private string _defaultName;
        private string _defaultManufacturer;
        private string _defaultModel;
        private string _defaultMode;

        public static void Show(GameObject[] roots)
        {
            var w = CreateInstance<OptionsWindow>();
            w.titleContent = new GUIContent("MagicQ CSV Export");
            w._roots = roots;

            w._unitsScale = EditorPrefs.GetFloat(PrefUnitsScale, 1.0f);
            w._invertZ = EditorPrefs.GetBool(PrefInvertZ, true); // you validated this
            w._includeInactive = EditorPrefs.GetBool(PrefIncludeInactive, true);
            w._sortMode = (SortMode)EditorPrefs.GetInt(PrefSortMode, (int)SortMode.ByHierarchyOrder);
            w._rotSpace = (RotationSpace)EditorPrefs.GetInt(PrefRotationSpace, (int)RotationSpace.World);
            w._ruleToleranceDeg = EditorPrefs.GetFloat(PrefRuleTolerance, 3.0f);

            w._defaultName = EditorPrefs.GetString(PrefDefaultName, "LM70S");
            w._defaultManufacturer = EditorPrefs.GetString(PrefDefaultManufacturer, "Betopper");
            w._defaultModel = EditorPrefs.GetString(PrefDefaultModel, "LM70S");
            w._defaultMode = EditorPrefs.GetString(PrefDefaultMode, "9ch");

            w.minSize = new Vector2(560, 360);
            w.maxSize = new Vector2(920, 520);
            w.ShowUtility();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Export Options", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope("box"))
            {
                _unitsScale = EditorGUILayout.FloatField(
                    new GUIContent("Units Scale", "Unity units を何mとして出力するか（通常 1 = 1m なら 1.0）\nSets how many meters each Unity unit represents in the exported data (normally 1.0 when 1 unit = 1 m)."),
                    _unitsScale);

                _invertZ = EditorGUILayout.Toggle(
                    new GUIContent("Invert Z (MagicQ)", "MagicQ側で正面/背面が逆になる場合の補正（Z = -Z）\nInverts Z when the forward/back direction appears reversed in MagicQ (Z = -Z)."),
                    _invertZ);

                _rotSpace = (RotationSpace)EditorGUILayout.EnumPopup(
                    new GUIContent("Rotation Space (Unity)", "Unity回転をWorld/Localどちらから取得するか\nSelects whether Unity rotation is read in World or Local space."),
                    _rotSpace);

                _ruleToleranceDeg = EditorGUILayout.FloatField(
                    new GUIContent("Rule Tolerance (deg)", "キャリブレーションルールの一致判定許容度（例 3°）\nSets the matching tolerance for calibration rules (for example, 3 degrees)."),
                    _ruleToleranceDeg);

                _includeInactive = EditorGUILayout.Toggle(
                    new GUIContent("Include Inactive", "非アクティブも含める\nIncludes inactive objects in the export."),
                    _includeInactive);

                _sortMode = (SortMode)EditorGUILayout.EnumPopup(
                    new GUIContent("Sort", "出力行の並び順\nSelects the order of exported rows."),
                    _sortMode);
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("CSV String Defaults (used if not found on components)", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope("box"))
            {
                _defaultName = EditorGUILayout.TextField("Name", _defaultName);
                _defaultManufacturer = EditorGUILayout.TextField("Manufacturer", _defaultManufacturer);
                _defaultModel = EditorGUILayout.TextField("Model", _defaultModel);
                _defaultMode = EditorGUILayout.TextField("Mode", _defaultMode);
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Selected Roots", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope("box"))
            {
                foreach (var go in _roots)
                {
                    if (go == null) continue;
                    EditorGUILayout.LabelField("• " + GetHierarchyPath(go.transform));
                }
            }

            EditorGUILayout.Space(10);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Cancel", GUILayout.Width(120)))
                {
                    Close();
                    return;
                }

                if (GUILayout.Button("Export CSV...", GUILayout.Width(180)))
                {
                    SavePrefs();

                    var defaultName = $"MagicQ_show_ArtNetForUnity-Positions_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                    var path = EditorUtility.SaveFilePanel(
                        "Export MagicQ Calibration CSV",
                        Application.dataPath,
                        defaultName,
                        "csv");

                    if (!string.IsNullOrEmpty(path))
                    {
                        try
                        {
                            ExportCsv(path, _roots, _unitsScale, _invertZ, _includeInactive, _sortMode, _rotSpace, _ruleToleranceDeg,
                                _defaultName, _defaultManufacturer, _defaultModel, _defaultMode);

                            EditorUtility.RevealInFinder(path);
                            EditorUtility.DisplayDialog(
                                "MagicQ CSV Export",
                                "CSVを出力しました。\nCSV export completed.\n\n" + path,
                                "OK");
                        }
                        catch (Exception ex)
                        {
                            Debug.LogException(ex);
                            EditorUtility.DisplayDialog(
                                "MagicQ CSV Export",
                                "出力に失敗しました。\nCSV export failed.\n\n" + ex.Message,
                                "OK");
                        }
                    }

                    Close();
                }
            }
        }

        private void SavePrefs()
        {
            EditorPrefs.SetFloat(PrefUnitsScale, _unitsScale);
            EditorPrefs.SetBool(PrefInvertZ, _invertZ);
            EditorPrefs.SetBool(PrefIncludeInactive, _includeInactive);
            EditorPrefs.SetInt(PrefSortMode, (int)_sortMode);
            EditorPrefs.SetInt(PrefRotationSpace, (int)_rotSpace);
            EditorPrefs.SetFloat(PrefRuleTolerance, _ruleToleranceDeg);

            EditorPrefs.SetString(PrefDefaultName, _defaultName);
            EditorPrefs.SetString(PrefDefaultManufacturer, _defaultManufacturer);
            EditorPrefs.SetString(PrefDefaultModel, _defaultModel);
            EditorPrefs.SetString(PrefDefaultMode, _defaultMode);
        }
    }

    private static void ExportCsv(
        string filePath,
        GameObject[] roots,
        float unitsScale,
        bool invertZ,
        bool includeInactive,
        SortMode sortMode,
        RotationSpace rotSpace,
        float ruleToleranceDeg,
        string defaultName,
        string defaultManufacturer,
        string defaultModel,
        string defaultMode)
    {
        if (Mathf.Approximately(unitsScale, 0f)) unitsScale = 1f;
        if (ruleToleranceDeg <= 0f) ruleToleranceDeg = 3f;

        var transforms = new HashSet<Transform>();
        foreach (var r in roots)
        {
            if (r == null) continue;
            foreach (var t in r.GetComponentsInChildren<Transform>(includeInactive))
            {
                if (t != null) transforms.Add(t);
            }
        }

        var rows = new List<Row>(transforms.Count);

        int autoHead = 1;

        foreach (var t in transforms)
        {
            if (t == null) continue;

            // Unity values (meters / degrees)
            Vector3 unityPos = t.position * unitsScale;
            Vector3 unityEuler = (rotSpace == RotationSpace.Local) ? t.localEulerAngles : t.eulerAngles;
            unityEuler = Normalize360(unityEuler);

            // MagicQ converted values
            Vector3 mqPos = unityPos;
            if (invertZ) mqPos.z = -mqPos.z;

            Vector3 mqEuler = ConvertUnityEulerToMagicQ(unityEuler, invertZ, ruleToleranceDeg);
            mqEuler = NormalizeSigned180(mqEuler);

            // Metadata
            var meta = TryExtractFixtureMeta(t.gameObject);

            string name = ReadStringMemberFromLikelyComponent(t.gameObject, "Name", "name", "FixtureName", "fixtureName") ?? defaultName;
            string manu = ReadStringMemberFromLikelyComponent(t.gameObject, "Manufacturer", "manufacturer", "Mfg", "mfg") ?? defaultManufacturer;
            string model = ReadStringMemberFromLikelyComponent(t.gameObject, "Model", "model") ?? defaultModel;
            string mode = ReadStringMemberFromLikelyComponent(t.gameObject, "Mode", "mode", "ChannelMode", "channelMode") ?? defaultMode;

            int head = meta.HeadNo > 0 ? meta.HeadNo : autoHead++;
            int universe = meta.Universe;
            int address = meta.StartAddress;

            rows.Add(new Row
            {
                Head = head,
                Name = name,
                Manufacturer = manu,
                Model = model,
                Mode = mode,
                Universe = universe,
                Address = address,

                XPosMagicQ = mqPos.x,
                YPosMagicQ = mqPos.y,
                ZPosMagicQ = mqPos.z,
                XRotMagicQ = mqEuler.x,
                YRotMagicQ = mqEuler.y,
                ZRotMagicQ = mqEuler.z,

                XPosUnity = unityPos.x,
                YPosUnity = unityPos.y,
                ZPosUnity = unityPos.z,
                XRotUnity = unityEuler.x,
                YRotUnity = unityEuler.y,
                ZRotUnity = unityEuler.z,

                HierarchyIndex = GetHierarchyIndex(t),
                GoName = t.name
            });
        }

        rows = sortMode switch
        {
            SortMode.ByHeadNoThenName => rows
                .OrderBy(r => r.Head <= 0 ? int.MaxValue : r.Head)
                .ThenBy(r => r.GoName, StringComparer.OrdinalIgnoreCase)
                .ToList(),

            SortMode.ByHierarchyOrder => rows
                .OrderBy(r => r.HierarchyIndex)
                .ToList(),

            _ => rows
                .OrderBy(r => r.GoName, StringComparer.OrdinalIgnoreCase)
                .ToList(),
        };

        // CSV write (match your attached columns)
        var sb = new StringBuilder(1024 * 64);
        sb.AppendLine("Head,Name,Manufacturer,Model,Mode,Universe,Address," +
                      "X Pos(MagicQ),Y Pos(MagicQ),Z Pos(MagicQ),X Rot(MagicQ),Y Rot(MagicQ),Z Rot(MagicQ)," +
                      "X Pos(Unity),Y Pos(Unity),Z Pos(Unity),X Rot(Unity),Y Rot(Unity),Z Rot(Unity)");

        foreach (var r in rows)
        {
            sb.Append(r.Head).Append(',')
              .Append(EscapeCsv(r.Name)).Append(',')
              .Append(EscapeCsv(r.Manufacturer)).Append(',')
              .Append(EscapeCsv(r.Model)).Append(',')
              .Append(EscapeCsv(r.Mode)).Append(',')
              .Append(r.Universe).Append(',')
              .Append(r.Address).Append(',')

              .Append(FormatMetersMagicQ(r.XPosMagicQ)).Append(',')
              .Append(FormatMetersMagicQ(r.YPosMagicQ)).Append(',')
              .Append(FormatMetersMagicQ(r.ZPosMagicQ)).Append(',')
              .Append(FormatDegreesMagicQ(r.XRotMagicQ)).Append(',')
              .Append(FormatDegreesMagicQ(r.YRotMagicQ)).Append(',')
              .Append(FormatDegreesMagicQ(r.ZRotMagicQ)).Append(',')

              .Append(FormatFloatUnity(r.XPosUnity)).Append(',')
              .Append(FormatFloatUnity(r.YPosUnity)).Append(',')
              .Append(FormatFloatUnity(r.ZPosUnity)).Append(',')
              .Append(FormatAngleUnity(r.XRotUnity)).Append(',')
              .Append(FormatAngleUnity(r.YRotUnity)).Append(',')
              .Append(FormatAngleUnity(r.ZRotUnity))
              .AppendLine();
        }

        File.WriteAllText(filePath, sb.ToString(), new UTF8Encoding(false));
        AssetDatabase.Refresh();
    }

    // -----------------------------
    // Rotation conversion (calibrated rules)
    // -----------------------------

    private static Vector3 ConvertUnityEulerToMagicQ(Vector3 unityEuler360, bool invertZ, float tolDeg)
    {
        // unityEuler360: (0..360)
        // Based on your measured pairs (Unity -> MagicQ):
        // (90,0,0) -> (0,180,0)
        // (0,270,0) -> (-90,-90,0)
        // (0,90,180) -> (-90,90,0)
        // (0,180,180) -> (-90,0,0)
        // (270,0,0) -> (0,0,-180)
        // (~272.21,180,0) -> (~-87.79,0,0)
        //
        // Implementation: detect near-known Unity patterns then apply per-axis deltas.
        // If not matched: fallback uses your current empirical rule: (Y += 180 when invertZ) and (X -= 90).

        float x = unityEuler360.x;
        float y = unityEuler360.y;
        float z = unityEuler360.z;

        // helper: angle near target considering wrap
        bool Near(float a, float target, float tol) => Mathf.Abs(Mathf.DeltaAngle(a, target)) <= tol;

        Vector3 delta;

        // Rule A: (90,0,0) -> (0,180,0)
        if (Near(x, 90f, tolDeg) && Near(y, 0f, tolDeg) && Near(z, 0f, tolDeg))
        {
            delta = new Vector3(-90f, 180f, 0f);
        }
        // Rule B: (270,0,0) -> (0,0,-180)
        else if (Near(x, 270f, tolDeg) && Near(y, 0f, tolDeg) && Near(z, 0f, tolDeg))
        {
            delta = new Vector3(90f, 0f, -180f);
        }
        // Rule C: (0,270,0) -> (-90,-90,0)
        else if (Near(x, 0f, tolDeg) && Near(y, 270f, tolDeg) && Near(z, 0f, tolDeg))
        {
            delta = new Vector3(-90f, 0f, 0f);
        }
        // Rule D: (0,90,180) -> (-90,90,0)
        else if (Near(x, 0f, tolDeg) && Near(y, 90f, tolDeg) && Near(z, 180f, tolDeg))
        {
            delta = new Vector3(-90f, 0f, -180f);
        }
        // Rule E: (0,180,180) -> (-90,0,0)
        else if (Near(x, 0f, tolDeg) && Near(y, 180f, tolDeg) && Near(z, 180f, tolDeg))
        {
            delta = new Vector3(-90f, 180f, -180f);
        }
        // Rule F: (~272.21,180,0) -> (~-87.79,0,0)  (keep X, but Y -= 180)
        else if (Near(y, 180f, tolDeg) && Near(z, 0f, tolDeg) && Near(x, 270f, 10f))
        {
            delta = new Vector3(0f, -180f, 0f);
        }
        else
        {
            // Fallback (good starting point for many rigs):
            // - hanging fixtures often need X -= 90
            // - front/back flips often corrected by Y += 180 when invertZ
            delta = new Vector3(-90f, invertZ ? 180f : 0f, 0f);
        }

        Vector3 mq = unityEuler360 + delta;
        return Normalize360(mq);
    }

    // -----------------------------
    // Metadata extraction (Head/Universe/Address etc.)
    // -----------------------------

    private struct FixtureMeta
    {
        public int HeadNo;
        public int Universe;
        public int StartAddress;
    }

    private static FixtureMeta TryExtractFixtureMeta(GameObject go)
    {
        var meta = new FixtureMeta { HeadNo = 0, Universe = 0, StartAddress = 0 };
        if (go == null) return meta;

        var components = go.GetComponents<Component>();
        foreach (var c in components)
        {
            if (c == null) continue;

            var type = c.GetType();
            var typeName = type.Name;

            if (!typeName.Contains("Fixture", StringComparison.OrdinalIgnoreCase) &&
                !typeName.Contains("Dmx", StringComparison.OrdinalIgnoreCase) &&
                !typeName.Contains("MovingLight", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (meta.HeadNo == 0) meta.HeadNo = ReadIntMember(c, type, "Head", "HeadNo", "headNo", "HeadNumber", "headNumber");
            if (meta.Universe == 0) meta.Universe = ReadIntMember(c, type, "Universe", "universe");
            if (meta.StartAddress == 0) meta.StartAddress = ReadIntMember(c, type, "StartAddress", "startAddress", "DmxAddress", "dmxAddress", "Address", "address");
        }

        return meta;
    }

    private static int ReadIntMember(object instance, Type type, params string[] names)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        foreach (var n in names)
        {
            var p = type.GetProperty(n, flags);
            if (p != null && p.PropertyType == typeof(int) && p.GetIndexParameters().Length == 0)
            {
                try { return (int)p.GetValue(instance); } catch { }
            }

            var f = type.GetField(n, flags);
            if (f != null && f.FieldType == typeof(int))
            {
                try { return (int)f.GetValue(instance); } catch { }
            }
        }
        return 0;
    }

    private static string ReadStringMemberFromLikelyComponent(GameObject go, params string[] names)
    {
        if (go == null) return null;

        var components = go.GetComponents<Component>();
        foreach (var c in components)
        {
            if (c == null) continue;

            var type = c.GetType();
            var typeName = type.Name;

            if (!typeName.Contains("Fixture", StringComparison.OrdinalIgnoreCase) &&
                !typeName.Contains("Dmx", StringComparison.OrdinalIgnoreCase) &&
                !typeName.Contains("MovingLight", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var v = ReadStringMember(c, type, names);
            if (!string.IsNullOrEmpty(v)) return v;
        }

        return null;
    }

    private static string ReadStringMember(object instance, Type type, params string[] names)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        foreach (var n in names)
        {
            var p = type.GetProperty(n, flags);
            if (p != null && p.PropertyType == typeof(string) && p.GetIndexParameters().Length == 0)
            {
                try { return (string)p.GetValue(instance); } catch { }
            }

            var f = type.GetField(n, flags);
            if (f != null && f.FieldType == typeof(string))
            {
                try { return (string)f.GetValue(instance); } catch { }
            }
        }
        return null;
    }

    // -----------------------------
    // Formatting
    // -----------------------------

    private static string FormatMetersMagicQ(float meters)
    {
        // match look like "-6.16m" / "0m"
        var s = meters.ToString("0.##", CultureInfo.InvariantCulture);
        return s + "m";
    }

    private static string FormatDegreesMagicQ(float degreesSigned180)
    {
        // match look like "180°" / "-90°"
        // trim near-integers for readability
        float d = Mathf.Abs(degreesSigned180 - Mathf.Round(degreesSigned180)) < 0.0005f
            ? Mathf.Round(degreesSigned180)
            : degreesSigned180;

        var s = d.ToString("0.##", CultureInfo.InvariantCulture);
        return s + "°";
    }

    private static string FormatFloatUnity(float v)
        => v.ToString("0.###", CultureInfo.InvariantCulture);

    private static string FormatAngleUnity(float degrees360)
    {
        float d = Mathf.Abs(degrees360 - Mathf.Round(degrees360)) < 0.0005f
            ? Mathf.Round(degrees360)
            : degrees360;

        return d.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string EscapeCsv(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var mustQuote = s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r');
        if (!mustQuote) return s;
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }

    // -----------------------------
    // Helpers
    // -----------------------------

    private struct Row
    {
        public int Head;
        public string Name;
        public string Manufacturer;
        public string Model;
        public string Mode;
        public int Universe;
        public int Address;

        public float XPosMagicQ, YPosMagicQ, ZPosMagicQ;
        public float XRotMagicQ, YRotMagicQ, ZRotMagicQ;

        public float XPosUnity, YPosUnity, ZPosUnity;
        public float XRotUnity, YRotUnity, ZRotUnity;

        public long HierarchyIndex;
        public string GoName;
    }

    private static Vector3 Normalize360(Vector3 euler)
    {
        euler.x = Mathf.Repeat(euler.x, 360f);
        euler.y = Mathf.Repeat(euler.y, 360f);
        euler.z = Mathf.Repeat(euler.z, 360f);
        return euler;
    }

    private static Vector3 NormalizeSigned180(Vector3 euler)
    {
        euler.x = ToSigned180(euler.x);
        euler.y = ToSigned180(euler.y);
        euler.z = ToSigned180(euler.z);
        return euler;
    }

    private static float ToSigned180(float a)
    {
        float s = Mathf.Repeat(a + 180f, 360f) - 180f;
        // prefer 180 over -180 for readability (matches your sheet style)
        if (Mathf.Abs(s + 180f) < 0.0001f) s = 180f;
        return s;
    }

    private static string GetHierarchyPath(Transform t)
    {
        if (t == null) return "";
        var stack = new Stack<string>();
        while (t != null)
        {
            stack.Push(t.name);
            t = t.parent;
        }
        return string.Join("/", stack);
    }

    private static long GetHierarchyIndex(Transform t)
    {
        long key = 0;
        long mul = 1;
        while (t != null && mul < 1_000_000_000_000L)
        {
            key += (t.GetSiblingIndex() + 1) * mul;
            mul *= 1000;
            t = t.parent;
        }
        return key;
    }
}
