/*
 * ArtNetReceiverDmxRecorder.cs
 * - ArtNetReceiver.OnDataReceived を購読して OpDmx の 512ch を時系列で記録
 * - 録画停止時に AnimationClip を .asset として保存（Editor専用）
 */

#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
#endif

using UnityEngine;
using ArtNet.Runtime;

namespace ArtNet.Editor
{
#if UNITY_EDITOR
    public class ArtNetReceiverDmxRecorder : MonoBehaviour
    {
        [Header("Source (Subscribe target)")]
        [SerializeField] private ArtNetReceiver receiver;

        [Header("Record Control")]
        [SerializeField] private bool recordOnPlay = false;
        [SerializeField] private KeyCode startKey = KeyCode.R;
        [SerializeField] private KeyCode stopKey = KeyCode.S;

        [Header("Save (AnimationClip asset)")]
        [Tooltip("Assets配下の保存先フォルダ名（例: Record）")]
        [SerializeField] private string directoryPath = "Record";

        [Tooltip("生成するAnimationClip名（拡張子不要）")]
        [SerializeField] private string clipName = "NewArtNetClip";

        [Tooltip("SetCurveの対象にするコンポーネントの型名（完全修飾名推奨）\n例: ArtNet.Runtime.ArtNetChannels")]
        [SerializeField] private string channelsComponentTypeName = "ArtNet.Runtime.ArtNetChannels";

        [Header("Advanced")]
        [SerializeField, Min(1)] private int channelCount = 512;
        [SerializeField] private bool verboseLog = false;

        private AnimationCurve[] _curves;
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

        [ContextMenu("Start Recording")]
        public void StartRecording()
        {
            if (_isRecording) return;

            if (receiver == null)
            {
                Debug.LogError("[Recorder] ArtNetReceiver が未アサインです。Inspectorで receiver に ArtNetReceiver を割り当ててください。");
                return;
            }

            _curves = new AnimationCurve[channelCount];
            for (int i = 0; i < channelCount; i++)
                _curves[i] = new AnimationCurve();

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

            var clip = new AnimationClip();

            // 512ch分のCurveを登録（プロパティ名: Ch1..Ch512 を想定）
            for (int i = 0; i < _curves.Length; i++)
            {
                var propName = $"Ch{i + 1}";
                clip.SetCurve("", targetType, propName, _curves[i]);
            }

            // 保存先作成
            var assetFolder = $"Assets/{directoryPath}";
            var fullFolderPath = Path.Combine(Application.dataPath, directoryPath);
            if (!Directory.Exists(fullFolderPath))
                Directory.CreateDirectory(fullFolderPath);

            // 同名を避けて保存
            var assetPath = $"{assetFolder}/{clipName}.asset";
            while (File.Exists(assetPath))
                assetPath = assetPath.Split('.').First() + "_1.asset";

            AssetDatabase.CreateAsset(clip, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Recorder] Record Finish: {assetPath}");

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

            if (_curves == null || _curves.Length == 0) return;

            float t = Time.time - _startTime;

            // 受信ch数が512未満の可能性もあるので短い方に合わせる
            int len = Mathf.Min(_curves.Length, data.Channels?.Length ?? 0);

            for (int i = 0; i < len; i++)
            {
                // 連続同値の中間キーを間引く（元コードの軽量化ロジック）
                var curve = _curves[i];
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
            _curves = null;
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
    }
#else
    // Build時にEditor専用が混ざってもコンパイルを壊さないためのダミー
    public class ArtNetReceiverDmxRecorder : MonoBehaviour { }
#endif
}
