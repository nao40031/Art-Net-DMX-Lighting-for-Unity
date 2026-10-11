using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArtNet.Runtime
{
    public enum ColorWheelRangeType
    {
        Open = 0,
        Slot = 1,
        Split = 2,
        Continuous = 3,
        SpinCW = 4,
        SpinCCW = 5,
        Random = 6,
        Stop = 7
    }

    [Serializable]
    public sealed class ColorWheelRange
    {
        [Tooltip("DMX範囲の表示名です。\n\nDisplay name of this DMX range.")]
        public string name;
        [Tooltip("この範囲のDMX最小値です。\n\nMinimum DMX value in this range.")]
        [Range(0, 65535)] public int dmxMin;
        [Tooltip("この範囲のDMX最大値です。\n\nMaximum DMX value in this range.")]
        [Range(0, 65535)] public int dmxMax = 255;
        [Tooltip("Open、Slot、Split、回転などの動作です。\n\nOpen, slot, split, spin, or other wheel behavior.")]
        public ColorWheelRangeType type = ColorWheelRangeType.Slot;

        [Tooltip("スロットの透過色、または連続範囲の開始色です。\n\nSlot transmission color or the starting color of a continuous range.")]
        public Color colorA = Color.white;
        [Tooltip("Splitの隣接色、または連続範囲の終了色です。\n\nAdjacent split color or the ending color of a continuous range.")]
        public Color colorB = Color.white;
        [Tooltip("Split時に色Bが占める面積比です。\n\nArea fraction of color B for a split slot.")]
        [Range(0f, 1f)] public float splitFraction = 0.5f;
        [Tooltip("回転・ランダム時の物理ホイール順にこの範囲を含めます。\n\nInclude this range in the physical wheel order for spin and random behavior.")]
        public bool includeInSpin = true;
        [Tooltip("RGB透過率に追加で掛ける無彩色の透過率です。\n\nNeutral transmission applied in addition to RGB transmittance.")]
        [Range(0f, 1f)] public float neutralTransmission = 1f;
        [Tooltip("範囲のDMX最小値に対応する回転数（毎秒）です。\n\nWheel cycles per second at the range minimum.")]
        [Min(0f)] public float cyclesPerSecondAtMin = 0.2f;
        [Tooltip("範囲のDMX最大値に対応する回転数（毎秒）です。\n\nWheel cycles per second at the range maximum.")]
        [Min(0f)] public float cyclesPerSecondAtMax = 2f;

        public bool Contains(int raw) => raw >= Mathf.Min(dmxMin, dmxMax) && raw <= Mathf.Max(dmxMin, dmxMax);

        public float Normalize(int raw) => Mathf.InverseLerp(dmxMin, dmxMax, raw);

        public Color EvaluateStatic(int raw)
        {
            Color result = type == ColorWheelRangeType.Split
                ? Color.Lerp(colorA, colorB, Mathf.Clamp01(splitFraction))
                : type == ColorWheelRangeType.Continuous
                    ? Color.Lerp(colorA, colorB, Normalize(raw)) : colorA;
            float transmission = Mathf.Clamp01(neutralTransmission);
            return new Color(Mathf.Clamp01(result.r) * transmission,
                Mathf.Clamp01(result.g) * transmission, Mathf.Clamp01(result.b) * transmission, 1f);
        }
    }

    [CreateAssetMenu(menuName = "ArtNet/DMX/Color Wheel Profile", fileName = "ColorWheelProfile")]
    public sealed class ColorWheelProfile : ScriptableObject
    {
        [Tooltip("このホイールのDMX範囲が8bitか16bitかを選びます。\n\nSelect whether this wheel profile uses 8-bit or 16-bit DMX ranges.")]
        public bool use16BitRanges;
        [Tooltip("各DMX範囲と色の対応です。重複時は最初の範囲を採用します。\n\nDMX ranges and colors. The first matching range wins on overlap.")]
        public List<ColorWheelRange> ranges = new();

        public ColorWheelRange ResolveRange(int raw)
        {
            if (ranges == null) return null;
            for (int i = 0; i < ranges.Count; i++)
            {
                ColorWheelRange range = ranges[i];
                if (range != null && range.Contains(raw)) return range;
            }
            return null;
        }

        public int CountSpinSlots()
        {
            int count = 0;
            if (ranges == null) return 0;
            for (int i = 0; i < ranges.Count; i++)
                if (IsSpinSlot(ranges[i])) count++;
            return count;
        }

        public Color EvaluateSpinSlot(int index)
        {
            if (ranges == null) return Color.white;
            int current = 0;
            for (int i = 0; i < ranges.Count; i++)
            {
                ColorWheelRange range = ranges[i];
                if (!IsSpinSlot(range)) continue;
                if (current++ == index) return range.EvaluateStatic(range.dmxMin);
            }
            return Color.white;
        }

        private static bool IsSpinSlot(ColorWheelRange range)
        {
            return range != null && range.includeInSpin &&
                (range.type == ColorWheelRangeType.Slot || range.type == ColorWheelRangeType.Split);
        }
    }
}
