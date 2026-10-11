using System.Collections.Generic;
using UnityEngine;

namespace ArtNet.Runtime
{
    public partial class DmxFixtureComponent
    {
        private sealed class ColorWheelRuntime
        {
            public int instance;
            public ColorWheelProfile profile;
            public ColorWheelRange range;
            public int raw;
            public float phase;
            public Color tint = Color.white;
        }

        private readonly List<ColorWheelRuntime> _colorWheels = new();
        private Color _colorBeforeWheels = Color.white;

        private void ClearColorWheelBindings()
        {
            _colorWheels.Clear();
            _colorBeforeWheels = Color.white;
        }

        private void ResetColorWheelState()
        {
            _colorBeforeWheels = Color.white;
            for (int i = 0; i < _colorWheels.Count; i++)
            {
                ColorWheelRuntime wheel = _colorWheels[i];
                wheel.range = null;
                wheel.raw = 0;
                wheel.phase = 0f;
                wheel.tint = Color.white;
            }
        }

        private void BindColorWheel(int instance, ColorWheelProfile profile, bool replace = true)
        {
            if (profile == null) return;
            instance = Mathf.Max(1, instance);
            for (int i = 0; i < _colorWheels.Count; i++)
            {
                if (_colorWheels[i].instance != instance) continue;
                if (replace) _colorWheels[i].profile = profile;
                return;
            }
            _colorWheels.Add(new ColorWheelRuntime { instance = instance, profile = profile });
        }

        private bool HasColorWheelElement(int instance)
        {
            return _elementMap.ContainsKey(new ElementKey(FixtureAttribute.ColorWheel, instance, FixtureChannelRole.SelectMode)) ||
                   _elementMap.ContainsKey(new ElementKey(FixtureAttribute.ColorWheel, instance, FixtureChannelRole.Value));
        }

        private void ReadColorWheels(int[] universe512)
        {
            for (int i = 0; i < _colorWheels.Count; i++)
            {
                ColorWheelRuntime wheel = _colorWheels[i];
                if (TryReadElementRawForRange(universe512, FixtureAttribute.ColorWheel, wheel.instance,
                        FixtureChannelRole.SelectMode, out int raw, out int rawMax, out _) ||
                    TryReadElementRawForRange(universe512, FixtureAttribute.ColorWheel, wheel.instance,
                        FixtureChannelRole.Value, out raw, out rawMax, out _))
                    SetColorWheelRaw(wheel, raw, rawMax);
            }
        }

        private void ReadColorWheels(byte[] universe512)
        {
            for (int i = 0; i < _colorWheels.Count; i++)
            {
                ColorWheelRuntime wheel = _colorWheels[i];
                if (TryReadElementRawForRange(universe512, FixtureAttribute.ColorWheel, wheel.instance,
                        FixtureChannelRole.SelectMode, out int raw, out int rawMax, out _) ||
                    TryReadElementRawForRange(universe512, FixtureAttribute.ColorWheel, wheel.instance,
                        FixtureChannelRole.Value, out raw, out rawMax, out _))
                    SetColorWheelRaw(wheel, raw, rawMax);
            }
        }

        private void ReadLegacyColorWheel(int[] universe512)
        {
            if (_colorWheels.Count == 0 || !TryGetRelativeChannel(FixtureFunction.ColorWheel, out int channel)) return;
            SetColorWheelRaw(_colorWheels[0], Read8Abs(universe512, startAddress + channel - 1), 255);
        }

        private void ReadLegacyColorWheel(byte[] universe512)
        {
            if (_colorWheels.Count == 0 || !TryGetRelativeChannel(FixtureFunction.ColorWheel, out int channel)) return;
            SetColorWheelRaw(_colorWheels[0], Read8Abs(universe512, startAddress + channel - 1), 255);
        }

        private Color ApplyLegacyColorFilters(Color rgb, int[] universe512)
        {
            if (!HasLegacyColorFilterChannels()) return rgb;
            ColorMixProfile profile = fixture != null ? fixture.colorMixProfile : null;
            bool c = TryGetRelativeChannel(FixtureFunction.Cyan, out int cRel);
            bool m = TryGetRelativeChannel(FixtureFunction.Magenta, out int mRel);
            bool y = TryGetRelativeChannel(FixtureFunction.Yellow, out int yRel);
            if (c || m || y)
            {
                float cyan = c ? Read8Abs(universe512, startAddress + cRel - 1) / 255f : 0f;
                float magenta = m ? Read8Abs(universe512, startAddress + mRel - 1) / 255f : 0f;
                float yellow = y ? Read8Abs(universe512, startAddress + yRel - 1) / 255f : 0f;
                rgb = profile != null ? profile.EvaluateCmy(cyan, magenta, yellow)
                    : new Color(1f - cyan, 1f - magenta, 1f - yellow, 1f);
            }
            if (TryGetRelativeChannel(FixtureFunction.CTC, out int ctcRel))
                rgb = MultiplyColor(rgb, profile != null ? profile.EvaluateTemperature(FixtureAttribute.CTC, Read8Abs(universe512, startAddress + ctcRel - 1) / 255f)
                    : ApplyCtcApproximation(Color.white, Read8Abs(universe512, startAddress + ctcRel - 1) / 255f));
            if (TryGetRelativeChannel(FixtureFunction.CTO, out int ctoRel))
                rgb = MultiplyColor(rgb, profile != null ? profile.EvaluateTemperature(FixtureAttribute.CTO, Read8Abs(universe512, startAddress + ctoRel - 1) / 255f)
                    : ApplyCtcApproximation(Color.white, Read8Abs(universe512, startAddress + ctoRel - 1) / 255f));
            if (TryGetRelativeChannel(FixtureFunction.CTB, out int ctbRel))
            {
                float amount = Read8Abs(universe512, startAddress + ctbRel - 1) / 255f;
                rgb = MultiplyColor(rgb, profile != null ? profile.EvaluateTemperature(FixtureAttribute.CTB, amount)
                    : Color.Lerp(Color.white, new Color(0.65f, 0.82f, 1f), amount));
            }
            return ClampColor(rgb);
        }

        private Color ApplyLegacyColorFilters(Color rgb, byte[] universe512)
        {
            if (!HasLegacyColorFilterChannels()) return rgb;
            ColorMixProfile profile = fixture != null ? fixture.colorMixProfile : null;
            bool c = TryGetRelativeChannel(FixtureFunction.Cyan, out int cRel);
            bool m = TryGetRelativeChannel(FixtureFunction.Magenta, out int mRel);
            bool y = TryGetRelativeChannel(FixtureFunction.Yellow, out int yRel);
            if (c || m || y)
            {
                float cyan = c ? Read8Abs(universe512, startAddress + cRel - 1) / 255f : 0f;
                float magenta = m ? Read8Abs(universe512, startAddress + mRel - 1) / 255f : 0f;
                float yellow = y ? Read8Abs(universe512, startAddress + yRel - 1) / 255f : 0f;
                rgb = profile != null ? profile.EvaluateCmy(cyan, magenta, yellow)
                    : new Color(1f - cyan, 1f - magenta, 1f - yellow, 1f);
            }
            if (TryGetRelativeChannel(FixtureFunction.CTC, out int ctcRel))
                rgb = MultiplyColor(rgb, profile != null ? profile.EvaluateTemperature(FixtureAttribute.CTC, Read8Abs(universe512, startAddress + ctcRel - 1) / 255f)
                    : ApplyCtcApproximation(Color.white, Read8Abs(universe512, startAddress + ctcRel - 1) / 255f));
            if (TryGetRelativeChannel(FixtureFunction.CTO, out int ctoRel))
                rgb = MultiplyColor(rgb, profile != null ? profile.EvaluateTemperature(FixtureAttribute.CTO, Read8Abs(universe512, startAddress + ctoRel - 1) / 255f)
                    : ApplyCtcApproximation(Color.white, Read8Abs(universe512, startAddress + ctoRel - 1) / 255f));
            if (TryGetRelativeChannel(FixtureFunction.CTB, out int ctbRel))
            {
                float amount = Read8Abs(universe512, startAddress + ctbRel - 1) / 255f;
                rgb = MultiplyColor(rgb, profile != null ? profile.EvaluateTemperature(FixtureAttribute.CTB, amount)
                    : Color.Lerp(Color.white, new Color(0.65f, 0.82f, 1f), amount));
            }
            return ClampColor(rgb);
        }

        private static void SetColorWheelRaw(ColorWheelRuntime wheel, int raw, int rawMax)
        {
            if (wheel.profile == null) return;
            int profileMax = wheel.profile.use16BitRanges ? 65535 : 255;
            int scaled = Mathf.Clamp(Mathf.RoundToInt(raw * (profileMax / Mathf.Max(1f, rawMax))), 0, profileMax);
            ColorWheelRange range = wheel.profile.ResolveRange(scaled);
            if (range != wheel.range)
            {
                if (range != null && range.type != ColorWheelRangeType.Stop)
                    wheel.phase = 0f;
                wheel.range = range;
            }
            wheel.raw = scaled;
            if (range == null || range.type == ColorWheelRangeType.Open)
                wheel.tint = Color.white;
            else if (range.type == ColorWheelRangeType.Slot || range.type == ColorWheelRangeType.Split ||
                     range.type == ColorWheelRangeType.Continuous)
                wheel.tint = range.EvaluateStatic(scaled);
            else if (range.type != ColorWheelRangeType.Stop)
                wheel.tint = EvaluateMovingWheel(wheel);
        }

        private static Color EvaluateMovingWheel(ColorWheelRuntime wheel)
        {
            int count = wheel.profile.CountSpinSlots();
            if (count == 0) return Color.white;
            float slotPosition = wheel.phase * count;
            int step = Mathf.FloorToInt(slotPosition);
            int index;
            if (wheel.range.type == ColorWheelRangeType.Random)
            {
                uint hash = (uint)(step + wheel.instance * 101);
                hash ^= hash >> 16;
                hash *= 0x7feb352d;
                hash ^= hash >> 15;
                index = (int)(hash % (uint)count);
            }
            else
            {
                index = ((step % count) + count) % count;
                if (wheel.range.type == ColorWheelRangeType.SpinCCW)
                    index = count - 1 - index;
            }
            Color current = wheel.profile.EvaluateSpinSlot(index);
            if (wheel.range.type == ColorWheelRangeType.Random) return current;
            int next = wheel.range.type == ColorWheelRangeType.SpinCCW
                ? (index + count - 1) % count : (index + 1) % count;
            return Color.Lerp(current, wheel.profile.EvaluateSpinSlot(next), slotPosition - step);
        }

        private Color ApplyColorWheels(Color baseColor)
        {
            _colorBeforeWheels = baseColor;
            if (_colorWheels.Count == 0) return baseColor;
            for (int i = 0; i < _colorWheels.Count; i++)
                baseColor = MultiplyColor(baseColor, _colorWheels[i].tint);
            return ClampColor(baseColor);
        }

        private bool HasLegacyColorFilterChannels()
        {
            return TryGetRelativeChannel(FixtureFunction.Cyan, out _) ||
                   TryGetRelativeChannel(FixtureFunction.Magenta, out _) ||
                   TryGetRelativeChannel(FixtureFunction.Yellow, out _) ||
                   TryGetRelativeChannel(FixtureFunction.CTC, out _) ||
                   TryGetRelativeChannel(FixtureFunction.CTO, out _) ||
                   TryGetRelativeChannel(FixtureFunction.CTB, out _);
        }

        private void UpdateColorWheelMotion()
        {
            if (!_hasDmxTargets || _colorWheels.Count == 0) return;
            bool moving = false;
            for (int i = 0; i < _colorWheels.Count; i++)
            {
                ColorWheelRuntime wheel = _colorWheels[i];
                ColorWheelRange range = wheel.range;
                if (range == null || (range.type != ColorWheelRangeType.SpinCW &&
                    range.type != ColorWheelRangeType.SpinCCW && range.type != ColorWheelRangeType.Random))
                    continue;
                float speed = Mathf.Lerp(Mathf.Max(0f, range.cyclesPerSecondAtMin),
                    Mathf.Max(0f, range.cyclesPerSecondAtMax), range.Normalize(wheel.raw));
                wheel.phase = Mathf.Repeat(wheel.phase + speed * Time.unscaledDeltaTime, 10000f);
                wheel.tint = EvaluateMovingWheel(wheel);
                moving = true;
            }
            if (moving)
                _targetColor = ApplyColorWheels(_colorBeforeWheels);
        }
    }
}
