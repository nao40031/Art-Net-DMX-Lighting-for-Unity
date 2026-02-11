/*
 * ArtNetReceiverDmxRecorder.cs
 * - ArtNetReceiver.OnDataReceived を購読して OpDmx の 512ch を時系列で記録
 * - 録画停止時に AnimationClip を .asset として保存（Editor専用）
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace ArtNet.Runtime
{
    public class ArtNetReceiverDmxRecorder : MonoBehaviour
    {
        [Header("Source (Subscribe target)")]
        [SerializeField] private ArtNetReceiver receiver;

        [Header("Record Control")]
        [SerializeField] private bool recordOnPlay = false;
        [SerializeField] private KeyCode startKey = KeyCode.R;
        [SerializeField] private KeyCode stopKey = KeyCode.S;

        [Header("Record Targets")]
        [Tooltip("trueなら受信した全Universeを録画。falseの場合は targetUniverses のみ録画")]
        [SerializeField] private bool recordAllUniverses = true;

        [Tooltip("recordAllUniverses=false の時に録画対象にするUniverse一覧")]
        [SerializeField] private List<int> targetUniverses = new();

        [Header("Save (AnimationClip asset)")]
        [Tooltip("Assets配下の保存先フォルダ名（例: Record）")]
        [SerializeField] private string directoryPath = "Record";

        [Tooltip("生成するAnimationClip名（拡張子不要）")]
        [SerializeField] private string clipName = "NewArtNetClip";

        [Tooltip("Universe番号をクリップ名に付与（例: NewArtNetClip_U0）")]
        [SerializeField] private bool appendUniverseSuffix = true;

        [Tooltip("SetCurveの対象にするコンポーネントの型名（完全修飾名推奨）\n例: ArtNet.Runtime.ArtNetChannels")]
        [SerializeField] private string channelsComponentTypeName = "ArtNet.Runtime.ArtNetChannels";

        [Header("Advanced")]
        [SerializeField, Min(1)] private int channelCount = 512;
        [SerializeField] private bool verboseLog = false;

        private readonly Dictionary<int, AnimationCurve[]> _curvesByUniverse = new();
        private bool _isRecording;
        private float _startTime;

        private void OnEnable()
        {
            if (recordOnPlay)
                StartRecording();
        }

        private void OnDisable()
        {
            // 念のため購読解除
            Unsubscribe();
        }

        private void Update()
        {
            if (Input.GetKeyDown(startKey)) StartRecording();
            if (Input.GetKeyDown(stopKey)) StopAndSave();
        }

        public bool IsRecording => _isRecording;

        public void SetRecording(bool enable)
        {
            if (enable) StartRecording();
            else StopAndSave();
        }

        [ContextMenu("Start Recording")]
        public void StartRecording()
        {
            if (_isRecording) return;

            if (receiver == null)
            {
                Debug.LogError("[Recorder] ArtNetReceiver が未アサインです。Inspectorで receiver に ArtNetReceiver を割り当ててください。");
                return;
            }

            _curvesByUniverse.Clear();

            _startTime = Time.time;
            _isRecording = true;

            receiver.OnDataReceived += OnArtNetDataReceived;
            if (verboseLog) Debug.Log("[Recorder] RecordStart");
        }

        [ContextMenu("Stop & Save")]
        public void StopAndSave()
        {
            if (!_isRecording) return;

            Unsubscribe();

            // ターゲット型（ArtNetChannels等）を文字列から解決（コンパイル依存を消す）
            var targetType = ResolveType(channelsComponentTypeName);
            if (targetType == null)
            {
                Debug.LogError($"[Recorder] 型 '{channelsComponentTypeName}' が見つかりませんでした。\n" +
                               $"例: 'ArtNet.Runtime.ArtNetChannels' のように完全修飾名で指定してください。");
                Cleanup();
                return;
            }

            if (_curvesByUniverse.Count == 0)
            {
                Debug.LogWarning("[Recorder] No data recorded.");
                Cleanup();
                return;
            }

            // 保存先作成
            var assetFolder = $"Assets/{directoryPath}";
            var fullFolderPath = Path.Combine(Application.dataPath, directoryPath);
            if (!Directory.Exists(fullFolderPath))
                Directory.CreateDirectory(fullFolderPath);

            bool savedAny = false;
            foreach (var kv in _curvesByUniverse)
            {
                int universe = kv.Key;
                var curves = kv.Value;
                if (curves == null || curves.Length == 0) continue;

                var clip = new AnimationClip();

                // 512ch分のCurveを登録（プロパティ名: Ch1..Ch512 を想定）
                for (int i = 0; i < curves.Length; i++)
                {
                    var propName = $"Ch{i + 1}";
                    clip.SetCurve("", targetType, propName, curves[i]);
                }

                string name = clipName;
                if (appendUniverseSuffix || _curvesByUniverse.Count > 1)
                    name = $"{clipName}_U{universe}";

                var assetPath = GetUniqueAssetPath(assetFolder, name);
                if (TryCreateAsset(clip, assetPath))
                {
                    savedAny = true;
                    if (verboseLog) Debug.Log($"[Recorder] Saved: {assetPath}");
                }
                else
                {
                    Debug.LogWarning("[Recorder] AssetDatabase が使えないため保存できません（Editor専用）。");
                }
            }

            if (savedAny)
            {
                TrySaveAssets();
                Debug.Log($"[Recorder] Record Finish: {assetFolder}");
            }

            Cleanup();
        }

        private void OnApplicationQuit()
        {
            // 録画中なら停止だけしておく（保存したいなら StopAndSave を呼ぶ運用でもOK）
            Unsubscribe();
        }

        private void OnArtNetDataReceived(ArtNetData data)
        {
            // DMX以外は無視
            if (data.OpCode != ArtNetOpCode.OpDmx) return;

            if (!recordAllUniverses && (targetUniverses == null || !targetUniverses.Contains(data.Universe)))
                return;

            if (!_curvesByUniverse.TryGetValue(data.Universe, out var curves))
            {
                curves = new AnimationCurve[channelCount];
                for (int i = 0; i < channelCount; i++)
                    curves[i] = new AnimationCurve();

                _curvesByUniverse.Add(data.Universe, curves);
            }

            if (curves == null || curves.Length == 0) return;

            float t = Time.time - _startTime;

            // 受信ch数が512未満の可能性もあるので短い方に合わせる
            int len = Mathf.Min(curves.Length, data.Channels?.Length ?? 0);

            for (int i = 0; i < len; i++)
            {
                // 連続同値の中間キーを間引く（元コードの軽量化ロジック）
                var curve = curves[i];
                if (curve.length > 2)
                {
                    int secondLast = curve.length - 2;
                    int last = curve.length - 1;
                    var secondLastKey = curve.keys[secondLast];
                    var lastKey = curve.keys[last];

                    if (Mathf.Approximately(secondLastKey.value, lastKey.value) &&
                        Mathf.Approximately(lastKey.value, data.Channels[i]))
                    {
                        curve.RemoveKey(secondLast);
                    }
                }

                curve.AddKey(new Keyframe(t, data.Channels[i]));
            }

            if (verboseLog) Debug.Log($"[Recorder] t={t:0.000}s");
        }

        private void Unsubscribe()
        {
            if (receiver != null)
                receiver.OnDataReceived -= OnArtNetDataReceived;

            _isRecording = false;
        }

        private void Cleanup()
        {
            _curvesByUniverse.Clear();
            _isRecording = false;
            _startTime = 0f;
        }

        private static Type ResolveType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName)) return null;

            // まずはType.GetType（同一アセンブリ等で取れる場合）
            var t = Type.GetType(typeName);
            if (t != null) return t;

            // 全アセンブリから探索
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    t = asm.GetType(typeName);
                    if (t != null) return t;
                }
                catch { /* ignore */ }
            }
            return null;
        }
        private static string GetUniqueAssetPath(string assetFolder, string name)
        {
            string assetPath = $"{assetFolder}/{name}.asset";
            string projectRoot = GetProjectRootPath();
            string fullPath = Path.Combine(projectRoot, assetPath);

            int suffix = 1;
            while (File.Exists(fullPath))
            {
                assetPath = $"{assetFolder}/{name}_{suffix}.asset";
                fullPath = Path.Combine(projectRoot, assetPath);
                suffix++;
            }

            return assetPath;
        }

        private static string GetProjectRootPath()
        {
            var dataPath = Application.dataPath;
            var dir = Directory.GetParent(dataPath);
            return dir?.FullName ?? Path.GetDirectoryName(dataPath);
        }

        private static bool TryCreateAsset(UnityEngine.Object asset, string assetPath)
        {
            if (!Application.isEditor) return false;

            var assetDbType = Type.GetType("UnityEditor.AssetDatabase, UnityEditor");
            if (assetDbType == null) return false;

            var create = assetDbType.GetMethod(
                "CreateAsset",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(UnityEngine.Object), typeof(string) },
                null
            );
            if (create == null) return false;

            create.Invoke(null, new object[] { asset, assetPath });
            return true;
        }

        private static void TrySaveAssets()
        {
            if (!Application.isEditor) return;

            var assetDbType = Type.GetType("UnityEditor.AssetDatabase, UnityEditor");
            if (assetDbType == null) return;

            assetDbType.GetMethod("SaveAssets", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
            assetDbType.GetMethod("Refresh", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
        }
    }
}
