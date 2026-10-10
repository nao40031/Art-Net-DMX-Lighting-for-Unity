/*!
 * Copyright (c) 2026 Oshino
 *
 * Released under the MIT license.
 * see https://opensource.org/licenses/MIT
 */

using System.Reflection;
using ArtNet.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace ArtNet.Editor.Tests
{
    public sealed class DmxChannelMonitorModelTests
    {
        [Test]
        public void Receive_CopiesAndClampsBoundaryChannels()
        {
            var model = new DmxChannelMonitorModel();
            int[] channels = CreateChannels();
            channels[0] = -12;
            channels[1] = 128;
            channels[511] = 999;

            Assert.That(model.Receive(10, "Receiver A", CreateData(7, channels), 1d), Is.True);
            Assert.That(model.TryGetSnapshot(7, 10, out var snapshot), Is.True);
            Assert.That(snapshot.GetChannel(0), Is.EqualTo(0));
            Assert.That(snapshot.GetChannel(1), Is.EqualTo(128));
            Assert.That(snapshot.GetChannel(511), Is.EqualTo(255));
            Assert.That(snapshot.Universe, Is.EqualTo(7));
            Assert.That(snapshot.Sequence, Is.EqualTo(23));
            Assert.That(snapshot.Physical, Is.EqualTo(4));
            Assert.That(snapshot.Length, Is.EqualTo(512));

            channels[1] = 12;
            Assert.That(snapshot.GetChannel(1), Is.EqualTo(128), "The monitor must own a copy of received values.");
        }

        [Test]
        public void Receive_IgnoresNonDmxPackets()
        {
            var model = new DmxChannelMonitorModel();
            var data = new ArtNetData(CreateChannels(), ArtNetOpCode.OpSync, 0, 0, 0, 0, 14, 0, 0);

            Assert.That(model.Receive(1, "Receiver", data, 1d), Is.False);
            Assert.That(model.TryGetSnapshot(0, null, out _), Is.False);
        }

        [Test]
        public void TryGetSnapshot_KeepsSourcesAndUniversesSeparate()
        {
            var model = new DmxChannelMonitorModel();
            int[] first = CreateChannels();
            int[] second = CreateChannels();
            first[0] = 10;
            second[0] = 20;

            model.Receive(100, "First", CreateData(1, first), 1d);
            model.Receive(200, "Second", CreateData(1, second), 2d);
            model.Receive(100, "First", CreateData(2, first), 3d);

            Assert.That(model.TryGetSnapshot(1, 100, out var firstSnapshot), Is.True);
            Assert.That(firstSnapshot.GetChannel(0), Is.EqualTo(10));
            Assert.That(model.TryGetSnapshot(1, 200, out var secondSnapshot), Is.True);
            Assert.That(secondSnapshot.GetChannel(0), Is.EqualTo(20));
            Assert.That(model.TryGetSnapshot(1, null, out var mergedSnapshot), Is.True);
            Assert.That(mergedSnapshot.SourceId, Is.EqualTo(200), "All Receivers uses the latest packet for the selected Universe.");
            Assert.That(model.TryGetSnapshot(2, 100, out var otherUniverse), Is.True);
            Assert.That(otherUniverse.Universe, Is.EqualTo(2));
        }

        [Test]
        public void Pause_FreezesDisplayButKeepsLatestLiveFrame()
        {
            var model = new DmxChannelMonitorModel();
            int[] channels = CreateChannels();
            channels[0] = 10;
            model.Receive(1, "Receiver", CreateData(0, channels), 1d);
            model.SetPaused(true);

            channels[0] = 90;
            model.Receive(1, "Receiver", CreateData(0, channels), 2d);

            Assert.That(model.TryGetSnapshot(0, 1, out var paused), Is.True);
            Assert.That(paused.GetChannel(0), Is.EqualTo(10));

            model.SetPaused(false);
            Assert.That(model.TryGetSnapshot(0, 1, out var resumed), Is.True);
            Assert.That(resumed.GetChannel(0), Is.EqualTo(90));
        }

        [Test]
        public void Clear_RemovesLiveAndPausedBuffers()
        {
            var model = new DmxChannelMonitorModel();
            model.Receive(1, "Receiver", CreateData(0, CreateChannels()), 1d);
            model.SetPaused(true);

            model.Clear();

            Assert.That(model.TryGetSnapshot(0, 1, out _), Is.False);
            model.SetPaused(false);
            Assert.That(model.TryGetSnapshot(0, 1, out _), Is.False);
        }

        [Test]
        public void ReceiveRigBuffer_CopiesTheEffectiveBufferAndTracksRevision()
        {
            var model = new DmxChannelMonitorModel();
            var channels = new byte[DmxChannelMonitorModel.ChannelCount];
            channels[0] = 42;
            channels[511] = 255;

            model.ReceiveRigBuffer(99, "Rig (Timeline Playback)", 7, channels, 12, 1d);
            channels[0] = 0;

            Assert.That(model.TryGetSnapshot(7, 99, out var snapshot), Is.True);
            Assert.That(snapshot.IsRigOutput, Is.True);
            Assert.That(snapshot.Revision, Is.EqualTo(12));
            Assert.That(snapshot.GetChannel(0), Is.EqualTo(42));
            Assert.That(snapshot.GetChannel(511), Is.EqualTo(255));
        }

        [Test]
        public void RigOutput_TimelineInjectionCopiesTheEffectiveBufferAndSource()
        {
            var gameObject = new GameObject("DMX Rig Output Test");
            try
            {
                var rig = gameObject.AddComponent<DmxRigController>();
                rig.inputMode = DmxRigController.InputMode.PlaybackOnly;
                var timelineChannels = new byte[DmxChannelMonitorModel.ChannelCount];
                timelineChannels[0] = 128;
                timelineChannels[511] = 64;

                rig.InjectUniverse(3, timelineChannels, DmxRigController.UniverseBufferSource.TimelinePlayback);

                var copied = new byte[DmxChannelMonitorModel.ChannelCount];
                Assert.That(rig.TryCopyUniverseBuffer(3, copied, out var source, out long revision), Is.True);
                Assert.That(source, Is.EqualTo(DmxRigController.UniverseBufferSource.TimelinePlayback));
                Assert.That(revision, Is.GreaterThan(0));
                Assert.That(copied[0], Is.EqualTo(128));
                Assert.That(copied[511], Is.EqualTo(64));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ActivityMonitor_UsesTheHighestChannelValueForActivity()
        {
            var windowType = typeof(DmxChannelMonitorModel).Assembly.GetType("ArtNet.Editor.DmxActivityMonitorWindow");
            Assert.That(windowType, Is.Not.Null);
            var method = windowType.GetMethod("GetMax", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(byte[]) }, null);
            Assert.That(method, Is.Not.Null);

            int activity = (int)method.Invoke(null, new object[] { new byte[] { 0, 21, 255, 64 } });
            Assert.That(activity, Is.EqualTo(255));
        }

        [Test]
        public void ActivityMonitor_ClampsLiveInputActivityToDmxRange()
        {
            var windowType = typeof(DmxChannelMonitorModel).Assembly.GetType("ArtNet.Editor.DmxActivityMonitorWindow");
            var method = windowType.GetMethod("GetMax", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(int[]) }, null);

            int activity = (int)method.Invoke(null, new object[] { new[] { -10, 12, 999 } });
            Assert.That(activity, Is.EqualTo(255));
        }

        [Test]
        public void ResponsiveGrid_MinimumWindowFitsAllChannelsWithoutScrollbars()
        {
            GridMetrics metrics = GetGridMetrics(new Rect(0f, 0f, 900f, 434f));

            Assert.That(metrics.contentWidth, Is.LessThanOrEqualTo(900f));
            Assert.That(metrics.contentHeight, Is.LessThanOrEqualTo(434f));
            Assert.That(metrics.cellWidth, Is.GreaterThanOrEqualTo(22f));
            Assert.That(metrics.cellHeight, Is.GreaterThanOrEqualTo(24f));
        }

        [Test]
        public void ResponsiveGrid_ExpandsWhenTheWindowHasRoom()
        {
            GridMetrics compact = GetGridMetrics(new Rect(0f, 0f, 900f, 434f));
            GridMetrics expanded = GetGridMetrics(new Rect(0f, 0f, 1500f, 700f));

            Assert.That(expanded.cellWidth, Is.GreaterThan(compact.cellWidth));
            Assert.That(expanded.cellHeight, Is.GreaterThan(compact.cellHeight));
        }

        [Test]
        public void ResponsiveGrid_UsesScrollContentOnlyBelowMinimumCellSize()
        {
            GridMetrics metrics = GetGridMetrics(new Rect(0f, 0f, 600f, 300f));

            Assert.That(metrics.contentWidth, Is.GreaterThan(600f));
            Assert.That(metrics.contentHeight, Is.GreaterThan(300f));
            Assert.That(metrics.cellWidth, Is.EqualTo(22f));
            Assert.That(metrics.cellHeight, Is.EqualTo(24f));
        }

        private static int[] CreateChannels() => new int[DmxChannelMonitorModel.ChannelCount];

        private static ArtNetData CreateData(int universe, int[] channels)
        {
            return new ArtNetData(
                channels,
                ArtNetOpCode.OpDmx,
                sequence: 23,
                physical: 4,
                universe,
                protocolVersionHi: 0,
                protocolVersionLo: 14,
                lengthHi: 2,
                lengthLo: 0);
        }

        private static GridMetrics GetGridMetrics(Rect viewport)
        {
            var windowType = typeof(DmxChannelMonitorModel).Assembly.GetType("ArtNet.Editor.DmxChannelMonitorWindow");
            Assert.That(windowType, Is.Not.Null);
            var method = windowType.GetMethod("GetResponsiveGridMetrics", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);

            object[] args = { viewport, 0f, 0f, 0f, 0f, 0f, 0f };
            method.Invoke(null, args);
            return new GridMetrics(
                (float)args[1],
                (float)args[2],
                (float)args[3],
                (float)args[4]);
        }

        private readonly struct GridMetrics
        {
            public readonly float cellWidth;
            public readonly float cellHeight;
            public readonly float contentWidth;
            public readonly float contentHeight;

            public GridMetrics(float cellWidth, float cellHeight, float contentWidth, float contentHeight)
            {
                this.cellWidth = cellWidth;
                this.cellHeight = cellHeight;
                this.contentWidth = contentWidth;
                this.contentHeight = contentHeight;
            }
        }
    }
}
