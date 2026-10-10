using System;
using System.Collections.Generic;
using ArtNet.Runtime;
using UnityEditor;
using UnityEngine;

namespace ArtNet.Editor
{
    internal sealed class DmxActivityMonitorWindow : EditorWindow
    {
        private enum SourceMode { LiveInput, RigOutput }

        private const float ToolbarHeight = 84f;
        private const float UniverseColumnWidth = 72f;
        private const float ActivityCellWidth = 40f;
        private const float ActivityCellHeight = 46f;
        private const int DefaultUniverseCount = 64;
        private const double RefreshInterval = 1d / 20d;
        [SerializeField] private SourceMode _sourceMode;
        [SerializeField] private DmxRigController _rig;
        [SerializeField] private int _startUniverse;
        [SerializeField] private int _universeCount = DefaultUniverseCount;

        private readonly Dictionary<ArtNetReceiver, Action<ArtNetData>> _subscriptions = new Dictionary<ArtNetReceiver, Action<ArtNetData>>();
        private readonly Dictionary<int, Frame> _liveFrames = new Dictionary<int, Frame>();
        private readonly Dictionary<int, long> _rigRevisions = new Dictionary<int, long>();
        private readonly byte[] _rigChannels = new byte[DmxChannelMonitorModel.ChannelCount];
        private readonly List<ActivityChannel> _activeChannels = new List<ActivityChannel>();
        private GUIStyle _universeStyle;
        private GUIStyle _valueStyle;
        private Vector2 _scrollPosition;
        private double _nextRefreshAt;

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
            if (mode != _sourceMode) { _sourceMode = mode; RefreshSubscriptions(); TryAutoSelectRig(); }
            if (_sourceMode == SourceMode.RigOutput)
            {
                EditorGUI.LabelField(new Rect(x, 8f, 24f, 20f), "Rig"); x += 26f;
                _rig = (DmxRigController)EditorGUI.ObjectField(new Rect(x, 8f, 210f, 20f), _rig, typeof(DmxRigController), true); x += 220f;
            }
            x = 10f;
            EditorGUI.LabelField(new Rect(x, 36f, 32f, 20f), "Start"); x += 34f;
            _startUniverse = Mathf.Clamp(EditorGUI.IntField(new Rect(x, 36f, 55f, 20f), _startUniverse), 0, 32767); x += 64f;
            EditorGUI.LabelField(new Rect(x, 36f, 36f, 20f), "Count"); x += 38f;
            _universeCount = Mathf.Clamp(EditorGUI.IntField(new Rect(x, 36f, 45f, 20f), _universeCount), 1, 256); x += 55f;
            if (GUI.Button(new Rect(x, 36f, 110f, 20f), "Clear Activity")) { _liveFrames.Clear(); _rigRevisions.Clear(); _activeChannels.Clear(); }
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

            int cursor = 0;
            int row = 0;
            while (cursor < _activeChannels.Count)
            {
                int universe = _activeChannels[cursor].Universe;
                float y = 24f + row * ActivityCellHeight;
                GUI.Label(new Rect(4f, y, UniverseColumnWidth - 4f, ActivityCellHeight), universe.ToString(), _valueStyle);
                int column = 0;
                while (cursor < _activeChannels.Count && _activeChannels[cursor].Universe == universe)
                {
                    var activity = _activeChannels[cursor++];
                    var rect = new Rect(UniverseColumnWidth + column * ActivityCellWidth, y, ActivityCellWidth - 2f, ActivityCellHeight - 2f);
                var idle = EditorGUIUtility.isProSkin ? new Color(.055f, .06f, .068f) : new Color(.78f, .8f, .82f);
                EditorGUI.DrawRect(rect, Color.Lerp(idle, new Color(.015f, .37f, .64f), activity.Value / 255f));
                    GUI.Label(new Rect(rect.x, rect.y + 2f, rect.width, 16f), (activity.Channel + 1).ToString(), _universeStyle);
                GUI.Label(new Rect(rect.x, rect.y + 19f, rect.width, rect.height - 20f), activity.Value.ToString(), _valueStyle);
                    column++;
                }
                row++;
            }
            GUI.EndScrollView();
        }

        private int GetActivityValue(int universe)
        {
            if (_sourceMode == SourceMode.RigOutput)
            {
                if (_rig == null || !_rig.TryCopyUniverseBuffer(universe, _rigChannels, out _, out long revision)) return 0;
                _rigRevisions[universe] = revision;
                return GetMax(_rigChannels);
            }
            return _liveFrames.TryGetValue(universe, out var frame) ? GetMax(frame.Data.Channels) : 0;
        }

        private void OnEditorUpdate()
        {
            if (EditorApplication.timeSinceStartup < _nextRefreshAt) return;
            _nextRefreshAt = EditorApplication.timeSinceStartup + RefreshInterval;
            TryAutoSelectRig();
            RefreshActiveChannels();
            Repaint();
        }

        private void RefreshActiveChannels()
        {
            _activeChannels.Clear();
            for (int universe = _startUniverse; universe < _startUniverse + _universeCount; universe++)
            {
                if (_sourceMode == SourceMode.RigOutput)
                {
                    if (_rig == null || !_rig.TryCopyUniverseBuffer(universe, _rigChannels, out _, out long revision)) continue;
                    _rigRevisions[universe] = revision;
                    AddActiveChannels(universe, _rigChannels);
                }
                else if (_liveFrames.TryGetValue(universe, out var frame))
                    AddActiveChannels(universe, frame.Data.Channels);
            }
        }

        private void AddActiveChannels(int universe, byte[] channels)
        {
            for (int i = 0; i < channels.Length; i++) if (channels[i] > 0) _activeChannels.Add(new ActivityChannel(universe, i, channels[i]));
        }

        private void AddActiveChannels(int universe, int[] channels)
        {
            for (int i = 0; i < channels.Length; i++) if (channels[i] > 0) _activeChannels.Add(new ActivityChannel(universe, i, Mathf.Clamp(channels[i], 0, 255)));
        }

        private void OnHierarchyChanged() { RefreshSubscriptions(); TryAutoSelectRig(); }

        private void RefreshSubscriptions()
        {
            if (_sourceMode == SourceMode.RigOutput) { RemoveSubscriptions(); return; }
            var receivers = FindObjectsByType<ArtNetReceiver>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var wanted = new HashSet<ArtNetReceiver>(receivers);
            var remove = new List<ArtNetReceiver>();
            foreach (var pair in _subscriptions) if (pair.Key == null || !wanted.Contains(pair.Key)) remove.Add(pair.Key);
            foreach (var receiver in remove) { if (receiver != null) receiver.OnDataReceived -= _subscriptions[receiver]; _subscriptions.Remove(receiver); }
            foreach (var receiver in receivers)
            {
                if (receiver == null || _subscriptions.ContainsKey(receiver)) continue;
                Action<ArtNetData> handler = data => { if (data.OpCode == ArtNetOpCode.OpDmx && data.Channels != null) _liveFrames[data.Universe] = new Frame(data); };
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
            if (_sourceMode != SourceMode.RigOutput || _rig != null) return;
            var rigs = FindObjectsByType<DmxRigController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (rigs.Length == 1) _rig = rigs[0];
        }

        private void EnsureStyles()
        {
            if (_universeStyle != null) return;
            _universeStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
            _valueStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter, fontSize = 14 };
        }

        private static int GetMax(int[] values) { int max = 0; if (values != null) for (int i = 0; i < values.Length; i++) max = Mathf.Max(max, values[i]); return Mathf.Clamp(max, 0, 255); }
        private static int GetMax(byte[] values) { int max = 0; if (values != null) for (int i = 0; i < values.Length; i++) max = Mathf.Max(max, values[i]); return max; }
        private readonly struct Frame { public readonly ArtNetData Data; public Frame(ArtNetData data) { Data = data; } }
        private readonly struct ActivityChannel { public readonly int Universe; public readonly int Channel; public readonly int Value; public ActivityChannel(int universe, int channel, int value) { Universe = universe; Channel = channel; Value = value; } }
    }
}
