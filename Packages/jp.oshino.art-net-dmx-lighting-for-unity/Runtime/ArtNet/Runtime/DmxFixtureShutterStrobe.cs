/*!
 * Copyright (c) 2026 Oshino
 *
 * Released under the MIT license.
 * see https://opensource.org/licenses/MIT
 */

using UnityEngine;

namespace ArtNet.Runtime
{
    public enum ShutterStrobeMode
    {
        Open = 0,
        Closed = 1,
        Regular = 2,
        PulseOpen = 3,
        PulseClose = 4,
        Random = 5,
        RandomPulse = 6,
        RandomPulseOpen = 7,
        RandomPulseClose = 8,
        Effect = 9
    }

    public enum ShutterStrobePreviewMode
    {
        Disabled = 0,
        SafeMaximum3Hz = 1,
        FullFixtureRate = 2
    }

    public struct ShutterStrobeCommand
    {
        public ShutterStrobeMode mode;
        public float frequencyHz;
        public float dutyCycle;
        public float flashDurationSeconds;
    }

    public static class ShutterStrobeEvaluator
    {
        public static ShutterStrobeMode ResolveMode(FixtureRangeType rangeType)
        {
            return rangeType switch
            {
                FixtureRangeType.Closed => ShutterStrobeMode.Closed,
                FixtureRangeType.Speed => ShutterStrobeMode.Regular,
                FixtureRangeType.RegularStrobe => ShutterStrobeMode.Regular,
                FixtureRangeType.Pulse => ShutterStrobeMode.PulseOpen,
                FixtureRangeType.PulseOpen => ShutterStrobeMode.PulseOpen,
                FixtureRangeType.PulseClose => ShutterStrobeMode.PulseClose,
                FixtureRangeType.Random => ShutterStrobeMode.Random,
                FixtureRangeType.RandomStrobe => ShutterStrobeMode.Random,
                FixtureRangeType.RandomPulse => ShutterStrobeMode.RandomPulse,
                FixtureRangeType.RandomPulseOpen => ShutterStrobeMode.RandomPulseOpen,
                FixtureRangeType.RandomPulseClose => ShutterStrobeMode.RandomPulseClose,
                FixtureRangeType.ShutterEffect => ShutterStrobeMode.Effect,
                _ => ShutterStrobeMode.Open
            };
        }

        public static ShutterStrobeCommand BuildCommand(FixtureChannelRange range, int rawValue)
        {
            if (range == null)
                return OpenCommand;

            ShutterStrobeMode mode = ResolveMode(range.type);
            float normalized = Mathf.Clamp01(range.Normalize(rawValue));
            float frequency = Mathf.Max(0f, Mathf.Lerp(range.shutterFrequencyFromHz, range.shutterFrequencyToHz, normalized));
            return new ShutterStrobeCommand
            {
                mode = mode,
                frequencyHz = frequency,
                dutyCycle = Mathf.Clamp(range.shutterDutyCycle, 0.01f, 0.99f),
                flashDurationSeconds = Mathf.Max(0f, range.shutterFlashDurationSeconds)
            };
        }

        public static float EvaluateGate(ShutterStrobeCommand command, double elapsedSeconds, int randomSeed,
            ShutterStrobePreviewMode previewMode, bool isPlaying)
        {
            if (command.mode == ShutterStrobeMode.Closed)
                return 0f;
            if (command.mode == ShutterStrobeMode.Open)
                return 1f;
            if (!isPlaying || previewMode == ShutterStrobePreviewMode.Disabled)
                return 1f;

            float frequency = Mathf.Max(0f, command.frequencyHz);
            if (previewMode == ShutterStrobePreviewMode.SafeMaximum3Hz)
                frequency = Mathf.Min(frequency, 3f);
            if (frequency <= 0.0001f)
                return 1f;

            double cycles = System.Math.Max(0d, elapsedSeconds) * frequency;
            long cycleIndex = (long)System.Math.Floor(cycles);
            float phase = (float)(cycles - cycleIndex);
            float duty = ResolveDutyCycle(command, frequency);

            switch (command.mode)
            {
                case ShutterStrobeMode.Regular:
                case ShutterStrobeMode.Effect:
                    return phase < duty ? 1f : 0f;
                case ShutterStrobeMode.PulseOpen:
                    return 1f - Mathf.Abs((phase * 2f) - 1f);
                case ShutterStrobeMode.PulseClose:
                    return Mathf.Abs((phase * 2f) - 1f);
                case ShutterStrobeMode.Random:
                    return EvaluateRandomFlash(phase, cycleIndex, randomSeed, duty);
                case ShutterStrobeMode.RandomPulse:
                case ShutterStrobeMode.RandomPulseOpen:
                    return EvaluateRandomPulse(phase, cycleIndex, randomSeed, false);
                case ShutterStrobeMode.RandomPulseClose:
                    return EvaluateRandomPulse(phase, cycleIndex, randomSeed, true);
                default:
                    return 1f;
            }
        }

        public static ShutterStrobeCommand OpenCommand => new ShutterStrobeCommand
        {
            mode = ShutterStrobeMode.Open,
            frequencyHz = 0f,
            dutyCycle = 1f,
            flashDurationSeconds = 0f
        };

        private static float ResolveDutyCycle(ShutterStrobeCommand command, float frequency)
        {
            if (command.flashDurationSeconds > 0f)
                return Mathf.Clamp(command.flashDurationSeconds * frequency, 0.01f, 0.99f);
            return Mathf.Clamp(command.dutyCycle, 0.01f, 0.99f);
        }

        private static float EvaluateRandomFlash(float phase, long cycleIndex, int seed, float duty)
        {
            float start = Hash01(seed, cycleIndex) * (1f - duty);
            return phase >= start && phase < start + duty ? 1f : 0f;
        }

        private static float EvaluateRandomPulse(float phase, long cycleIndex, int seed, bool reverse)
        {
            float offset = Hash01(seed, cycleIndex) * 0.8f;
            float shifted = Mathf.Repeat(phase + offset, 1f);
            float pulse = 1f - Mathf.Abs((shifted * 2f) - 1f);
            return reverse ? 1f - pulse : pulse;
        }

        private static float Hash01(int seed, long cycleIndex)
        {
            unchecked
            {
                uint value = (uint)seed;
                value ^= (uint)cycleIndex * 0x9E3779B9u;
                value ^= (uint)(cycleIndex >> 32) * 0x85EBCA6Bu;
                value ^= value >> 16;
                value *= 0x7FEB352Du;
                value ^= value >> 15;
                value *= 0x846CA68Bu;
                value ^= value >> 16;
                return (value & 0x00FFFFFFu) / 16777216f;
            }
        }
    }

    public partial class DmxFixtureComponent
    {
        [Header("Shutter / Strobe")]
        [Tooltip("DMXのシャッター／ストロボをUnity Light、Pseudo Beam、VLBへ反映します。フレーミングシャッターとは別機能です。\n\nApplies the DMX shutter/strobe to Unity Lights, Pseudo Beams, and VLB. This is separate from framing shutters.")]
        [InspectorName("Sync Beam Shutter / Strobe To DMX")]
        [SerializeField] private bool syncBeamShutterStrobeToDmx = true;

        [Tooltip("DMXのシャッター／ストロボをレンズ面の発光へ反映します。\n\nApplies the DMX shutter/strobe to lens-surface emission.")]
        [InspectorName("Sync Lens Shutter / Strobe To DMX")]
        [SerializeField] private bool syncLensShutterStrobeToDmx = true;

        [Tooltip("シャッター／ストロボの再生速度を設定します。既定ではFixture Profileの速度をそのまま使用します。\n\nControls shutter/strobe playback speed. By default, the Fixture Profile rate is used without a preview cap.")]
        [SerializeField] private ShutterStrobePreviewMode shutterStrobePreviewMode = ShutterStrobePreviewMode.FullFixtureRate;

        [Tooltip("0以外の場合、Random系ストロボの再現可能なシードとして使用します。0ではUniverse、Address、Instanceから生成します。\n\nWhen non-zero, provides a reproducible seed for random strobe modes. Zero derives a seed from Universe, Address, and Instance.")]
        [SerializeField] private int shutterStrobeRandomSeed;

        private ShutterStrobeCommand _shutterStrobeCommand = default;
        private float _shutterStrobeGate01 = 1f;
        private double _shutterStrobePhaseStartedAt;
        private bool _shutterStrobeInitialized;

        public ShutterStrobeMode CurrentShutterStrobeMode => _shutterStrobeInitialized
            ? _shutterStrobeCommand.mode
            : ShutterStrobeMode.Open;
        public float CurrentShutterStrobeFrequencyHz => _shutterStrobeInitialized
            ? _shutterStrobeCommand.frequencyHz
            : 0f;
        public float CurrentShutterStrobeGate01 => _shutterStrobeGate01;

        private void ReadShutterStrobe(int[] universe512)
        {
            if (TryReadElementRawForRange(universe512, FixtureAttribute.Strobe, 1, FixtureChannelRole.Value,
                    out int rawValue, out _, out var element))
                SetShutterStrobeCommand(ResolveShutterStrobeCommand(element, rawValue));
            else
                SetShutterStrobeCommand(ShutterStrobeEvaluator.OpenCommand);
        }

        private void ReadShutterStrobe(byte[] universe512)
        {
            if (TryReadElementRawForRange(universe512, FixtureAttribute.Strobe, 1, FixtureChannelRole.Value,
                    out int rawValue, out _, out var element))
                SetShutterStrobeCommand(ResolveShutterStrobeCommand(element, rawValue));
            else
                SetShutterStrobeCommand(ShutterStrobeEvaluator.OpenCommand);
        }

        private static ShutterStrobeCommand ResolveShutterStrobeCommand(FixtureChannelElement element, int rawValue)
        {
            if (element?.ranges == null)
                return ShutterStrobeEvaluator.OpenCommand;

            FixtureChannelRange best = null;
            int bestWidth = int.MaxValue;
            for (int i = 0; i < element.ranges.Count; i++)
            {
                FixtureChannelRange candidate = element.ranges[i];
                if (candidate == null || !candidate.Contains(rawValue))
                    continue;

                int width = Mathf.Abs(candidate.dmxMax - candidate.dmxMin);
                if (best == null || width < bestWidth)
                {
                    best = candidate;
                    bestWidth = width;
                }
            }

            return ShutterStrobeEvaluator.BuildCommand(best, rawValue);
        }

        private void SetShutterStrobeCommand(ShutterStrobeCommand command)
        {
            if (!_shutterStrobeInitialized || command.mode != _shutterStrobeCommand.mode)
                _shutterStrobePhaseStartedAt = CurrentShutterClockTime;

            _shutterStrobeCommand = command;
            _shutterStrobeInitialized = true;
            UpdateShutterStrobe();
        }

        private void UpdateShutterStrobe()
        {
            if (!_shutterStrobeInitialized)
            {
                _shutterStrobeGate01 = 1f;
                return;
            }

            int seed = shutterStrobeRandomSeed != 0
                ? shutterStrobeRandomSeed
                : unchecked((universe * 73856093) ^ (startAddress * 19349663) ^ GetInstanceID());
            double elapsed = System.Math.Max(0d, CurrentShutterClockTime - _shutterStrobePhaseStartedAt);
            _shutterStrobeGate01 = ShutterStrobeEvaluator.EvaluateGate(
                _shutterStrobeCommand, elapsed, seed, shutterStrobePreviewMode, Application.isPlaying);
        }

        private void ResetShutterStrobeState()
        {
            _shutterStrobeCommand = ShutterStrobeEvaluator.OpenCommand;
            _shutterStrobeGate01 = 1f;
            _shutterStrobePhaseStartedAt = CurrentShutterClockTime;
            _shutterStrobeInitialized = false;
        }

        private static double CurrentShutterClockTime => Application.isPlaying ? Time.timeAsDouble : 0d;
    }
}
