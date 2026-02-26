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

#if UNITY_EDITOR
using UnityEditor;
#endif

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

        [Header("Recording Mode")]
        [Tooltip("有効時、録画中は軽量サンプルのみ蓄積し、Stop時にAnimationClipを生成します。")]
        [SerializeField] private bool useLowGcRecording = true;

        [Tooltip("低GCモード時の1チャネルあたり初期サンプル容量。")]
        [SerializeField, Min(1)] private int initialSampleCapacityPerChannel = 8;

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

        [Header("Curve Recording")]
        [Tooltip("値が変わった瞬間に「直前キー+新キー」を入れてほぼ瞬時切替にする")]
        [SerializeField] private bool useStepKeys = true;

        [Tooltip("ステップ用の微小時間(秒)。0.001 = 1ms")]
        [SerializeField, Range(0.0001f, 0.02f)] private float stepEpsilon = 0.001f;

        [Tooltip("小さな揺れを無視する（デッドバンド）。0=無効")]
        [SerializeField, Range(0, 20)] private int deadbandThreshold = 2;

        [Header("Hybrid Recording")]
        [Tooltip("Pan/Tilt系チャネルのみ線形記録にして、Color/Dimmerはステップ記録を維持")]
        [SerializeField] private bool hybridPanTiltLinear = true;

        [Tooltip("DmxFixtureComponentのマッピングからPan/Tiltチャネルを自動検出")]
        [SerializeField] private bool autoDetectPanTiltChannels = true;

        [Tooltip("線形(Pan/Tilt)チャネルにもデッドバンドを適用する")]
        [SerializeField] private bool applyDeadbandToLinearChannels = false;

        [Tooltip("自動検出できない場合の線形チャネル(Absolute 1-512)。全Universe共通")]
        [SerializeField] private List<int> fallbackLinearChannels = new();

        [Header("Curve Tangents (Legacy)")]
        [Tooltip("保存時に全チャンネルをConstantに変換します（非推奨）。ONにすると挙動が不安定になる場合があります。")]
        [SerializeField] private bool setCurvesToConstant = false;

        [Header("Clip Length")]
        [Tooltip("複数Universeを保存する際、全Clipの終端時刻を最長Clipに揃える")]
        [SerializeField] private bool normalizeClipEndAcrossUniverses = true;

        private readonly Dictionary<int, AnimationCurve[]> _curvesByUniverse = new();
        private readonly Dictionary<int, UniverseSampleBuffer> _samplesByUniverse = new();
        private readonly Dictionary<int, bool[]> _linearChannelsByUniverse = new();
        private bool _isRecording;
        private float _startTime;

        private struct DmxSample
        {
            public float time;
            public byte value;

            public DmxSample(float time, byte value)
            {
                this.time = time;
                this.value = value;
            }
        }

        private sealed class UniverseSampleBuffer
        {
            public readonly List<DmxSample>[] samplesByChannel;
            public readonly int[] lastValues;
            public readonly bool[] hasLastValue;

            public UniverseSampleBuffer(int channelCount)
            {
                samplesByChannel = new List<DmxSample>[channelCount];
                lastValues = new int[channelCount];
                hasLastValue = new bool[channelCount];
            }
        }

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
            _samplesByUniverse.Clear();
            RebuildLinearChannelCache();

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

            var curvesByUniverse = useLowGcRecording
                ? BuildCurvesFromSamples()
                : _curvesByUniverse;

            if (curvesByUniverse.Count == 0)
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

            float normalizedEndTime = 0f;
            if (normalizeClipEndAcrossUniverses && curvesByUniverse.Count > 1)
            {
                normalizedEndTime = GetGlobalEndTime(curvesByUniverse);
                if (verboseLog)
                    Debug.Log($"[Recorder] Normalize clip end time: {normalizedEndTime:0.000}s");
            }

            bool savedAny = false;
            foreach (var kv in curvesByUniverse)
            {
                int universe = kv.Key;
                var curves = kv.Value;
                if (curves == null || curves.Length == 0) continue;

                if (normalizedEndTime > 0f)
                    ExtendCurvesToEndTime(curves, normalizedEndTime);

                var clip = new AnimationClip();

                // 512ch分のCurveを登録（プロパティ名: Ch1..Ch512 を想定）
                for (int i = 0; i < curves.Length; i++)
                {
                    var propName = $"Ch{i + 1}";
                    clip.SetCurve("", targetType, propName, curves[i]);
                }

#if UNITY_EDITOR
                if (setCurvesToConstant)
                {
                    SetClipCurvesToConstant(clip);
                }
#endif

                string name = clipName;
                if (appendUniverseSuffix || curvesByUniverse.Count > 1)
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

            float t = Time.time - _startTime;

            if (useLowGcRecording)
            {
                RecordPacketLowGc(data, t);
            }
            else
            {
                RecordPacketLegacyCurves(data, t);
            }

            if (verboseLog) Debug.Log($"[Recorder] t={t:0.000}s");
        }

        private void RecordPacketLowGc(ArtNetData data, float t)
        {
            int len = Mathf.Min(channelCount, data.Channels?.Length ?? 0);
            if (len <= 0) return;

            var buffer = GetOrCreateSampleBuffer(data.Universe);

            for (int i = 0; i < len; i++)
            {
                int newVal = Mathf.Clamp(data.Channels[i], 0, 255);
                int absCh = i + 1;
                bool isLinearChannel = IsLinearChannel(data.Universe, absCh);
                int activeDeadband = (isLinearChannel && !applyDeadbandToLinearChannels) ? 0 : deadbandThreshold;

                if (!buffer.hasLastValue[i])
                {
                    buffer.hasLastValue[i] = true;
                    buffer.lastValues[i] = newVal;
                    GetOrCreateSampleList(buffer, i).Add(new DmxSample(t, (byte)newVal));
                    continue;
                }

                int lastVal = buffer.lastValues[i];
                if (activeDeadband > 0 && Mathf.Abs(newVal - lastVal) < activeDeadband)
                    continue;

                if (newVal == lastVal)
                    continue;

                buffer.lastValues[i] = newVal;
                GetOrCreateSampleList(buffer, i).Add(new DmxSample(t, (byte)newVal));
            }
        }

        private void RecordPacketLegacyCurves(ArtNetData data, float t)
        {
            if (!_curvesByUniverse.TryGetValue(data.Universe, out var curves))
            {
                curves = new AnimationCurve[channelCount];
                for (int i = 0; i < channelCount; i++)
                    curves[i] = new AnimationCurve();

                _curvesByUniverse.Add(data.Universe, curves);
            }

            if (curves == null || curves.Length == 0) return;

            int len = Mathf.Min(curves.Length, data.Channels?.Length ?? 0);

            for (int i = 0; i < len; i++)
            {
                var curve = curves[i];
                int newVal = Mathf.Clamp(data.Channels[i], 0, 255);
                int absCh = i + 1;
                bool isLinearChannel = IsLinearChannel(data.Universe, absCh);

                if (curve.length == 0)
                {
                    curve.AddKey(new Keyframe(t, newVal));
                    continue;
                }

                var lastKey = curve.keys[curve.length - 1];
                int lastVal = Mathf.RoundToInt(lastKey.value);
                int activeDeadband = (isLinearChannel && !applyDeadbandToLinearChannels) ? 0 : deadbandThreshold;

                if (activeDeadband > 0 && Mathf.Abs(newVal - lastVal) < activeDeadband)
                    continue;

                if (newVal == lastVal)
                    continue;

                bool useStepForThisChannel = useStepKeys && !isLinearChannel;
                if (useStepForThisChannel)
                {
                    float writeTime = t;
                    float tPrev = writeTime - stepEpsilon;
                    bool canAddPrev = tPrev > lastKey.time + 0.00001f;
                    if (canAddPrev)
                        curve.AddKey(new Keyframe(tPrev, lastVal));

                    if (writeTime <= lastKey.time)
                        writeTime = lastKey.time + 0.0001f;

                    curve.AddKey(new Keyframe(writeTime, newVal));
                }
                else
                {
                    if (curve.length > 2)
                    {
                        int secondLast = curve.length - 2;
                        int last = curve.length - 1;
                        var secondLastKey = curve.keys[secondLast];
                        var lastKey2 = curve.keys[last];

                        if (Mathf.Approximately(secondLastKey.value, lastKey2.value) &&
                            Mathf.Approximately(lastKey2.value, newVal))
                        {
                            curve.RemoveKey(secondLast);
                        }
                    }

                    curve.AddKey(new Keyframe(t, newVal));
                }
            }
        }

        private UniverseSampleBuffer GetOrCreateSampleBuffer(int universe)
        {
            if (_samplesByUniverse.TryGetValue(universe, out var buffer) && buffer != null)
                return buffer;

            buffer = new UniverseSampleBuffer(channelCount);
            _samplesByUniverse[universe] = buffer;
            return buffer;
        }

        private List<DmxSample> GetOrCreateSampleList(UniverseSampleBuffer buffer, int index)
        {
            var list = buffer.samplesByChannel[index];
            if (list != null) return list;

            list = new List<DmxSample>(Mathf.Max(1, initialSampleCapacityPerChannel));
            buffer.samplesByChannel[index] = list;
            return list;
        }

        private Dictionary<int, AnimationCurve[]> BuildCurvesFromSamples()
        {
            var result = new Dictionary<int, AnimationCurve[]>();
            if (_samplesByUniverse.Count == 0) return result;

            foreach (var kv in _samplesByUniverse)
            {
                int universe = kv.Key;
                var buffer = kv.Value;
                if (buffer == null) continue;

                var curves = new AnimationCurve[channelCount];
                for (int i = 0; i < channelCount; i++)
                {
                    var curve = new AnimationCurve();
                    curves[i] = curve;

                    var samples = buffer.samplesByChannel[i];
                    if (samples == null || samples.Count == 0)
                        continue;

                    int absCh = i + 1;
                    bool isLinearChannel = IsLinearChannel(universe, absCh);
                    bool useStepForThisChannel = useStepKeys && !isLinearChannel;

                    if (!useStepForThisChannel)
                    {
                        for (int s = 0; s < samples.Count; s++)
                            curve.AddKey(new Keyframe(samples[s].time, samples[s].value));
                        continue;
                    }

                    var first = samples[0];
                    curve.AddKey(new Keyframe(first.time, first.value));

                    for (int s = 1; s < samples.Count; s++)
                    {
                        var prev = samples[s - 1];
                        var cur = samples[s];

                        float tPrev = cur.time - stepEpsilon;
                        float lastKeyTime = curve.keys[curve.length - 1].time;
                        if (tPrev > lastKeyTime + 0.00001f)
                            curve.AddKey(new Keyframe(tPrev, prev.value));

                        float tNow = cur.time;
                        if (tNow <= curve.keys[curve.length - 1].time)
                            tNow = curve.keys[curve.length - 1].time + 0.0001f;

                        curve.AddKey(new Keyframe(tNow, cur.value));
                    }
                }

                result[universe] = curves;
            }

            return result;
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
            _samplesByUniverse.Clear();
            _linearChannelsByUniverse.Clear();
            _isRecording = false;
            _startTime = 0f;
        }

        private static float GetGlobalEndTime(Dictionary<int, AnimationCurve[]> curvesByUniverse)
        {
            if (curvesByUniverse == null) return 0f;

            float maxTime = 0f;
            foreach (var kv in curvesByUniverse)
            {
                var curves = kv.Value;
                if (curves == null) continue;

                for (int i = 0; i < curves.Length; i++)
                {
                    var curve = curves[i];
                    if (curve == null || curve.length == 0) continue;

                    var last = curve.keys[curve.length - 1];
                    if (last.time > maxTime)
                        maxTime = last.time;
                }
            }

            return maxTime;
        }

        private static void ExtendCurvesToEndTime(AnimationCurve[] curves, float endTime)
        {
            if (curves == null || endTime <= 0f) return;

            const float epsilon = 0.00001f;
            for (int i = 0; i < curves.Length; i++)
            {
                var curve = curves[i];
                if (curve == null || curve.length == 0) continue;

                var last = curve.keys[curve.length - 1];
                if (endTime <= last.time + epsilon) continue;

                curve.AddKey(new Keyframe(endTime, last.value));
            }
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

        private bool IsLinearChannel(int universe, int absoluteChannel)
        {
            if (!hybridPanTiltLinear) return false;
            if (absoluteChannel < 1 || absoluteChannel > channelCount) return false;

            if (_linearChannelsByUniverse.TryGetValue(universe, out var map) &&
                map != null &&
                absoluteChannel < map.Length &&
                map[absoluteChannel])
            {
                return true;
            }

            if (fallbackLinearChannels == null) return false;
            for (int i = 0; i < fallbackLinearChannels.Count; i++)
            {
                if (fallbackLinearChannels[i] == absoluteChannel)
                    return true;
            }

            return false;
        }

        private void RebuildLinearChannelCache()
        {
            _linearChannelsByUniverse.Clear();

            if (!hybridPanTiltLinear || !autoDetectPanTiltChannels)
                return;

#if UNITY_2023_1_OR_NEWER
            var fixtures = FindObjectsByType<DmxFixtureComponent>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var fixtures = FindObjectsOfType<DmxFixtureComponent>(true);
#endif
            if (fixtures == null || fixtures.Length == 0)
                return;

            for (int i = 0; i < fixtures.Length; i++)
            {
                var f = fixtures[i];
                if (f == null) continue;

                if (!recordAllUniverses && (targetUniverses == null || !targetUniverses.Contains(f.universe)))
                    continue;

                // mappingが未解決のケースに備えて更新
                f.ResolveMapping();

                var map = GetOrCreateLinearMap(f.universe);
                MarkLinearChannel(map, f, FixtureFunction.PanCoarse);
                MarkLinearChannel(map, f, FixtureFunction.PanFine);
                MarkLinearChannel(map, f, FixtureFunction.TiltCoarse);
                MarkLinearChannel(map, f, FixtureFunction.TiltFine);
            }

            if (verboseLog)
            {
                foreach (var kv in _linearChannelsByUniverse)
                {
                    int count = 0;
                    var map = kv.Value;
                    for (int ch = 1; ch < map.Length; ch++)
                    {
                        if (map[ch]) count++;
                    }
                    Debug.Log($"[Recorder] Hybrid linear channels: U{kv.Key} -> {count}ch");
                }
            }
        }

        private bool[] GetOrCreateLinearMap(int universe)
        {
            if (_linearChannelsByUniverse.TryGetValue(universe, out var map) && map != null)
                return map;

            map = new bool[Mathf.Max(513, channelCount + 1)];
            _linearChannelsByUniverse[universe] = map;
            return map;
        }

        private void MarkLinearChannel(bool[] map, DmxFixtureComponent fixture, FixtureFunction function)
        {
            if (map == null || fixture == null) return;
            if (!fixture.TryGetRelativeChannel(function, out int rel)) return;

            int abs = fixture.startAddress + rel - 1;
            if (abs < 1 || abs > channelCount) return;
            if (abs >= map.Length) return;

            map[abs] = true;
        }

#if UNITY_EDITOR
        private static void SetClipCurvesToConstant(AnimationClip clip)
        {
            if (clip == null) return;

            var bindings = AnimationUtility.GetCurveBindings(clip);
            for (int b = 0; b < bindings.Length; b++)
            {
                var binding = bindings[b];
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null) continue;

                for (int i = 0; i < curve.length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
                }

                AnimationUtility.SetEditorCurve(clip, binding, curve);
            }
        }
#endif
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
