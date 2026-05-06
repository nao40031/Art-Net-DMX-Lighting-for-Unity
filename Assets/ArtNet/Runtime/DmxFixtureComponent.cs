/*!
 * Copyright (c) 2026 Oshino
 *
 * Released under the MIT license.
 * see https://opensource.org/licenses/MIT
 */

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
        [Tooltip("DMX Universe番号。送信側と合わせてください（0/1始まりはRigController設定に合わせる）。")]
        public int universe = 0;

        [Tooltip("DMX開始アドレス（1-512）。")]
        [Range(1, 512)]
        public int startAddress = 1;

        [Header("Profile (Fixture/Mode)")]
        public FixtureDefinition fixture;

        // RigControllerが参照するため public。Inspector上はMode名表示でも内部値はindex。
        [SerializeField] public int mode = 0;

        // ------------------------------------------------------------
        // Targets
        // ------------------------------------------------------------

        [Header("Targets")]
        public Light targetLight;
        [Tooltip("複数ライトを対象にする場合に設定。ここに登録した全Lightへ同じDMXを適用します。")]
        public List<Light> targetLights = new();
        public Transform panTransform;
        public Transform tiltTransform;


        // ------------------------------------------------------------
        // Lens (ShaderGraph DMX Sync)
        // ------------------------------------------------------------

        [Header("Lens (ShaderGraph DMX Sync)")]
        [Tooltip("レンズ面のRenderer。ShaderGraph側に _DmxColor(Color) / _DmxDimmer(Float) がある前提です。")]
        [SerializeField] private Renderer lensRenderer;

        [Tooltip("レンズが複数Rendererに分かれる場合の追加登録（任意）。")]
        [SerializeField] private Renderer[] extraLensRenderers;

        [Tooltip("レンズへDMX同期を行います。")]
        [SerializeField] private bool syncLensToDmx = true;
        [SerializeField] private bool syncLensColorToDmx = true;
        [SerializeField] private bool syncLensDimmerToDmx = true;

        [Tooltip("ShaderGraphのColorプロパティ名（例: _DmxColor）。")]
        [SerializeField] private string lensColorProperty = "_DmxColor";

        [Tooltip("ShaderGraphのFloatプロパティ名（例: _DmxDimmer）。")]
        [SerializeField] private string lensDimmerProperty = "_DmxDimmer";

        [Tooltip("Dimmer(0-1)に掛ける倍率。レンズの明るさ調整用。")]
        [SerializeField, Min(0f)] private float lensDimmerScale = 1.0f;

        // ------------------------------------------------------------
        // Gobo
        // ------------------------------------------------------------

        [Header("Gobo")]
        [Tooltip("DMX値とゴボテクスチャを対応付ける定義。")]
        [SerializeField] private GoboWheelDefinition goboWheel;

        [Tooltip("ライトのcookieへゴボを同期します。")]
        [SerializeField] private bool syncLightCookieToGobo = true;

        [Tooltip("レンズ表現へゴボを同期します。")]
        [SerializeField] private bool syncLensGoboToDmx = true;

        [Tooltip("ビーム表現へゴボを同期します。")]
        [SerializeField] private bool syncBeamGoboToDmx = true;

        [Tooltip("GoboRotation=255 のときの角速度（deg/sec）。")]
        [SerializeField, Min(0f)] private float maxGoboRotateDegPerSec = 360f;

        [Tooltip("ゴボ回転をLight Cookie用Transformのロール回転へ同期します。")]
        [SerializeField] private bool syncGoboRotationToCookieTransform = true;

        [Tooltip("Light Cookieを回転させるTransform。通常はSpot Light本体のTransformを指定します。")]
        [SerializeField] private Transform goboCookieRollTransform;

        [Tooltip("Cookie Transformを回転させるローカル軸。通常はZ軸です。")]
        [SerializeField] private Vector3 goboCookieRollAxis = Vector3.forward;

        [Tooltip("Cookie Transformへ加算する固定ロール角度補正（deg）。")]
        [SerializeField] private float goboCookieRollOffsetDeg = 0f;

        [Tooltip("レンズ用ゴボTextureプロパティ名。")]
        [SerializeField] private string lensGoboTextureProperty = "_GoboTexture";

        [Tooltip("レンズ用ゴボ回転プロパティ名。")]
        [SerializeField] private string lensGoboRotationProperty = "_GoboRotationDeg";

        [Tooltip("レンズ用ゴボ有効プロパティ名。")]
        [SerializeField] private string lensGoboEnabledProperty = "_GoboEnabled";

        [Tooltip("ビーム用ゴボTextureプロパティ名。")]
        [SerializeField] private string beamGoboTextureProperty = "_GoboTexture";

        [Tooltip("ビーム用ゴボ回転プロパティ名。")]
        [SerializeField] private string beamGoboRotationProperty = "_GoboRotationDeg";

        [Tooltip("ビーム用ゴボ有効プロパティ名。")]
        [SerializeField] private string beamGoboEnabledProperty = "_GoboEnabled";

        // ------------------------------------------------------------
        // Pseudo Beam (for Volumetrics OFF)
        // ------------------------------------------------------------

        [Header("Pseudo Beam (Volumetrics OFF Alternative)")]
        [Tooltip("有効時、ビーム用RendererへDMX同期を行います（擬似ビーム）。")]
        [SerializeField] private bool syncPseudoBeamToDmx = false;

        [Tooltip("ビームメッシュの代表Renderer。")]
        [SerializeField] private Renderer beamRenderer;

        [Tooltip("ビームメッシュが複数ある場合の追加Renderer。")]
        [SerializeField] private Renderer[] extraBeamRenderers;

        [SerializeField] private bool syncBeamColorToDmx = true;
        [SerializeField] private bool syncBeamDimmerToDmx = true;

        [Tooltip("ビームマテリアルのColorプロパティ名。例: _DmxColor / _BaseColor")]
        [SerializeField] private string beamColorProperty = "_DmxColor";

        [Tooltip("ビームマテリアルの強度プロパティ名。例: _DmxDimmer / _BeamIntensity")]
        [SerializeField] private string beamDimmerProperty = "_DmxDimmer";

        [Tooltip("ビーム強度に掛ける倍率。")]
        [SerializeField, Min(0f)] private float beamDimmerScale = 1.0f;

        [Tooltip("ビーム強度の下限値。暗転時の残光調整用。")]
        [SerializeField, Range(0f, 1f)] private float beamDimmerFloor = 0f;

        // ------------------------------------------------------------
        // Light Response (LED / Halogen)
        // ------------------------------------------------------------

        public enum LightResponseMode { Led, Halogen }

        [Header("Light Response")]
        public LightResponseMode lightResponseMode = LightResponseMode.Led;

        [Header("Halogen Response (seconds)")]
        [Tooltip("ソース(レンズ)の立ち上がり時間（0→1秒）。")]
        [SerializeField, Min(0f)] private float halogenSourceRiseTime = 0.06f;

        [Tooltip("ソース(レンズ)の立ち下がり時間（1→0秒）。")]
        [SerializeField, Min(0f)] private float halogenSourceFallTime = 0.10f;

        [Tooltip("ソースON後、ビーム(ライト)が点灯するまでの遅延。")]
        [SerializeField, Min(0f)] private float halogenBeamOnDelay = 0.04f;

        [Tooltip("ソースOFF後、ビーム(ライト)が消灯するまでの遅延。")]
        [SerializeField, Min(0f)] private float halogenBeamOffDelay = 0.04f;

        [Tooltip("ビーム(ライト)の立ち上がり時間（0→1秒）。")]
        [SerializeField, Min(0f)] private float halogenBeamRiseTime = 0.06f;

        [Tooltip("ビーム(ライト)の立ち下がり時間（1→0秒）。")]
        [SerializeField, Min(0f)] private float halogenBeamFallTime = 0.08f;

        // MPBで per-renderer 上書きし、マテリアルインスタンス増加を避ける
        private MaterialPropertyBlock _lensMpb;
        private int _lensColorId;
        private int _lensDimmerId;
        private int _lensGoboTextureId;
        private int _lensGoboRotationId;
        private int _lensGoboEnabledId;
        private bool _lensPropertyIdsReady;

        private MaterialPropertyBlock _beamMpb;
        private int _beamColorId;
        private int _beamDimmerId;
        private int _beamGoboTextureId;
        private int _beamGoboRotationId;
        private int _beamGoboEnabledId;
        private bool _beamPropertyIdsReady;
        [Header("Pan/Tilt Range (degrees)")]
        public float panRangeDeg = 540f;
        public float tiltRangeDeg = 270f;

        // ------------------------------------------------------------
        // Pipeline / Driver (Built-in / URP / HDRP)
        // ------------------------------------------------------------

        public enum PipelineMode { Auto, ForceGeneric, ForceHDRP }

        [Header("Pipeline / Driver")]
        public PipelineMode pipelineMode = PipelineMode.Auto;

        [Tooltip("明示的に使用するドライバ（GenericLightDriver / HdrpLightDriver など）。")]
        public MonoBehaviour driverOverride;

        [Tooltip("ドライバが無い場合に自動追加します（同一GameObject）。")]
        public bool autoAddDriverIfMissing = true;

        [Header("Driver Intensity Defaults")]
        [Tooltip("GenericLightDriver.maxIntensity の既定値。")]
        public float genericMaxIntensity = 10f;

        [Tooltip("HdrpLightDriver.maxIntensity の既定値。")]
        public float hdrpMaxIntensity = 9870f;

        [Tooltip("trueの場合、Initialize時に上記maxIntensityをドライバへ上書きします。")]
        public bool overrideDriverMaxIntensity = true;

        // ------------------------------------------------------------
        // Pan/Tilt Axis / Tuning
        // ------------------------------------------------------------

        public enum LocalAxis { X, Y, Z, MinusX, MinusY, MinusZ }

        [Header("Pan/Tilt Axis / Tuning")]
        [Tooltip("Panのローカル回転軸。")]
        public LocalAxis panAxis = LocalAxis.Z; // default: Z axis

        [Tooltip("Tiltのローカル回転軸。")]
        public LocalAxis tiltAxis = LocalAxis.X;

        public bool panInvert = false;
        public bool tiltInvert = false;

        [Tooltip("Panの角度オフセット（度）。")]
        public float panOffsetDeg = 0f;

        [Tooltip("Tiltの角度オフセット（度）。")]
        public float tiltOffsetDeg = 0f;

        [Range(0f, 30f)]
        [Tooltip("Pan/Tiltの追従スムージング。0=即時、値を上げるほど滑らか。")]
        public float panTiltSmoothing = 12f;


        [Tooltip("DMX更新(例:40Hz)と描画更新の差を毎フレーム補間で滑らかにします。")]
        public bool enableContinuousPanTiltUpdate = true;

        [Header("Edit Mode Preview")]
        [Tooltip("EditモードのTimelineプレビュー時にPan/Tiltと光量を即時反映します。")]
        public bool applyImmediateInEditMode = true;

        [Header("Pan/Tilt Speed (deg/sec)  ※PanTiltSpeedがある機種のみ有効")]
        [Tooltip("PanTiltSpeed=0 のときの角速度（deg/sec）。")]
        public float panTiltSpeedMinDegPerSec = 30f;

        [Tooltip("PanTiltSpeed=255 のときの角速度（deg/sec）。")]
        public float panTiltSpeedMaxDegPerSec = 720f;

        [Header("Reset Threshold")]
        [Tooltip("Reset ch がこの値以上のとき Reset を実行します（機種に合わせて調整）。")]
        [Range(0, 255)]
        public int resetTriggerThreshold = 250;

        [Header("Auto Resolve")]
        [SerializeField] private bool autoResolveOnValidate = true;

        // ------------------------------------------------------------
        // Monitoring (migrated from DmxDebugMonitor, no editor extension)
        // ------------------------------------------------------------

        [Header("Monitoring (DMX)")]
        [Tooltip("このFixtureのDMXモニタを有効化します（Inspector表示/周期ログ）。")]
        [SerializeField] private bool monitorEnabled = true;

        [Header("Monitor Channels (Absolute 1-512)")]
        [Tooltip("Universe内の絶対チャンネル（1-512）を監視します。")]
        [Range(1, 512)] public int monitorCh1 = 1;
        [Range(1, 512)] public int monitorCh2 = 2;
        [Range(1, 512)] public int monitorCh3 = 3;
        [Range(1, 512)] public int monitorCh4 = 4;
        [Range(1, 512)] public int monitorCh5 = 5;

        [Header("Monitoring (Auto / Relative)")]
        [Tooltip("FixtureDefinitionのModeで解決されたFixtureFunctionを自動で一覧表示します。")]
        [SerializeField] private bool monitorIncludeResolvedFunctions = true;

        [Tooltip("startAddress基準の相対ch（1=Start）を追加監視します（例: 1,2,3,10）。")]
        [SerializeField] private List<int> monitorExtraRelativeChannels = new();

        [Header("Debug Values (ReadOnly)")]
        [SerializeField, Range(0, 255)] private int ch1;
        [SerializeField, Range(0, 255)] private int ch2;
        [SerializeField, Range(0, 255)] private int ch3;
        [SerializeField, Range(0, 255)] private int ch4;
        [SerializeField, Range(0, 255)] private int ch5;

        [Serializable]
        public struct DmxMonitorItem
        {
            public bool useElementSchema;
            public FixtureFunction function; // 0 の場合は function未指定。UIでは relXX 表示
            public string label;
            public FixtureAttribute attribute;
            public int instance;
            public FixtureChannelRole role;
            public FixtureByteRole byteRole;
            public int relativeCh;           // fixture内の相対ch（1=startAddress）
            public int absoluteCh;           // 1-based within universe
            [Range(0, 255)] public int value;
        }

        [Header("Debug Values (Resolved Function -> Value)")]
        [SerializeField] private List<DmxMonitorItem> monitorItems = new();

        [Header("Periodic Logging (Flood Protection)")]
        [Tooltip("一定間隔で受信データを要約ログ出力します（ログ洪水対策あり）。")]
        [SerializeField] private bool enablePeriodicLog = false;

        [Tooltip("ログ出力間隔（秒）。例: 1.0で1秒ごと。")]
        [SerializeField, Min(0.1f)] private float logIntervalSec = 1.0f;

        [Tooltip("受信が無い間はログを出さない（無駄ログ抑制）。")]
        [SerializeField] private bool logOnlyWhenDataArrived = true;

        [Tooltip("ログ先頭にヘッダー（Universe/Start/Modeなど）を含めます。")]
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
        private bool _goboEnabled;
        private Texture _goboTexture;
        private float _goboRotationDeg;
        private float _goboRotationOffsetDeg;
        private float _goboRotationSpeedDegPerSec;
        private Transform _goboCookieRollBaseTransform;
        private Quaternion _goboCookieRollBaseLocalRot;
        private bool _hasGoboCookieRollBaseLocalRot;


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
        private readonly Dictionary<ElementKey, ElementBinding> _elementMap = new();
        private readonly Dictionary<WheelKey, GoboWheelDefinition> _goboWheelMap = new();
        private bool _usesElementMode;
        [SerializeField] private string _activeProfileLabel;
        [SerializeField] private string _activeModeLabel;

        private struct ElementKey : IEquatable<ElementKey>
        {
            public FixtureAttribute attribute;
            public int instance;
            public FixtureChannelRole role;

            public ElementKey(FixtureAttribute attribute, int instance, FixtureChannelRole role)
            {
                this.attribute = attribute;
                this.instance = Mathf.Max(1, instance);
                this.role = role;
            }

            public bool Equals(ElementKey other)
                => attribute == other.attribute && instance == other.instance && role == other.role;

            public override bool Equals(object obj)
                => obj is ElementKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = (int)attribute;
                    hash = (hash * 397) ^ instance;
                    hash = (hash * 397) ^ (int)role;
                    return hash;
                }
            }
        }

        private struct WheelKey : IEquatable<WheelKey>
        {
            public FixtureAttribute attribute;
            public int instance;

            public WheelKey(FixtureAttribute attribute, int instance)
            {
                this.attribute = attribute;
                this.instance = Mathf.Max(1, instance);
            }

            public bool Equals(WheelKey other)
                => attribute == other.attribute && instance == other.instance;

            public override bool Equals(object obj)
                => obj is WheelKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((int)attribute * 397) ^ instance;
                }
            }
        }

        private struct ElementBinding
        {
            public int singleRel;
            public int coarseRel;
            public int fineRel;
            public FixtureChannelElement singleElement;
            public FixtureChannelElement coarseElement;

            public FixtureChannelElement PrimaryElement => singleElement ?? coarseElement;
        }

        [Serializable]
        public struct ResolvedItem
        {
            public FixtureFunction function;
            public bool useElementSchema;
            public string label;
            public FixtureAttribute attribute;
            public int instance;
            public FixtureChannelRole role;
            public FixtureByteRole byteRole;
            public int relativeChannel; // 1-based within fixture
            public int absoluteChannel; // 1-based within universe
        }

        [SerializeField] private List<ResolvedItem> _resolvedItems = new();

        // ------------------------------------------------------------
        // RigController integration
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

        [ContextMenu("Auto Attach Beam Renderers (Name contains Beam)")]
        public void Context_AutoAttachBeamRenderers()
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            var found = new List<Renderer>();

            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                if (r == lensRenderer) continue;

                var n = r.gameObject.name;
                if (n.IndexOf("beam", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("volum", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    found.Add(r);
                }
            }

            if (found.Count == 0)
                return;

            beamRenderer = found[0];
            if (found.Count > 1)
            {
                extraBeamRenderers = new Renderer[found.Count - 1];
                for (int i = 1; i < found.Count; i++)
                    extraBeamRenderers[i - 1] = found[i];
            }
            else
            {
                extraBeamRenderers = Array.Empty<Renderer>();
            }
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
            UpdateGoboMotion();
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
            _elementMap.Clear();
            _goboWheelMap.Clear();
            _resolvedItems.Clear();
            _usesElementMode = false;

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

            if (md.UsesChannelElements())
            {
                BuildElementMapping(md);
                if (_elementMap.Count > 0)
                {
                    _usesElementMode = true;
                    return;
                }
            }

            if (md.channels == null) return;

            foreach (var fc in md.channels)
            {
                if (fc == null) continue;

                int rel = Mathf.Clamp(fc.channel, 1, 512);
                _relativeMap[fc.function] = rel;

                int abs = startAddress + rel - 1;
                _resolvedItems.Add(new ResolvedItem
                {
                    useElementSchema = false,
                    function = fc.function,
                    label = fc.function.ToString(),
                    relativeChannel = rel,
                    absoluteChannel = abs
                });
            }
        }

        private void BuildElementMapping(FixtureModeDefinition md)
        {
            int count = Mathf.Clamp(md.channelCount, 1, 512);

            for (int i = 0; i < count; i++)
            {
                var element = (md.elements != null && i < md.elements.Count) ? md.elements[i] : null;
                int rel = i + 1;
                int instance = element != null ? Mathf.Max(1, element.instance) : 1;
                var attribute = element != null ? element.attribute : FixtureAttribute.NoFeature;
                var role = element != null ? element.role : FixtureChannelRole.Value;
                var byteRole = element != null ? element.byteRole : FixtureByteRole.Single;

                _resolvedItems.Add(new ResolvedItem
                {
                    useElementSchema = true,
                    function = default,
                    label = BuildElementLabel(attribute, instance, role, byteRole),
                    attribute = attribute,
                    instance = instance,
                    role = role,
                    byteRole = byteRole,
                    relativeChannel = rel,
                    absoluteChannel = startAddress + rel - 1
                });

                if (element == null) continue;
                if (element.attribute == FixtureAttribute.NoFeature) continue;

                var key = new ElementKey(element.attribute, instance, element.role);

                if (!_elementMap.TryGetValue(key, out var binding))
                    binding = default;

                switch (element.byteRole)
                {
                    case FixtureByteRole.Single:
                        binding.singleRel = rel;
                        binding.singleElement = element;
                        break;
                    case FixtureByteRole.Coarse:
                        binding.coarseRel = rel;
                        binding.coarseElement = element;
                        break;
                    case FixtureByteRole.Fine:
                        binding.fineRel = rel;
                        break;
                }

                _elementMap[key] = binding;
            }

            if (md.wheelBindings == null) return;

            for (int i = 0; i < md.wheelBindings.Count; i++)
            {
                var binding = md.wheelBindings[i];
                if (binding == null) continue;
                if (binding.goboWheel == null) continue;

                var key = new WheelKey(binding.attribute, binding.instance);
                _goboWheelMap[key] = binding.goboWheel;
            }
        }

        private void RebuildMonitorItemsSkeleton()
        {
            if (monitorItems == null) monitorItems = new List<DmxMonitorItem>();
            monitorItems.Clear();

            // 1) FixtureDefinition modeで解決した関数群を追加
            if (monitorIncludeResolvedFunctions && _resolvedItems != null)
            {
                for (int i = 0; i < _resolvedItems.Count; i++)
                {
                    var r = _resolvedItems[i];
                    monitorItems.Add(new DmxMonitorItem
                    {
                        useElementSchema = r.useElementSchema,
                        function = r.function,
                        label = r.label,
                        attribute = r.attribute,
                        instance = r.instance,
                        role = r.role,
                        byteRole = r.byteRole,
                        relativeCh = Mathf.Clamp(r.relativeChannel, 1, 512),
                        absoluteCh = Mathf.Clamp(r.absoluteChannel, 1, 512),
                        value = 0
                    });
                }
            }

            // 2) 追加の相対chを追記（重複除外）
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
                        useElementSchema = false,
                        function = default,
                        label = $"rel{rel}",
                        relativeCh = rel,
                        absoluteCh = Mathf.Clamp(abs, 1, 512),
                        value = 0
                    });
                }
            }

            // 表示を安定させるため relativeCh 昇順に並べる（件数が少ないため O(n^2) で十分）
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

                // 2.5) legacy fallback: targetLight側にあるDriverを探す
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
                        Debug.LogWarning($"[DmxFixtureComponent] HDRP is requested but HAS_HDRP is not enabled. Using GenericLightDriver: {name}", this);

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

            if (_usesElementMode)
            {
                ApplyElementMode(universe512);
                UpdateMonitorValues(universe512);
                ApplyImmediateInEditMode();
                return;
            }

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

                // Whiteがある場合は加算して簡易ミックス
                if (TryGetRelativeChannel(FixtureFunction.White, out int wRel))
                {
                    float w = DmxValueUtils.ByteTo01(Read8Abs(universe512, startAddress + wRel - 1));
                    rgb = rgb + Color.white * w;
                }
            }

            int goboValue = 0;
            if (TryGetRelativeChannel(FixtureFunction.Gobo, out int goboRel))
                goboValue = Read8Abs(universe512, startAddress + goboRel - 1);

            int goboRotationValue = 127;
            if (TryGetRelativeChannel(FixtureFunction.GoboRotation, out int goboRotationRel))
                goboRotationValue = Read8Abs(universe512, startAddress + goboRotationRel - 1);

            UpdateGoboTargetsFromDmx(goboValue, goboRotationValue);
            UpdateLightTargetsFromDmx(dim01, rgb);

            // --- Pan/Tilt Speed（ある機種のみ適用） ---
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

            if (_usesElementMode)
            {
                ApplyElementMode(universe512);
                UpdateMonitorValues(universe512);
                ApplyImmediateInEditMode();
                return;
            }

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

                // Whiteがある場合は加算して簡易ミックス
                if (TryGetRelativeChannel(FixtureFunction.White, out int wRel))
                {
                    float w = DmxValueUtils.ByteTo01(Read8Abs(universe512, startAddress + wRel - 1));
                    rgb = rgb + Color.white * w;
                }
            }

            int goboValue = 0;
            if (TryGetRelativeChannel(FixtureFunction.Gobo, out int goboRel))
                goboValue = Read8Abs(universe512, startAddress + goboRel - 1);

            int goboRotationValue = 127;
            if (TryGetRelativeChannel(FixtureFunction.GoboRotation, out int goboRotationRel))
                goboRotationValue = Read8Abs(universe512, startAddress + goboRotationRel - 1);

            UpdateGoboTargetsFromDmx(goboValue, goboRotationValue);
            UpdateLightTargetsFromDmx(dim01, rgb);

            // --- Pan/Tilt Speed（ある機種のみ適用） ---
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


        private void ApplyElementMode(int[] universe512)
        {
            if (TryElementValueMatchesRange(universe512, FixtureAttribute.Control, 1, FixtureChannelRole.Control, FixtureRangeType.Reset, out _) ||
                TryElementValueMatchesRange(universe512, FixtureAttribute.Control, 1, FixtureChannelRole.Value, FixtureRangeType.Reset, out _))
            {
                PerformReset();
                return;
            }

            float dim01 = TryReadElement01(universe512, FixtureAttribute.Dimmer, 1, FixtureChannelRole.Value, out float dim)
                ? dim
                : 1f;

            if (TryElementValueMatchesRange(universe512, FixtureAttribute.Strobe, 1, FixtureChannelRole.Value, FixtureRangeType.Closed, out _))
                dim01 = 0f;

            Color rgb = ReadElementColor(universe512);

            int goboValue = TryReadElementRaw8(universe512, FixtureAttribute.GoboWheel, 1, FixtureChannelRole.SelectMode, out int goboRaw)
                ? goboRaw
                : 0;

            int goboRotationValue = 127;
            bool hasGoboRotationSpeedOverride = false;
            float goboRotationSpeedOverride = 0f;
            if (TryReadElementRaw16(universe512, FixtureAttribute.GoboWheel, 1, FixtureChannelRole.PositionOrRotation, out int goboRotationRaw16, out var goboRotationElement) ||
                TryReadElementRaw16(universe512, FixtureAttribute.GoboWheel, 1, FixtureChannelRole.Rotation, out goboRotationRaw16, out goboRotationElement))
            {
                if (TryMapGoboRotationRangeToSpeed(goboRotationElement, goboRotationRaw16, out goboRotationSpeedOverride))
                {
                    hasGoboRotationSpeedOverride = true;
                }
                else
                {
                    goboRotationValue = Mathf.Clamp(Mathf.RoundToInt((goboRotationRaw16 / 65535f) * 255f), 0, 255);
                }
            }

            UpdateGoboTargetsFromDmx(goboValue, goboRotationValue, ResolveGoboWheelDefinition(1), hasGoboRotationSpeedOverride, goboRotationSpeedOverride);
            UpdateLightTargetsFromDmx(dim01, rgb);

            if (panTransform != null && TryReadElement01(universe512, FixtureAttribute.Pan, 1, FixtureChannelRole.Position, out float pan01))
            {
                float panDeg = (pan01 - 0.5f) * panRangeDeg;
                if (panInvert) panDeg = -panDeg;
                panDeg += panOffsetDeg;
                SetPanTarget(panDeg, -1f, panTiltSmoothing);
            }

            if (tiltTransform != null && TryReadElement01(universe512, FixtureAttribute.Tilt, 1, FixtureChannelRole.Position, out float tilt01))
            {
                float tiltDeg = (tilt01 - 0.5f) * tiltRangeDeg;
                if (tiltInvert) tiltDeg = -tiltDeg;
                tiltDeg += tiltOffsetDeg;
                SetTiltTarget(tiltDeg, -1f, panTiltSmoothing);
            }
        }

        private void ApplyElementMode(byte[] universe512)
        {
            if (TryElementValueMatchesRange(universe512, FixtureAttribute.Control, 1, FixtureChannelRole.Control, FixtureRangeType.Reset, out _) ||
                TryElementValueMatchesRange(universe512, FixtureAttribute.Control, 1, FixtureChannelRole.Value, FixtureRangeType.Reset, out _))
            {
                PerformReset();
                return;
            }

            float dim01 = TryReadElement01(universe512, FixtureAttribute.Dimmer, 1, FixtureChannelRole.Value, out float dim)
                ? dim
                : 1f;

            if (TryElementValueMatchesRange(universe512, FixtureAttribute.Strobe, 1, FixtureChannelRole.Value, FixtureRangeType.Closed, out _))
                dim01 = 0f;

            Color rgb = ReadElementColor(universe512);

            int goboValue = TryReadElementRaw8(universe512, FixtureAttribute.GoboWheel, 1, FixtureChannelRole.SelectMode, out int goboRaw)
                ? goboRaw
                : 0;

            int goboRotationValue = 127;
            bool hasGoboRotationSpeedOverride = false;
            float goboRotationSpeedOverride = 0f;
            if (TryReadElementRaw16(universe512, FixtureAttribute.GoboWheel, 1, FixtureChannelRole.PositionOrRotation, out int goboRotationRaw16, out var goboRotationElement) ||
                TryReadElementRaw16(universe512, FixtureAttribute.GoboWheel, 1, FixtureChannelRole.Rotation, out goboRotationRaw16, out goboRotationElement))
            {
                if (TryMapGoboRotationRangeToSpeed(goboRotationElement, goboRotationRaw16, out goboRotationSpeedOverride))
                {
                    hasGoboRotationSpeedOverride = true;
                }
                else
                {
                    goboRotationValue = Mathf.Clamp(Mathf.RoundToInt((goboRotationRaw16 / 65535f) * 255f), 0, 255);
                }
            }

            UpdateGoboTargetsFromDmx(goboValue, goboRotationValue, ResolveGoboWheelDefinition(1), hasGoboRotationSpeedOverride, goboRotationSpeedOverride);
            UpdateLightTargetsFromDmx(dim01, rgb);

            if (panTransform != null && TryReadElement01(universe512, FixtureAttribute.Pan, 1, FixtureChannelRole.Position, out float pan01))
            {
                float panDeg = (pan01 - 0.5f) * panRangeDeg;
                if (panInvert) panDeg = -panDeg;
                panDeg += panOffsetDeg;
                SetPanTarget(panDeg, -1f, panTiltSmoothing);
            }

            if (tiltTransform != null && TryReadElement01(universe512, FixtureAttribute.Tilt, 1, FixtureChannelRole.Position, out float tilt01))
            {
                float tiltDeg = (tilt01 - 0.5f) * tiltRangeDeg;
                if (tiltInvert) tiltDeg = -tiltDeg;
                tiltDeg += tiltOffsetDeg;
                SetTiltTarget(tiltDeg, -1f, panTiltSmoothing);
            }
        }

        private bool TryReadElement01(int[] universe512, FixtureAttribute attribute, int instance, FixtureChannelRole role, out float value01)
        {
            value01 = 0f;
            if (!_elementMap.TryGetValue(new ElementKey(attribute, instance, role), out var binding))
                return false;

            if (binding.singleRel > 0)
            {
                value01 = DmxValueUtils.ByteTo01(Read8Abs(universe512, startAddress + binding.singleRel - 1));
                return true;
            }

            if (binding.coarseRel > 0)
            {
                int coarseAbs = startAddress + binding.coarseRel - 1;
                int fineAbs = binding.fineRel > 0 ? startAddress + binding.fineRel - 1 : -1;
                value01 = Read16Abs(universe512, coarseAbs, fineAbs) / 65535f;
                return true;
            }

            return false;
        }

        private bool TryReadElement01(byte[] universe512, FixtureAttribute attribute, int instance, FixtureChannelRole role, out float value01)
        {
            value01 = 0f;
            if (!_elementMap.TryGetValue(new ElementKey(attribute, instance, role), out var binding))
                return false;

            if (binding.singleRel > 0)
            {
                value01 = DmxValueUtils.ByteTo01(Read8Abs(universe512, startAddress + binding.singleRel - 1));
                return true;
            }

            if (binding.coarseRel > 0)
            {
                int coarseAbs = startAddress + binding.coarseRel - 1;
                int fineAbs = binding.fineRel > 0 ? startAddress + binding.fineRel - 1 : -1;
                value01 = Read16Abs(universe512, coarseAbs, fineAbs) / 65535f;
                return true;
            }

            return false;
        }

        private bool TryReadElementRaw8(int[] universe512, FixtureAttribute attribute, int instance, FixtureChannelRole role, out int value)
        {
            value = 0;
            if (!_elementMap.TryGetValue(new ElementKey(attribute, instance, role), out var binding))
                return false;

            int rel = binding.singleRel > 0 ? binding.singleRel : binding.coarseRel;
            if (rel <= 0) return false;

            value = Read8Abs(universe512, startAddress + rel - 1);
            return true;
        }

        private bool TryReadElementRaw8(byte[] universe512, FixtureAttribute attribute, int instance, FixtureChannelRole role, out int value)
        {
            value = 0;
            if (!_elementMap.TryGetValue(new ElementKey(attribute, instance, role), out var binding))
                return false;

            int rel = binding.singleRel > 0 ? binding.singleRel : binding.coarseRel;
            if (rel <= 0) return false;

            value = Read8Abs(universe512, startAddress + rel - 1);
            return true;
        }

        private bool TryReadElementRaw16(int[] universe512, FixtureAttribute attribute, int instance, FixtureChannelRole role, out int value, out FixtureChannelElement element)
        {
            value = 0;
            element = null;
            if (!_elementMap.TryGetValue(new ElementKey(attribute, instance, role), out var binding))
                return false;

            element = binding.PrimaryElement;

            if (binding.coarseRel > 0)
            {
                int coarseAbs = startAddress + binding.coarseRel - 1;
                int fineAbs = binding.fineRel > 0 ? startAddress + binding.fineRel - 1 : -1;
                value = Read16Abs(universe512, coarseAbs, fineAbs);
                return true;
            }

            if (binding.singleRel > 0)
            {
                value = Read8Abs(universe512, startAddress + binding.singleRel - 1) * 257;
                return true;
            }

            return false;
        }

        private bool TryReadElementRaw16(byte[] universe512, FixtureAttribute attribute, int instance, FixtureChannelRole role, out int value, out FixtureChannelElement element)
        {
            value = 0;
            element = null;
            if (!_elementMap.TryGetValue(new ElementKey(attribute, instance, role), out var binding))
                return false;

            element = binding.PrimaryElement;

            if (binding.coarseRel > 0)
            {
                int coarseAbs = startAddress + binding.coarseRel - 1;
                int fineAbs = binding.fineRel > 0 ? startAddress + binding.fineRel - 1 : -1;
                value = Read16Abs(universe512, coarseAbs, fineAbs);
                return true;
            }

            if (binding.singleRel > 0)
            {
                value = Read8Abs(universe512, startAddress + binding.singleRel - 1) * 257;
                return true;
            }

            return false;
        }

        private bool TryMapGoboRotationRangeToSpeed(FixtureChannelElement element, int rawValue, out float speedDegPerSec)
        {
            speedDegPerSec = 0f;
            if (element == null || element.ranges == null)
                return false;

            FixtureChannelRange bestRange = null;
            int bestWidth = int.MaxValue;

            for (int i = 0; i < element.ranges.Count; i++)
            {
                var range = element.ranges[i];
                if (range == null) continue;
                if (!range.Contains(rawValue)) continue;
                if (!IsGoboRotationRangeType(range.type)) continue;

                int width = Mathf.Abs(range.dmxMax - range.dmxMin);
                if (bestRange == null || width < bestWidth)
                {
                    bestRange = range;
                    bestWidth = width;
                }
            }

            if (bestRange == null)
                return false;

            switch (bestRange.type)
            {
                case FixtureRangeType.RotationCW:
                    speedDegPerSec = -MapGoboRotationRangeMagnitude(bestRange, rawValue, true);
                    return true;
                case FixtureRangeType.RotationCCW:
                    speedDegPerSec = MapGoboRotationRangeMagnitude(bestRange, rawValue, false);
                    return true;
                case FixtureRangeType.NoFunction:
                case FixtureRangeType.Open:
                case FixtureRangeType.Closed:
                case FixtureRangeType.Indexed:
                    speedDegPerSec = 0f;
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsGoboRotationRangeType(FixtureRangeType type)
        {
            return type == FixtureRangeType.RotationCW ||
                   type == FixtureRangeType.RotationCCW ||
                   type == FixtureRangeType.NoFunction ||
                   type == FixtureRangeType.Open ||
                   type == FixtureRangeType.Closed ||
                   type == FixtureRangeType.Indexed;
        }

        private float MapGoboRotationRangeMagnitude(FixtureChannelRange range, int rawValue, bool fastToSlow)
        {
            int min = Mathf.Min(range.dmxMin, range.dmxMax);
            int max = Mathf.Max(range.dmxMin, range.dmxMax);
            if (max <= min)
                return 0f;

            float t = Mathf.InverseLerp(min, max, rawValue);
            return fastToSlow
                ? Mathf.Lerp(maxGoboRotateDegPerSec, 0f, t)
                : Mathf.Lerp(0f, maxGoboRotateDegPerSec, t);
        }

        private bool TryElementValueMatchesRange(int[] universe512, FixtureAttribute attribute, int instance, FixtureChannelRole role, FixtureRangeType type, out int rawValue)
        {
            rawValue = 0;
            if (!_elementMap.TryGetValue(new ElementKey(attribute, instance, role), out var binding))
                return false;

            var element = binding.PrimaryElement;
            if (element == null) return false;
            if (!TryReadElementRaw8(universe512, attribute, instance, role, out rawValue))
                return false;

            return ElementHasRangeType(element, rawValue, type);
        }

        private bool TryElementValueMatchesRange(byte[] universe512, FixtureAttribute attribute, int instance, FixtureChannelRole role, FixtureRangeType type, out int rawValue)
        {
            rawValue = 0;
            if (!_elementMap.TryGetValue(new ElementKey(attribute, instance, role), out var binding))
                return false;

            var element = binding.PrimaryElement;
            if (element == null) return false;
            if (!TryReadElementRaw8(universe512, attribute, instance, role, out rawValue))
                return false;

            return ElementHasRangeType(element, rawValue, type);
        }

        private static bool ElementHasRangeType(FixtureChannelElement element, int rawValue, FixtureRangeType type)
        {
            if (element.ranges == null) return false;

            for (int i = 0; i < element.ranges.Count; i++)
            {
                var range = element.ranges[i];
                if (range == null) continue;
                if (range.type != type) continue;
                if (range.Contains(rawValue)) return true;
            }

            return false;
        }

        private Color ReadElementColor(int[] universe512)
        {
            bool hasC = TryReadElement01(universe512, FixtureAttribute.Cyan, 1, FixtureChannelRole.Value, out float c);
            bool hasM = TryReadElement01(universe512, FixtureAttribute.Magenta, 1, FixtureChannelRole.Value, out float m);
            bool hasY = TryReadElement01(universe512, FixtureAttribute.Yellow, 1, FixtureChannelRole.Value, out float y);

            Color rgb;
            if (hasC || hasM || hasY)
            {
                rgb = new Color(1f - (hasC ? c : 0f), 1f - (hasM ? m : 0f), 1f - (hasY ? y : 0f), 1f);
            }
            else
            {
                bool hasR = TryReadElement01(universe512, FixtureAttribute.Red, 1, FixtureChannelRole.Value, out float r);
                bool hasG = TryReadElement01(universe512, FixtureAttribute.Green, 1, FixtureChannelRole.Value, out float g);
                bool hasB = TryReadElement01(universe512, FixtureAttribute.Blue, 1, FixtureChannelRole.Value, out float b);
                rgb = (hasR || hasG || hasB) ? new Color(hasR ? r : 0f, hasG ? g : 0f, hasB ? b : 0f, 1f) : Color.white;
            }

            if (TryReadElement01(universe512, FixtureAttribute.White, 1, FixtureChannelRole.Value, out float w))
                rgb += Color.white * w;

            if (TryReadElement01(universe512, FixtureAttribute.CTC, 1, FixtureChannelRole.Value, out float ctc01))
                rgb = ApplyCtcApproximation(rgb, ctc01);

            return ClampColor(rgb);
        }

        private Color ReadElementColor(byte[] universe512)
        {
            bool hasC = TryReadElement01(universe512, FixtureAttribute.Cyan, 1, FixtureChannelRole.Value, out float c);
            bool hasM = TryReadElement01(universe512, FixtureAttribute.Magenta, 1, FixtureChannelRole.Value, out float m);
            bool hasY = TryReadElement01(universe512, FixtureAttribute.Yellow, 1, FixtureChannelRole.Value, out float y);

            Color rgb;
            if (hasC || hasM || hasY)
            {
                rgb = new Color(1f - (hasC ? c : 0f), 1f - (hasM ? m : 0f), 1f - (hasY ? y : 0f), 1f);
            }
            else
            {
                bool hasR = TryReadElement01(universe512, FixtureAttribute.Red, 1, FixtureChannelRole.Value, out float r);
                bool hasG = TryReadElement01(universe512, FixtureAttribute.Green, 1, FixtureChannelRole.Value, out float g);
                bool hasB = TryReadElement01(universe512, FixtureAttribute.Blue, 1, FixtureChannelRole.Value, out float b);
                rgb = (hasR || hasG || hasB) ? new Color(hasR ? r : 0f, hasG ? g : 0f, hasB ? b : 0f, 1f) : Color.white;
            }

            if (TryReadElement01(universe512, FixtureAttribute.White, 1, FixtureChannelRole.Value, out float w))
                rgb += Color.white * w;

            if (TryReadElement01(universe512, FixtureAttribute.CTC, 1, FixtureChannelRole.Value, out float ctc01))
                rgb = ApplyCtcApproximation(rgb, ctc01);

            return ClampColor(rgb);
        }

        private static Color ApplyCtcApproximation(Color color, float ctc01)
        {
            Color warmTint = new Color(1f, 0.78f, 0.55f, 1f);
            Color tint = Color.Lerp(Color.white, warmTint, Mathf.Clamp01(ctc01));
            return new Color(color.r * tint.r, color.g * tint.g, color.b * tint.b, 1f);
        }

        private static Color ClampColor(Color color)
        {
            return new Color(Mathf.Clamp01(color.r), Mathf.Clamp01(color.g), Mathf.Clamp01(color.b), 1f);
        }

        private GoboWheelDefinition ResolveGoboWheelDefinition(int instance)
        {
            if (_goboWheelMap.TryGetValue(new WheelKey(FixtureAttribute.GoboWheel, instance), out var wheel) && wheel != null)
                return wheel;

            return goboWheel;
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

                    // function未指定なら relXX で表示
                    string label = !string.IsNullOrWhiteSpace(it.label)
                        ? it.label
                        : (Convert.ToInt32(it.function) == 0)
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

        private static string BuildElementLabel(FixtureAttribute attribute, int instance, FixtureChannelRole role, FixtureByteRole byteRole)
        {
            return $"{attribute}{Mathf.Max(1, instance)}.{role}.{byteRole}";
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
            _goboEnabled = false;
            _goboTexture = null;
            _goboRotationDeg = 0f;
            _goboRotationOffsetDeg = 0f;
            _goboRotationSpeedDegPerSec = 0f;

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
            var state = BuildRenderState(lightDim01, lensDim01, rgb);
            var driverState = state;
            if (!syncLightCookieToGobo)
            {
                driverState.goboEnabled = false;
                driverState.goboTexture = null;
            }

            if (isInitialized && runtimeDrivers != null && runtimeDrivers.Count > 0)
            {
                for (int i = 0; i < runtimeDrivers.Count; i++)
                    runtimeDrivers[i]?.Apply(driverState);
            }
            else
            {
                ApplyDirectToLights(driverState);
            }

            ApplyLensDmx(state);
            ApplyPseudoBeamDmx(state);
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

        private void ApplyDirectToLights(FixtureRenderState state)
        {
            var targets = GatherTargetLights();
            for (int i = 0; i < targets.Count; i++)
            {
                var l = targets[i];
                if (l == null) continue;
                l.color = state.color;
                l.intensity = state.lightDimmer01 * 10f;
                l.cookie = state.goboEnabled ? state.goboTexture : null;
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
            if (string.IsNullOrWhiteSpace(lensGoboTextureProperty)) lensGoboTextureProperty = "_GoboTexture";
            if (string.IsNullOrWhiteSpace(lensGoboRotationProperty)) lensGoboRotationProperty = "_GoboRotationDeg";
            if (string.IsNullOrWhiteSpace(lensGoboEnabledProperty)) lensGoboEnabledProperty = "_GoboEnabled";

            _lensColorId = Shader.PropertyToID(lensColorProperty);
            _lensDimmerId = Shader.PropertyToID(lensDimmerProperty);
            _lensGoboTextureId = Shader.PropertyToID(lensGoboTextureProperty);
            _lensGoboRotationId = Shader.PropertyToID(lensGoboRotationProperty);
            _lensGoboEnabledId = Shader.PropertyToID(lensGoboEnabledProperty);
            if (_lensMpb == null) _lensMpb = new MaterialPropertyBlock();
            _lensPropertyIdsReady = true;
        }

        private void ApplyLensDmx(FixtureRenderState state)
        {
            if (!syncLensToDmx) return;
            if (!syncLensColorToDmx && !syncLensDimmerToDmx && !syncLensGoboToDmx) return;

            // Renderer未設定なら何もしない
            if (lensRenderer == null && (extraLensRenderers == null || extraLensRenderers.Length == 0))
                return;

            EnsureLensPropertyIds();

            if (_lensMpb == null) return;

            float d = Mathf.Clamp01(state.lensDimmer01) * Mathf.Max(0f, lensDimmerScale);

            // 共通のMPBに値をセットし、各Rendererに適用
            _lensMpb.Clear();
            if (syncLensColorToDmx)
                _lensMpb.SetColor(_lensColorId, state.color);
            if (syncLensDimmerToDmx)
                _lensMpb.SetFloat(_lensDimmerId, d);
            if (syncLensGoboToDmx)
            {
                _lensMpb.SetTexture(_lensGoboTextureId, state.goboEnabled && state.goboTexture != null ? state.goboTexture : Texture2D.whiteTexture);
                _lensMpb.SetFloat(_lensGoboRotationId, state.goboRotationDeg);
                _lensMpb.SetFloat(_lensGoboEnabledId, state.goboEnabled ? 1f : 0f);
            }

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

        private void EnsureBeamPropertyIds()
        {
            if (_beamPropertyIdsReady && _beamMpb != null) return;

            if (string.IsNullOrWhiteSpace(beamColorProperty)) beamColorProperty = "_DmxColor";
            if (string.IsNullOrWhiteSpace(beamDimmerProperty)) beamDimmerProperty = "_DmxDimmer";
            if (string.IsNullOrWhiteSpace(beamGoboTextureProperty)) beamGoboTextureProperty = "_GoboTexture";
            if (string.IsNullOrWhiteSpace(beamGoboRotationProperty)) beamGoboRotationProperty = "_GoboRotationDeg";
            if (string.IsNullOrWhiteSpace(beamGoboEnabledProperty)) beamGoboEnabledProperty = "_GoboEnabled";

            _beamColorId = Shader.PropertyToID(beamColorProperty);
            _beamDimmerId = Shader.PropertyToID(beamDimmerProperty);
            _beamGoboTextureId = Shader.PropertyToID(beamGoboTextureProperty);
            _beamGoboRotationId = Shader.PropertyToID(beamGoboRotationProperty);
            _beamGoboEnabledId = Shader.PropertyToID(beamGoboEnabledProperty);
            if (_beamMpb == null) _beamMpb = new MaterialPropertyBlock();
            _beamPropertyIdsReady = true;
        }

        private void ApplyPseudoBeamDmx(FixtureRenderState state)
        {
            if (!syncPseudoBeamToDmx) return;
            if (!syncBeamColorToDmx && !syncBeamDimmerToDmx && !syncBeamGoboToDmx) return;

            if (beamRenderer == null && (extraBeamRenderers == null || extraBeamRenderers.Length == 0))
                return;

            EnsureBeamPropertyIds();
            if (_beamMpb == null) return;

            float d = Mathf.Clamp01(state.lightDimmer01) * Mathf.Max(0f, beamDimmerScale);
            d = Mathf.Max(Mathf.Clamp01(beamDimmerFloor), d);

            _beamMpb.Clear();
            if (syncBeamColorToDmx)
                _beamMpb.SetColor(_beamColorId, state.color);
            if (syncBeamDimmerToDmx)
                _beamMpb.SetFloat(_beamDimmerId, d);
            if (syncBeamGoboToDmx)
            {
                _beamMpb.SetTexture(_beamGoboTextureId, state.goboEnabled && state.goboTexture != null ? state.goboTexture : Texture2D.whiteTexture);
                _beamMpb.SetFloat(_beamGoboRotationId, state.goboRotationDeg);
                _beamMpb.SetFloat(_beamGoboEnabledId, state.goboEnabled ? 1f : 0f);
            }

            if (beamRenderer != null)
                beamRenderer.SetPropertyBlock(_beamMpb);

            if (extraBeamRenderers != null)
            {
                for (int i = 0; i < extraBeamRenderers.Length; i++)
                {
                    var r = extraBeamRenderers[i];
                    if (r != null) r.SetPropertyBlock(_beamMpb);
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

        private FixtureRenderState BuildRenderState(float lightDim01, float lensDim01, Color rgb)
        {
            return new FixtureRenderState
            {
                lightDimmer01 = lightDim01,
                lensDimmer01 = lensDim01,
                color = rgb,
                goboEnabled = _goboEnabled && _goboTexture != null,
                goboTexture = _goboTexture,
                goboRotationDeg = Mathf.Repeat(_goboRotationDeg + _goboRotationOffsetDeg, 360f)
            };
        }

        private void UpdateGoboTargetsFromDmx(int goboValue, int goboRotationValue, GoboWheelDefinition wheelDefinition = null, bool overrideGoboRotationSpeed = false, float goboRotationSpeedOverride = 0f)
        {
            _goboEnabled = false;
            _goboTexture = null;
            _goboRotationOffsetDeg = 0f;

            var wheel = wheelDefinition != null ? wheelDefinition : goboWheel;
            if (wheel != null)
            {
                var slot = wheel.ResolveSlot(goboValue);
                if (slot != null)
                {
                    _goboEnabled = !slot.isOpen && slot.texture != null;
                    _goboTexture = slot.texture;
                    _goboRotationOffsetDeg = slot.rotationOffsetDeg;
                }
            }

            _goboRotationSpeedDegPerSec = overrideGoboRotationSpeed ? goboRotationSpeedOverride : MapGoboRotationDmxToSpeed(goboRotationValue);
        }

        private void UpdateGoboMotion()
        {
            if (!Mathf.Approximately(_goboRotationSpeedDegPerSec, 0f))
                _goboRotationDeg = Mathf.Repeat(_goboRotationDeg + (_goboRotationSpeedDegPerSec * Time.deltaTime), 360f);

            ApplyGoboCookieRollTransform();
        }

        private void ApplyGoboCookieRollTransform()
        {
            if (!syncGoboRotationToCookieTransform)
                return;

            if (goboCookieRollTransform == null)
            {
                _goboCookieRollBaseTransform = null;
                _hasGoboCookieRollBaseLocalRot = false;
                return;
            }

            if (!_hasGoboCookieRollBaseLocalRot || _goboCookieRollBaseTransform != goboCookieRollTransform)
            {
                _goboCookieRollBaseTransform = goboCookieRollTransform;
                _goboCookieRollBaseLocalRot = goboCookieRollTransform.localRotation;
                _hasGoboCookieRollBaseLocalRot = true;
            }

            Vector3 axis = goboCookieRollAxis.sqrMagnitude > 0.0001f ? goboCookieRollAxis.normalized : Vector3.forward;
            float rollDeg = Mathf.Repeat(_goboRotationDeg + _goboRotationOffsetDeg + goboCookieRollOffsetDeg, 360f);
            goboCookieRollTransform.localRotation = _goboCookieRollBaseLocalRot * Quaternion.AngleAxis(rollDeg, axis);
        }

        private float MapGoboRotationDmxToSpeed(int dmxValue)
        {
            int clamped = Mathf.Clamp(dmxValue, 0, 255);
            if (clamped <= 126)
            {
                float t = clamped / 126f;
                return Mathf.Lerp(-maxGoboRotateDegPerSec, 0f, t);
            }

            if (clamped <= 128)
                return 0f;

            float t2 = (clamped - 129) / 126f;
            return Mathf.Lerp(0f, maxGoboRotateDegPerSec, t2);
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

            // 最初のDMX目標値が来るまでは何もしない
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
