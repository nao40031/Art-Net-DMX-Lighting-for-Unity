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
    /// ArtNetReceiver を購読し、Universeバッファ更新と Fixture へのルーティングを担当する。
    /// Fixtureの設定/適用は DmxFixtureComponent 側に寄せる。
    ///
    /// 追加：Hierarchy順の自動採番（addressingRoot配下を上から順に辿る）
    /// 追加：Editモードで採番値をシーンへ保存（ベイク）する機能
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
        [Tooltip("Update内で最後に受信したUniverseを毎フレ適用します（受信頻度が高い場合はfalse推奨）")]
        public bool applyOnUpdate = true;

        [Header("Debug")]
        [Tooltip("受信レートをログ表示します")]
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

        [Header("Bake (Persist in Scene)")]
        [Tooltip("Editモードで採番してシーンに保存（PrefabインスタンスOverride含む）します。Play中に採番しても停止時に戻るのはUnity仕様です")]
        public bool bakeWritesToScene = true;

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

            int uni;
            lock (_lock) { uni = _lastUniverse; }

            ApplyUniverse(uni);
        }

        // ------------------------------------------------------------
        // Public API (optional: called by DmxFixtureComponent)
        // ------------------------------------------------------------

        public void Register(DmxFixtureComponent fixture)
        {
            // 任意：今はDiscover側で収集するため、Registerは必須ではない
            // （DmxFixtureComponent側が Register/Unregister を持っている互換のため残す）
        }

        public void Unregister(DmxFixtureComponent fixture)
        {
            // 任意
        }

        // ------------------------------------------------------------
        // Receiver callback
        // ------------------------------------------------------------

        private void OnArtNetData(ArtNetData data)
        {
            if (data.Channels == null) return;

            lock (_lock)
            {
                _rxCount++;
                _lastUniverse = data.Universe;
                _lastOffset = 0;
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
            {
#if UNITY_EDITOR
                bool writeToScene = !Application.isPlaying && bakeWritesToScene;
                AutoAssignStartAddresses(fixtures, writeToScene);
#else
                AutoAssignStartAddresses(fixtures, false);
#endif
            }

            // Universe別にまとめる
            _fixturesByUniverse.Clear();
            int registered = 0;

            for (int i = 0; i < fixtures.Length; i++)
            {
                var f = fixtures[i];
                if (f == null) continue;

                // 初期化（Fixture側でMappingなどを解決）
                f.Initialize(_detectedHdrp);

                // ルーティング登録
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
                Debug.LogWarning("[DmxRigController] BAKEはEditモード専用です。Playを停止してから実行してください。", this);
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

                // 512超えチェック
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
