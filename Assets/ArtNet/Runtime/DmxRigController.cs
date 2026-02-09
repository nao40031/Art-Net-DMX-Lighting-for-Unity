using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace ArtNet.Runtime
{
    /// <summary>
    /// ArtNetReceiver 繧定ｳｼ隱ｭ縺励ゞniverse繝舌ャ繝輔ぃ譖ｴ譁ｰ縺ｨ Fixture 縺ｸ縺ｮ繝ｫ繝ｼ繝・ぅ繝ｳ繧ｰ繧呈球蠖薙☆繧九・
    /// Fixture縺ｮ險ｭ螳・驕ｩ逕ｨ縺ｯ DmxFixtureComponent 蛛ｴ縺ｫ蟇・○繧九・
    ///
    /// 霑ｽ蜉・唏ierarchy鬆・・閾ｪ蜍墓治逡ｪ・・ddressingRoot驟堺ｸ九ｒ荳翫°繧蛾・↓霎ｿ繧具ｼ・
    /// 霑ｽ蜉・哘dit繝｢繝ｼ繝峨〒謗｡逡ｪ蛟､繧偵す繝ｼ繝ｳ縺ｸ菫晏ｭ假ｼ医・繧､繧ｯ・峨☆繧区ｩ溯・
    /// </summary>
    [DisallowMultipleComponent]
    public class DmxRigController : MonoBehaviour
    {
        public enum InputMode
        {
            LiveOnly,
            PlaybackOnly,
            LiveAndPlayback,
        }

        [Header("Input Mode")]
        public InputMode inputMode = InputMode.LiveOnly;

        [Header("Art-Net Source")]
        [Tooltip("蜿嶺ｿ｡蜈・よ悴險ｭ螳壹↑繧峨す繝ｼ繝ｳ縺九ｉ閾ｪ蜍輔〒謗｢縺励∪縺・")]
        public ArtNetReceiver receiver;

        [Header("Auto Discover")]
        [Tooltip("OnEnable譎ゅ↓繧ｷ繝ｼ繝ｳ蜀・・ DmxFixtureComponent 繧定・蜍募庶髮・＠縺ｦ蛻晄悄蛹悶＠縺ｾ縺・")]
        public bool autoDiscoverFixturesOnEnable = true;

        [Header("Apply")]
        [Tooltip("Update蜀・〒譛蠕後↓蜿嶺ｿ｡縺励◆Universe繧呈ｯ弱ヵ繝ｬ驕ｩ逕ｨ縺励∪縺呻ｼ亥女菫｡鬆ｻ蠎ｦ縺碁ｫ倥＞蝣ｴ蜷医・false謗ｨ螂ｨ・・")]
        public bool applyOnUpdate = true;

        [Header("Debug")]
        [Tooltip("蜿嶺ｿ｡繝ｬ繝ｼ繝医ｒ繝ｭ繧ｰ陦ｨ遉ｺ縺励∪縺・")]
        public bool logRxRate = true;

        [Range(0.2f, 5f)] public float logRxIntervalSec = 1.0f;

        [Tooltip("驕ｩ逕ｨ譎ゅ↓蜈磯ｭCH繧偵Ο繧ｰ陦ｨ遉ｺ・医ョ繝舌ャ繧ｰ逕ｨ・・")]
        public bool logApplyHeadChannels = false;

        [Header("Auto Addressing (Hierarchy Order)")]
        [Tooltip("Discover譎ゅ↓ addressingRoot 驟堺ｸ九・Fixture縺ｸ startAddress 繧定・蜍墓治逡ｪ縺励∪縺呻ｼ・ierarchy鬆・ｼ・")]
        public bool autoAssignStartAddressOnEnable = false;

        [Tooltip("謗｡逡ｪ蟇ｾ雎｡縺ｮ隕ｪ・・ovingLight鄒､縺ｮ隕ｪ・峨よ悴謖・ｮ壹↑繧峨す繝ｼ繝ｳ蜈ｨ菴難ｼ医◆縺縺鈴・ｺ丈ｿ晁ｨｼ縺ｯ蠑ｱ縺・ｼ・")]
        public Transform addressingRoot;

        [Tooltip("襍ｷ轤ｹUniverse・嗽sePrefabValueAsBase=false 縺ｮ縺ｨ縺阪・縺ｿ菴ｿ逕ｨ")]
        public int universeStart = 0;

        [Tooltip("襍ｷ轤ｹStartAddress・嗽sePrefabValueAsBase=false 縺ｮ縺ｨ縺阪・縺ｿ菴ｿ逕ｨ")]
        [Range(1, 512)] public int startAddressStart = 1;

        [Tooltip("true: addressingRoot驟堺ｸ九・蜈磯ｭFixture縺ｮ迴ｾ蝨ｨ蛟､(universe/startAddress)繧定ｵｷ轤ｹ縺ｫ縺吶ｋ・・Prefab蛟､繧定ｵｷ轤ｹ縺ｫ縺励◆縺・ｴ蜷茨ｼ・")]
        public bool usePrefabValueAsBase = true;

        [Tooltip("512ch繧定ｶ・∴縺溘ｉUniverse繧定・蜍輔〒+1縺励※邯夊｡後＠縺ｾ縺・")]
        public bool autoIncrementUniverse = true;

        [Header("Bake (Persist in Scene)")]
        [Tooltip("Edit繝｢繝ｼ繝峨〒謗｡逡ｪ縺励※繧ｷ繝ｼ繝ｳ縺ｫ菫晏ｭ假ｼ・refab繧､繝ｳ繧ｹ繧ｿ繝ｳ繧ｹOverride蜷ｫ繧・峨＠縺ｾ縺吶１lay荳ｭ縺ｫ謗｡逡ｪ縺励※繧ょ●豁｢譎ゅ↓謌ｻ繧九・縺ｯUnity莉墓ｧ倥〒縺・")]
        public bool bakeWritesToScene = true;

        // ------------------------------------------------------------
        // runtime
        // ------------------------------------------------------------

        private readonly object _lock = new object();
        private readonly Dictionary<int, byte[]> _universeBuffers = new Dictionary<int, byte[]>();
        private readonly Dictionary<int, List<DmxFixtureComponent>> _fixturesByUniverse = new Dictionary<int, List<DmxFixtureComponent>>();

        // Dirty universe apply (avoid applying only last universe and reduce allocations)
        private readonly HashSet<int> _dirtyUniverses = new HashSet<int>();
        private readonly List<int> _dirtyScratch = new List<int>(8);

        private int _rxCount;
        private float _lastRxLogTime;
        private int _lastUniverse;
        private int _lastOffset;

        private bool _detectedHdrp;

        // ------------------------------------------------------------
        // Unity lifecycle
        // ------------------------------------------------------------

        private void OnEnable()
        {
            if (receiver == null)
                receiver = FindReceiver();

            if (receiver != null)
                receiver.OnDataReceived += OnArtNetData;

            _detectedHdrp = DetectIsHDRP();

            if (autoDiscoverFixturesOnEnable)
                DiscoverAndInitializeAllFixtures();
        }

        private void OnDisable()
        {
            if (receiver != null)
                receiver.OnDataReceived -= OnArtNetData;

            lock (_lock)
            {
                _dirtyUniverses.Clear();
                _dirtyScratch.Clear();
                _rxCount = 0;
                _lastUniverse = 0;
                _lastOffset = 0;
            }
        }

        private void Update()
        {
            if (!logRxRate && !applyOnUpdate) return;

            if (logRxRate)
            {
                float t = Time.unscaledTime;
                if (_lastRxLogTime <= 0f) _lastRxLogTime = t;

                if ((t - _lastRxLogTime) >= logRxIntervalSec)
                {
                    float dt = Mathf.Max(0.0001f, (t - _lastRxLogTime));
                    float rate = _rxCount / dt;

                    int u, off;
                    lock (_lock) { u = _lastUniverse; off = _lastOffset; }

                    Debug.Log($"[DmxRigController] DMX RX: ~{rate:0}/sec  lastUniverse:{u}  offset:{off}");
                    _rxCount = 0;
                    _lastRxLogTime = t;
                }
            }

            if (!applyOnUpdate) return;

            // Dirty譁ｹ蠑擾ｼ壹％縺ｮ繝輔Ξ繝ｼ繝縺ｧ譖ｴ譁ｰ縺後≠縺｣縺欟niverse縺縺鷹←逕ｨ縺吶ｋ・・Universe莉･荳翫〒蜉ｹ譫懷､ｧ・・
            _dirtyScratch.Clear();
            lock (_lock)
            {
                if (_dirtyUniverses.Count == 0) return;

                foreach (var u in _dirtyUniverses)
                    _dirtyScratch.Add(u);

                _dirtyUniverses.Clear();
            }

            for (int i = 0; i < _dirtyScratch.Count; i++)
                ApplyUniverse(_dirtyScratch[i]);
        }

        // ------------------------------------------------------------
        // Public API (optional: called by DmxFixtureComponent)
        // ------------------------------------------------------------

        public void Register(DmxFixtureComponent fixture)
        {
            // 莉ｻ諢擾ｼ壻ｻ翫・Discover蛛ｴ縺ｧ蜿朱寔縺吶ｋ縺溘ａ縲ヽegister縺ｯ蠢・医〒縺ｯ縺ｪ縺・
            // ・・mxFixtureComponent蛛ｴ縺・Register/Unregister 繧呈戟縺｣縺ｦ縺・ｋ莠呈鋤縺ｮ縺溘ａ谿九☆・・
        }

        public void Unregister(DmxFixtureComponent fixture)
        {
            // 莉ｻ諢・
        }

        // ------------------------------------------------------------
        // Receiver callback
        // ------------------------------------------------------------

        private void OnArtNetData(ArtNetData data)
        {
            if (inputMode == InputMode.PlaybackOnly) return;
            if (data.Channels == null) return;

            lock (_lock)
            {
                _rxCount++;
                _lastUniverse = data.Universe;
                _lastOffset = 0;

                // mark dirty universe for Update apply
                _dirtyUniverses.Add(data.Universe);
            }

            byte[] buf = GetOrCreateUniverseBuffer(data.Universe);

            int len = Mathf.Min(512, data.Channels.Length);
            for (int i = 0; i < len; i++)
            {
                int v = data.Channels[i];
                if (v < 0) v = 0;
                if (v > 255) v = 255;
                buf[i] = (byte)v;
            }
        }

        // ------------------------------------------------------------
        // Playback / External Injection
        // ------------------------------------------------------------

        public void InjectUniverse(int universe, byte[] src)
        {
            if (inputMode == InputMode.LiveOnly) return;
            if (src == null || src.Length == 0) return;

            byte[] buf = GetOrCreateUniverseBuffer(universe);
            int len = Mathf.Min(512, src.Length);
            for (int i = 0; i < len; i++)
                buf[i] = src[i];

            lock (_lock)
            {
                _dirtyUniverses.Add(universe);
            }
        }

        // ------------------------------------------------------------
        // Discover / Initialize
        // ------------------------------------------------------------

        [ContextMenu("Discover & Initialize Fixtures")]
        public void DiscoverAndInitializeAllFixtures()
        {
            var fixtures = FindAllFixtures(includeInactive: true);
            if (fixtures == null || fixtures.Length == 0)
            {
                Debug.LogWarning("[DmxRigController] No DmxFixtureComponent found in scene.");
                return;
            }

            // 閾ｪ蜍墓治逡ｪ・亥ｿ・ｦ√↑繧会ｼ・
            if (autoAssignStartAddressOnEnable)
            {
#if UNITY_EDITOR
                bool writeToScene = !Application.isPlaying && bakeWritesToScene;
                AutoAssignStartAddresses(fixtures, writeToScene);
#else
                AutoAssignStartAddresses(fixtures, false);
#endif
            }

            // Universe蛻･縺ｫ縺ｾ縺ｨ繧√ｋ
            _fixturesByUniverse.Clear();
            int registered = 0;

            for (int i = 0; i < fixtures.Length; i++)
            {
                var f = fixtures[i];
                if (f == null) continue;

                // 蛻晄悄蛹厄ｼ・ixture蛛ｴ縺ｧMapping縺ｪ縺ｩ繧定ｧ｣豎ｺ・・
                f.Initialize(_detectedHdrp);

                // 繝ｫ繝ｼ繝・ぅ繝ｳ繧ｰ逋ｻ骭ｲ
                if (!_fixturesByUniverse.TryGetValue(f.universe, out var list))
                {
                    list = new List<DmxFixtureComponent>(32);
                    _fixturesByUniverse.Add(f.universe, list);
                }
                list.Add(f);
                registered++;
            }

            Debug.Log($"[DmxRigController] Discovered fixtures: {fixtures.Length} (registered: {registered})");
        }

        // ------------------------------------------------------------
        // Apply
        // ------------------------------------------------------------

        private void ApplyUniverse(int universe)
        {
            byte[] uniBuf;
            List<DmxFixtureComponent> list;

            lock (_lock)
            {
                if (!_universeBuffers.TryGetValue(universe, out uniBuf) || uniBuf == null || uniBuf.Length < 512)
                    return;

                if (!_fixturesByUniverse.TryGetValue(universe, out list) || list == null || list.Count == 0)
                    return;
            }

            if (logApplyHeadChannels)
                Debug.Log($"[DmxRigController] APPLY universe:{universe} fixtures:{list.Count}  CH1..4=({uniBuf[0]},{uniBuf[1]},{uniBuf[2]},{uniBuf[3]})");

            for (int i = 0; i < list.Count; i++)
            {
                var f = list[i];
                if (f == null) continue;
                if (!f.IsValid) continue;

                f.ApplyFromUniverseBuffer(uniBuf);
            }
        }

        // ------------------------------------------------------------
        // Auto Addressing (Hierarchy Order)
        // ------------------------------------------------------------

        [ContextMenu("Auto Assign StartAddress (Hierarchy Order)")]
        public void AutoAssignStartAddress_ContextMenu()
        {
            var fixtures = FindAllFixtures(includeInactive: true);
            if (fixtures == null || fixtures.Length == 0)
            {
                Debug.LogWarning("[DmxRigController] No fixtures found for auto addressing.");
                return;
            }

            AutoAssignStartAddresses(fixtures, writeToScene: false);
            Debug.Log("[DmxRigController] AutoAssignStartAddresses done (Hierarchy Order).");
        }

#if UNITY_EDITOR
        [ContextMenu("BAKE StartAddress to Scene (Hierarchy Order)")]
        public void BakeStartAddressToScene_ContextMenu()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[DmxRigController] BAKE縺ｯEdit繝｢繝ｼ繝牙ｰら畑縺ｧ縺吶１lay繧貞●豁｢縺励※縺九ｉ螳溯｡後＠縺ｦ縺上□縺輔＞縲・", this);
                return;
            }

            var fixtures = FindAllFixtures(includeInactive: true);
            if (fixtures == null || fixtures.Length == 0)
            {
                Debug.LogWarning("[DmxRigController] No fixtures found for bake auto addressing.", this);
                return;
            }

            AutoAssignStartAddresses(fixtures, writeToScene: true);
            Debug.Log($"[DmxRigController] Baked StartAddress for {fixtures.Length} fixtures.", this);
        }
#endif

        private void AutoAssignStartAddresses(DmxFixtureComponent[] fixtures, bool writeToScene)
        {
            List<DmxFixtureComponent> targets;

            if (addressingRoot != null)
            {
                targets = CollectFixturesInHierarchyOrder(addressingRoot, includeRoot: false);
            }
            else
            {
                // addressingRoot譛ｪ謖・ｮ壽凾縺ｯ蜈ｨ莉ｶ・磯・ｺ丈ｿ晁ｨｼ縺ｯ蠑ｱ縺・ｼ・
                targets = new List<DmxFixtureComponent>(fixtures.Length);
                for (int i = 0; i < fixtures.Length; i++)
                {
                    var f = fixtures[i];
                    if (f != null) targets.Add(f);
                }
            }

            if (targets.Count == 0)
            {
                Debug.LogWarning("[DmxRigController] No fixtures resolved for auto addressing.");
                return;
            }

            int baseUniverse = usePrefabValueAsBase ? targets[0].universe : universeStart;
            int baseStart = usePrefabValueAsBase ? Mathf.Clamp(targets[0].startAddress, 1, 512) : startAddressStart;

            int curUni = baseUniverse;
            int curAddr = baseStart;

            for (int i = 0; i < targets.Count; i++)
            {
                var f = targets[i];
                if (f == null) continue;

                int chCount = Mathf.Clamp(GetChannelCountSafe(f), 1, 512);

                // 512雜・∴繝√ぉ繝・け
                if (curAddr + chCount - 1 > 512)
                {
                    if (autoIncrementUniverse)
                    {
                        curUni += 1;
                        curAddr = baseStart;

                        if (curAddr + chCount - 1 > 512)
                        {
                            Debug.LogWarning($"[DmxRigController] Address overflow at '{f.name}' (chCount={chCount}) even after universe increment. Stop.");
                            break;
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[DmxRigController] Address overflow at '{f.name}'. autoIncrementUniverse=false, stop.");
                        break;
                    }
                }

#if UNITY_EDITOR
                if (writeToScene && !Application.isPlaying)
                {
                    Undo.RecordObject(f, "Bake DMX StartAddress");
                    f.universe = curUni;
                    f.startAddress = curAddr;

                    PrefabUtility.RecordPrefabInstancePropertyModifications(f);
                    EditorUtility.SetDirty(f);

                    var sc = f.gameObject.scene;
                    if (sc.IsValid() && sc.isLoaded) EditorSceneManager.MarkSceneDirty(sc);
                }
                else
                {
                    f.universe = curUni;
                    f.startAddress = curAddr;
                }
#else
                f.universe = curUni;
                f.startAddress = curAddr;
#endif

                curAddr += chCount;
            }
        }

        private static List<DmxFixtureComponent> CollectFixturesInHierarchyOrder(Transform root, bool includeRoot)
        {
            var list = new List<DmxFixtureComponent>(128);
            if (root == null) return list;

            if (includeRoot)
            {
                var f0 = root.GetComponent<DmxFixtureComponent>();
                if (f0 != null) list.Add(f0);
            }

            CollectRecursive(root, list);
            return list;

            static void CollectRecursive(Transform t, List<DmxFixtureComponent> acc)
            {
                int n = t.childCount;
                for (int i = 0; i < n; i++)
                {
                    var c = t.GetChild(i);
                    if (c == null) continue;

                    var f = c.GetComponent<DmxFixtureComponent>();
                    if (f != null) acc.Add(f);

                    CollectRecursive(c, acc);
                }
            }
        }

        private static int GetChannelCountSafe(DmxFixtureComponent f)
        {
            if (f == null) return 1;

            if (f.fixture != null && f.fixture.modes != null && f.fixture.modes.Count > 0)
            {
                int mi = Mathf.Clamp(f.mode, 0, f.fixture.modes.Count - 1);
                var md = f.fixture.modes[mi];
                return Mathf.Clamp(md.channelCount, 1, 512);
            }

            return 1;
        }

        private byte[] GetOrCreateUniverseBuffer(int universe)
        {
            if (!_universeBuffers.TryGetValue(universe, out var buf) || buf == null || buf.Length != 512)
            {
                buf = new byte[512];
                _universeBuffers[universe] = buf;
            }
            return buf;
        }

        private static DmxFixtureComponent[] FindAllFixtures(bool includeInactive)
        {
#if UNITY_2023_1_OR_NEWER
            return FindObjectsByType<DmxFixtureComponent>(
                includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                FindObjectsSortMode.None
            );
#else
            return FindObjectsOfType<DmxFixtureComponent>(includeInactive);
#endif
        }

        private static ArtNetReceiver FindReceiver()
        {
#if UNITY_2023_1_OR_NEWER
            return FindFirstObjectByType<ArtNetReceiver>();
#else
            return FindObjectOfType<ArtNetReceiver>();
#endif
        }

        private static bool DetectIsHDRP()
        {
            var rp = GraphicsSettings.currentRenderPipeline;
            if (rp == null) return false;

            var typeName = rp.GetType().FullName;
            if (string.IsNullOrEmpty(typeName)) return false;

            return typeName.IndexOf("HighDefinition", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
