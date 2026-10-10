/*!
 * Copyright (c) 2026 Oshino
 *
 * Released under the MIT license.
 * see https://opensource.org/licenses/MIT
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using ArtNet.Runtime;
using UnityEditor;
using UnityEngine;

namespace ArtNet.Editor
{
    internal sealed class DmxChannelMonitorWindow : EditorWindow
    {
        private enum MonitorSource
        {
            LiveInput,
            RigOutput,
        }

        private const string MenuPath = "Art-Net/Monitoring/DMX Channel Monitor";
        private const float ToolbarHeight = 66f;
        private const float PreferredCellWidth = 39f;
        private const float PreferredCellHeight = 43f;
        private const float MinimumCellWidth = 22f;
        private const float MinimumCellHeight = 24f;
        private const float MaximumCellWidth = 48f;
        private const float MaximumCellHeight = 53f;
        private const float CellGap = 2f;
        private const float GridPadding = 6f;
        private const int ColumnCount = 32;
        private const int RowCount = 16;
        private const double RepaintInterval = 1d / 30d;
        private const double ReceiverRefreshInterval = 1d;
        private const double StaleAfterSeconds = 1d;

        private static readonly string[] ChannelLabels = BuildNumberLabels(DmxChannelMonitorModel.ChannelCount, 1);
        private static readonly string[] ValueLabels = BuildNumberLabels(256, 0);

        [SerializeField] private bool _monitorAllReceivers = true;
        [SerializeField] private ArtNetReceiver _selectedReceiver;
        [SerializeField] private MonitorSource _monitorSource;
        [SerializeField] private DmxRigController _selectedRig;
        [SerializeField] private int _selectedUniverse;
        [SerializeField] private bool _paused;

        private readonly Dictionary<ArtNetReceiver, Action<ArtNetData>> _subscriptions =
            new Dictionary<ArtNetReceiver, Action<ArtNetData>>();
        private readonly Dictionary<ArtNetReceiver, Action<ArtNetData>> _backgroundSubscriptions =
            new Dictionary<ArtNetReceiver, Action<ArtNetData>>();
        private readonly HashSet<ArtNetReceiver> _desiredReceivers = new HashSet<ArtNetReceiver>();
        private readonly List<ArtNetReceiver> _receiverScratch = new List<ArtNetReceiver>();
        private readonly ConcurrentDictionary<LiveFrameKey, PendingLiveFrame> _pendingLiveFrames =
            new ConcurrentDictionary<LiveFrameKey, PendingLiveFrame>();
        private readonly byte[] _displayChannels = new byte[DmxChannelMonitorModel.ChannelCount];
        private readonly byte[] _rigChannels = new byte[DmxChannelMonitorModel.ChannelCount];

        private DmxChannelMonitorModel _model;
        private Vector2 _scrollPosition;
        private GUIStyle _channelStyle;
        private GUIStyle _valueStyle;
        private GUIStyle _statusStyle;
        private double _nextRepaintAt;
        private double _nextReceiverRefreshAt;

        [MenuItem(MenuPath)]
        private static void Open()
        {
            var window = GetWindow<DmxChannelMonitorWindow>();
            window.titleContent = new GUIContent("DMX Channel Monitor");
            window.minSize = new Vector2(900f, 500f);
            window.Show();
        }

        private void OnEnable()
        {
            _model = new DmxChannelMonitorModel();
            _model.SetPaused(_paused);
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            RefreshReceiverSubscriptions();
            TryAutoSelectRig();
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            RemoveAllSubscriptions();
            _pendingLiveFrames.Clear();
        }

        private void OnGUI()
        {
            EnsureStyles();
            DrawToolbar();
            DrawChannelGrid(new Rect(0f, ToolbarHeight, position.width, Mathf.Max(0f, position.height - ToolbarHeight)));
        }

        private void DrawToolbar()
        {
            bool proSkin = EditorGUIUtility.isProSkin;
            EditorGUI.DrawRect(new Rect(0f, 0f, position.width, ToolbarHeight),
                proSkin ? new Color(0.075f, 0.075f, 0.075f) : new Color(0.76f, 0.76f, 0.76f));

            float x = 10f;
            const float rowOneY = 8f;
            const float controlHeight = 20f;

            EditorGUI.LabelField(new Rect(x, rowOneY, 42f, controlHeight), "Mode");
            x += 44f;
            var monitorSource = (MonitorSource)EditorGUI.EnumPopup(
                new Rect(x, rowOneY, 108f, controlHeight), _monitorSource);
            if (monitorSource != _monitorSource)
            {
                _monitorSource = monitorSource;
                RefreshReceiverSubscriptions();
                _model.Clear();
                TryAutoSelectRig();
            }
            x += 118f;

            if (_monitorSource == MonitorSource.LiveInput)
            {
                bool monitorAll = EditorGUI.ToggleLeft(
                    new Rect(x, rowOneY, 145f, controlHeight), "Monitor All Receivers", _monitorAllReceivers);
                if (monitorAll != _monitorAllReceivers)
                {
                    _monitorAllReceivers = monitorAll;
                    RefreshReceiverSubscriptions();
                }
                x += 150f;

                using (new EditorGUI.DisabledScope(_monitorAllReceivers))
                {
                    var receiver = (ArtNetReceiver)EditorGUI.ObjectField(
                        new Rect(x, rowOneY, 190f, controlHeight), _selectedReceiver, typeof(ArtNetReceiver), true);
                    if (receiver != _selectedReceiver)
                    {
                        _selectedReceiver = receiver;
                        RefreshReceiverSubscriptions();
                    }
                }
                x += 200f;
            }
            else
            {
                EditorGUI.LabelField(new Rect(x, rowOneY, 24f, controlHeight), "Rig");
                x += 26f;
                var rig = (DmxRigController)EditorGUI.ObjectField(
                    new Rect(x, rowOneY, 255f, controlHeight), _selectedRig, typeof(DmxRigController), true);
                if (rig != _selectedRig)
                {
                    _selectedRig = rig;
                    _model.Clear();
                }
                x += 265f;
            }

            EditorGUI.LabelField(new Rect(x, rowOneY, 86f, controlHeight), "Local Universe");
            x += 88f;
            int universe = EditorGUI.IntField(new Rect(x, rowOneY, 62f, controlHeight), _selectedUniverse);
            _selectedUniverse = Mathf.Clamp(universe, 0, 32767);
            x += 72f;

            string pauseLabel = _paused ? "Resume" : "Pause";
            if (GUI.Button(new Rect(x, rowOneY, 70f, controlHeight), pauseLabel))
            {
                _paused = !_paused;
                _model.SetPaused(_paused);
                Repaint();
            }
            x += 78f;

            if (GUI.Button(new Rect(x, rowOneY, 142f, controlHeight), "Clear DMX Buffers"))
            {
                _model.Clear();
                Array.Clear(_displayChannels, 0, _displayChannels.Length);
                Repaint();
            }

            DrawStatus(new Rect(10f, 36f, Mathf.Max(0f, position.width - 20f), 22f));
        }

        private void DrawStatus(Rect rect)
        {
            double now = EditorApplication.timeSinceStartup;

            string text;
            Color color;
            if (!EditorApplication.isPlaying)
            {
                text = "Start Play Mode to monitor incoming ArtDMX.";
                color = new Color(0.62f, 0.62f, 0.62f);
            }
            else if (_monitorSource == MonitorSource.LiveInput && !_monitorAllReceivers && _selectedReceiver == null)
            {
                text = "Select an ArtNetReceiver.";
                color = new Color(0.72f, 0.62f, 0.34f);
            }
            else if (_monitorSource == MonitorSource.RigOutput && _selectedRig == null)
            {
                text = "Select a DmxRigController.";
                color = new Color(0.72f, 0.62f, 0.34f);
            }
            else if (!TryGetSelectedSnapshot(out var snapshot))
            {
                text = "No Data";
                color = new Color(0.72f, 0.62f, 0.34f);
            }
            else
            {
                double age = Math.Max(0d, now - snapshot.LastReceivedAt);
                string state = _paused ? "Paused" : age <= StaleAfterSeconds ? "Receiving" : "Stale";
                string frameDetails = snapshot.IsRigOutput
                    ? $"Updates: {snapshot.PacketsPerSecond:0.0}/s   Revision: {snapshot.Revision}"
                    : $"RX: {snapshot.PacketsPerSecond:0.0}/s   Seq: {snapshot.Sequence}   Phys: {snapshot.Physical}";
                text = $"{state}   Source: {snapshot.SourceName}   {frameDetails}   " +
                       $"Last: {age:0.00}s   Length: {snapshot.Length}";
                color = _paused
                    ? new Color(0.52f, 0.68f, 0.86f)
                    : age <= StaleAfterSeconds
                        ? new Color(0.34f, 0.78f, 0.88f)
                        : new Color(0.78f, 0.65f, 0.32f);
            }

            Color previous = GUI.color;
            GUI.color = color;
            GUI.Label(rect, text, _statusStyle);
            GUI.color = previous;
        }

        private void DrawChannelGrid(Rect viewport)
        {
            if (viewport.width <= 0f || viewport.height <= 0f)
                return;

            double now = EditorApplication.timeSinceStartup;
            DmxChannelMonitorModel.Snapshot snapshot = null;
            if (TryGetSelectedSnapshot(out snapshot))
                snapshot.CopyChannelsTo(_displayChannels);
            else
                Array.Clear(_displayChannels, 0, _displayChannels.Length);

            GetResponsiveGridMetrics(viewport, out float cellWidth, out float cellHeight,
                out float contentWidth, out float contentHeight, out float gridOriginX, out float gridOriginY);
            var content = new Rect(0f, 0f, contentWidth, contentHeight);
            _scrollPosition = GUI.BeginScrollView(viewport, _scrollPosition, content);

            UpdateGridStyleSizes(cellWidth, cellHeight);

            bool proSkin = EditorGUIUtility.isProSkin;
            Color zeroColor = proSkin ? new Color(0.055f, 0.06f, 0.068f) : new Color(0.78f, 0.8f, 0.82f);
            Color activeColor = proSkin ? new Color(0.015f, 0.37f, 0.64f) : new Color(0.12f, 0.48f, 0.74f);
            Color borderColor = proSkin ? new Color(0.12f, 0.14f, 0.16f) : new Color(0.48f, 0.5f, 0.53f);
            Color separatorColor = proSkin ? new Color(0.22f, 0.42f, 0.56f) : new Color(0.24f, 0.42f, 0.58f);

            for (int index = 0; index < DmxChannelMonitorModel.ChannelCount; index++)
            {
                int row = index / ColumnCount;
                int column = index % ColumnCount;
                var cell = new Rect(
                    gridOriginX + column * (cellWidth + CellGap),
                    gridOriginY + row * (cellHeight + CellGap),
                    cellWidth,
                    cellHeight);

                int value = _displayChannels[index];
                float normalized = value / 255f;
                Color background = value == 0
                    ? zeroColor
                    : Color.Lerp(zeroColor, activeColor, Mathf.Lerp(0.35f, 1f, normalized));

                if (snapshot != null)
                {
                    double changedAge = now - snapshot.GetChangedAt(index);
                    if (changedAge >= 0d && changedAge < 0.22d)
                    {
                        float flash = 1f - (float)(changedAge / 0.22d);
                        background = Color.Lerp(background, new Color(0.12f, 0.66f, 0.94f), flash * 0.55f);
                    }
                }

                EditorGUI.DrawRect(cell, borderColor);
                var inner = new Rect(cell.x + 1f, cell.y + 1f, cell.width - 2f, cell.height - 2f);
                EditorGUI.DrawRect(inner, background);
                float headerHeight = Mathf.Clamp(inner.height * 0.38f, 9f, 15f);
                float separatorY = inner.y + headerHeight;
                EditorGUI.DrawRect(new Rect(inner.x + 3f, separatorY, Mathf.Max(0f, inner.width - 6f), 1f), separatorColor);
                GUI.Label(new Rect(inner.x, inner.y, inner.width, headerHeight), ChannelLabels[index], _channelStyle);
                GUI.Label(new Rect(inner.x, separatorY + 1f, inner.width, inner.height - headerHeight - 1f),
                    ValueLabels[value], _valueStyle);
            }

            GUI.EndScrollView();
        }

        private static void GetResponsiveGridMetrics(
            Rect viewport,
            out float cellWidth,
            out float cellHeight,
            out float contentWidth,
            out float contentHeight,
            out float gridOriginX,
            out float gridOriginY)
        {
            float widthForCells = viewport.width - GridPadding * 2f - (ColumnCount - 1) * CellGap;
            float heightForCells = viewport.height - GridPadding * 2f - (RowCount - 1) * CellGap;

            cellWidth = Mathf.Clamp(widthForCells / ColumnCount, MinimumCellWidth, MaximumCellWidth);
            cellHeight = Mathf.Clamp(heightForCells / RowCount, MinimumCellHeight, MaximumCellHeight);

            float gridWidth = ColumnCount * cellWidth + (ColumnCount - 1) * CellGap;
            float gridHeight = RowCount * cellHeight + (RowCount - 1) * CellGap;
            contentWidth = Mathf.Max(viewport.width, gridWidth + GridPadding * 2f);
            contentHeight = Mathf.Max(viewport.height, gridHeight + GridPadding * 2f);
            gridOriginX = (contentWidth - gridWidth) * 0.5f;
            gridOriginY = (contentHeight - gridHeight) * 0.5f;
        }

        private void UpdateGridStyleSizes(float cellWidth, float cellHeight)
        {
            float scale = Mathf.Min(cellWidth / PreferredCellWidth, cellHeight / PreferredCellHeight);
            _channelStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(10f * scale), 7, 10);
            _valueStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(14f * scale), 10, 14);
        }

        private bool TryGetSelectedSnapshot(out DmxChannelMonitorModel.Snapshot snapshot)
        {
            if (_monitorSource == MonitorSource.RigOutput)
            {
                if (_selectedRig == null)
                {
                    snapshot = null;
                    return false;
                }

                return _model.TryGetSnapshot(_selectedUniverse, _selectedRig.GetInstanceID(), out snapshot);
            }

            if (!_monitorAllReceivers && _selectedReceiver == null)
            {
                snapshot = null;
                return false;
            }

            int? sourceId = _monitorAllReceivers ? null : _selectedReceiver.GetInstanceID();
            return _model.TryGetSnapshot(_selectedUniverse, sourceId, out snapshot);
        }

        private void OnEditorUpdate()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now >= _nextReceiverRefreshAt)
            {
                _nextReceiverRefreshAt = now + ReceiverRefreshInterval;
                RefreshReceiverSubscriptions();
                TryAutoSelectRig();
            }

            DrainPendingLiveFrames(now);

            if (now < _nextRepaintAt)
                return;

            _nextRepaintAt = now + RepaintInterval;
            PollRigOutput(now);
            if (EditorApplication.isPlaying || _paused)
                Repaint();
        }

        private void PollRigOutput(double now)
        {
            if (_monitorSource != MonitorSource.RigOutput || _selectedRig == null)
                return;

            if (!_selectedRig.TryCopyUniverseBuffer(
                    _selectedUniverse,
                    _rigChannels,
                    out var source,
                    out long revision))
                return;

            if (_model.TryGetSnapshot(_selectedUniverse, _selectedRig.GetInstanceID(), out var current) &&
                current.IsRigOutput && current.Revision == revision)
                return;

            _model.ReceiveRigBuffer(
                _selectedRig.GetInstanceID(),
                $"{_selectedRig.name} ({GetRigSourceName(source)})",
                _selectedUniverse,
                _rigChannels,
                revision,
                now);
        }

        private static string GetRigSourceName(DmxRigController.UniverseBufferSource source)
        {
            switch (source)
            {
                case DmxRigController.UniverseBufferSource.LiveArtNet:
                    return "Live Input";
                case DmxRigController.UniverseBufferSource.TimelinePlayback:
                    return "Timeline Playback";
                default:
                    return "External Injection";
            }
        }

        private void OnDataReceived(ArtNetReceiver receiver, ArtNetData data)
        {
            if (receiver == null)
                return;

            _pendingLiveFrames.TryRemove(
                new LiveFrameKey(receiver.GetInstanceID(), data.Universe), out _);
            _model.Receive(receiver.GetInstanceID(), receiver.name, data, EditorApplication.timeSinceStartup);
        }

        private void OnDataReceivedInBackground(int sourceId, string sourceName, ArtNetData data)
        {
            if (data.OpCode != ArtNetOpCode.OpDmx || data.Channels == null)
                return;

            _pendingLiveFrames[new LiveFrameKey(sourceId, data.Universe)] =
                new PendingLiveFrame(sourceId, sourceName, data);
        }

        private void DrainPendingLiveFrames(double now)
        {
            if (_monitorSource != MonitorSource.LiveInput)
                return;

            foreach (var pair in _pendingLiveFrames)
            {
                if (_pendingLiveFrames.TryRemove(pair.Key, out var frame))
                    _model.Receive(frame.SourceId, frame.SourceName, frame.Data, now);
            }
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
            {
                _model.Clear();
                _pendingLiveFrames.Clear();
            }
            RefreshReceiverSubscriptions();
            Repaint();
        }

        private void OnHierarchyChanged()
        {
            _nextReceiverRefreshAt = 0d;
        }

        private void RefreshReceiverSubscriptions()
        {
            _desiredReceivers.Clear();
            if (_monitorSource != MonitorSource.LiveInput)
            {
                RemoveAllSubscriptions();
                return;
            }

            if (_monitorAllReceivers)
            {
                var receivers = FindObjectsByType<ArtNetReceiver>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (int i = 0; i < receivers.Length; i++)
                {
                    if (receivers[i] != null)
                        _desiredReceivers.Add(receivers[i]);
                }
            }
            else if (_selectedReceiver != null)
            {
                _desiredReceivers.Add(_selectedReceiver);
            }

            _receiverScratch.Clear();
            foreach (var pair in _subscriptions)
            {
                if (pair.Key == null || !_desiredReceivers.Contains(pair.Key))
                    _receiverScratch.Add(pair.Key);
            }

            for (int i = 0; i < _receiverScratch.Count; i++)
            {
                var receiver = _receiverScratch[i];
                if (receiver != null && _subscriptions.TryGetValue(receiver, out var handler))
                    receiver.OnDataReceived -= handler;
                if (receiver != null && _backgroundSubscriptions.TryGetValue(receiver, out var backgroundHandler))
                    receiver.OnDataReceivedInBackground -= backgroundHandler;
                _subscriptions.Remove(receiver);
                _backgroundSubscriptions.Remove(receiver);
            }

            foreach (var receiver in _desiredReceivers)
            {
                if (_subscriptions.ContainsKey(receiver))
                    continue;

                ArtNetReceiver capturedReceiver = receiver;
                Action<ArtNetData> handler = data => OnDataReceived(capturedReceiver, data);
                int sourceId = receiver.GetInstanceID();
                string sourceName = receiver.name;
                Action<ArtNetData> backgroundHandler = data => OnDataReceivedInBackground(sourceId, sourceName, data);
                receiver.OnDataReceived += handler;
                receiver.OnDataReceivedInBackground += backgroundHandler;
                _subscriptions.Add(receiver, handler);
                _backgroundSubscriptions.Add(receiver, backgroundHandler);
            }
        }

        private void RemoveAllSubscriptions()
        {
            foreach (var pair in _subscriptions)
            {
                if (pair.Key != null)
                    pair.Key.OnDataReceived -= pair.Value;
            }
            foreach (var pair in _backgroundSubscriptions)
            {
                if (pair.Key != null)
                    pair.Key.OnDataReceivedInBackground -= pair.Value;
            }
            _subscriptions.Clear();
            _backgroundSubscriptions.Clear();
            _desiredReceivers.Clear();
            _receiverScratch.Clear();
        }

        private void TryAutoSelectRig()
        {
            if (_monitorSource != MonitorSource.RigOutput || _selectedRig != null)
                return;

            var rigs = FindObjectsByType<DmxRigController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (rigs.Length == 1)
                _selectedRig = rigs[0];
        }

        private readonly struct LiveFrameKey : IEquatable<LiveFrameKey>
        {
            private readonly int _sourceId;
            private readonly int _universe;

            public LiveFrameKey(int sourceId, int universe)
            {
                _sourceId = sourceId;
                _universe = universe;
            }

            public bool Equals(LiveFrameKey other) => _sourceId == other._sourceId && _universe == other._universe;
            public override bool Equals(object obj) => obj is LiveFrameKey other && Equals(other);
            public override int GetHashCode() => (_sourceId * 397) ^ _universe;
        }

        private readonly struct PendingLiveFrame
        {
            public readonly int SourceId;
            public readonly string SourceName;
            public readonly ArtNetData Data;

            public PendingLiveFrame(int sourceId, string sourceName, ArtNetData data)
            {
                SourceId = sourceId;
                SourceName = sourceName;
                Data = data;
            }
        }

        private void EnsureStyles()
        {
            if (_channelStyle == null)
            {
                _channelStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 10,
                    clipping = TextClipping.Clip
                };
            }

            if (_valueStyle == null)
            {
                _valueStyle = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 14,
                    fontStyle = FontStyle.Normal,
                    clipping = TextClipping.Clip
                };
            }

            if (_statusStyle == null)
            {
                _statusStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleLeft,
                    clipping = TextClipping.Clip
                };
            }

            Color textColor = EditorGUIUtility.isProSkin
                ? new Color(0.88f, 0.93f, 0.97f)
                : new Color(0.08f, 0.12f, 0.16f);
            _channelStyle.normal.textColor = EditorGUIUtility.isProSkin
                ? new Color(0.46f, 0.66f, 0.78f)
                : new Color(0.12f, 0.34f, 0.48f);
            _valueStyle.normal.textColor = textColor;
        }

        private static string[] BuildNumberLabels(int count, int offset)
        {
            var labels = new string[count];
            for (int i = 0; i < count; i++)
                labels[i] = (i + offset).ToString();
            return labels;
        }
    }
}
