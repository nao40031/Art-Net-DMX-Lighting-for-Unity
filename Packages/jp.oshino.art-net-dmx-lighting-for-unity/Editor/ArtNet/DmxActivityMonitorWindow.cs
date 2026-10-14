using System;
using System.Collections.Generic;
using ArtNet.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArtNet.Editor
{
    internal sealed class DmxActivityMonitorWindow : EditorWindow
    {
        private enum SourceMode { LiveInput, TimelinePlayback, SenderOutput }

        private const float ToolbarHeight = 84f;
        private const float UniverseColumnWidth = 72f;
        private const float ActivityCellWidth = 40f;
        private const float ActivityCellHeight = 46f;
        private const int DefaultUniverseCount = 64;
        private const double RefreshInterval = 1d / 20d;
        private const float HistoryHeight = 96f;
        private const int HistorySampleCapacity = 180;
        private const double HistorySampleInterval = 1d / 15d;
        private const double HistoryDurationSeconds = 10d;
        private static readonly string[] SourceModeLabels = { "Live", "Timeline Playback", "Sender Output" };
        private static readonly string[] AddressLabels = BuildLabels(DmxChannelMonitorModel.ChannelCount, 1);
        private static readonly string[] ValueLabels = BuildLabels(256, 0);
        private static readonly int[] HistoryValueTicks = { 255, 192, 128, 64, 0 };
        private static readonly string[] HistoryValueLabels = { "255", "192", "128", "64", "0" };
        private static readonly string[] HistoryTimeLabels = { "-10s", "-5s", "Now" };
        [SerializeField] private SourceMode _sourceMode;
        [SerializeField] private DmxRigController _rig;
        [SerializeField] private ArtNetSender _sender;
        [SerializeField] private int _startUniverse;
        [SerializeField] private int _universeCount = DefaultUniverseCount;
        [SerializeField] private int _selectedHistoryUniverse = -1;
        [SerializeField] private int _selectedHistoryChannel = 1;

        private readonly Dictionary<ArtNetReceiver, Action<ArtNetData>> _subscriptions = new Dictionary<ArtNetReceiver, Action<ArtNetData>>();
        private readonly Dictionary<ArtNetSender, Action<ArtNetData>> _senderSubscriptions = new Dictionary<ArtNetSender, Action<ArtNetData>>();
        private readonly Dictionary<int, Frame> _liveFrames = new Dictionary<int, Frame>();
        private readonly Dictionary<int, long> _liveRevisions = new Dictionary<int, long>();
        private readonly Dictionary<int, long> _rigRevisions = new Dictionary<int, long>();
        private readonly Dictionary<int, List<ActivityChannel>> _activeChannelsByUniverse = new Dictionary<int, List<ActivityChannel>>();
        private readonly byte[] _rigChannels = new byte[DmxChannelMonitorModel.ChannelCount];
        private readonly List<ActivityChannel> _activeChannels = new List<ActivityChannel>();
        private readonly HistorySample[] _historySamples = new HistorySample[HistorySampleCapacity];
        private GUIStyle _universeStyle;
        private GUIStyle _valueStyle;
        private GUIStyle _historyAxisStyle;
        private GUIStyle _historyTimeStyle;
        private string _historySelectionLabel;
        private Vector2 _scrollPosition;
        private double _nextRefreshAt;
        private double _nextHistorySampleAt;
        private long _nextLiveFrameRevision;
        private int _historySampleStart;
        private int _historySampleCount;
        private int _historySourceId = int.MinValue;
        private bool _activityDisplayDirty = true;

        [MenuItem("Art-Net/Monitoring/DMX Activity Monitor")]
        private static void Open()
        {
            var window = GetWindow<DmxActivityMonitorWindow>();
            window.titleContent = new GUIContent("DMX Activity Monitor");
            window.minSize = new Vector2(600f, 360f);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            RefreshSubscriptions();
            TryAutoSelectRig();
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            foreach (var pair in _subscriptions)
                if (pair.Key != null) pair.Key.OnDataReceived -= pair.Value;
            _subscriptions.Clear();
            RemoveSenderSubscriptions();
        }

        private void OnGUI()
        {
            EnsureStyles();
            DrawToolbar();
            float historyHeight = _selectedHistoryUniverse >= 0
                ? Mathf.Min(HistoryHeight, Mathf.Max(0f, position.height - ToolbarHeight - 120f))
                : 0f;
            float historyY = position.height - historyHeight;
            DrawGrid(new Rect(0f, ToolbarHeight, position.width, Mathf.Max(0f, historyY - ToolbarHeight)));
            if (historyHeight > 0f)
                DrawHistory(new Rect(0f, historyY, position.width, historyHeight));
        }

        private void DrawToolbar()
        {
            EditorGUI.DrawRect(new Rect(0f, 0f, position.width, ToolbarHeight), EditorGUIUtility.isProSkin ? new Color(.075f, .075f, .075f) : new Color(.76f, .76f, .76f));
            float x = 10f;
            EditorGUI.LabelField(new Rect(x, 8f, 42f, 20f), "Mode"); x += 44f;
            var mode = (SourceMode)EditorGUI.Popup(new Rect(x, 8f, 138f, 20f), (int)_sourceMode, SourceModeLabels); x += 148f;
            if (mode != _sourceMode) { _sourceMode = mode; _liveFrames.Clear(); ResetActivityCache(); RefreshSubscriptions(); TryAutoSelectRig(); }
            if (_sourceMode == SourceMode.TimelinePlayback)
            {
                EditorGUI.LabelField(new Rect(x, 8f, 24f, 20f), "Rig"); x += 26f;
                var rig = (DmxRigController)EditorGUI.ObjectField(new Rect(x, 8f, 210f, 20f), _rig, typeof(DmxRigController), true);
                if (rig != _rig) { _rig = rig; ResetActivityCache(); }
                x += 220f;
            }
            else if (_sourceMode == SourceMode.SenderOutput)
            {
                EditorGUI.LabelField(new Rect(x, 8f, 46f, 20f), "Sender"); x += 48f;
                var sender = (ArtNetSender)EditorGUI.ObjectField(new Rect(x, 8f, 210f, 20f), _sender, typeof(ArtNetSender), true);
                if (sender != _sender) { _sender = sender; _liveFrames.Clear(); ResetActivityCache(); RefreshSubscriptions(); }
            }
            x = 10f;
            EditorGUI.LabelField(new Rect(x, 36f, 32f, 20f), "Start"); x += 34f;
            int startUniverse = Mathf.Clamp(EditorGUI.IntField(new Rect(x, 36f, 55f, 20f), _startUniverse), 0, 32767);
            if (startUniverse != _startUniverse) { _startUniverse = startUniverse; ResetActivityCache(); }
            x += 64f;
            EditorGUI.LabelField(new Rect(x, 36f, 36f, 20f), "Count"); x += 38f;
            int universeCount = Mathf.Clamp(EditorGUI.IntField(new Rect(x, 36f, 45f, 20f), _universeCount), 1, 256);
            if (universeCount != _universeCount) { _universeCount = universeCount; ResetActivityCache(); }
            x += 55f;
            if (GUI.Button(new Rect(x, 36f, 110f, 20f), "Clear Activity")) { _liveFrames.Clear(); ResetActivityCache(); }
            GUI.Label(new Rect(10f, 60f, position.width - 20f, 18f), "Only channels with a DMX value above 0 are displayed.", EditorStyles.miniLabel);
            if (_selectedHistoryUniverse >= 0 &&
                (_selectedHistoryUniverse < _startUniverse || _selectedHistoryUniverse >= _startUniverse + _universeCount))
                _selectedHistoryUniverse = -1;
        }

        private void DrawGrid(Rect area)
        {
            int rowCount = 0;
            int maxChannelsInRow = 0;
            for (int i = 0; i < _activeChannels.Count;)
            {
                int universe = _activeChannels[i].Universe;
                int count = 0;
                while (i < _activeChannels.Count && _activeChannels[i].Universe == universe) { count++; i++; }
                rowCount++;
                maxChannelsInRow = Mathf.Max(maxChannelsInRow, count);
            }

            float contentWidth = Mathf.Max(area.width - 16f, UniverseColumnWidth + maxChannelsInRow * ActivityCellWidth + 8f);
            float contentHeight = Mathf.Max(area.height, 24f + rowCount * ActivityCellHeight + 8f);
            _scrollPosition = GUI.BeginScrollView(area, _scrollPosition, new Rect(0f, 0f, contentWidth, contentHeight));
            GUI.Label(new Rect(4f, 2f, UniverseColumnWidth - 4f, 20f), "Universe", EditorStyles.boldLabel);
            GUI.Label(new Rect(UniverseColumnWidth, 2f, contentWidth - UniverseColumnWidth, 20f), "Addr / Value", EditorStyles.boldLabel);

            int firstVisibleRow = Mathf.Max(0, Mathf.FloorToInt((_scrollPosition.y - 24f) / ActivityCellHeight));
            int lastVisibleRow = Mathf.CeilToInt((_scrollPosition.y + area.height - 24f) / ActivityCellHeight);
            Color idle = EditorGUIUtility.isProSkin ? new Color(.055f, .06f, .068f) : new Color(.78f, .8f, .82f);
            int cursor = 0;
            int row = 0;
            while (cursor < _activeChannels.Count)
            {
                int universe = _activeChannels[cursor].Universe;
                float y = 24f + row * ActivityCellHeight;
                bool visible = row >= firstVisibleRow && row <= lastVisibleRow;
                if (visible)
                    GUI.Label(new Rect(4f, y, UniverseColumnWidth - 4f, ActivityCellHeight), universe.ToString(), _valueStyle);
                int column = 0;
                while (cursor < _activeChannels.Count && _activeChannels[cursor].Universe == universe)
                {
                    var activity = _activeChannels[cursor++];
                    if (visible)
                    {
                        var rect = new Rect(UniverseColumnWidth + column * ActivityCellWidth, y, ActivityCellWidth - 2f, ActivityCellHeight - 2f);
                        EditorGUI.DrawRect(rect, Color.Lerp(idle, new Color(.015f, .37f, .64f), activity.Value / 255f));
                        if (activity.Universe == _selectedHistoryUniverse && activity.Channel + 1 == _selectedHistoryChannel)
                        {
                            const float border = 2f;
                            Color selected = new Color(.22f, .76f, 1f);
                            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, border), selected);
                            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - border, rect.width, border), selected);
                            EditorGUI.DrawRect(new Rect(rect.x, rect.y, border, rect.height), selected);
                            EditorGUI.DrawRect(new Rect(rect.xMax - border, rect.y, border, rect.height), selected);
                        }
                        GUI.Label(new Rect(rect.x, rect.y + 2f, rect.width, 16f), AddressLabels[activity.Channel], _universeStyle);
                        GUI.Label(new Rect(rect.x, rect.y + 19f, rect.width, rect.height - 20f), ValueLabels[activity.Value], _valueStyle);
                        if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && rect.Contains(Event.current.mousePosition))
                        {
                            _selectedHistoryUniverse = activity.Universe;
                            _selectedHistoryChannel = activity.Channel + 1;
                            _historySelectionLabel = null;
                            ResetHistory();
                            Event.current.Use();
                            Repaint();
                        }
                    }
                    column++;
                }
                row++;
            }
            GUI.EndScrollView();
        }

        private void DrawHistory(Rect rect)
        {
            if (_historySelectionLabel == null)
                _historySelectionLabel = $"Universe {_selectedHistoryUniverse}  Ch {_selectedHistoryChannel}";
            bool proSkin = EditorGUIUtility.isProSkin;
            EditorGUI.DrawRect(rect, proSkin ? new Color(.055f, .06f, .068f) : new Color(.82f, .84f, .86f));
            EditorGUI.LabelField(new Rect(10f, rect.y + 6f, 160f, 20f), _historySelectionLabel);
            if (GUI.Button(new Rect(174f, rect.y + 6f, 92f, 20f), "Clear History"))
                ResetHistory();
            EditorGUI.LabelField(new Rect(276f, rect.y + 6f, 148f, 20f), "Last 10 seconds", EditorStyles.miniLabel);

            const float axisLabelWidth = 30f;
            var graph = new Rect(10f + axisLabelWidth, rect.y + 30f,
                Mathf.Max(0f, rect.width - axisLabelWidth - 20f), Mathf.Max(0f, rect.height - 50f));
            EditorGUI.DrawRect(graph, proSkin ? new Color(.035f, .04f, .048f) : new Color(.9f, .92f, .94f));
            Color gridColor = proSkin ? new Color(.13f, .2f, .24f) : new Color(.68f, .74f, .77f);
            for (int i = 0; i < HistoryValueTicks.Length; i++)
            {
                float y = graph.yMax - HistoryValueTicks[i] / 255f * graph.height;
                EditorGUI.DrawRect(new Rect(graph.x, y, graph.width, 1f), gridColor);
                GUI.Label(new Rect(8f, y - 7f, axisLabelWidth, 14f), HistoryValueLabels[i], _historyAxisStyle);
            }
            for (int i = 0; i < HistoryTimeLabels.Length; i++)
            {
                float x = graph.x + i * graph.width / 2f;
                EditorGUI.DrawRect(new Rect(x, graph.y, 1f, graph.height), gridColor);
                float labelX = i == 0 ? graph.x : i == HistoryTimeLabels.Length - 1 ? graph.xMax - 40f : x - 20f;
                GUI.Label(new Rect(labelX, graph.yMax + 2f, 40f, 14f), HistoryTimeLabels[i], _historyTimeStyle);
            }
            DrawHistoryGraph(graph, EditorApplication.timeSinceStartup);
        }

        private void DrawHistoryGraph(Rect graph, double now)
        {
            if (_historySampleCount == 0 || graph.width <= 0f || graph.height <= 0f)
                return;

            double earliestTime = now - HistoryDurationSeconds;
            bool hasPrevious = false;
            Vector3 previous = default;
            Color previousColor = Handles.color;
            Handles.color = new Color(.08f, .62f, .96f);
            for (int i = 0; i < _historySampleCount; i++)
            {
                HistorySample sample = _historySamples[(_historySampleStart + i) % HistorySampleCapacity];
                if (sample.Time < earliestTime)
                    continue;

                float x = graph.x + Mathf.Clamp01((float)((sample.Time - earliestTime) / HistoryDurationSeconds)) * graph.width;
                float y = graph.yMax - sample.Value / 255f * graph.height;
                Vector3 point = new Vector3(x, y, 0f);
                if (hasPrevious)
                    Handles.DrawLine(previous, point);
                previous = point;
                hasPrevious = true;
            }
            Handles.color = previousColor;
        }

        private void OnEditorUpdate()
        {
            double now = EditorApplication.timeSinceStartup;
            bool historyChanged = CaptureHistory(now);
            if (now < _nextRefreshAt)
            {
                if (historyChanged) Repaint();
                return;
            }

            _nextRefreshAt = now + RefreshInterval;
            TryAutoSelectSender();
            bool activityChanged = RefreshActiveChannels();
            if (activityChanged || historyChanged) Repaint();
        }

        private bool CaptureHistory(double now)
        {
            if (_selectedHistoryUniverse < _startUniverse ||
                _selectedHistoryUniverse >= _startUniverse + _universeCount ||
                now < _nextHistorySampleAt || !TryGetSelectedHistoryValue(out byte value, out int sourceId))
                return false;

            if (_historySourceId != sourceId)
            {
                ResetHistory();
                _historySourceId = sourceId;
            }
            _nextHistorySampleAt = now + HistorySampleInterval;
            if (_historySampleCount < HistorySampleCapacity)
            {
                int index = (_historySampleStart + _historySampleCount) % HistorySampleCapacity;
                _historySamples[index] = new HistorySample(now, value);
                _historySampleCount++;
            }
            else
            {
                _historySamples[_historySampleStart] = new HistorySample(now, value);
                _historySampleStart = (_historySampleStart + 1) % HistorySampleCapacity;
            }
            return true;
        }

        private bool TryGetSelectedHistoryValue(out byte value, out int sourceId)
        {
            value = 0;
            sourceId = int.MinValue;
            int channelIndex = _selectedHistoryChannel - 1;
            if (channelIndex < 0 || channelIndex >= DmxChannelMonitorModel.ChannelCount)
                return false;

            if (_sourceMode == SourceMode.TimelinePlayback)
            {
                if (_rig == null || !_rig.TryCopyUniverseBuffer(_selectedHistoryUniverse, _rigChannels, out _, out _))
                    return false;
                sourceId = _rig.GetInstanceID();
                value = _rigChannels[channelIndex];
                return true;
            }

            if (!_liveFrames.TryGetValue(_selectedHistoryUniverse, out var frame))
                return false;
            sourceId = frame.SourceId;
            int[] channels = frame.Data.Channels;
            value = (byte)(channels != null && channelIndex < channels.Length ? Mathf.Clamp(channels[channelIndex], 0, 255) : 0);
            return true;
        }

        private void ResetHistory()
        {
            _historySampleStart = 0;
            _historySampleCount = 0;
            _historySourceId = int.MinValue;
            _nextHistorySampleAt = 0d;
        }

        private bool RefreshActiveChannels()
        {
            bool changed = _activityDisplayDirty;
            for (int universe = _startUniverse; universe < _startUniverse + _universeCount; universe++)
            {
                if (_sourceMode == SourceMode.TimelinePlayback)
                {
                    if (_rig == null || !_rig.TryGetUniverseBufferRevision(universe, out _, out long revision))
                    {
                        changed |= RemoveActiveUniverse(universe);
                        _rigRevisions.Remove(universe);
                        continue;
                    }

                    if (_rigRevisions.TryGetValue(universe, out long previousRevision) && previousRevision == revision)
                        continue;

                    if (!_rig.TryCopyUniverseBuffer(universe, _rigChannels, out _, out revision))
                        continue;

                    _rigRevisions[universe] = revision;
                    UpdateActiveUniverse(universe, _rigChannels);
                    changed = true;
                }
                else if (_liveFrames.TryGetValue(universe, out var frame))
                {
                    if (_liveRevisions.TryGetValue(universe, out long previousRevision) && previousRevision == frame.Revision)
                        continue;

                    _liveRevisions[universe] = frame.Revision;
                    UpdateActiveUniverse(universe, frame.Data.Channels);
                    changed = true;
                }
                else
                {
                    changed |= RemoveActiveUniverse(universe);
                    _liveRevisions.Remove(universe);
                }
            }

            if (!changed) return false;

            _activeChannels.Clear();
            for (int universe = _startUniverse; universe < _startUniverse + _universeCount; universe++)
                if (_activeChannelsByUniverse.TryGetValue(universe, out var channels)) _activeChannels.AddRange(channels);
            _activityDisplayDirty = false;
            return true;
        }

        private void UpdateActiveUniverse(int universe, byte[] channels)
        {
            if (!_activeChannelsByUniverse.TryGetValue(universe, out var activeChannels))
            {
                activeChannels = new List<ActivityChannel>();
                _activeChannelsByUniverse.Add(universe, activeChannels);
            }

            activeChannels.Clear();
            for (int i = 0; i < channels.Length; i++) if (channels[i] > 0) activeChannels.Add(new ActivityChannel(universe, i, channels[i]));
        }

        private void UpdateActiveUniverse(int universe, int[] channels)
        {
            if (!_activeChannelsByUniverse.TryGetValue(universe, out var activeChannels))
            {
                activeChannels = new List<ActivityChannel>();
                _activeChannelsByUniverse.Add(universe, activeChannels);
            }

            activeChannels.Clear();
            for (int i = 0; i < channels.Length; i++) if (channels[i] > 0) activeChannels.Add(new ActivityChannel(universe, i, Mathf.Clamp(channels[i], 0, 255)));
        }

        private void OnHierarchyChanged() { RefreshSubscriptions(); TryAutoSelectRig(); }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode && state != PlayModeStateChange.EnteredPlayMode)
                return;

            TryAutoSelectRig();
        }

        private void RefreshSubscriptions()
        {
            if (_sourceMode == SourceMode.TimelinePlayback) { RemoveSubscriptions(); RemoveSenderSubscriptions(); return; }
            if (_sourceMode == SourceMode.SenderOutput) { RemoveSubscriptions(); RefreshSenderSubscriptions(); return; }
            var receivers = FindObjectsByType<ArtNetReceiver>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var wanted = new HashSet<ArtNetReceiver>(receivers);
            var remove = new List<ArtNetReceiver>();
            foreach (var pair in _subscriptions) if (pair.Key == null || !wanted.Contains(pair.Key)) remove.Add(pair.Key);
            foreach (var receiver in remove) { if (receiver != null) receiver.OnDataReceived -= _subscriptions[receiver]; _subscriptions.Remove(receiver); }
            foreach (var receiver in receivers)
            {
                if (receiver == null || _subscriptions.ContainsKey(receiver)) continue;
                int receiverId = receiver.GetInstanceID();
                Action<ArtNetData> handler = data =>
                {
                    if (data.OpCode == ArtNetOpCode.OpDmx && data.Channels != null)
                        _liveFrames[data.Universe] = new Frame(data, ++_nextLiveFrameRevision, receiverId);
                };
                receiver.OnDataReceived += handler;
                _subscriptions.Add(receiver, handler);
            }
            RemoveSenderSubscriptions();
        }

        private void RemoveSubscriptions()
        {
            foreach (var pair in _subscriptions) if (pair.Key != null) pair.Key.OnDataReceived -= pair.Value;
            _subscriptions.Clear();
        }

        private void RefreshSenderSubscriptions()
        {
            RemoveSenderSubscriptions();
            TryAutoSelectSender();
            if (_sender == null)
                return;

            ArtNetSender capturedSender = _sender;
            int senderId = capturedSender.GetInstanceID();
            Action<ArtNetData> handler = data =>
            {
                if (data.OpCode == ArtNetOpCode.OpDmx && data.Channels != null)
                    _liveFrames[data.Universe] = new Frame(data, ++_nextLiveFrameRevision, senderId);
            };
            capturedSender.OnDataSent += handler;
            _senderSubscriptions.Add(capturedSender, handler);
        }

        private void RemoveSenderSubscriptions()
        {
            foreach (var pair in _senderSubscriptions)
                if (pair.Key != null) pair.Key.OnDataSent -= pair.Value;
            _senderSubscriptions.Clear();
        }

        private void TryAutoSelectRig()
        {
            if (_sourceMode != SourceMode.TimelinePlayback || _rig != null) return;

            var rigs = FindObjectsByType<DmxRigController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var activeScene = SceneManager.GetActiveScene();
            DmxRigController activeSceneCandidate = null;
            DmxRigController activeSceneRig = null;
            DmxRigController uniqueActiveRig = null;
            DmxRigController uniqueRig = null;
            int loadedRigCount = 0;
            int activeSceneCandidateCount = 0;
            int activeSceneRigCount = 0;
            int activeRigCount = 0;

            for (int i = 0; i < rigs.Length; i++)
            {
                var rig = rigs[i];
                if (rig == null || !rig.gameObject.scene.IsValid() || !rig.gameObject.scene.isLoaded)
                    continue;

                uniqueRig = rig;
                loadedRigCount++;
                if (rig.isActiveAndEnabled)
                {
                    uniqueActiveRig = rig;
                    activeRigCount++;
                }

                if (rig.gameObject.scene != activeScene)
                    continue;

                activeSceneCandidate = rig;
                activeSceneCandidateCount++;
                if (rig.isActiveAndEnabled)
                {
                    activeSceneRig = rig;
                    activeSceneRigCount++;
                }
            }

            if (activeSceneRigCount == 1)
                _rig = activeSceneRig;
            else if (activeSceneCandidateCount == 1)
                _rig = activeSceneCandidate;
            else if (activeRigCount == 1)
                _rig = uniqueActiveRig;
            else if (activeRigCount == 0 && loadedRigCount == 1)
                _rig = uniqueRig;

            if (_rig != null)
                ResetActivityCache();
        }

        private void TryAutoSelectSender()
        {
            if (_sourceMode != SourceMode.SenderOutput || _sender != null) return;
            var senders = FindObjectsByType<ArtNetSender>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (senders.Length == 1) _sender = senders[0];
        }

        private bool RemoveActiveUniverse(int universe)
        {
            return _activeChannelsByUniverse.Remove(universe);
        }

        private void ResetActivityCache()
        {
            _liveRevisions.Clear();
            _rigRevisions.Clear();
            _activeChannelsByUniverse.Clear();
            _activeChannels.Clear();
            _activityDisplayDirty = true;
            ResetHistory();
        }

        private void EnsureStyles()
        {
            if (_universeStyle != null) return;
            _universeStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
            _valueStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter, fontSize = 14 };
            _historyAxisStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
            _historyAxisStyle.normal.textColor = EditorGUIUtility.isProSkin
                ? new Color(.46f, .66f, .78f)
                : new Color(.12f, .34f, .48f);
            _historyTimeStyle = new GUIStyle(_historyAxisStyle) { alignment = TextAnchor.MiddleCenter };
        }

        private static string[] BuildLabels(int count, int offset)
        {
            var labels = new string[count];
            for (int i = 0; i < count; i++) labels[i] = (i + offset).ToString();
            return labels;
        }

        private static int GetMax(int[] values)
        {
            int max = 0;
            if (values != null)
                for (int i = 0; i < values.Length; i++) max = Mathf.Max(max, values[i]);
            return Mathf.Clamp(max, 0, 255);
        }

        private static int GetMax(byte[] values)
        {
            int max = 0;
            if (values != null)
                for (int i = 0; i < values.Length; i++) max = Mathf.Max(max, values[i]);
            return max;
        }

        private readonly struct Frame
        {
            public readonly ArtNetData Data;
            public readonly long Revision;
            public readonly int SourceId;

            public Frame(ArtNetData data, long revision, int sourceId)
            {
                Data = data;
                Revision = revision;
                SourceId = sourceId;
            }
        }
        private readonly struct HistorySample
        {
            public readonly double Time;
            public readonly byte Value;

            public HistorySample(double time, byte value)
            {
                Time = time;
                Value = value;
            }
        }
        private readonly struct ActivityChannel { public readonly int Universe; public readonly int Channel; public readonly int Value; public ActivityChannel(int universe, int channel, int value) { Universe = universe; Channel = channel; Value = value; } }
    }
}
