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
            TimelinePlayback,
            SenderOutput,
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
        private const float HistoryHeight = 96f;
        private const int HistorySampleCapacity = 180;
        private const double RepaintInterval = 1d / 30d;
        private const double ReceiverRefreshInterval = 1d;
        private const double StaleAfterSeconds = 1d;
        private const double HistorySampleInterval = 1d / 15d;
        private const double HistoryDurationSeconds = 10d;

        private static readonly string[] ChannelLabels = BuildNumberLabels(DmxChannelMonitorModel.ChannelCount, 1);
        private static readonly string[] ValueLabels = BuildNumberLabels(256, 0);

        [SerializeField] private bool _monitorAllReceivers = true;
        [SerializeField] private ArtNetReceiver _selectedReceiver;
        [SerializeField] private MonitorSource _monitorSource;
        [SerializeField] private DmxRigController _selectedRig;
        [SerializeField] private bool _monitorAllRigs;
        [SerializeField] private ArtNetSender _selectedSender;
        [SerializeField] private int _selectedUniverse;
        [SerializeField] private bool _paused;
        [SerializeField] private int _historyChannel = 1;

        private readonly Dictionary<ArtNetReceiver, Action<ArtNetData>> _subscriptions =
            new Dictionary<ArtNetReceiver, Action<ArtNetData>>();
        private readonly Dictionary<ArtNetReceiver, Action<ArtNetData>> _backgroundSubscriptions =
            new Dictionary<ArtNetReceiver, Action<ArtNetData>>();
        private readonly Dictionary<ArtNetSender, Action<ArtNetData>> _senderSubscriptions =
            new Dictionary<ArtNetSender, Action<ArtNetData>>();
        private readonly HashSet<ArtNetReceiver> _desiredReceivers = new HashSet<ArtNetReceiver>();
        private readonly List<ArtNetReceiver> _receiverScratch = new List<ArtNetReceiver>();
        private readonly List<DmxRigController> _availableRigs = new List<DmxRigController>();
        private readonly ConcurrentDictionary<LiveFrameKey, PendingLiveFrame> _pendingLiveFrames =
            new ConcurrentDictionary<LiveFrameKey, PendingLiveFrame>();
        private readonly byte[] _displayChannels = new byte[DmxChannelMonitorModel.ChannelCount];
        private readonly byte[] _rigChannels = new byte[DmxChannelMonitorModel.ChannelCount];
        private readonly HistorySample[] _historySamples = new HistorySample[HistorySampleCapacity];

        private DmxChannelMonitorModel _model;
        private Vector2 _scrollPosition;
        private GUIStyle _channelStyle;
        private GUIStyle _valueStyle;
        private GUIStyle _statusStyle;
        private GUIStyle _historyAxisStyle;
        private double _nextRepaintAt;
        private double _nextReceiverRefreshAt;
        private double _nextHistorySampleAt;
        private int _historySampleStart;
        private int _historySampleCount;
        private int _historySourceId = int.MinValue;
        private int _historyUniverse = -1;

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
            float historyHeight = Mathf.Min(HistoryHeight, Mathf.Max(0f, position.height - ToolbarHeight - 120f));
            float historyY = position.height - historyHeight;
            DrawChannelGrid(new Rect(0f, ToolbarHeight, position.width, Mathf.Max(0f, historyY - ToolbarHeight)));
            DrawHistory(new Rect(0f, historyY, position.width, historyHeight));
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
                new Rect(x, rowOneY, 138f, controlHeight), _monitorSource);
            if (monitorSource != _monitorSource)
            {
                _monitorSource = monitorSource;
                RefreshReceiverSubscriptions();
                _model.Clear();
                ResetHistory();
                TryAutoSelectRig();
            }
            x += 148f;

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
                        ResetHistory();
                    }
                }
                x += 200f;
            }
            else
            {
                if (_monitorSource == MonitorSource.SenderOutput)
                {
                    EditorGUI.LabelField(new Rect(x, rowOneY, 46f, controlHeight), "Sender");
                    x += 48f;
                    var sender = (ArtNetSender)EditorGUI.ObjectField(
                        new Rect(x, rowOneY, 231f, controlHeight), _selectedSender, typeof(ArtNetSender), true);
                    if (sender != _selectedSender)
                    {
                        _selectedSender = sender;
                        _model.Clear();
                        ResetHistory();
                        RefreshReceiverSubscriptions();
                    }
                    x += 241f;
                }
                else
                {
                    bool monitorAllRigs = EditorGUI.ToggleLeft(
                        new Rect(x, rowOneY, 72f, controlHeight), "All Rigs", _monitorAllRigs);
                    if (monitorAllRigs != _monitorAllRigs)
                    {
                        _monitorAllRigs = monitorAllRigs;
                        _model.Clear();
                        ResetHistory();
                    }
                    x += 74f;
                    EditorGUI.LabelField(new Rect(x, rowOneY, 24f, controlHeight), "Rig");
                    x += 26f;
                    using (new EditorGUI.DisabledScope(_monitorAllRigs))
                    {
                        var rig = (DmxRigController)EditorGUI.ObjectField(
                            new Rect(x, rowOneY, 180f, controlHeight), _selectedRig, typeof(DmxRigController), true);
                        if (rig != _selectedRig)
                        {
                            _selectedRig = rig;
                            _model.Clear();
                            ResetHistory();
                        }
                    }
                    x += 190f;
                }
            }

            EditorGUI.LabelField(new Rect(x, rowOneY, 86f, controlHeight), "Local Universe");
            x += 88f;
            int universe = EditorGUI.IntField(new Rect(x, rowOneY, 62f, controlHeight), _selectedUniverse);
            int selectedUniverse = Mathf.Clamp(universe, 0, 32767);
            if (selectedUniverse != _selectedUniverse)
            {
                _selectedUniverse = selectedUniverse;
                ResetHistory();
            }
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
                ResetHistory();
                Repaint();
            }

            DrawStatus(new Rect(10f, 36f, Mathf.Max(0f, position.width - 20f), 22f));
        }

        private void DrawStatus(Rect rect)
        {
            double now = EditorApplication.timeSinceStartup;

            string text;
            Color color;
            if (!EditorApplication.isPlaying && (_monitorSource != MonitorSource.LiveInput || !HasEditModeReceiver()))
            {
                text = "Start Play Mode to monitor incoming ArtDMX.";
                color = new Color(0.62f, 0.62f, 0.62f);
            }
            else if (_monitorSource == MonitorSource.LiveInput && !_monitorAllReceivers && _selectedReceiver == null)
            {
                text = "Select an ArtNetReceiver.";
                color = new Color(0.72f, 0.62f, 0.34f);
            }
            else if (_monitorSource == MonitorSource.TimelinePlayback && !_monitorAllRigs && _selectedRig == null)
            {
                text = "Select a DmxRigController.";
                color = new Color(0.72f, 0.62f, 0.34f);
            }
            else if (_monitorSource == MonitorSource.SenderOutput && _selectedSender == null)
            {
                text = "Select an ArtNetSender.";
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
                string receiverDetails = string.Empty;
                if (!snapshot.IsRigOutput && TryGetReceiver(snapshot.SourceId, out var receiver))
                {
                    ArtNetReceiver.ReceiveStatistics statistics = receiver.Statistics;
                    receiverDetails = $"   Packets: {statistics.ReceivedPacketCount}   Invalid: {statistics.InvalidPacketCount}";
                }
                text = $"{state}   Source: {snapshot.SourceName}   {frameDetails}   " +
                       $"Last: {age:0.00}s   Length: {snapshot.Length}{receiverDetails}";
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
                if (index == _historyChannel - 1)
                {
                    Color selectedColor = new Color(0.22f, 0.76f, 1f);
                    const float selectedBorderWidth = 2f;
                    EditorGUI.DrawRect(new Rect(cell.x, cell.y, cell.width, selectedBorderWidth), selectedColor);
                    EditorGUI.DrawRect(new Rect(cell.x, cell.yMax - selectedBorderWidth, cell.width, selectedBorderWidth), selectedColor);
                    EditorGUI.DrawRect(new Rect(cell.x, cell.y, selectedBorderWidth, cell.height), selectedColor);
                    EditorGUI.DrawRect(new Rect(cell.xMax - selectedBorderWidth, cell.y, selectedBorderWidth, cell.height), selectedColor);
                }
                GUI.Label(new Rect(inner.x, inner.y, inner.width, headerHeight), ChannelLabels[index], _channelStyle);
                GUI.Label(new Rect(inner.x, separatorY + 1f, inner.width, inner.height - headerHeight - 1f),
                    ValueLabels[value], _valueStyle);

                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && cell.Contains(Event.current.mousePosition))
                {
                    _historyChannel = index + 1;
                    ResetHistory();
                    Event.current.Use();
                }
            }

            GUI.EndScrollView();
        }

        private void DrawHistory(Rect rect)
        {
            if (rect.height <= 0f)
                return;

            bool proSkin = EditorGUIUtility.isProSkin;
            EditorGUI.DrawRect(rect, proSkin ? new Color(0.055f, 0.06f, 0.068f) : new Color(0.82f, 0.84f, 0.86f));
            EditorGUI.LabelField(new Rect(10f, rect.y + 6f, 72f, 20f), "History Ch");
            int historyChannel = Mathf.Clamp(EditorGUI.IntField(new Rect(84f, rect.y + 6f, 48f, 20f), _historyChannel), 1, DmxChannelMonitorModel.ChannelCount);
            if (historyChannel != _historyChannel)
            {
                _historyChannel = historyChannel;
                ResetHistory();
            }

            if (GUI.Button(new Rect(142f, rect.y + 6f, 92f, 20f), "Clear History"))
                ResetHistory();

            EditorGUI.LabelField(new Rect(244f, rect.y + 6f, 148f, 20f), "Last 10 seconds", EditorStyles.miniLabel);
            const float axisLabelWidth = 24f;
            var graph = new Rect(10f + axisLabelWidth, rect.y + 30f,
                Mathf.Max(0f, rect.width - axisLabelWidth - 20f), Mathf.Max(0f, rect.height - 38f));
            EditorGUI.DrawRect(graph, proSkin ? new Color(0.035f, 0.04f, 0.048f) : new Color(0.9f, 0.92f, 0.94f));
            GUI.Label(new Rect(8f, graph.y - 6f, axisLabelWidth, 14f), "255", _historyAxisStyle);
            GUI.Label(new Rect(8f, graph.yMax - 8f, axisLabelWidth, 14f), "0", _historyAxisStyle);
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
            Handles.color = new Color(0.08f, 0.62f, 0.96f);
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
            if (_monitorSource == MonitorSource.TimelinePlayback)
            {
                if (!_monitorAllRigs && _selectedRig == null)
                {
                    snapshot = null;
                    return false;
                }

                return _model.TryGetSnapshot(_selectedUniverse,
                    _monitorAllRigs ? (int?)null : _selectedRig.GetInstanceID(), out snapshot);
            }

            if (_monitorSource == MonitorSource.SenderOutput)
            {
                if (_selectedSender == null)
                {
                    snapshot = null;
                    return false;
                }

                return _model.TryGetSnapshot(_selectedUniverse, _selectedSender.GetInstanceID(), out snapshot);
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
            CaptureHistory(now);
            if (EditorApplication.isPlaying || _paused || _monitorSource == MonitorSource.LiveInput)
                Repaint();
        }

        private void CaptureHistory(double now)
        {
            if (_paused || now < _nextHistorySampleAt || !TryGetSelectedSnapshot(out var snapshot))
                return;

            _nextHistorySampleAt = now + HistorySampleInterval;
            if (_historySourceId != snapshot.SourceId || _historyUniverse != snapshot.Universe)
            {
                ResetHistory();
                _historySourceId = snapshot.SourceId;
                _historyUniverse = snapshot.Universe;
                _nextHistorySampleAt = now + HistorySampleInterval;
            }

            AppendHistorySample(new HistorySample(now, snapshot.GetChannel(_historyChannel - 1)));
        }

        private void AppendHistorySample(HistorySample sample)
        {
            if (_historySampleCount < HistorySampleCapacity)
            {
                int index = (_historySampleStart + _historySampleCount) % HistorySampleCapacity;
                _historySamples[index] = sample;
                _historySampleCount++;
                return;
            }

            _historySamples[_historySampleStart] = sample;
            _historySampleStart = (_historySampleStart + 1) % HistorySampleCapacity;
        }

        private void ResetHistory()
        {
            _historySampleStart = 0;
            _historySampleCount = 0;
            _historySourceId = int.MinValue;
            _historyUniverse = -1;
            _nextHistorySampleAt = 0d;
        }

        private void PollRigOutput(double now)
        {
            if (_monitorSource != MonitorSource.TimelinePlayback)
                return;

            if (_monitorAllRigs)
            {
                for (int i = 0; i < _availableRigs.Count; i++)
                    PollRigOutput(_availableRigs[i], now);
                return;
            }

            PollRigOutput(_selectedRig, now);
        }

        private void PollRigOutput(DmxRigController rig, double now)
        {
            if (rig == null || !rig.TryCopyUniverseBuffer(
                    _selectedUniverse,
                    _rigChannels,
                    out var source,
                    out long revision))
                return;

            if (_model.TryGetSnapshot(_selectedUniverse, rig.GetInstanceID(), out var current) &&
                current.IsRigOutput && current.Revision == revision)
                return;

            _model.ReceiveRigBuffer(
                rig.GetInstanceID(),
                $"{rig.name} ({GetRigSourceName(source)})",
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
            _model.Receive(receiver.GetInstanceID(), GetReceiverSourceName(receiver), data, EditorApplication.timeSinceStartup);
        }

        private void OnDataSent(ArtNetSender sender, ArtNetData data)
        {
            if (sender == null || data.OpCode != ArtNetOpCode.OpDmx)
                return;

            _model.Receive(sender.GetInstanceID(), $"{sender.name} (Sender Output: {sender.Host}:{sender.port})",
                data, EditorApplication.timeSinceStartup);
        }

        private static string GetReceiverSourceName(ArtNetReceiver receiver)
        {
            ArtNetReceiver.ReceiveStatistics statistics = receiver.Statistics;
            return GetReceiverSourceName(receiver.name, statistics);
        }

        private static string GetReceiverSourceName(
            string receiverName,
            ArtNetReceiver.ReceiveStatistics statistics)
        {
            if (string.IsNullOrEmpty(statistics.LastSenderAddress))
                return receiverName;

            return $"{receiverName} ({statistics.LastSenderAddress}:{statistics.LastSenderPort})";
        }

        private bool TryGetReceiver(int sourceId, out ArtNetReceiver receiver)
        {
            foreach (var pair in _subscriptions)
            {
                if (pair.Key != null && pair.Key.GetInstanceID() == sourceId)
                {
                    receiver = pair.Key;
                    return true;
                }
            }

            receiver = null;
            return false;
        }

        private bool HasEditModeReceiver()
        {
            foreach (var pair in _subscriptions)
            {
                if (pair.Key != null && pair.Key.ReceivesInEditMode)
                    return true;
            }

            return false;
        }

        private void OnDataReceivedInBackground(
            ArtNetReceiver receiver,
            int sourceId,
            string receiverName,
            ArtNetData data)
        {
            if (data.OpCode != ArtNetOpCode.OpDmx || data.Channels == null)
                return;

            string sourceName = GetReceiverSourceName(receiverName, receiver.Statistics);
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
                ResetHistory();
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
            }
            else if (_monitorAllReceivers)
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
                string receiverName = receiver.name;
                Action<ArtNetData> backgroundHandler = data =>
                    OnDataReceivedInBackground(capturedReceiver, sourceId, receiverName, data);
                receiver.OnDataReceived += handler;
                receiver.OnDataReceivedInBackground += backgroundHandler;
                _subscriptions.Add(receiver, handler);
                _backgroundSubscriptions.Add(receiver, backgroundHandler);
            }

            RefreshSenderSubscriptions();
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
            foreach (var pair in _senderSubscriptions)
            {
                if (pair.Key != null)
                    pair.Key.OnDataSent -= pair.Value;
            }
            _senderSubscriptions.Clear();
            _desiredReceivers.Clear();
            _receiverScratch.Clear();
        }

        private void RefreshSenderSubscriptions()
        {
            if (_monitorSource != MonitorSource.SenderOutput)
            {
                RemoveSenderSubscriptions();
                return;
            }

            TryAutoSelectSender();
            foreach (var pair in _senderSubscriptions)
            {
                if (pair.Key != _selectedSender && pair.Key != null)
                    pair.Key.OnDataSent -= pair.Value;
            }
            _senderSubscriptions.Clear();

            if (_selectedSender == null)
                return;

            ArtNetSender capturedSender = _selectedSender;
            Action<ArtNetData> handler = data => OnDataSent(capturedSender, data);
            capturedSender.OnDataSent += handler;
            _senderSubscriptions.Add(capturedSender, handler);
        }

        private void RemoveSenderSubscriptions()
        {
            foreach (var pair in _senderSubscriptions)
            {
                if (pair.Key != null)
                    pair.Key.OnDataSent -= pair.Value;
            }
            _senderSubscriptions.Clear();
        }

        private void TryAutoSelectRig()
        {
            if (_monitorSource != MonitorSource.TimelinePlayback)
            {
                _availableRigs.Clear();
                return;
            }

            _availableRigs.Clear();
            var rigs = FindObjectsByType<DmxRigController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < rigs.Length; i++)
                if (rigs[i] != null) _availableRigs.Add(rigs[i]);

            if (_selectedRig != null)
                return;

            if (_availableRigs.Count == 1)
                _selectedRig = _availableRigs[0];
        }

        private void TryAutoSelectSender()
        {
            if (_monitorSource != MonitorSource.SenderOutput || _selectedSender != null)
                return;

            var senders = FindObjectsByType<ArtNetSender>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (senders.Length == 1)
                _selectedSender = senders[0];
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

            if (_historyAxisStyle == null)
            {
                _historyAxisStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleRight,
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
            _historyAxisStyle.normal.textColor = _channelStyle.normal.textColor;
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
