using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

#if HAS_HDRP
using UnityEngine.Rendering.HighDefinition;
#endif

namespace ArtNet.Runtime
{
    [DisallowMultipleComponent]
    public class DmxFixtureComponent : MonoBehaviour
    {
        // ------------------------------------------------------------
        // Basic
        // ------------------------------------------------------------

        [Header("Addressing (RigController expects these)")]
        [Tooltip("DMX Universe番号（送信側と合わせる。0/1始まりどちらでもOK。RigController側と合わせてください）")]
        public int universe = 0;

        [Tooltip("DMX Start Address (1-512)")]
        [Range(1, 512)]
        public int startAddress = 1;

        [Header("Profile (Fixture/Mode)")]
        public FixtureDefinition fixture;

        // RigController互換のため public（Editorで ModeName 表示にしていても内部は index）
        [SerializeField] public int mode = 0;

        // ------------------------------------------------------------
        // Targets
        // ------------------------------------------------------------

        [Header("Targets")]
        public Light targetLight;
        public Transform panTransform;
        public Transform tiltTransform;

        [Header("Pan/Tilt Range (degrees)")]
        public float panRangeDeg = 540f;
        public float tiltRangeDeg = 270f;

        // ------------------------------------------------------------
        // Pipeline / Driver (Built-in / URP / HDRP)
        // ------------------------------------------------------------

        public enum PipelineMode { Auto, ForceGeneric, ForceHDRP }

        [Header("Pipeline / Driver")]
        public PipelineMode pipelineMode = PipelineMode.Auto;

        [Tooltip("明示的にドライバを指定したい場合（GenericLightDriver / HdrpLightDriver 等）")]
        public MonoBehaviour driverOverride;

        [Tooltip("ドライバが見つからない場合に自動追加します（※追加先はこのDmxFixtureComponentが付いている同一GameObject）")]
        public bool autoAddDriverIfMissing = true;

        [Header("Driver Intensity Defaults")]
        [Tooltip("GenericLightDriver.maxIntensity に設定するデフォルト値")]
        public float genericMaxIntensity = 10f;

        [Tooltip("HdrpLightDriver.maxIntensity に設定するデフォルト値")]
        public float hdrpMaxIntensity = 9870f;

        [Tooltip("true の場合、Initialize時に上記 maxIntensity をドライバに上書きします")]
        public bool overrideDriverMaxIntensity = true;

        // ------------------------------------------------------------
        // Pan/Tilt Axis / Tuning
        // ------------------------------------------------------------

        public enum LocalAxis { X, Y, Z, MinusX, MinusY, MinusZ }

        [Header("Pan/Tilt Axis / Tuning")]
        [Tooltip("Panの回転軸（local）")]
        public LocalAxis panAxis = LocalAxis.Z; // ✅ デフォルトZ

        [Tooltip("Tiltの回転軸（local）")]
        public LocalAxis tiltAxis = LocalAxis.X;

        public bool panInvert = false;
        public bool tiltInvert = false;

        [Tooltip("Panのオフセット（度）")]
        public float panOffsetDeg = 0f;

        [Tooltip("Tiltのオフセット（度）")]
        public float tiltOffsetDeg = 0f;

        [Range(0f, 30f)]
        [Tooltip("Pan/Tiltの追従スムージング。0=即時、値を上げるほどヌルッと動く")]
        public float panTiltSmoothing = 12f;

        [Header("Pan/Tilt Speed (deg/sec)  ※PanTiltSpeedがある場合のみ有効")]
        [Tooltip("PanTiltSpeed=0 のときの角速度")]
        public float panTiltSpeedMinDegPerSec = 30f;

        [Tooltip("PanTiltSpeed=255 のときの角速度")]
        public float panTiltSpeedMaxDegPerSec = 720f;

        [Header("Reset Threshold")]
        [Tooltip("Reset ch がこの値以上のとき Reset を実行（機種により異なるので必要なら調整）")]
        [Range(0, 255)]
        public int resetTriggerThreshold = 250;

        [Header("Auto Resolve")]
        [SerializeField] private bool autoResolveOnValidate = true;

        // ------------------------------------------------------------
        // Monitoring (migrated from DmxDebugMonitor, no editor extension)
        // ------------------------------------------------------------

        [Header("Monitoring (DMX)")]
        [Tooltip("このFixtureが受信しているDMX値のモニタを有効化（Inspector表示/周期ログ）")]
        [SerializeField] private bool monitorEnabled = true;

        [Header("Monitor Channels (Absolute 1-512)")]
        [Tooltip("Universe内の絶対ch番号（1-512）を監視します")]
        [Range(1, 512)] public int monitorCh1 = 1;
        [Range(1, 512)] public int monitorCh2 = 2;
        [Range(1, 512)] public int monitorCh3 = 3;
        [Range(1, 512)] public int monitorCh4 = 4;
        [Range(1, 512)] public int monitorCh5 = 5;

        [Header("Monitoring (Auto / Relative)")]
        [Tooltip("FixtureDefinitionのModeで解決された FixtureFunction を自動で一覧モニタします")]
        [SerializeField] private bool monitorIncludeResolvedFunctions = true;

        [Tooltip("startAddress基準の相対ch(1=Start)を追加監視したい場合に指定します（例: 1,2,3,10,11...）")]
        [SerializeField] private List<int> monitorExtraRelativeChannels = new();

        [Header("Debug Values (ReadOnly)")]
        [SerializeField, Range(0, 255)] private int ch1;
        [SerializeField, Range(0, 255)] private int ch2;
        [SerializeField, Range(0, 255)] private int ch3;
        [SerializeField, Range(0, 255)] private int ch4;
        [SerializeField, Range(0, 255)] private int ch5;

        [Serializable]
        private struct DmxMonitorItem
        {
            public FixtureFunction function; // 0の場合は「追加相対ch」枠（ラベルは relXX で表示）
            public int relativeCh;           // 1-based within fixture (startAddress基準)
            public int absoluteCh;           // 1-based within universe
            [Range(0, 255)] public int value;
        }

        [Header("Debug Values (Resolved Function -> Value)")]
        [SerializeField] private List<DmxMonitorItem> monitorItems = new();

        [Header("Periodic Logging (Flood Protection)")]
        [Tooltip("一定間隔で受信データを要約ログ表示します（ログ洪水対策あり）")]
        [SerializeField] private bool enablePeriodicLog = false;

        [Tooltip("ログを出す間隔（秒）。例：1.0 で1秒ごと")]
        [SerializeField, Min(0.1f)] private float logIntervalSec = 1.0f;

        [Tooltip("受信が無い間隔はログしない（無駄ログ抑制）")]
        [SerializeField] private bool logOnlyWhenDataArrived = true;

        [Tooltip("ログの先頭にヘッダー（Universe/Start/Mode等）を含める")]
        [SerializeField] private bool includeHeaderInLog = true;

        private float _nextLogTime;
        private bool _hasNewDataSinceLastLog;

        // ------------------------------------------------------------
        // Runtime
        // ------------------------------------------------------------

        [NonSerialized] public ILightDriver runtimeDriver;
        [NonSerialized] public bool isInitialized;

        private Quaternion _panBaseLocalRot;
        private Quaternion _tiltBaseLocalRot;
        private bool _movementBaseCaptured;

        private readonly Dictionary<FixtureFunction, int> _relativeMap = new();
        [SerializeField] private string _activeProfileLabel;
        [SerializeField] private string _activeModeLabel;

        [Serializable]
        private struct ResolvedItem
        {
            public FixtureFunction function;
            public int relativeChannel; // 1-based within fixture
            public int absoluteChannel; // 1-based within universe
        }

        [SerializeField] private List<ResolvedItem> _resolvedItems = new();

        // ------------------------------------------------------------
        // RigController互換
        // ------------------------------------------------------------

        public bool IsValid
        {
            get
            {
                if (fixture == null) return false;
                if (fixture.modes == null || fixture.modes.Count == 0) return false;
                if (mode < 0 || mode >= fixture.modes.Count) return false;

                int count = Mathf.Clamp(fixture.modes[mode].channelCount, 1, 512);
                if (startAddress < 1 || startAddress > 512) return false;
                if (startAddress + count - 1 > 512) return false;
                return true;
            }
        }

        public void Initialize() => ResolveAll();
        public void Initialize(bool _) => ResolveAll();

        [ContextMenu("Auto Attach Target Light (Find in Children)")]
        public void Context_AutoAttachTargetLight()
        {
            if (targetLight == null)
                targetLight = GetComponentInChildren<Light>(true);
        }

        public void ApplyFromUniverseBuffer(int[] universe512) => ApplyFromUniverseBufferInternal(universe512);

        public void ApplyFromUniverseBuffer(byte[] universe512)
        {
            if (universe512 == null) return;
            int len = universe512.Length;
            var tmp = new int[len];
            for (int i = 0; i < len; i++) tmp[i] = universe512[i];
            ApplyFromUniverseBufferInternal(tmp);
        }

        // ------------------------------------------------------------
        // Unity lifecycle
        // ------------------------------------------------------------

        private void Reset()
        {
            Context_AutoAttachTargetLight();
            CaptureMovementBaseIfNeeded(force: true);
        }

        private void OnEnable()
        {
            ResolveAll();

            _nextLogTime = Time.unscaledTime + Mathf.Max(0.1f, logIntervalSec);
            _hasNewDataSinceLastLog = false;
        }

        private void OnValidate()
        {
            if (!autoResolveOnValidate) return;
            ResolveAll();
        }

        private void Update()
        {
            if (!monitorEnabled) return;
            if (!enablePeriodicLog) return;

            if (Time.unscaledTime < _nextLogTime) return;

            if (logOnlyWhenDataArrived && !_hasNewDataSinceLastLog)
            {
                _nextLogTime = Time.unscaledTime + Mathf.Max(0.1f, logIntervalSec);
                return;
            }

            Debug.Log(BuildMonitorSummaryLog(), this);

            _hasNewDataSinceLastLog = false;
            _nextLogTime = Time.unscaledTime + Mathf.Max(0.1f, logIntervalSec);
        }

        // ------------------------------------------------------------
        // Debug / Labels
        // ------------------------------------------------------------

        public string ActiveProfileLabel => _activeProfileLabel;
        public string ActiveModeLabel => _activeModeLabel;

        public IReadOnlyList<(FixtureFunction function, int relativeCh, int absoluteCh)> GetResolved()
        {
            var result = new List<(FixtureFunction, int, int)>(_resolvedItems.Count);
            foreach (var it in _resolvedItems) result.Add((it.function, it.relativeChannel, it.absoluteChannel));
            return result;
        }

        // ------------------------------------------------------------
        // Resolve Mapping + Driver
        // ------------------------------------------------------------

        private void ResolveAll()
        {
            ResolveMapping();
            if (monitorEnabled) RebuildMonitorItemsSkeleton();
            ResolveAndInitializeDriver();
            CaptureMovementBaseIfNeeded(force: false);
        }

        public void ResolveMapping()
        {
            _relativeMap.Clear();
            _resolvedItems.Clear();

            if (fixture == null)
            {
                _activeProfileLabel = "(No Fixture)";
                _activeModeLabel = "(No Mode)";
                return;
            }

            _activeProfileLabel = fixture.GetDisplayLabel();

            if (fixture.modes == null || fixture.modes.Count == 0)
            {
                _activeModeLabel = "(No Modes)";
                return;
            }

            mode = Mathf.Clamp(mode, 0, fixture.modes.Count - 1);
            var md = fixture.modes[mode];

            _activeModeLabel = string.IsNullOrWhiteSpace(md.modeName) ? $"Mode {mode}" : md.modeName;

            if (md.channels == null) return;

            foreach (var fc in md.channels)
            {
                if (fc == null) continue;

                int rel = Mathf.Clamp(fc.channel, 1, 512);
                _relativeMap[fc.function] = rel;

                int abs = startAddress + rel - 1;
                _resolvedItems.Add(new ResolvedItem
                {
                    function = fc.function,
                    relativeChannel = rel,
                    absoluteChannel = abs
                });
            }
        }

        private void RebuildMonitorItemsSkeleton()
        {
            if (monitorItems == null) monitorItems = new List<DmxMonitorItem>();
            monitorItems.Clear();

            // 1) FixtureDefinitionの Mode 定義（ResolvedItem）をそのまま一覧化
            if (monitorIncludeResolvedFunctions && _resolvedItems != null)
            {
                for (int i = 0; i < _resolvedItems.Count; i++)
                {
                    var r = _resolvedItems[i];
                    monitorItems.Add(new DmxMonitorItem
                    {
                        function = r.function,
                        relativeCh = Mathf.Clamp(r.relativeChannel, 1, 512),
                        absoluteCh = Mathf.Clamp(r.absoluteChannel, 1, 512),
                        value = 0
                    });
                }
            }

            // 2) 任意の相対chを追加（重複は除外）
            if (monitorExtraRelativeChannels != null)
            {
                for (int i = 0; i < monitorExtraRelativeChannels.Count; i++)
                {
                    int rel = monitorExtraRelativeChannels[i];
                    if (rel < 1) continue;

                    bool dup = false;
                    for (int k = 0; k < monitorItems.Count; k++)
                    {
                        if (monitorItems[k].relativeCh == rel)
                        {
                            dup = true;
                            break;
                        }
                    }
                    if (dup) continue;

                    int abs = startAddress + rel - 1;
                    monitorItems.Add(new DmxMonitorItem
                    {
                        function = default,
                        relativeCh = rel,
                        absoluteCh = Mathf.Clamp(abs, 1, 512),
                        value = 0
                    });
                }
            }

            // 表示の安定性のため relativeCh順に並べる（小規模なのでO(n^2)で十分）
            for (int i = 0; i < monitorItems.Count - 1; i++)
            {
                for (int j = i + 1; j < monitorItems.Count; j++)
                {
                    if (monitorItems[j].relativeCh < monitorItems[i].relativeCh)
                    {
                        var tmp = monitorItems[i];
                        monitorItems[i] = monitorItems[j];
                        monitorItems[j] = tmp;
                    }
                }
            }
        }

        public bool TryGetRelativeChannel(FixtureFunction function, out int relative1Based)
            => _relativeMap.TryGetValue(function, out relative1Based);

        private void ResolveAndInitializeDriver()
        {
            isInitialized = false;
            runtimeDriver = null;

            if (targetLight == null)
                Context_AutoAttachTargetLight();

            if (targetLight == null)
                return;

            bool detectedHdrp = false;
#if HAS_HDRP
            detectedHdrp = (targetLight.GetComponent<HDAdditionalLightData>() != null);
#endif

            bool wantHdrp = pipelineMode switch
            {
                PipelineMode.ForceHDRP => true,
                PipelineMode.ForceGeneric => false,
                _ => detectedHdrp
            };

            // 1) override
            if (driverOverride != null)
            {
                runtimeDriver = driverOverride as ILightDriver;
                if (runtimeDriver == null)
                {
                    Debug.LogWarning($"[DmxFixtureComponent] driverOverride is set but does not implement ILightDriver: {driverOverride.GetType().Name}", this);
                }
            }

            // 2) resolve from SAME GameObject
            if (runtimeDriver == null)
            {
#if HAS_HDRP
                if (wantHdrp)
                {
                    var hdrp = GetComponent<HdrpLightDriver>();
                    if (hdrp != null) runtimeDriver = hdrp;
                }
#endif
                if (runtimeDriver == null)
                {
                    var generic = GetComponent<GenericLightDriver>();
                    if (generic != null) runtimeDriver = generic;
                }
            }

            // 2.5) legacy fallback: targetLight側に付いてるドライバを拾う（既存シーン破壊防止）
            if (runtimeDriver == null && targetLight != null)
            {
#if HAS_HDRP
                var hdrpOnLight = targetLight.GetComponent<HdrpLightDriver>();
                if (hdrpOnLight != null)
                {
                    runtimeDriver = hdrpOnLight;
                    Debug.LogWarning("[DmxFixtureComponent] Found LightDriver on targetLight GameObject (legacy). " +
                                     "Now we recommend attaching LightDriver to the same GameObject as DmxFixtureComponent.", this);
                }
#endif
                if (runtimeDriver == null)
                {
                    var genericOnLight = targetLight.GetComponent<GenericLightDriver>();
                    if (genericOnLight != null)
                    {
                        runtimeDriver = genericOnLight;
                        Debug.LogWarning("[DmxFixtureComponent] Found LightDriver on targetLight GameObject (legacy). " +
                                         "Now we recommend attaching LightDriver to the same GameObject as DmxFixtureComponent.", this);
                    }
                }
            }

            // 3) auto add to SAME GameObject
            if (runtimeDriver == null && autoAddDriverIfMissing)
            {
#if HAS_HDRP
                if (wantHdrp)
                    runtimeDriver = gameObject.AddComponent<HdrpLightDriver>();
                else
                    runtimeDriver = gameObject.AddComponent<GenericLightDriver>();
#else
                if (wantHdrp)
                    Debug.LogWarning($"[DmxFixtureComponent] HDRP希望ですが HAS_HDRP 未定義のため GenericLightDriver を追加します: {name}", this);

                runtimeDriver = gameObject.AddComponent<GenericLightDriver>();
#endif
            }

            // Initialize
            runtimeDriver?.Initialize(targetLight);

            // intensity defaults
            if (overrideDriverMaxIntensity && runtimeDriver != null)
            {
                if (runtimeDriver is GenericLightDriver gd)
                    gd.maxIntensity = genericMaxIntensity;

#if HAS_HDRP
                if (runtimeDriver is HdrpLightDriver hd)
                    hd.maxIntensity = hdrpMaxIntensity;
#endif
            }

            isInitialized = runtimeDriver != null;
        }

        private void CaptureMovementBaseIfNeeded(bool force)
        {
            if (!force && _movementBaseCaptured) return;

            if (panTransform != null) _panBaseLocalRot = panTransform.localRotation;
            if (tiltTransform != null) _tiltBaseLocalRot = tiltTransform.localRotation;

            _movementBaseCaptured = true;
        }

        // ------------------------------------------------------------
        // Apply core
        // ------------------------------------------------------------

        private void ApplyFromUniverseBufferInternal(int[] universe512)
        {
            if (universe512 == null) return;
            if (!IsValid) return;

            if (targetLight == null) Context_AutoAttachTargetLight();

            // --- Reset ---
            if (TryGetRelativeChannel(FixtureFunction.Reset, out int resetRel))
            {
                int resetAbs = startAddress + resetRel - 1;
                int resetVal = Read8Abs(universe512, resetAbs);
                if (resetVal >= resetTriggerThreshold)
                    PerformReset();
            }

            // --- Dimmer ---
            float dim01 = 1f;
            if (TryGetRelativeChannel(FixtureFunction.Dimmer, out int dimRel))
            {
                int dimAbs = startAddress + dimRel - 1;
                dim01 = DmxValueUtils.ByteTo01(Read8Abs(universe512, dimAbs));
            }

            // --- RGB (+White) ---
            Color rgb = Color.white;

            bool hasR = TryGetRelativeChannel(FixtureFunction.Red, out int rRel);
            bool hasG = TryGetRelativeChannel(FixtureFunction.Green, out int gRel);
            bool hasB = TryGetRelativeChannel(FixtureFunction.Blue, out int bRel);

            if (hasR && hasG && hasB)
            {
                float r = DmxValueUtils.ByteTo01(Read8Abs(universe512, startAddress + rRel - 1));
                float g = DmxValueUtils.ByteTo01(Read8Abs(universe512, startAddress + gRel - 1));
                float b = DmxValueUtils.ByteTo01(Read8Abs(universe512, startAddress + bRel - 1));

                rgb = new Color(r, g, b, 1f);

                // Whiteが割り当てられている場合は「白方向に寄せる」簡易モデル
                if (TryGetRelativeChannel(FixtureFunction.White, out int wRel))
                {
                    float w = DmxValueUtils.ByteTo01(Read8Abs(universe512, startAddress + wRel - 1));
                    rgb = rgb + Color.white * w;
                }
            }

            // --- Apply to Light via Driver ---
            if (isInitialized && runtimeDriver != null)
            {
                runtimeDriver.Apply(dim01, rgb);
            }
            else
            {
                // ドライバ未使用でも最低限は反映（保険）
                if (targetLight != null)
                {
                    targetLight.color = rgb;
                    targetLight.intensity = dim01 * 10f;
                }
            }

            // --- Pan/Tilt Speed（あればスムージング適用） ---
            bool hasSpeed = TryGetRelativeChannel(FixtureFunction.PanTiltSpeed, out int spRel);
            float maxDegPerSec = panTiltSpeedMaxDegPerSec;
            if (hasSpeed)
            {
                float sp01 = DmxValueUtils.ByteTo01(Read8Abs(universe512, startAddress + spRel - 1)); // slow -> fast
                maxDegPerSec = Mathf.Lerp(panTiltSpeedMinDegPerSec, panTiltSpeedMaxDegPerSec, sp01);
            }

            // --- Pan ---
            if (panTransform != null && TryGetRelativeChannel(FixtureFunction.PanCoarse, out int panCoarseRel))
            {
                int panFineRel = TryGetRelativeChannel(FixtureFunction.PanFine, out int pf) ? pf : -1;

                int panAbsCoarse = startAddress + panCoarseRel - 1;
                int panAbsFine = (panFineRel > 0) ? (startAddress + panFineRel - 1) : -1;

                int pan16 = Read16Abs(universe512, panAbsCoarse, panAbsFine);
                float pan01 = pan16 / 65535f;
                float panDeg = (pan01 - 0.5f) * panRangeDeg;

                if (panInvert) panDeg = -panDeg;
                panDeg += panOffsetDeg;

                ApplyAxisRotation(
                    panTransform,
                    _panBaseLocalRot,
                    panAxis,
                    panDeg,
                    hasSpeed ? maxDegPerSec : -1f,
                    panTiltSmoothing
                );
            }

            // --- Tilt ---
            if (tiltTransform != null && TryGetRelativeChannel(FixtureFunction.TiltCoarse, out int tiltCoarseRel))
            {
                int tiltFineRel = TryGetRelativeChannel(FixtureFunction.TiltFine, out int tf) ? tf : -1;

                int tiltAbsCoarse = startAddress + tiltCoarseRel - 1;
                int tiltAbsFine = (tiltFineRel > 0) ? (startAddress + tiltFineRel - 1) : -1;

                int tilt16 = Read16Abs(universe512, tiltAbsCoarse, tiltAbsFine);
                float tilt01 = tilt16 / 65535f;
                float tiltDeg = (tilt01 - 0.5f) * tiltRangeDeg;

                if (tiltInvert) tiltDeg = -tiltDeg;
                tiltDeg += tiltOffsetDeg;

                ApplyAxisRotation(
                    tiltTransform,
                    _tiltBaseLocalRot,
                    tiltAxis,
                    tiltDeg,
                    hasSpeed ? maxDegPerSec : -1f,
                    panTiltSmoothing
                );
            }

            // --- Monitoring update (per-fixture realtime values) ---
            UpdateMonitorValues(universe512);
        }

        private void UpdateMonitorValues(int[] universe512)
        {
            if (!monitorEnabled) return;

            // 5ch quick monitor (absolute)
            ch1 = Read8Abs(universe512, monitorCh1);
            ch2 = Read8Abs(universe512, monitorCh2);
            ch3 = Read8Abs(universe512, monitorCh3);
            ch4 = Read8Abs(universe512, monitorCh4);
            ch5 = Read8Abs(universe512, monitorCh5);

            // Function / relative list
            if (monitorItems != null)
            {
                for (int i = 0; i < monitorItems.Count; i++)
                {
                    var it = monitorItems[i];
                    it.absoluteCh = Mathf.Clamp(startAddress + it.relativeCh - 1, 1, 512);
                    it.value = Read8Abs(universe512, it.absoluteCh);
                    monitorItems[i] = it;
                }
            }

            _hasNewDataSinceLastLog = true;
        }

        private string BuildMonitorSummaryLog()
        {
            var sb = new StringBuilder(256);
            sb.Append("[DmxFixtureComponent Monitor] ");

            if (includeHeaderInLog)
            {
                sb.Append(name);
                sb.Append(" | Uni="); sb.Append(universe);
                sb.Append(", Start="); sb.Append(startAddress);
                sb.Append(", Profile="); sb.Append(_activeProfileLabel);
                sb.Append(", Mode="); sb.Append(_activeModeLabel);
                sb.Append(" | ");
            }

            sb.Append("ABS ");
            sb.Append("ch"); sb.Append(monitorCh1); sb.Append('='); sb.Append(ch1); sb.Append(", ");
            sb.Append("ch"); sb.Append(monitorCh2); sb.Append('='); sb.Append(ch2); sb.Append(", ");
            sb.Append("ch"); sb.Append(monitorCh3); sb.Append('='); sb.Append(ch3); sb.Append(", ");
            sb.Append("ch"); sb.Append(monitorCh4); sb.Append('='); sb.Append(ch4); sb.Append(", ");
            sb.Append("ch"); sb.Append(monitorCh5); sb.Append('='); sb.Append(ch5);

            if (monitorItems != null && monitorItems.Count > 0)
            {
                sb.Append(" | REL/FUNC ");
                for (int i = 0; i < monitorItems.Count; i++)
                {
                    var it = monitorItems[i];

                    // functionが0(未設定)なら relXX として表示
                    string label = (Convert.ToInt32(it.function) == 0)
                        ? $"rel{it.relativeCh}"
                        : it.function.ToString();

                    sb.Append(label);
                    sb.Append('(');
                    sb.Append(it.relativeCh);
                    sb.Append("->");
                    sb.Append(it.absoluteCh);
                    sb.Append(")=");
                    sb.Append(it.value);
                    if (i != monitorItems.Count - 1) sb.Append(", ");
                }
            }

            return sb.ToString();
        }

        private void PerformReset()
        {
            if (panTransform != null) panTransform.localRotation = _panBaseLocalRot;
            if (tiltTransform != null) tiltTransform.localRotation = _tiltBaseLocalRot;

            if (runtimeDriver != null)
                runtimeDriver.Apply(0f, Color.white);
            else if (targetLight != null)
            {
                targetLight.intensity = 0f;
                targetLight.color = Color.white;
            }
        }

        // ------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------

        private static int Read8Abs(int[] universe512, int absCh1Based)
        {
            int idx = absCh1Based - 1;
            if (idx < 0 || idx >= universe512.Length) return 0;
            return DmxValueUtils.ClampByte(universe512[idx]);
        }

        private static int Read16Abs(int[] universe512, int absCoarseCh1Based, int absFineCh1BasedOrMinus1)
        {
            int coarse = Read8Abs(universe512, absCoarseCh1Based);
            if (absFineCh1BasedOrMinus1 <= 0) return coarse << 8;
            int fine = Read8Abs(universe512, absFineCh1BasedOrMinus1);
            return (coarse << 8) | fine;
        }

        private static Vector3 AxisToVector(LocalAxis a) => a switch
        {
            LocalAxis.X => Vector3.right,
            LocalAxis.Y => Vector3.up,
            LocalAxis.Z => Vector3.forward,
            LocalAxis.MinusX => -Vector3.right,
            LocalAxis.MinusY => -Vector3.up,
            LocalAxis.MinusZ => -Vector3.forward,
            _ => Vector3.forward
        };

        private static void ApplyAxisRotation(
            Transform t,
            Quaternion baseLocalRotation,
            LocalAxis axis,
            float degrees,
            float speedDegPerSecOrMinus1,
            float smoothing)
        {
            var ax = AxisToVector(axis);
            var target = baseLocalRotation * Quaternion.AngleAxis(degrees, ax);

            // Speed優先（PanTiltSpeedがある場合）
            if (speedDegPerSecOrMinus1 > 0f)
            {
                t.localRotation = Quaternion.RotateTowards(t.localRotation, target, speedDegPerSecOrMinus1 * Time.deltaTime);
                return;
            }

            // Smoothing
            if (smoothing <= 0f)
            {
                t.localRotation = target;
                return;
            }

            float k = Mathf.Clamp01(Time.deltaTime * smoothing);
            t.localRotation = Quaternion.Slerp(t.localRotation, target, k);
        }
    }
}
