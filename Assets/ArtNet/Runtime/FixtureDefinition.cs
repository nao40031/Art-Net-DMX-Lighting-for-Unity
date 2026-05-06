/*!
 * Copyright (c) 2026 Oshino
 *
 * Released under the MIT license.
 * see https://opensource.org/licenses/MIT
 */

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArtNet.Runtime
{
    // ⚠️ 重要：
    // - 既存アセット互換のため「数値を固定」しています
    // - 今後は “数値を変更しない” でください（追加は末尾に新しい番号で）
    public enum FixtureFunction
    {
        // --- intensity / color ---
        Dimmer = 0,
        Red = 1,
        Green = 2,
        Blue = 3,

        // ✅ WhiteはBlueの直後に表示したい（ただし値は互換のまま固定）
        White = 14,

        // --- pan/tilt (8bit or 16bit by coarse+fine) ---
        PanCoarse = 4,
        PanFine = 5,
        TiltCoarse = 6,
        TiltFine = 7,

        // --- optional / expand as needed ---
        Strobe = 8,
        ColorWheel = 9,
        Gobo = 10,
        Prism = 11,
        Focus = 12,
        Zoom = 13,

        // --- misc ---
        PanTiltSpeed = 15,
        Reset = 16,
        GoboRotation = 17,
        SpecialFunction = 18,
        DimmerSpeedMode = 19,
        ColorMacro = 20
    }

    [Serializable]
    public class FunctionChannel
    {
        public FixtureFunction function;

        [Tooltip("DMX channel (1-512)")]
        [Range(1, 512)]
        public int channel = 1;
    }

    public enum FixtureAttribute
    {
        NoFeature = 0,
        Dimmer = 1,
        Strobe = 2,
        Red = 3,
        Green = 4,
        Blue = 5,
        White = 6,
        Cyan = 7,
        Magenta = 8,
        Yellow = 9,
        CTC = 10,
        ColorWheel = 11,
        GoboWheel = 12,
        AnimationWheel = 13,
        Prism = 14,
        Frost = 15,
        Iris = 16,
        Zoom = 17,
        Focus = 18,
        Pan = 19,
        Tilt = 20,
        FramingBlade = 21,
        FramingModule = 22,
        Control = 23,
        Fx = 24
    }

    public enum FixtureChannelRole
    {
        Value = 0,
        SelectMode = 1,
        Position = 2,
        Rotation = 3,
        PositionOrRotation = 4,
        Speed = 5,
        SpeedDirection = 6,
        Control = 7,
        Sync = 8,
        Macro = 9
    }

    public enum FixtureByteRole
    {
        Single = 0,
        Coarse = 1,
        Fine = 2
    }

    public enum FixtureRangeType
    {
        None = 0,
        NoFunction = 1,
        Open = 2,
        Closed = 3,
        SlotSelect = 4,
        Indexed = 5,
        RotationCW = 6,
        RotationCCW = 7,
        WheelScrollCW = 8,
        WheelScrollCCW = 9,
        Speed = 10,
        Pulse = 11,
        Random = 12,
        Macro = 13,
        Reset = 14,
        LampOn = 15,
        LampOff = 16,
        Sync = 17
    }

    [Serializable]
    public class FixtureChannelRange
    {
        public string name;

        [Range(0, 65535)]
        public int dmxMin = 0;

        [Range(0, 65535)]
        public int dmxMax = 255;

        public FixtureRangeType type = FixtureRangeType.None;
        public float normalizedFrom = 0f;
        public float normalizedTo = 1f;

        public bool Contains(int dmxValue)
        {
            int clamped = Mathf.Clamp(dmxValue, 0, 65535);
            return clamped >= dmxMin && clamped <= dmxMax;
        }
    }

    [Serializable]
    public class FixtureChannelElement
    {
        [Tooltip("What this DMX channel controls.")]
        public FixtureAttribute attribute = FixtureAttribute.NoFeature;

        [Tooltip("1-based instance for repeated attributes such as GoboWheel 1 / 2.")]
        [Min(1)]
        public int instance = 1;

        [Tooltip("The role of this channel inside the selected attribute.")]
        public FixtureChannelRole role = FixtureChannelRole.Value;

        [Tooltip("Single = 8-bit, Coarse/Fine = upper/lower byte of a 16-bit value.")]
        public FixtureByteRole byteRole = FixtureByteRole.Single;

        [Tooltip("Optional DMX value ranges. Wheel slot ranges should stay in the wheel definition.")]
        public List<FixtureChannelRange> ranges = new();
    }

    [Serializable]
    public class FixtureWheelBinding
    {
        [Tooltip("Wheel-like attribute to bind. GoboWheel is supported now; ColorWheel/AnimationWheel can be added later.")]
        public FixtureAttribute attribute = FixtureAttribute.GoboWheel;

        [Min(1)]
        public int instance = 1;

        public GoboWheelDefinition goboWheel;
    }

    [Serializable]
    public class FixtureModeDefinition
    {
        [Tooltip("表示用のモード名（例: Mode 3 / Extended 16bit）")]
        public string modeName = "Mode 1";

        [Tooltip("このModeが消費するチャンネル数（確認用）")]
        [Range(1, 512)]
        public int channelCount = 1;

        [Tooltip("Function -> channel 割当")]
        public List<FunctionChannel> channels = new();

        [Header("Channel Elements (GDTF-lite)")]
        [Tooltip("Optional CH-by-CH definition. Element 0 is CH1, Element 1 is CH2, and so on.")]
        public List<FixtureChannelElement> elements = new();

        [Tooltip("Wheel definitions bound by Attribute + Instance. Used by GoboWheel slots now.")]
        public List<FixtureWheelBinding> wheelBindings = new();

        public bool UsesChannelElements()
        {
            return elements != null && elements.Count > 0;
        }
    }

    [CreateAssetMenu(menuName = "ArtNet/DMX/Fixture Definition", fileName = "FixtureDefinition")]
    public class FixtureDefinition : ScriptableObject
    {
        [Header("Identity (for humans/logs)")]
        public string manufacturer;
        public string model;
        public string revision;
        public string displayName;

        [Header("Modes")]
        public List<FixtureModeDefinition> modes = new();

        [ContextMenu("Sync Element Lists To Channel Counts")]
        public void SyncElementListsToChannelCounts()
        {
            SyncElementListsToChannelCounts(forceCreate: true);
        }

        private void OnValidate()
        {
            SyncElementListsToChannelCounts(forceCreate: false);
        }

        private void SyncElementListsToChannelCounts(bool forceCreate)
        {
            if (modes == null) return;

            for (int i = 0; i < modes.Count; i++)
            {
                var modeDefinition = modes[i];
                if (modeDefinition == null) continue;

                int count = Mathf.Clamp(modeDefinition.channelCount, 1, 512);
                modeDefinition.channelCount = count;

                if (modeDefinition.elements == null)
                    modeDefinition.elements = new List<FixtureChannelElement>();

                if (!forceCreate && modeDefinition.elements.Count == 0)
                    continue;

                while (modeDefinition.elements.Count < count)
                    modeDefinition.elements.Add(new FixtureChannelElement());

                while (modeDefinition.elements.Count > count)
                    modeDefinition.elements.RemoveAt(modeDefinition.elements.Count - 1);
            }
        }

        public string GetDisplayLabel()
        {
            string name = string.IsNullOrWhiteSpace(displayName)
                ? $"{manufacturer} {model}".Trim()
                : displayName.Trim();

            if (!string.IsNullOrWhiteSpace(revision))
                name += $" (Rev {revision.Trim()})";

            return name;
        }
    }
}
