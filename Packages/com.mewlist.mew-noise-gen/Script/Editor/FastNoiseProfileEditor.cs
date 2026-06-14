using System;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Mewlist.MewNoiseGen
{
    [CustomEditor(typeof(NoiseProfile))]
    public class FastNoiseProfileEditor : Editor
    {
        private Texture2D preview;
        private NoiseProfile profile;
        private TextureFormat textureFormat;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            profile = target as NoiseProfile;

            var resolution = serializedObject.FindProperty("resolution");
            var colorFormat = serializedObject.FindProperty("colorFormat");
            var shapes = serializedObject.FindProperty("shapes");

            var texSize  = CalcResolution(profile.Resolution);
            textureFormat = TextureFormat.RGBA32;
            switch (profile.TargetColorFormat)
            {
                case NoiseProfile.ColorFormat.R8:
                    textureFormat = TextureFormat.R8;
                    break;
                case NoiseProfile.ColorFormat.A8:
                    textureFormat = TextureFormat.Alpha8;
                    break;
                case NoiseProfile.ColorFormat.RGBA32:
                default:
                    break;
            }

            EditorGUILayout.HelpBox("Generator", MessageType.Info);
            if (GUILayout.Button("Save 3DTexture")) Save3DTexture();
            if (GUILayout.Button("Save 2DTexture")) Save2DTexture();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Output Settings", MessageType.Info);
            EditorGUILayout.PropertyField(resolution);
            EditorGUILayout.PropertyField(colorFormat);
            EditorGUILayout.LabelField("Texture Resolution: " + texSize + "x" + texSize + "(x" + texSize + ")");

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Preview", MessageType.Info);
            DrawPreview();

            EditorGUILayout.Space();

            EditorGUILayout.HelpBox("FastNoise parameters", MessageType.Info);
            CommonGUI();

            EditorGUILayout.Space();

            EditorGUILayout.HelpBox("Tiling / Shape Settings", MessageType.Info);
            ShapeGenerator();
            EditorGUILayout.PropertyField(shapes, true);

            serializedObject.ApplyModifiedProperties();
        }

        private void Save2DTexture()
        {
            var path = EditorUtility.SaveFilePanelInProject("Save 2DTexture", "2DTexture", "asset", "");
            Debug.Log(path);
            if (!string.IsNullOrEmpty(path))
            {
                var tex = Create2DTexture(CalcResolution(profile.Resolution), textureFormat);
                var sw = new Stopwatch();
                sw.Start();
                TextureGenerator.Gen2D(tex, profile);
                sw.Stop();
                AssetDatabase.CreateAsset(tex, path);
                EditorUtility.ClearProgressBar();
                Debug.Log(sw.Elapsed);
            }
        }

        private void Save3DTexture()
        {
            var path = EditorUtility.SaveFilePanelInProject("Save 3DTexture", "3DTexture", "asset", "");
            Debug.Log(path);
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    var tex = Create3DTexture(CalcResolution(profile.Resolution), textureFormat);
                    var sw = new Stopwatch();
                    sw.Start();
                    TextureGenerator.Gen3D(tex, profile,
                        (i, total) =>
                        {
                            return EditorUtility.DisplayCancelableProgressBar("generating", sw.Elapsed.ToString(),
                                (float) i / total);
                        });
                    sw.Stop();
                    AssetDatabase.CreateAsset(tex, path);
                    Debug.Log(sw.Elapsed);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }
            }
        }
        

        private void CommonGUI()
        {
            var seed = serializedObject.FindProperty("seed");
            var frequency = serializedObject.FindProperty("frequency");
            var interp = serializedObject.FindProperty("interp");
            var noiseType = serializedObject.FindProperty("noiseType");
            var octaves = serializedObject.FindProperty("octaves");
            var lacunarity = serializedObject.FindProperty("lacunarity");
            var gain = serializedObject.FindProperty("gain");
            var fractalType = serializedObject.FindProperty("fractalType");
            var cellularDistanceFunction = serializedObject.FindProperty("cellularDistanceFunction");
            var cellularReturnType = serializedObject.FindProperty("cellularReturnType");
            var cellularNoiseLookup = serializedObject.FindProperty("cellularNoiseLookup");
            var cellularDistanceIndex0 = serializedObject.FindProperty("cellularDistanceIndex0");
            var cellularDistanceIndex1 = serializedObject.FindProperty("cellularDistanceIndex1");
            var cellularJitter = serializedObject.FindProperty("cellularJitter");
            var gradientPerturbAmp = serializedObject.FindProperty("gradientPerturbAmp");
            var baseValue = serializedObject.FindProperty("baseValue");

            EditorGUILayout.PropertyField(seed);
            EditorGUILayout.PropertyField(frequency);
            EditorGUILayout.PropertyField(interp);

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(noiseType);
            if (profile.NoiseType == NoiseProfile.Noise.CubicFractal
                || profile.NoiseType == NoiseProfile.Noise.PerlinFractal
                || profile.NoiseType == NoiseProfile.Noise.SimplexFractal
                || profile.NoiseType == NoiseProfile.Noise.ValueFractal)
            {
                EditorGUILayout.PropertyField(octaves);
                EditorGUILayout.PropertyField(lacunarity);
                EditorGUILayout.PropertyField(gain);
                EditorGUILayout.PropertyField(fractalType);
            }

            if (profile.NoiseType == NoiseProfile.Noise.Cellular)
            {
                EditorGUILayout.PropertyField(cellularDistanceFunction);
                EditorGUILayout.PropertyField(cellularReturnType);
                EditorGUILayout.PropertyField(cellularNoiseLookup);
                EditorGUILayout.PropertyField(cellularDistanceIndex0);
                EditorGUILayout.PropertyField(cellularDistanceIndex1);
                EditorGUILayout.PropertyField(cellularJitter);
            }

            EditorGUILayout.PropertyField(gradientPerturbAmp);
            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(baseValue, true);
        }

        private void DrawPreview()
        {
            profile = target as NoiseProfile;

            var autoRedraw = serializedObject.FindProperty("autoRedraw");
            var previewQuality = serializedObject.FindProperty("previewQuality");
            EditorGUILayout.PropertyField(autoRedraw);
            EditorGUILayout.PropertyField(previewQuality);
            CreatePreviewTexture(previewQuality.intValue);
                        
            profile.Apply();

            if (profile.AutoRedraw || GUILayout.Button("Preview"))
            {
                if (profile.CellularReturnType == FastNoise.CellularReturnType.NoiseLookup
                    && profile.CellularNoiseLookup == null)
                {
                    
                }
                else
                {
                    TextureGenerator.Gen2D(preview, profile);
                }
            }
            var rect = EditorGUILayout.GetControlRect(GUILayout.Width(256), GUILayout.Height(256));
            EditorGUI.DrawPreviewTexture(rect, preview);
            DrawShape(rect);
        }

        private void DrawShape(Rect rect)
        {
            var clipRect = rect;
            using (new GUI.ClipScope(clipRect))
            {
                foreach (var profileShape in profile.Shapes)
                {
                    switch (profileShape.Type)
                    {
                        case Shape.ShapeType.Box: 
                            var boxPos = new Vector2(profileShape.Position.x, 1f - profileShape.Position.y);
                            Handles.DrawWireCube(boxPos * rect.size, profileShape.Size * rect.size);
                            break;
                        case Shape.ShapeType.Sphere:
                            var pos = new Vector2(profileShape.Position.x, 1f - profileShape.Position.y);
                            Handles.DrawWireArc(
                                pos * rect.size,
                                Vector3.forward,
                                Vector3.right,
                                360f,
                                profileShape.Size.x / 2f * rect.size.x);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }
        }

        private void ShapeGenerator()
        {
            if (GUILayout.Button("Primitive"))
            {
                Undo.RecordObject(profile, "Change Shapes");
                profile.Shapes = new []
                {
                    Shape.Box(0f, 0.01f, new Vector3(0.5f, 0.5f, 0.5f), Vector3.one)
                };
                EditorUtility.SetDirty(profile);
            }
            if (GUILayout.Button("Tiling2D"))
            {
                Undo.RecordObject(profile, "Change Shapes");
                profile.Shapes = new []
                {
                    Shape.Sphere(0.4f, 0.5f, new Vector3(0f, 0f, 0f), 1f),
                    Shape.Sphere(0.4f, 0.5f, new Vector3(0.5f, 0f, 0f), 1f),
                    Shape.Sphere(0.4f, 0.5f, new Vector3(0f, 0.5f, 0f), 1f),
                    Shape.Sphere(0.4f, 0.5f, new Vector3(0.5f, 0.5f, 0f), 1f),
                };
                EditorUtility.SetDirty(profile);
            }
            if (GUILayout.Button("Tiling3D"))
            {
                Undo.RecordObject(profile, "Change Shapes");
                profile.Shapes = new []
                {
                    Shape.Sphere(0.4f, 0.5f, new Vector3(0f, 0f, 0f), 1f),
                    Shape.Sphere(0.4f, 0.5f, new Vector3(0.5f, 0f, 0f), 1f),
                    Shape.Sphere(0.4f, 0.5f, new Vector3(0f, 0.5f, 0f), 1f),
                    Shape.Sphere(0.4f, 0.5f, new Vector3(0f, 0f, 0.5f), 1f),
                    Shape.Sphere(0.4f, 0.5f, new Vector3(0f, 0.5f, 0.5f), 1f),
                    Shape.Sphere(0.4f, 0.5f, new Vector3(0.5f, 0.5f, 0f), 1f),
                    Shape.Sphere(0.4f, 0.5f, new Vector3(0.5f, 0f, 0.5f), 1f),
                    Shape.Sphere(0.4f, 0.5f, new Vector3(0.5f, 0.5f, 0.5f), 1f)
                };
                EditorUtility.SetDirty(profile);
            }
        }

        private static int CalcResolution(int quality)
        {
            return 32 * Mathf.RoundToInt(Mathf.Pow(2, quality));
        }
        
        private void CreatePreviewTexture(int quality)
        {
            var resolution = CalcResolution(quality);

            if (!preview || preview.width != resolution)
            {
                preview = new Texture2D(resolution, resolution)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Repeat
                };
            }
        }

        private static Texture2D Create2DTexture(int resolution, TextureFormat textureFormat)
        {
            return new Texture2D(resolution, resolution, textureFormat, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Repeat
            };
        }

        public static Texture3D Create3DTexture(int resolution, TextureFormat textureFormat)
        {
            return new Texture3D(resolution, resolution, resolution, textureFormat, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Repeat
            };
        }
    }
}