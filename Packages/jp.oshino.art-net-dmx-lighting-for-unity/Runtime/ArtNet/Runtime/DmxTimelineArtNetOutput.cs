using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ArtNet.Runtime
{
    /// <summary>
    /// Sends effective Timeline Playback DMX buffers from one or more rigs to an ArtNetSender.
    /// This component never forwards Live Art-Net or External Injection buffers.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class DmxTimelineArtNetOutput : MonoBehaviour
    {
        public enum UniverseSelection { Specified, All }
        public enum RigSelection { Assigned, AllActive }

        [Header("Output")]
        [Tooltip("Explicitly enables Art-Net output. Off by default.")]
        [SerializeField] private bool enableOutput;
        [Tooltip("ArtNetSender used for output.")]
        [SerializeField] private ArtNetSender sender;
        [Tooltip("Allow output in Edit Mode. Use care with physical fixtures.")]
        [SerializeField] private bool sendInEditMode;
        [Tooltip("If assigned, send only while the director is playing. Otherwise follow Timeline updates on the rigs.")]
        [SerializeField] private PlayableDirector director;

        [Header("Rig")]
        [Tooltip("Assigned uses list order as priority. All Active discovers enabled rigs.")]
        [SerializeField] private RigSelection rigSelection = RigSelection.Assigned;
        [SerializeField] private List<DmxRigController> rigs = new List<DmxRigController>();

        [Header("Universe")]
        [Tooltip("Specified sends only listed numbers; All sends every active Timeline Universe.")]
        [SerializeField] private UniverseSelection universeSelection = UniverseSelection.Specified;
        [Tooltip("Art-Net Port-Address (0–32767). Duplicate numbers are sent once.")]
        [SerializeField] private List<int> universes = new List<int> { 0 };
        [Range(1, 60)]
        [Tooltip("Maximum checks per second. Unchanged buffers are resent at the keepalive interval.")]
        [SerializeField] private int maxSendRate = 30;
        [Tooltip("Interval in seconds for resending unchanged data. Set 0 to disable.")]
        [Min(0f)] [SerializeField] private float resendInterval = 1f;

        private readonly List<DmxRigController> _activeRigs = new List<DmxRigController>();
        private readonly List<int> _universeScratch = new List<int>();
        private readonly HashSet<int> _claimedUniverses = new HashSet<int>();
        private readonly Dictionary<int, SentState> _lastSent = new Dictionary<int, SentState>();
        private readonly byte[] _bytes = new byte[512];
        private readonly int[] _channels = new int[512];
        private double _nextCheckTime;
        private bool _openedSenderInEditMode;
        private ArtNetSender _openedSender;

        private struct SentState
        {
            public DmxRigController rig;
            public long revision;
            public double sentAt;
        }

        public bool OutputEnabled => enableOutput;
        public int PacketsSent { get; private set; }
        public int LastUniverse { get; private set; } = -1;
        public string LastError { get; private set; }
        public int ActiveRigCount => _activeRigs.Count;

        private void OnEnable()
        {
            RefreshRigs();
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
#if UNITY_EDITOR
            EditorApplication.update += OnEditorUpdate;
#endif
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
#if UNITY_EDITOR
            EditorApplication.update -= OnEditorUpdate;
#endif
            CloseOwnedSender();
            _lastSent.Clear();
        }

        private void OnValidate()
        {
            maxSendRate = Mathf.Clamp(maxSendRate, 1, 60);
            resendInterval = Mathf.Max(0f, resendInterval);
            _nextCheckTime = 0;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => RefreshRigs();
        private void OnSceneUnloaded(Scene scene) => RefreshRigs();

        /// <summary>Refreshes the cached Rig selection without searching on every frame.</summary>
        public void RefreshRigs()
        {
            _activeRigs.Clear();
            if (rigSelection == RigSelection.AllActive)
            {
                var found = FindObjectsByType<DmxRigController>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);
                for (int i = 0; i < found.Length; i++)
                    if (found[i].isActiveAndEnabled) _activeRigs.Add(found[i]);
            }
            else
            {
                for (int i = 0; i < rigs.Count; i++)
                    if (rigs[i] != null && rigs[i].isActiveAndEnabled && !_activeRigs.Contains(rigs[i]))
                        _activeRigs.Add(rigs[i]);

                // A scene with exactly one Rig needs no manual wiring.
                if (_activeRigs.Count == 0 && rigs.Count == 0)
                {
                    var found = FindObjectsByType<DmxRigController>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);
                    if (found.Length == 1 && found[0].isActiveAndEnabled)
                        _activeRigs.Add(found[0]);
                }
            }
            _lastSent.Clear();
        }

        private void Update()
        {
            if (Application.isPlaying) Tick(false);
        }

#if UNITY_EDITOR
        private void OnEditorUpdate()
        {
            if (!Application.isPlaying && this != null && isActiveAndEnabled)
                Tick(false);
        }
#endif

        /// <summary>Sends the current eligible buffers once, even when revisions have not changed.</summary>
        public void SendCurrentFrameOnce() => Tick(true);

        private void Tick(bool force)
        {
            if (!enableOutput || (!Application.isPlaying && !sendInEditMode))
            {
                CloseOwnedSender();
                return;
            }
            if (director != null && director.state != PlayState.Playing)
            {
                _lastSent.Clear();
                return;
            }

            double now = Time.realtimeSinceStartupAsDouble;
            if (!force && now < _nextCheckTime) return;
            _nextCheckTime = now + 1.0 / maxSendRate;

            if (sender == null || !sender.isActiveAndEnabled)
            {
                LastError = "ArtNetSender is not assigned or enabled.";
                return;
            }
            if (_openedSenderInEditMode && _openedSender != sender) CloseOwnedSender();
            if (!Application.isPlaying && !sender.IsActive)
            {
                try
                {
                    sender.Open();
                    _openedSenderInEditMode = true;
                    _openedSender = sender;
                }
                catch (Exception e)
                {
                    LastError = "Failed to open the sender: " + e.Message;
                    return;
                }
            }
            if (!sender.IsActive)
            {
                LastError = "ArtNetSender is not open.";
                return;
            }

            // The first eligible Rig owns a Universe. The same Port-Address is never
            // emitted twice in one tick, even if several rigs expose it.
            _claimedUniverses.Clear();
            for (int rigIndex = 0; rigIndex < _activeRigs.Count; rigIndex++)
            {
                var rig = _activeRigs[rigIndex];
                if (rig == null || !rig.isActiveAndEnabled) continue;

                if (universeSelection == UniverseSelection.All)
                {
                    rig.CopyActiveUniverseNumbers(_universeScratch);
                    _universeScratch.Sort();
                }
                else
                {
                    _universeScratch.Clear();
                    _universeScratch.AddRange(universes);
                }

                for (int i = 0; i < _universeScratch.Count; i++)
                {
                    int universe = _universeScratch[i];
                    if (universe < 0 || universe > 32767 || _claimedUniverses.Contains(universe)) continue;
                    if (!rig.TryGetUniverseBufferRevision(universe, out var source, out long revision) ||
                        source != DmxRigController.UniverseBufferSource.TimelinePlayback || revision == 0) continue;

                    if (!force && _lastSent.TryGetValue(universe, out var previous) &&
                        previous.rig == rig && previous.revision == revision &&
                        (resendInterval <= 0f || now - previous.sentAt < resendInterval))
                    {
                        _claimedUniverses.Add(universe);
                        continue;
                    }

                    if (!rig.TryCopyUniverseBuffer(universe, _bytes, out source, out revision) ||
                        source != DmxRigController.UniverseBufferSource.TimelinePlayback) continue;
                    _claimedUniverses.Add(universe);
                    for (int channel = 0; channel < 512; channel++) _channels[channel] = _bytes[channel];

                    try
                    {
                        sender.Send(_channels, universe: universe);
                        _lastSent[universe] = new SentState { rig = rig, revision = revision, sentAt = now };
                        PacketsSent++;
                        LastUniverse = universe;
                        LastError = null;
                    }
                    catch (Exception e)
                    {
                        LastError = "Failed to send DMX: " + e.Message;
                    }
                }
            }
        }

        private void CloseOwnedSender()
        {
            if (!_openedSenderInEditMode) return;
            if (_openedSender != null) _openedSender.Close();
            _openedSender = null;
            _openedSenderInEditMode = false;
        }
    }
}
