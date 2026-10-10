/*!
 * Copyright (c) 2026 Oshino
 *
 * Released under the MIT license.
 * see https://opensource.org/licenses/MIT
 */

using System;
using System.Collections.Generic;
using ArtNet.Runtime;
using UnityEngine;

namespace ArtNet.Editor
{
    /// <summary>
    /// Stores copied ArtDMX frames for the Editor-only channel monitor.
    /// It never exposes or mutates the receiver's channel arrays.
    /// </summary>
    public sealed class DmxChannelMonitorModel
    {
        public const int ChannelCount = 512;

        public sealed class Snapshot
        {
            private readonly byte[] _channels = new byte[ChannelCount];
            private readonly double[] _changedAt = new double[ChannelCount];
            private double _rateWindowStartedAt;
            private int _rateWindowPacketCount;

            public int SourceId { get; private set; }
            public string SourceName { get; private set; }
            public int Universe { get; private set; }
            public int Sequence { get; private set; }
            public int Physical { get; private set; }
            public int Length { get; private set; }
            public long PacketCount { get; private set; }
            public long Revision { get; private set; }
            public bool IsRigOutput { get; private set; }
            public double LastReceivedAt { get; private set; }
            public float PacketsPerSecond { get; private set; }

            public byte GetChannel(int index) =>
                index >= 0 && index < ChannelCount ? _channels[index] : (byte)0;

            public double GetChangedAt(int index) =>
                index >= 0 && index < ChannelCount ? _changedAt[index] : 0d;

            public void CopyChannelsTo(byte[] destination)
            {
                if (destination == null)
                    throw new ArgumentNullException(nameof(destination));
                if (destination.Length < ChannelCount)
                    throw new ArgumentException($"Destination must contain at least {ChannelCount} elements.", nameof(destination));

                Buffer.BlockCopy(_channels, 0, destination, 0, ChannelCount);
            }

            internal void Apply(int sourceId, string sourceName, ArtNetData data, double receivedAt)
            {
                SourceId = sourceId;
                SourceName = sourceName ?? string.Empty;
                Universe = data.Universe;
                Sequence = data.Sequence;
                Physical = data.Physical;
                Length = Mathf.Clamp(((data.LengthHi & 0xff) << 8) | (data.LengthLo & 0xff), 0, ChannelCount);
                Revision = 0;
                IsRigOutput = false;
                LastReceivedAt = receivedAt;
                PacketCount++;

                if (_rateWindowPacketCount == 0)
                    _rateWindowStartedAt = receivedAt;
                _rateWindowPacketCount++;

                double rateElapsed = receivedAt - _rateWindowStartedAt;
                if (rateElapsed >= 1d)
                {
                    PacketsPerSecond = (float)(_rateWindowPacketCount / rateElapsed);
                    _rateWindowPacketCount = 0;
                    _rateWindowStartedAt = receivedAt;
                }

                int sourceLength = data.Channels == null ? 0 : Mathf.Min(ChannelCount, data.Channels.Length);
                for (int i = 0; i < ChannelCount; i++)
                {
                    int incoming = i < sourceLength ? data.Channels[i] : 0;
                    byte next = (byte)Mathf.Clamp(incoming, 0, 255);
                    if (_channels[i] != next)
                        _changedAt[i] = receivedAt;
                    _channels[i] = next;
                }
            }

            internal void ApplyRigBuffer(int sourceId, string sourceName, int universe, byte[] channels, long revision, double receivedAt)
            {
                SourceId = sourceId;
                SourceName = sourceName ?? string.Empty;
                Universe = universe;
                Sequence = 0;
                Physical = 0;
                Length = ChannelCount;
                Revision = revision;
                IsRigOutput = true;
                LastReceivedAt = receivedAt;
                PacketCount++;

                if (_rateWindowPacketCount == 0)
                    _rateWindowStartedAt = receivedAt;
                _rateWindowPacketCount++;

                double rateElapsed = receivedAt - _rateWindowStartedAt;
                if (rateElapsed >= 1d)
                {
                    PacketsPerSecond = (float)(_rateWindowPacketCount / rateElapsed);
                    _rateWindowPacketCount = 0;
                    _rateWindowStartedAt = receivedAt;
                }

                for (int i = 0; i < ChannelCount; i++)
                {
                    byte next = channels != null && i < channels.Length ? channels[i] : (byte)0;
                    if (_channels[i] != next)
                        _changedAt[i] = receivedAt;
                    _channels[i] = next;
                }
            }

            internal Snapshot Clone()
            {
                var clone = new Snapshot
                {
                    SourceId = SourceId,
                    SourceName = SourceName,
                    Universe = Universe,
                    Sequence = Sequence,
                    Physical = Physical,
                    Length = Length,
                    PacketCount = PacketCount,
                    Revision = Revision,
                    IsRigOutput = IsRigOutput,
                    LastReceivedAt = LastReceivedAt,
                    PacketsPerSecond = PacketsPerSecond,
                    _rateWindowStartedAt = _rateWindowStartedAt,
                    _rateWindowPacketCount = _rateWindowPacketCount
                };
                Buffer.BlockCopy(_channels, 0, clone._channels, 0, ChannelCount);
                Array.Copy(_changedAt, clone._changedAt, ChannelCount);
                return clone;
            }
        }

        private readonly struct Key : IEquatable<Key>
        {
            public readonly int SourceId;
            public readonly int Universe;

            public Key(int sourceId, int universe)
            {
                SourceId = sourceId;
                Universe = universe;
            }

            public bool Equals(Key other) => SourceId == other.SourceId && Universe == other.Universe;
            public override bool Equals(object obj) => obj is Key other && Equals(other);
            public override int GetHashCode() => (SourceId * 397) ^ Universe;
        }

        private readonly Dictionary<Key, Snapshot> _liveSnapshots = new Dictionary<Key, Snapshot>();
        private readonly Dictionary<Key, Snapshot> _pausedSnapshots = new Dictionary<Key, Snapshot>();

        public bool IsPaused { get; private set; }

        public bool Receive(int sourceId, string sourceName, ArtNetData data, double receivedAt)
        {
            if (data.OpCode != ArtNetOpCode.OpDmx || data.Channels == null)
                return false;

            var key = new Key(sourceId, data.Universe);
            if (!_liveSnapshots.TryGetValue(key, out var snapshot))
            {
                snapshot = new Snapshot();
                _liveSnapshots.Add(key, snapshot);
            }

            snapshot.Apply(sourceId, sourceName, data, receivedAt);
            return true;
        }

        public void ReceiveRigBuffer(int sourceId, string sourceName, int universe, byte[] channels, long revision, double receivedAt)
        {
            var key = new Key(sourceId, universe);
            if (!_liveSnapshots.TryGetValue(key, out var snapshot))
            {
                snapshot = new Snapshot();
                _liveSnapshots.Add(key, snapshot);
            }

            snapshot.ApplyRigBuffer(sourceId, sourceName, universe, channels, revision, receivedAt);
        }

        public void SetPaused(bool paused)
        {
            if (IsPaused == paused)
                return;

            IsPaused = paused;
            _pausedSnapshots.Clear();
            if (!paused)
                return;

            foreach (var pair in _liveSnapshots)
                _pausedSnapshots.Add(pair.Key, pair.Value.Clone());
        }

        public void Clear()
        {
            _liveSnapshots.Clear();
            _pausedSnapshots.Clear();
        }

        public bool TryGetSnapshot(int universe, int? sourceId, out Snapshot snapshot)
        {
            var snapshots = IsPaused ? _pausedSnapshots : _liveSnapshots;
            if (sourceId.HasValue)
                return snapshots.TryGetValue(new Key(sourceId.Value, universe), out snapshot);

            snapshot = null;
            foreach (var pair in snapshots)
            {
                if (pair.Key.Universe != universe)
                    continue;
                if (snapshot == null || pair.Value.LastReceivedAt > snapshot.LastReceivedAt)
                    snapshot = pair.Value;
            }

            return snapshot != null;
        }
    }
}
