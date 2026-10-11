using System;
using System.Collections.Generic;
using ArtNet.Runtime;
using UnityEditor;
using UnityEngine;

namespace ArtNet.Editor
{
    internal sealed class DmxActivityMonitorWindow : EditorWindow
    {
        private enum SourceMode { LiveInput, TimelinePlayback }

        private const float ToolbarHeight = 84f;
        private const float UniverseColumnWidth = 72f;
        private const float ActivityCellWidth = 40f;
        private const float ActivityCellHeight = 46f;
        private const int DefaultUniverseCount = 64;
        private const double RefreshInterval = 1d / 20d;
        private static readonly string[] AddressLabels = BuildLabels(DmxChannelMonitorModel.ChannelCount, 1);
        private static readonly string[] ValueLabels = BuildLabels(256, 0);
        [SerializeField] private SourceMode _sourceMode;
        [SerializeField] private DmxRigController _rig;
        [SerializeField] private int _startUniverse;
        [SerializeField] private int _universeCount = DefaultUniverseCount;

        private readonly Dictionary<ArtNetReceiver, Action<ArtNetData>> _subscriptions = new Dictionary<ArtNetReceiver, Action<ArtNetData>>();
        private readonly Dictionary<int, Frame> _liveFrames = new Dictionary<int, Frame>();
        private readonly Dictionary<int, long> _liveRevisions = new Dictionary<int, long>();
        private readonly Dictionary<int, long> _rigRevisions = new Dictionary<int, long>();
        private readonly Dictionary<int, List<ActivityChannel>> _activeChannelsByUniverse = new Dictionary<int, List<ActivityChannel>>();
        private readonly byte[] _rigChannels = new byte[DmxChannelMonitorModel.ChannelCount];
        private readonly List<ActivityChannel> _activeChannels = new List<ActivityChannel>();
        private GUIStyle _universeStyle;
        private GUIStyle _valueStyle;
        private Vector2 _scrollPosition;
        private double _nextRefreshAt;
        private long _nextLiveFrameRevision;
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
            RefreshSubscriptions();
            TryAutoSelectRig();
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            foreach (var pair in _subscriptions)
                if (pair.Key != null) pair.Key.OnDataReceived -= pair.Value;
            _subscriptions.Clear();
        }

        private void OnGUI()
        {
            EnsureStyles();
            DrawToolbar();
            DrawGrid(new Rect(0f, ToolbarHeight, position.width, Mathf.Max(0f, position.height - ToolbarHeight)));
        }

        private void DrawToolbar()
        {
            EditorGUI.DrawRect(new Rect(0f, 0f, position.width, ToolbarHeight), EditorGUIUtility.isProSkin ? new Color(.075f, .075f, .075f) : new Color(.76f, .76f, .76f));
            float x = 10f;
            EditorGUI.LabelField(new Rect(x, 8f, 42f, 20f), "Mode"); x += 44f;
            var mode = (SourceMode)EditorGUI.EnumPopup(new Rect(x, 8f, 105f, 20f), _sourceMode); x += 115f;
            if (mode != _sourceMode) { _sourceMode = mode; ResetActivityCache(); RefreshSubscriptions(); TryAutoSelectRig(); }
            if (_sourceMode == SourceMode.TimelinePlayback)
            {
                EditorGUI.LabelField(new Rect(x, 8f, 24f, 20f), "Rig"); x += 26f;
                var rig = (DmxRigController)EditorGUI.ObjectField(new Rect(x, 8f, 210f, 20f), _rig, typeof(DmxRigController), true);
                if (rig != _rig) { _rig = rig; ResetActivityCache(); }
                x += 220f;
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
                        GUI.Label(new Rect(rect.x, rect.y + 2f, rect.width, 16f), AddressLabels[activity.Channel], _universeStyle);
                        GUI.Label(new Rect(rect.x, rect.y + 19f, rect.width, rect.height - 20f), ValueLabels[activity.Value], _valueStyle);
                    }
                    column++;
                }
                row++;
            }
            GUI.EndScrollView();
        }

        private void OnEditorUpdate()
        {
            if (EditorApplication.timeSinceStartup < _nextRefreshAt) return;
            _nextRefreshAt = EditorApplication.timeSinceStartup + RefreshInterval;
            TryAutoSelectRig();
            if (RefreshActiveChannels()) Repaint();
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

        private void RefreshSubscriptions()
        {
            if (_sourceMode == SourceMode.TimelinePlayback) { RemoveSubscriptions(); return; }
            var receivers = FindObjectsByType<ArtNetReceiver>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var wanted = new HashSet<ArtNetReceiver>(receivers);
            var remove = new List<ArtNetReceiver>();
            foreach (var pair in _subscriptions) if (pair.Key == null || !wanted.Contains(pair.Key)) remove.Add(pair.Key);
            foreach (var receiver in remove) { if (receiver != null) receiver.OnDataReceived -= _subscriptions[receiver]; _subscriptions.Remove(receiver); }
            foreach (var receiver in receivers)
            {
                if (receiver == null || _subscriptions.ContainsKey(receiver)) continue;
                Action<ArtNetData> handler = data =>
                {
                    if (data.OpCode == ArtNetOpCode.OpDmx && data.Channels != null)
                        _liveFrames[data.Universe] = new Frame(data, ++_nextLiveFrameRevision);
                };
                receiver.OnDataReceived += handler;
                _subscriptions.Add(receiver, handler);
            }
        }

        private void RemoveSubscriptions()
        {
            foreach (var pair in _subscriptions) if (pair.Key != null) pair.Key.OnDataReceived -= pair.Value;
            _subscriptions.Clear();
        }

        private void TryAutoSelectRig()
        {
            if (_sourceMode != SourceMode.TimelinePlayback || _rig != null) return;
            var rigs = FindObjectsByType<DmxRigController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (rigs.Length == 1) _rig = rigs[0];
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
        }

        private void EnsureStyles()
        {
            if (_universeStyle != null) return;
            _universeStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
            _valueStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter, fontSize = 14 };
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

            public Frame(ArtNetData data, long revision)
            {
                Data = data;
                Revision = revision;
            }
        }
        private readonly struct ActivityChannel { public readonly int Universe; public readonly int Channel; public readonly int Value; public ActivityChannel(int universe, int channel, int value) { Universe = universe; Channel = channel; Value = value; } }
    }
}
