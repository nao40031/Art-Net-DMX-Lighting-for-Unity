using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArtNet.Runtime
{
    /// <summary>
    /// ArtNetReceiver を購読し、Universeバッファ更新と Fixture へのルーティングを担当する。
    /// Fixtureの設定/適用は DmxFixtureComponent 側に寄せる。
    ///
    /// 追加：Hierarchy順の自動採番（addressingRoot配下を上から順に辿る）
    /// </summary>
    [DisallowMultipleComponent]
    public class DmxRigController : MonoBehaviour
    {
        [Header("Art-Net Source")]
        [Tooltip("受信元。未設定ならシーンから自動で探します")]
        public ArtNetReceiver receiver;

        [Header("Auto Discover")]
        [Tooltip("OnEnable時にシーン内の DmxFixtureComponent を自動収集して初期化します")]
        public bool autoDiscoverFixturesOnEnable = true;

        [Header("Apply")]
        [Tooltip("DMXを受信したUniverseを毎フレームFixtureへ適用します")]
        public bool applyOnUpdate = true;

        [Header("Logging")]
        public bool logRxRate = true;
        [Range(0.2f, 5f)] public float logRxIntervalSec = 1.0f;

        [Tooltip("適用時に先頭CHをログ表示（デバッグ用）")]
        public bool logApplyHeadChannels = false;

        [Header("Auto Addressing (Hierarchy Order)")]
        [Tooltip("Discover時に addressingRoot 配下のFixtureへ startAddress を自動採番します（Hierarchy順）")]
        public bool autoAssignStartAddressOnEnable = false;

        [Tooltip("採番対象の親（MovingLight群の親）。未指定ならシーン全体（ただし順序保証は弱い）")]
        public Transform addressingRoot;

        [Tooltip("起点Universe：usePrefabValueAsBase=false のときのみ使用")]
        public int universeStart = 0;

        [Tooltip("起点StartAddress：usePrefabValueAsBase=false のときのみ使用")]
        [Range(1, 512)] public int startAddressStart = 1;

        [Tooltip("true: addressingRoot配下の先頭Fixtureの現在値(universe/startAddress)を起点にする（=Prefab値を起点にしたい場合）")]
        public bool usePrefabValueAsBase = true;

        [Tooltip("512chを超えたらUniverseを自動で+1して続行します")]
        public bool autoIncrementUniverse = true;

        // ------------------------------------------------------------
        // runtime
        // ------------------------------------------------------------

        private readonly object _lock = new object();
        private readonly Dictionary<int, byte[]> _universeBuffers = new Dictionary<int, byte[]>();
        private readonly Dictionary<int, List<DmxFixtureComponent>> _fixturesByUniverse = new Dictionary<int, List<DmxFixtureComponent>>();

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
            _detectedHdrp = DetectIsHDRP();

            if (receiver == null)
                receiver = FindReceiver();

            if (receiver != null)
                receiver.OnDataReceived += OnArtNetData;
            else
                Debug.LogWarning("[DmxRigController] ArtNetReceiver not found in scene.");

            if (autoDiscoverFixturesOnEnable)
                DiscoverAndInitializeAllFixtures();
        }

        private void OnDisable()
        {
            if (receiver != null)
                receiver.OnDataReceived -= OnArtNetData;
        }

        private void Update()
        {
            if (logRxRate && (Time.unscaledTime - _lastRxLogTime) >= logRxIntervalSec)
            {
                float dt = Mathf.Max(0.0001f, Time.unscaledTime - _lastRxLogTime);
                int perSec = Mathf.RoundToInt(_rxCount / dt);
                _rxCount = 0;
                _lastRxLogTime = Time.unscaledTime;

                Debug.Log($"[DmxRigController] DMX RX: ~{perSec}/sec  lastUniverse:{_lastUniverse}  offset:{_lastOffset}");
            }

            if (!applyOnUpdate) return;

            int uni;
            lock (_lock) { uni = _lastUniverse; }

            ApplyUniverse(uni);
        }

        // ------------------------------------------------------------
        // Public API (optional: called by DmxFixtureComponent)
        // ------------------------------------------------------------

        public void Register(DmxFixtureComponent fixture)
        {
            if (fixture == null) return;

            lock (_lock)
            {
                if (!_fixturesByUniverse.TryGetValue(fixture.universe, out var list))
                {
                    list = new List<DmxFixtureComponent>();
                    _fixturesByUniverse[fixture.universe] = list;
                }

                if (!list.Contains(fixture))
                    list.Add(fixture);

                GetOrCreateUniverseBuffer(fixture.universe);
            }
        }

        public void Unregister(DmxFixtureComponent fixture)
        {
            if (fixture == null) return;

            lock (_lock)
            {
                if (_fixturesByUniverse.TryGetValue(fixture.universe, out var list))
                {
                    list.Remove(fixture);
                }
            }
        }

        // ------------------------------------------------------------
        // Art-Net callback
        // ------------------------------------------------------------

        private void OnArtNetData(ArtNetData data)
        {
            if (data.Channels == null) return;

            int universe = data.Universe;
            var buf = GetOrCreateUniverseBuffer(universe);

            int len = Mathf.Min(512, data.Channels.Length);
            for (int i = 0; i < len; i++)
                buf[i] = (byte)Mathf.Clamp(data.Channels[i], 0, 255);

            lock (_lock)
            {
                _lastUniverse = universe;
                _lastOffset = 0;
                _rxCount++;
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

            // 自動採番（必要なら）
            if (autoAssignStartAddressOnEnable)
                AutoAssignStartAddresses(fixtures);

            // 登録し直し
            lock (_lock)
            {
                _fixturesByUniverse.Clear();
                _universeBuffers.Clear();
            }

            int registered = 0;
            for (int i = 0; i < fixtures.Length; i++)
            {
                var f = fixtures[i];
                if (f == null) continue;
                Register(f);
                registered++;
            }

            // 初期化（互換Initialize(bool)を想定）
            for (int i = 0; i < fixtures.Length; i++)
            {
                var f = fixtures[i];
                if (f == null) continue;
                f.Initialize(_detectedHdrp);
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
                if (!_universeBuffers.TryGetValue(universe, out uniBuf) || uniBuf == null || uniBuf.Length != 512)
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

            AutoAssignStartAddresses(fixtures);
            Debug.Log("[DmxRigController] AutoAssignStartAddresses done (Hierarchy Order).");
        }

        private void AutoAssignStartAddresses(DmxFixtureComponent[] fixtures)
        {
            // ★Hierarchy順でターゲット列を作る
            List<DmxFixtureComponent> targets;
            if (addressingRoot != null)
            {
                // addressingRoot配下を、Hierarchy（兄弟順）で上から順に収集
                targets = CollectFixturesInHierarchyOrder(addressingRoot, includeRoot: false);
            }
            else
            {
                // addressingRoot未指定時は全件（順序保証は弱い）
                targets = new List<DmxFixtureComponent>(fixtures.Length);
                for (int i = 0; i < fixtures.Length; i++)
                {
                    var f = fixtures[i];
                    if (f != null) targets.Add(f);
                }
            }

            if (targets.Count == 0)
            {
                Debug.LogWarning("[DmxRigController] No fixtures found for auto addressing.");
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

                // 512超えチェック
                if (curAddr + chCount - 1 > 512)
                {
                    if (autoIncrementUniverse)
                    {
                        curUni += 1;
                        curAddr = baseStart;

                        if (curAddr + chCount - 1 > 512)
                        {
                            Debug.LogWarning($"[DmxRigController] Cannot fit fixture '{f.name}' (chCount={chCount}) even after universe increment. Stop.");
                            break;
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[DmxRigController] Address overflow at '{f.name}'. autoIncrementUniverse=false, stop.");
                        break;
                    }
                }

                f.universe = curUni;
                f.startAddress = curAddr;

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

            static void CollectRecursive(Transform parent, List<DmxFixtureComponent> acc)
            {
                // 子をHierarchy順（sibling index順）に辿る
                for (int i = 0; i < parent.childCount; i++)
                {
                    var t = parent.GetChild(i);

                    var f = t.GetComponent<DmxFixtureComponent>();
                    if (f != null) acc.Add(f);

                    // 孫も同じ順で
                    if (t.childCount > 0)
                        CollectRecursive(t, acc);
                }
            }
        }

        private static int GetChannelCountSafe(DmxFixtureComponent f)
        {
            if (f == null) return 1;

            // DmxFixtureComponent の fixture/mode から読む（現行実装に合わせる）
            if (f.fixture == null || f.fixture.modes == null || f.fixture.modes.Count == 0)
                return 1;

            int m = Mathf.Clamp(f.mode, 0, f.fixture.modes.Count - 1);
            return Mathf.Clamp(f.fixture.modes[m].channelCount, 1, 512);
        }

        // ------------------------------------------------------------
        // Universe buffer
        // ------------------------------------------------------------

        private byte[] GetOrCreateUniverseBuffer(int universe)
        {
            lock (_lock)
            {
                if (!_universeBuffers.TryGetValue(universe, out var buf) || buf == null || buf.Length != 512)
                {
                    buf = new byte[512];
                    _universeBuffers[universe] = buf;
                }
                return buf;
            }
        }

        // ------------------------------------------------------------
        // Finders
        // ------------------------------------------------------------

        private static DmxFixtureComponent[] FindAllFixtures(bool includeInactive)
        {
#if UNITY_2023_1_OR_NEWER
            return FindObjectsByType<DmxFixtureComponent>(
                includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                FindObjectsSortMode.None
            );
#else
            return GameObject.FindObjectsOfType<DmxFixtureComponent>(includeInactive);
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

            string typeName = rp.GetType().FullName ?? "";
            return typeName.IndexOf("HighDefinition", StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.IndexOf("HDRenderPipeline", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
