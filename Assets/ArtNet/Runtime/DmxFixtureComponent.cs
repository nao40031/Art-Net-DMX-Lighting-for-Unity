using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

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
        [Tooltip("DMX Universe逡ｪ蜿ｷ・磯∽ｿ｡蛛ｴ縺ｨ蜷医ｏ縺帙ｋ縲・/1蟋九∪繧翫←縺｡繧峨〒繧０K縲３igController蛛ｴ縺ｨ蜷医ｏ縺帙※縺上□縺輔＞・・")]
        public int universe = 0;

        [Tooltip("DMX Start Address (1-512)")]
        [Range(1, 512)]
        public int startAddress = 1;

        [Header("Profile (Fixture/Mode)")]
        public FixtureDefinition fixture;

        // RigController莠呈鋤縺ｮ縺溘ａ public・・ditor縺ｧ ModeName 陦ｨ遉ｺ縺ｫ縺励※縺・※繧ょ・驛ｨ縺ｯ index・・
        [SerializeField] public int mode = 0;

        // ------------------------------------------------------------
        // Targets
        // ------------------------------------------------------------

        [Header("Targets")]
        public Light targetLight;
        [Tooltip("隍・焚繧ｿ繝ｼ繧ｲ繝・ヨ逕ｨ縲りｨｭ螳壹☆繧九→蜈ｨ縺ｦ縺ｮLight縺ｫ蜷後§DMX縺碁←逕ｨ縺輔ｌ縺ｾ縺呻ｼ・argetLight繧ゆｽｵ逕ｨ蜿ｯ・・")]
        public List<Light> targetLights = new();
        public Transform panTransform;
        public Transform tiltTransform;


        // ------------------------------------------------------------
        // Lens (ShaderGraph DMX Sync)
        // ------------------------------------------------------------

        [Header("Lens (ShaderGraph DMX Sync)")]
        [Tooltip("繝ｬ繝ｳ繧ｺ髱｢縺ｮRenderer縲４haderGraph蛛ｴ縺ｫ _DmxColor(Color) / _DmxDimmer(Float) 縺後≠繧句燕謠舌〒縲．MX縺ｮ濶ｲ縺ｨDimmer繧呈ｸ｡縺励∪縺吶・")]
        [SerializeField] private Renderer lensRenderer;

        [Tooltip("繝ｬ繝ｳ繧ｺ縺瑚､・焚Renderer縺ｫ蛻・°繧後※縺・ｋ蝣ｴ蜷医・霑ｽ蜉縺ｧ逋ｻ骭ｲ縺励∪縺呻ｼ井ｻｻ諢擾ｼ峨・")]
        [SerializeField] private Renderer[] extraLensRenderers;

        [Tooltip("繝ｬ繝ｳ繧ｺ縺ｸDMX蜷梧悄繧定｡後≧縺・")]
        [SerializeField] private bool syncLensToDmx = true;
        [SerializeField] private bool syncLensColorToDmx = true;
        [SerializeField] private bool syncLensDimmerToDmx = true;

        [Tooltip("ShaderGraph縺ｮColor繝励Ο繝代ユ繧｣蜷搾ｼ・eference・峨ゆｾ・ _DmxColor")]
        [SerializeField] private string lensColorProperty = "_DmxColor";

        [Tooltip("ShaderGraph縺ｮFloat繝励Ο繝代ユ繧｣蜷搾ｼ・eference・峨ゆｾ・ _DmxDimmer")]
        [SerializeField] private string lensDimmerProperty = "_DmxDimmer";

        [Tooltip("Dimmer(0-1)縺ｫ謗帙￠繧句咲紫縲ゅΞ繝ｳ繧ｺ縺梧囓縺・譏弱ｋ縺吶℃繧区凾縺ｮ隱ｿ謨ｴ逕ｨ縲・")]
        [SerializeField, Min(0f)] private float lensDimmerScale = 1.0f;

        // ------------------------------------------------------------
        // Light Response (LED / Halogen)
        // ------------------------------------------------------------

        public enum LightResponseMode { Led, Halogen }

        [Header("Light Response")]
        public LightResponseMode lightResponseMode = LightResponseMode.Led;

        [Header("Halogen Response (seconds)")]
        [Tooltip("Source (lens) rise time from 0 to 1.")]
        [SerializeField, Min(0f)] private float halogenSourceRiseTime = 0.06f;

        [Tooltip("Source (lens) fall time from 1 to 0.")]
        [SerializeField, Min(0f)] private float halogenSourceFallTime = 0.10f;

        [Tooltip("Beam (light) delay on after source turns on.")]
        [SerializeField, Min(0f)] private float halogenBeamOnDelay = 0.04f;

        [Tooltip("Beam (light) delay off after source turns off.")]
        [SerializeField, Min(0f)] private float halogenBeamOffDelay = 0.04f;

        [Tooltip("Beam (light) rise time from 0 to 1.")]
        [SerializeField, Min(0f)] private float halogenBeamRiseTime = 0.06f;

        [Tooltip("Beam (light) fall time from 1 to 0.")]
        [SerializeField, Min(0f)] private float halogenBeamFallTime = 0.08f;

        // MaterialPropertyBlock縺ｧ per-renderer 縺ｫ螳牙・縺ｫ譖ｸ縺崎ｾｼ繧・医・繝・Μ繧｢繝ｫ隍・｣ｽ繧帝∩縺代ｋ・・
        private MaterialPropertyBlock _lensMpb;
        private int _lensColorId;
        private int _lensDimmerId;
        private bool _lensPropertyIdsReady;
        [Header("Pan/Tilt Range (degrees)")]
        public float panRangeDeg = 540f;
        public float tiltRangeDeg = 270f;

        // ------------------------------------------------------------
        // Pipeline / Driver (Built-in / URP / HDRP)
        // ------------------------------------------------------------

        public enum PipelineMode { Auto, ForceGeneric, ForceHDRP }

        [Header("Pipeline / Driver")]
        public PipelineMode pipelineMode = PipelineMode.Auto;

        [Tooltip("譏守､ｺ逧・↓繝峨Λ繧､繝舌ｒ謖・ｮ壹＠縺溘＞蝣ｴ蜷茨ｼ・enericLightDriver / HdrpLightDriver 遲会ｼ・")]
        public MonoBehaviour driverOverride;

        [Tooltip("繝峨Λ繧､繝舌′隕九▽縺九ｉ縺ｪ縺・ｴ蜷医↓閾ｪ蜍戊ｿｽ蜉縺励∪縺呻ｼ遺ｻ霑ｽ蜉蜈医・縺薙・DmxFixtureComponent縺御ｻ倥＞縺ｦ縺・ｋ蜷御ｸGameObject・・")]
        public bool autoAddDriverIfMissing = true;

        [Header("Driver Intensity Defaults")]
        [Tooltip("GenericLightDriver.maxIntensity 縺ｫ險ｭ螳壹☆繧九ョ繝輔か繝ｫ繝亥､")]
        public float genericMaxIntensity = 10f;

        [Tooltip("HdrpLightDriver.maxIntensity 縺ｫ險ｭ螳壹☆繧九ョ繝輔か繝ｫ繝亥､")]
        public float hdrpMaxIntensity = 9870f;

        [Tooltip("true 縺ｮ蝣ｴ蜷医！nitialize譎ゅ↓荳願ｨ・maxIntensity 繧偵ラ繝ｩ繧､繝舌↓荳頑嶌縺阪＠縺ｾ縺・")]
        public bool overrideDriverMaxIntensity = true;

        // ------------------------------------------------------------
        // Pan/Tilt Axis / Tuning
        // ------------------------------------------------------------

        public enum LocalAxis { X, Y, Z, MinusX, MinusY, MinusZ }

        [Header("Pan/Tilt Axis / Tuning")]
        [Tooltip("Pan縺ｮ蝗櫁ｻ｢霆ｸ・・ocal・・")]
        public LocalAxis panAxis = LocalAxis.Z; // 笨・繝・ヵ繧ｩ繝ｫ繝・

        [Tooltip("Tilt縺ｮ蝗櫁ｻ｢霆ｸ・・ocal・・")]
        public LocalAxis tiltAxis = LocalAxis.X;

        public bool panInvert = false;
        public bool tiltInvert = false;

        [Tooltip("Pan縺ｮ繧ｪ繝輔そ繝・ヨ・亥ｺｦ・・")]
        public float panOffsetDeg = 0f;

        [Tooltip("Tilt縺ｮ繧ｪ繝輔そ繝・ヨ・亥ｺｦ・・")]
        public float tiltOffsetDeg = 0f;

        [Range(0f, 30f)]
        [Tooltip("Pan/Tilt縺ｮ霑ｽ蠕薙せ繝繝ｼ繧ｸ繝ｳ繧ｰ縲・=蜊ｳ譎ゅ∝､繧剃ｸ翫￡繧九⊇縺ｩ繝後Ν繝・→蜍輔￥")]
        public float panTiltSmoothing = 12f;


        [Tooltip("DMX蜿嶺ｿ｡譖ｴ譁ｰ(萓・40Hz)縺ｨ謠冗判(萓・60Hz)縺ｮ蟾ｮ縺ｧ谿ｵ蟾ｮ縺瑚ｦ九∴繧句ｴ蜷医↓縲∵ｯ弱ヵ繝ｬ繝ｼ繝陬憺俣縺ｧ貊代ｉ縺九↓縺励∪縺・")]
        public bool enableContinuousPanTiltUpdate = true;

        [Header("Edit Mode Preview")]
        [Tooltip("EditモードのTimelineプレビュー時にPan/Tiltと光量を即時反映します")]
        public bool applyImmediateInEditMode = true;

        [Header("Pan/Tilt Speed (deg/sec)  窶ｻPanTiltSpeed縺後≠繧句ｴ蜷医・縺ｿ譛牙柑")]
        [Tooltip("PanTiltSpeed=0 縺ｮ縺ｨ縺阪・隗帝溷ｺｦ")]
        public float panTiltSpeedMinDegPerSec = 30f;

        [Tooltip("PanTiltSpeed=255 縺ｮ縺ｨ縺阪・隗帝溷ｺｦ")]
        public float panTiltSpeedMaxDegPerSec = 720f;

        [Header("Reset Threshold")]
        [Tooltip("Reset ch 縺後％縺ｮ蛟､莉･荳翫・縺ｨ縺・Reset 繧貞ｮ溯｡鯉ｼ域ｩ溽ｨｮ縺ｫ繧医ｊ逡ｰ縺ｪ繧九・縺ｧ蠢・ｦ√↑繧芽ｪｿ謨ｴ・・")]
        [Range(0, 255)]
        public int resetTriggerThreshold = 250;

        [Header("Auto Resolve")]
        [SerializeField] private bool autoResolveOnValidate = true;

        // ------------------------------------------------------------
        // Monitoring (migrated from DmxDebugMonitor, no editor extension)
        // ------------------------------------------------------------

        [Header("Monitoring (DMX)")]
        [Tooltip("縺薙・Fixture縺悟女菫｡縺励※縺・ｋDMX蛟､縺ｮ繝｢繝九ち繧呈怏蜉ｹ蛹厄ｼ・nspector陦ｨ遉ｺ/蜻ｨ譛溘Ο繧ｰ・・")]
        [SerializeField] private bool monitorEnabled = true;

        [Header("Monitor Channels (Absolute 1-512)")]
        [Tooltip("Universe蜀・・邨ｶ蟇ｾch逡ｪ蜿ｷ・・-512・峨ｒ逶｣隕悶＠縺ｾ縺・")]
        [Range(1, 512)] public int monitorCh1 = 1;
        [Range(1, 512)] public int monitorCh2 = 2;
        [Range(1, 512)] public int monitorCh3 = 3;
        [Range(1, 512)] public int monitorCh4 = 4;
        [Range(1, 512)] public int monitorCh5 = 5;

        [Header("Monitoring (Auto / Relative)")]
        [Tooltip("FixtureDefinition縺ｮMode縺ｧ隗｣豎ｺ縺輔ｌ縺・FixtureFunction 繧定・蜍輔〒荳隕ｧ繝｢繝九ち縺励∪縺・")]
        [SerializeField] private bool monitorIncludeResolvedFunctions = true;

        [Tooltip("startAddress蝓ｺ貅悶・逶ｸ蟇ｾch(1=Start)繧定ｿｽ蜉逶｣隕悶＠縺溘＞蝣ｴ蜷医↓謖・ｮ壹＠縺ｾ縺呻ｼ井ｾ・ 1,2,3,10,11...・・")]
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
            public FixtureFunction function; // 0縺ｮ蝣ｴ蜷医・縲瑚ｿｽ蜉逶ｸ蟇ｾch縲肴棧・医Λ繝吶Ν縺ｯ relXX 縺ｧ陦ｨ遉ｺ・・
            public int relativeCh;           // 1-based within fixture (startAddress蝓ｺ貅・
            public int absoluteCh;           // 1-based within universe
            [Range(0, 255)] public int value;
        }

        [Header("Debug Values (Resolved Function -> Value)")]
        [SerializeField] private List<DmxMonitorItem> monitorItems = new();

        [Header("Periodic Logging (Flood Protection)")]
        [Tooltip("荳螳夐俣髫斐〒蜿嶺ｿ｡繝・・繧ｿ繧定ｦ∫ｴ・Ο繧ｰ陦ｨ遉ｺ縺励∪縺呻ｼ医Ο繧ｰ豢ｪ豌ｴ蟇ｾ遲悶≠繧奇ｼ・")]
        [SerializeField] private bool enablePeriodicLog = false;

        [Tooltip("繝ｭ繧ｰ繧貞・縺咎俣髫費ｼ育ｧ抵ｼ峨ゆｾ具ｼ・.0 縺ｧ1遘偵＃縺ｨ")]
        [SerializeField, Min(0.1f)] private float logIntervalSec = 1.0f;

        [Tooltip("蜿嶺ｿ｡縺檎┌縺・俣髫斐・繝ｭ繧ｰ縺励↑縺・ｼ育┌鬧・Ο繧ｰ謚大宛・・")]
        [SerializeField] private bool logOnlyWhenDataArrived = true;

        [Tooltip("繝ｭ繧ｰ縺ｮ蜈磯ｭ縺ｫ繝倥ャ繝繝ｼ・・niverse/Start/Mode遲会ｼ峨ｒ蜷ｫ繧√ｋ")]
        [SerializeField] private bool includeHeaderInLog = true;

        private float _nextLogTime;
        private bool _hasNewDataSinceLastLog;

        // ------------------------------------------------------------
        // Runtime
        // ------------------------------------------------------------

        [NonSerialized] public ILightDriver runtimeDriver;
        [NonSerialized] public bool isInitialized;
        [NonSerialized] public List<ILightDriver> runtimeDrivers = new();

        private Quaternion _panBaseLocalRot;
        private Quaternion _tiltBaseLocalRot;
        private bool _movementBaseCaptured;

#if UNITY_EDITOR
        [NonSerialized] private bool _previewLightCaptured;
        [NonSerialized] private List<Light> _previewLightTargets = new();
        [NonSerialized] private List<float> _previewLightIntensity = new();
        [NonSerialized] private List<Color> _previewLightColor = new();
#endif

        private bool _hasDmxTargets;
        private float _targetDimmer01;
        private Color _targetColor = Color.white;
        private float _lensTargetDimmer01;
        private float _lightTargetDimmer01;
        private float _currentLensDimmer01;
        private float _currentLightDimmer01;
        private bool _lightTargetPending;
        private float _lightTargetPendingAt;
        private float _pendingLightTargetDimmer01;
        private bool _lensTargetPending;
        private float _lensTargetPendingAt;
        private float _pendingLensTargetDimmer01;


        // --- Pan/Tilt continuous interpolation targets (updated by DMX, applied every frame) ---
        private bool _hasPanTarget;
        private bool _hasTiltTarget;
        private Quaternion _panTargetLocalRot;
        private Quaternion _tiltTargetLocalRot;
        private float _panTargetMaxDegPerSec = -1f;
        private float _tiltTargetMaxDegPerSec = -1f;
        private float _panTargetSmoothing = 0.15f;
        private float _tiltTargetSmoothing = 0.15f;

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
        // RigController莠呈鋤
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

        [ContextMenu("Auto Attach Target Lights (Find in Children)")]
        public void Context_AutoAttachTargetLights()
        {
            var lights = GetComponentsInChildren<Light>(true);
            targetLights = new List<Light>(lights);
            if (targetLight == null && lights.Length > 0)
                targetLight = lights[0];
        }

        public void ApplyFromUniverseBuffer(int[] universe512) => ApplyFromUniverseBufferInternal(universe512);

        public void ApplyFromUniverseBuffer(byte[] universe512) => ApplyFromUniverseBufferInternal(universe512);

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

            if (Application.isPlaying)
                CaptureMovementBaseIfNeeded(force: true);

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
            UpdatePanTiltMotion();
            UpdateLightResponse();

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
            ResolveAndInitializeDrivers();
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

            // 1) FixtureDefinition縺ｮ Mode 螳夂ｾｩ・・esolvedItem・峨ｒ縺昴・縺ｾ縺ｾ荳隕ｧ蛹・
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

            // 2) 莉ｻ諢上・逶ｸ蟇ｾch繧定ｿｽ蜉・磯㍾隍・・髯､螟厄ｼ・
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

            // 陦ｨ遉ｺ縺ｮ螳牙ｮ壽ｧ縺ｮ縺溘ａ relativeCh鬆・↓荳ｦ縺ｹ繧具ｼ亥ｰ剰ｦ乗ｨ｡縺ｪ縺ｮ縺ｧO(n^2)縺ｧ蜊∝・・・
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

        private void ResolveAndInitializeDrivers()
        {
            isInitialized = false;
            runtimeDriver = null;

            if (targetLight == null && (targetLights == null || targetLights.Count == 0))
                Context_AutoAttachTargetLight();

            if (runtimeDrivers == null) runtimeDrivers = new List<ILightDriver>();
            runtimeDrivers.Clear();

            var targets = GatherTargetLights();
            if (targets.Count == 0)
                return;

            var usedDrivers = new HashSet<ILightDriver>();
            var overrideDriver = driverOverride as ILightDriver;

            if (driverOverride != null && overrideDriver == null)
                Debug.LogWarning($"[DmxFixtureComponent] driverOverride is set but does not implement ILightDriver: {driverOverride.GetType().Name}", this);

#if HAS_HDRP
            var hdrpOnSelf = GetComponents<HdrpLightDriver>();
#endif
            var genericOnSelf = GetComponents<GenericLightDriver>();

            bool warnedOverrideReuse = false;
            foreach (var light in targets)
            {
                if (light == null) continue;

                bool detectedHdrp = false;
#if HAS_HDRP
                detectedHdrp = (light.GetComponent<HDAdditionalLightData>() != null);
#endif

                bool wantHdrp = pipelineMode switch
                {
                    PipelineMode.ForceHDRP => true,
                    PipelineMode.ForceGeneric => false,
                    _ => detectedHdrp
                };

                ILightDriver driver = null;

                // 1) override (first light only)
                if (overrideDriver != null && !usedDrivers.Contains(overrideDriver))
                {
                    driver = overrideDriver;
                }
                else if (overrideDriver != null && !warnedOverrideReuse)
                {
                    Debug.LogWarning("[DmxFixtureComponent] driverOverride is set but multiple targetLights were found. " +
                                     "driverOverride will be used for the first light only.", this);
                    warnedOverrideReuse = true;
                }

                // 2) resolve from SAME GameObject (unused components only)
                if (driver == null)
                {
#if HAS_HDRP
                    if (wantHdrp)
                    {
                        for (int i = 0; i < hdrpOnSelf.Length; i++)
                        {
                            if (!usedDrivers.Contains(hdrpOnSelf[i]))
                            {
                                driver = hdrpOnSelf[i];
                                break;
                            }
                        }
                    }
#endif
                    if (driver == null)
                    {
                        for (int i = 0; i < genericOnSelf.Length; i++)
                        {
                            if (!usedDrivers.Contains(genericOnSelf[i]))
                            {
                                driver = genericOnSelf[i];
                                break;
                            }
                        }
                    }
                }

                // 2.5) legacy fallback: targetLight???????????????????????
                if (driver == null)
                {
#if HAS_HDRP
                    var hdrpOnLight = light.GetComponent<HdrpLightDriver>();
                    if (hdrpOnLight != null && !usedDrivers.Contains(hdrpOnLight))
                    {
                        driver = hdrpOnLight;
                        Debug.LogWarning("[DmxFixtureComponent] Found LightDriver on targetLight GameObject (legacy). " +
                                         "Now we recommend attaching LightDriver to the same GameObject as DmxFixtureComponent.", this);
                    }
#endif
                    if (driver == null)
                    {
                        var genericOnLight = light.GetComponent<GenericLightDriver>();
                        if (genericOnLight != null && !usedDrivers.Contains(genericOnLight))
                        {
                            driver = genericOnLight;
                            Debug.LogWarning("[DmxFixtureComponent] Found LightDriver on targetLight GameObject (legacy). " +
                                             "Now we recommend attaching LightDriver to the same GameObject as DmxFixtureComponent.", this);
                        }
                    }
                }

                // 3) auto add to SAME GameObject
                if (driver == null && autoAddDriverIfMissing)
                {
#if HAS_HDRP
                    if (wantHdrp)
                        driver = gameObject.AddComponent<HdrpLightDriver>();
                    else
                        driver = gameObject.AddComponent<GenericLightDriver>();
#else
                    if (wantHdrp)
                        Debug.LogWarning($"[DmxFixtureComponent] HDRP????? HAS_HDRP ?????? GenericLightDriver ??????: {name}", this);

                    driver = gameObject.AddComponent<GenericLightDriver>();
#endif
                }

                if (driver == null) continue;

                usedDrivers.Add(driver);
                driver.Initialize(light);

                if (overrideDriverMaxIntensity)
                {
                    if (driver is GenericLightDriver gd)
                        gd.maxIntensity = genericMaxIntensity;

#if HAS_HDRP
                    if (driver is HdrpLightDriver hd)
                        hd.maxIntensity = hdrpMaxIntensity;
#endif
                }

                runtimeDrivers.Add(driver);
            }

            runtimeDriver = runtimeDrivers.Count > 0 ? runtimeDrivers[0] : null;
            isInitialized = runtimeDrivers.Count > 0;
        }

        private List<Light> GatherTargetLights()
        {
            var result = new List<Light>();

            if (targetLights != null)
            {
                for (int i = 0; i < targetLights.Count; i++)
                {
                    var l = targetLights[i];
                    if (l == null) continue;
                    if (!result.Contains(l)) result.Add(l);
                }
            }

            if (targetLight != null && !result.Contains(targetLight))
                result.Add(targetLight);

            return result;
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

                // White縺悟牡繧雁ｽ薙※繧峨ｌ縺ｦ縺・ｋ蝣ｴ蜷医・縲檎區譁ｹ蜷代↓蟇・○繧九咲ｰ｡譏薙Δ繝・Ν
                if (TryGetRelativeChannel(FixtureFunction.White, out int wRel))
                {
                    float w = DmxValueUtils.ByteTo01(Read8Abs(universe512, startAddress + wRel - 1));
                    rgb = rgb + Color.white * w;
                }
            }

            UpdateLightTargetsFromDmx(dim01, rgb);

            // --- Pan/Tilt Speed・医≠繧後・繧ｹ繝繝ｼ繧ｸ繝ｳ繧ｰ驕ｩ逕ｨ・・---
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

                SetPanTarget(panDeg, hasSpeed ? maxDegPerSec : -1f, panTiltSmoothing);
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

                SetTiltTarget(tiltDeg, hasSpeed ? maxDegPerSec : -1f, panTiltSmoothing);
}

            // --- Monitoring update (per-fixture realtime values) ---
            UpdateMonitorValues(universe512);
            ApplyImmediateInEditMode();
        }

        private void ApplyFromUniverseBufferInternal(byte[] universe512)
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

                // White縺悟牡繧雁ｽ薙※繧峨ｌ縺ｦ縺・ｋ蝣ｴ蜷医・縲檎區譁ｹ蜷代↓蟇・○繧九咲ｰ｡譏薙Δ繝・Ν
                if (TryGetRelativeChannel(FixtureFunction.White, out int wRel))
                {
                    float w = DmxValueUtils.ByteTo01(Read8Abs(universe512, startAddress + wRel - 1));
                    rgb = rgb + Color.white * w;
                }
            }

            UpdateLightTargetsFromDmx(dim01, rgb);

            // --- Pan/Tilt Speed・医≠繧後・繧ｹ繝繝ｼ繧ｸ繝ｳ繧ｰ驕ｩ逕ｨ・・---
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

                SetPanTarget(panDeg, hasSpeed ? maxDegPerSec : -1f, panTiltSmoothing);
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

                SetTiltTarget(tiltDeg, hasSpeed ? maxDegPerSec : -1f, panTiltSmoothing);
}

            // --- Monitoring update (per-fixture realtime values) ---
            UpdateMonitorValues(universe512);
            ApplyImmediateInEditMode();
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

        private void UpdateMonitorValues(byte[] universe512)
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

                    // function縺・(譛ｪ險ｭ螳・縺ｪ繧・relXX 縺ｨ縺励※陦ｨ遉ｺ
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

        public void ResetPanTiltToBase()
        {
            CaptureMovementBaseIfNeeded(force: false);
            if (panTransform != null) panTransform.localRotation = _panBaseLocalRot;
            if (tiltTransform != null) tiltTransform.localRotation = _tiltBaseLocalRot;
            _hasPanTarget = false;
            _hasTiltTarget = false;
        }
        public void RecapturePanTiltBase()
        {
            CaptureMovementBaseIfNeeded(force: true);
        }

#if UNITY_EDITOR
        public void CapturePreviewLightBase()
        {
            if (_previewLightCaptured) return;
            _previewLightTargets.Clear();
            _previewLightIntensity.Clear();
            _previewLightColor.Clear();

            var lights = GatherTargetLights();
            for (int i = 0; i < lights.Count; i++)
            {
                var l = lights[i];
                if (l == null) continue;
                _previewLightTargets.Add(l);
                _previewLightIntensity.Add(l.intensity);
                _previewLightColor.Add(l.color);
            }

            _previewLightCaptured = true;
        }

        public void RestorePreviewLightBase()
        {
            if (!_previewLightCaptured) return;

            for (int i = 0; i < _previewLightTargets.Count; i++)
            {
                var l = _previewLightTargets[i];
                if (l == null) continue;
                if (i < _previewLightIntensity.Count) l.intensity = _previewLightIntensity[i];
                if (i < _previewLightColor.Count) l.color = _previewLightColor[i];
            }

            _previewLightTargets.Clear();
            _previewLightIntensity.Clear();
            _previewLightColor.Clear();
            _previewLightCaptured = false;
        }

        [ContextMenu("Restore Pan/Tilt From Prefab")]
        public void RestorePanTiltFromPrefab()
        {
            if (panTransform != null)
            {
                var srcPan = PrefabUtility.GetCorrespondingObjectFromSource(panTransform) as Transform;
                if (srcPan != null) panTransform.localRotation = srcPan.localRotation;
            }
            if (tiltTransform != null)
            {
                var srcTilt = PrefabUtility.GetCorrespondingObjectFromSource(tiltTransform) as Transform;
                if (srcTilt != null) tiltTransform.localRotation = srcTilt.localRotation;
            }
            CaptureMovementBaseIfNeeded(force: true);
        }

        [ContextMenu("Restore Light From Prefab")]
        public void RestoreLightFromPrefab()
        {
            var lights = GatherTargetLights();
            for (int i = 0; i < lights.Count; i++)
            {
                var l = lights[i];
                if (l == null) continue;
                var src = PrefabUtility.GetCorrespondingObjectFromSource(l) as Light;
                if (src == null) continue;
                l.color = src.color;
                l.intensity = src.intensity;
            }
        }
#endif

        private void PerformReset()
        {
            if (panTransform != null) panTransform.localRotation = _panBaseLocalRot;
            if (tiltTransform != null) tiltTransform.localRotation = _tiltBaseLocalRot;

            _hasDmxTargets = true;
            _targetDimmer01 = 0f;
            _targetColor = Color.white;
            _lensTargetDimmer01 = 0f;
            _lightTargetDimmer01 = 0f;
            _currentLensDimmer01 = 0f;
            _currentLightDimmer01 = 0f;
            _lightTargetPending = false;
            _lensTargetPending = false;

            ApplyLightAndLens(0f, 0f, Color.white);
        }

        private void UpdateLightTargetsFromDmx(float dim01, Color rgb)
        {
            _hasDmxTargets = true;
            _targetColor = rgb;

            float prevTarget = _targetDimmer01;
            _targetDimmer01 = Mathf.Clamp01(dim01);

            if (lightResponseMode == LightResponseMode.Led)
            {
                _lensTargetDimmer01 = _targetDimmer01;
                _lightTargetDimmer01 = _targetDimmer01;
                _lightTargetPending = false;
                _lensTargetPending = false;
                _currentLensDimmer01 = _lensTargetDimmer01;
                _currentLightDimmer01 = _lightTargetDimmer01;
                ApplyLightAndLens(_currentLightDimmer01, _currentLensDimmer01, _targetColor);
                return;
            }

            bool isRising = _targetDimmer01 > prevTarget + 0.0001f;
            bool isFalling = _targetDimmer01 < prevTarget - 0.0001f;

            if (isRising)
            {
                _lensTargetDimmer01 = _targetDimmer01;

                if (halogenBeamOnDelay <= 0f)
                {
                    _lightTargetDimmer01 = _targetDimmer01;
                    _lightTargetPending = false;
                }
                else
                {
                    _pendingLightTargetDimmer01 = _targetDimmer01;
                    _lightTargetPendingAt = Time.time + halogenBeamOnDelay;
                    _lightTargetPending = true;
                }

                _lensTargetPending = false;
                return;
            }

            if (isFalling)
            {
                _lightTargetDimmer01 = _targetDimmer01;
                _lightTargetPending = false;

                if (halogenBeamOffDelay <= 0f)
                {
                    _lensTargetDimmer01 = _targetDimmer01;
                    _lensTargetPending = false;
                }
                else
                {
                    _pendingLensTargetDimmer01 = _targetDimmer01;
                    _lensTargetPendingAt = Time.time + halogenBeamOffDelay;
                    _lensTargetPending = true;
                }
                return;
            }

            _lensTargetDimmer01 = _targetDimmer01;
            _lightTargetDimmer01 = _targetDimmer01;
            _lightTargetPending = false;
            _lensTargetPending = false;
        }

        private void UpdateLightResponse()
        {
            if (!_hasDmxTargets) return;

            if (lightResponseMode == LightResponseMode.Led)
            {
                _currentLensDimmer01 = _lensTargetDimmer01;
                _currentLightDimmer01 = _lightTargetDimmer01;
            }
            else
            {
                if (_lightTargetPending && Time.time >= _lightTargetPendingAt)
                {
                    _lightTargetDimmer01 = _pendingLightTargetDimmer01;
                    _lightTargetPending = false;
                }

                if (_lensTargetPending && Time.time >= _lensTargetPendingAt)
                {
                    _lensTargetDimmer01 = _pendingLensTargetDimmer01;
                    _lensTargetPending = false;
                }

                _currentLensDimmer01 = MoveDimmer(_currentLensDimmer01, _lensTargetDimmer01, halogenSourceRiseTime, halogenSourceFallTime);
                _currentLightDimmer01 = MoveDimmer(_currentLightDimmer01, _lightTargetDimmer01, halogenBeamRiseTime, halogenBeamFallTime);
            }

            ApplyLightAndLens(_currentLightDimmer01, _currentLensDimmer01, _targetColor);
        }

        private void ApplyImmediateInEditMode()
        {
#if UNITY_EDITOR
            if (Application.isPlaying) return;
            if (!applyImmediateInEditMode) return;

            if (lightResponseMode == LightResponseMode.Halogen)
            {
                _lensTargetDimmer01 = _targetDimmer01;
                _lightTargetDimmer01 = _targetDimmer01;
                _lightTargetPending = false;
                _lensTargetPending = false;
            }

            _currentLensDimmer01 = _lensTargetDimmer01;
            _currentLightDimmer01 = _lightTargetDimmer01;
            ApplyLightAndLens(_currentLightDimmer01, _currentLensDimmer01, _targetColor);

            if (panTransform != null && _hasPanTarget)
                panTransform.localRotation = _panTargetLocalRot;
            if (tiltTransform != null && _hasTiltTarget)
                tiltTransform.localRotation = _tiltTargetLocalRot;
#endif
        }

        private void ApplyLightAndLens(float lightDim01, float lensDim01, Color rgb)
        {
            if (isInitialized && runtimeDrivers != null && runtimeDrivers.Count > 0)
            {
                for (int i = 0; i < runtimeDrivers.Count; i++)
                    runtimeDrivers[i]?.Apply(lightDim01, rgb);
            }
            else
            {
                ApplyDirectToLights(rgb, lightDim01);
            }

            ApplyLensDmx(rgb, lensDim01);
        }

        private static float MoveDimmer(float current, float target, float riseTime, float fallTime)
        {
            if (Mathf.Approximately(current, target)) return target;

            float dt = Mathf.Max(0.0001f, Time.deltaTime);
            float time = (target > current) ? riseTime : fallTime;
            if (time <= 0f) return target;

            float step = dt / time;
            return Mathf.MoveTowards(current, target, step);
        }

        private void ApplyDirectToLights(Color rgb, float dim01)
        {
            var targets = GatherTargetLights();
            for (int i = 0; i < targets.Count; i++)
            {
                var l = targets[i];
                if (l == null) continue;
                l.color = rgb;
                l.intensity = dim01 * 10f;
            }
        }

// ------------------------------------------------------------
        // Lens DMX sync helpers
        // ------------------------------------------------------------

                private void EnsureLensPropertyIds()
        {
            if (_lensPropertyIdsReady && _lensMpb != null) return;

            // propertyが空だと PropertyToID で0になるので、最低限ガード
            if (string.IsNullOrWhiteSpace(lensColorProperty)) lensColorProperty = "_DmxColor";
            if (string.IsNullOrWhiteSpace(lensDimmerProperty)) lensDimmerProperty = "_DmxDimmer";

            _lensColorId = Shader.PropertyToID(lensColorProperty);
            _lensDimmerId = Shader.PropertyToID(lensDimmerProperty);
            if (_lensMpb == null) _lensMpb = new MaterialPropertyBlock();
            _lensPropertyIdsReady = true;
        }

        private void ApplyLensDmx(Color rgb, float dim01)
        {
            if (!syncLensToDmx) return;
            if (!syncLensColorToDmx && !syncLensDimmerToDmx) return;

            // Renderer譛ｪ險ｭ螳壹〒繧ょｮ牙・縺ｫ繧ｹ繝ｫ繝ｼ
            if (lensRenderer == null && (extraLensRenderers == null || extraLensRenderers.Length == 0))
                return;

            EnsureLensPropertyIds();

            if (_lensMpb == null) return;

            float d = Mathf.Clamp01(dim01) * Mathf.Max(0f, lensDimmerScale);

            // 縺ｾ縺壹・蜈ｱ騾壹・MPB縺ｫ蛟､繧偵そ繝・ヨ・・enderer縺斐→縺ｫSetPropertyBlock縺吶ｋ・・
            _lensMpb.Clear();
            if (syncLensColorToDmx)
                _lensMpb.SetColor(_lensColorId, rgb);
            if (syncLensDimmerToDmx)
                _lensMpb.SetFloat(_lensDimmerId, d);

            if (lensRenderer != null)
                lensRenderer.SetPropertyBlock(_lensMpb);

            if (extraLensRenderers != null)
            {
                for (int i = 0; i < extraLensRenderers.Length; i++)
                {
                    var r = extraLensRenderers[i];
                    if (r != null) r.SetPropertyBlock(_lensMpb);
                }
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

        private static int Read8Abs(byte[] universe512, int absCh1Based)
        {
            int idx = absCh1Based - 1;
            if (idx < 0 || idx >= universe512.Length) return 0;
            return universe512[idx];
        }


        private static int Read16Abs(int[] universe512, int absCoarseCh1Based, int absFineCh1BasedOrMinus1)
        {
            int coarse = Read8Abs(universe512, absCoarseCh1Based);
            if (absFineCh1BasedOrMinus1 <= 0) return coarse << 8;
            int fine = Read8Abs(universe512, absFineCh1BasedOrMinus1);
            return (coarse << 8) | fine;
        }

        private static int Read16Abs(byte[] universe512, int absCoarseCh1Based, int absFineCh1BasedOrMinus1)
        {
            int coarse = Read8Abs(universe512, absCoarseCh1Based);
            if (absFineCh1BasedOrMinus1 <= 0) return coarse << 8;
            int fine = Read8Abs(universe512, absFineCh1BasedOrMinus1);
            return (coarse << 8) | fine;
        }


        
        // ------------------------------------------------------------
        // Pan/Tilt continuous interpolation (target set on DMX update, applied every frame)
        // ------------------------------------------------------------

        private void SetPanTarget(float panDeg, float maxDegPerSec, float smoothing01)
        {
            _panTargetLocalRot = _panBaseLocalRot * Quaternion.AngleAxis(panDeg, AxisToVector(panAxis));
            _hasPanTarget = true;
            _panTargetMaxDegPerSec = maxDegPerSec;
            _panTargetSmoothing = smoothing01;
        }

        private void SetTiltTarget(float tiltDeg, float maxDegPerSec, float smoothing01)
        {
            _tiltTargetLocalRot = _tiltBaseLocalRot * Quaternion.AngleAxis(tiltDeg, AxisToVector(tiltAxis));
            _hasTiltTarget = true;
            _tiltTargetMaxDegPerSec = maxDegPerSec;
            _tiltTargetSmoothing = smoothing01;
        }

        private void UpdatePanTiltMotion()
        {
            if (!enableContinuousPanTiltUpdate) return;

            // DMX縺後∪縺譚･縺ｦ縺・↑縺・→縺阪・菴輔ｂ縺励↑縺・
            if (panTransform != null && _hasPanTarget)
            {
                ApplyRotationToTarget(panTransform, _panTargetLocalRot, _panTargetMaxDegPerSec, _panTargetSmoothing);
            }

            if (tiltTransform != null && _hasTiltTarget)
            {
                ApplyRotationToTarget(tiltTransform, _tiltTargetLocalRot, _tiltTargetMaxDegPerSec, _tiltTargetSmoothing);
            }
        }

        private static void ApplyRotationToTarget(Transform t, Quaternion targetLocalRot, float maxDegPerSec, float smoothing01)
        {
            if (t == null) return;

            if (maxDegPerSec > 0f)
            {
                float dt = Mathf.Max(0.0001f, Time.deltaTime);
                float step = maxDegPerSec * dt;
                t.localRotation = Quaternion.RotateTowards(t.localRotation, targetLocalRot, step);
                return;
            }

            if (smoothing01 <= 0f)
            {
                t.localRotation = targetLocalRot;
                return;
            }

            float dt2 = Mathf.Max(0.0001f, Time.deltaTime);
            // smoothing01 is treated as 0..1
            float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(smoothing01), dt2 * 60f);
            t.localRotation = Quaternion.Slerp(t.localRotation, targetLocalRot, k);
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

            // Speed蜆ｪ蜈茨ｼ・anTiltSpeed縺後≠繧句ｴ蜷茨ｼ・
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
