/*!
 * Copyright (c) 2026 Oshino
 *
 * Released under the MIT license.
 * see https://opensource.org/licenses/MIT
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

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
        ColorMacro = 20,
        Frost = 21
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
        Sync = 17,
        IrisHold = 18,
        IrisPulseReverse = 19,
        PanTiltSpeedStandard = 20,
        PanTiltSpeedFast = 21,
        PanTiltSpeedSmooth = 22
    }

    public enum NormalizedMappingContext
    {
        Generic = 0,
        Zoom = 1,
        Focus = 2,
        Iris = 3,
        Frost = 4,
        RotationSpeed = 5,
        IndexPosition = 6
    }

    public enum NormalizedMappingPreset
    {
        Normal = 0,
        Inverted = 1,
        Custom = 2,
        NotUsed = 3
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

        [Range(0f, 10f)]
        [Tooltip("この範囲を有効にするためDMX値を保持する秒数です。0なら即時反映します。\n\nSeconds the DMX value must remain in this range before activation. Zero applies immediately.")]
        public float activationHoldSeconds = 0f;

        public NormalizedMappingContext mappingContext = NormalizedMappingContext.Generic;
        public NormalizedMappingPreset mappingPreset = NormalizedMappingPreset.Normal;
        public float normalizedFrom = 0f;
        public float normalizedTo = 1f;

        public bool Contains(int dmxValue)
        {
            int clamped = Mathf.Clamp(dmxValue, 0, 65535);
            int min = Mathf.Min(dmxMin, dmxMax);
            int max = Mathf.Max(dmxMin, dmxMax);
            return clamped >= min && clamped <= max;
        }

        public bool HasExplicitMapping(int defaultDmxMax)
        {
            if (mappingPreset == NormalizedMappingPreset.NotUsed)
                return false;

            if (mappingContext != NormalizedMappingContext.Generic)
                return true;

            if (mappingPreset != NormalizedMappingPreset.Normal)
                return true;

            if (dmxMin != 0 || dmxMax != defaultDmxMax)
                return true;

            return false;
        }

        public float Normalize(int dmxValue)
        {
            if (mappingPreset == NormalizedMappingPreset.NotUsed)
                return 0f;

            int min = Mathf.Min(dmxMin, dmxMax);
            int max = Mathf.Max(dmxMin, dmxMax);
            if (max <= min)
                return ResolvePresetValue(0f);

            float t = Mathf.InverseLerp(min, max, Mathf.Clamp(dmxValue, min, max));
            return ResolvePresetValue(t);
        }

        private float ResolvePresetValue(float t)
        {
            return mappingPreset switch
            {
                NormalizedMappingPreset.Inverted => 1f - t,
                NormalizedMappingPreset.Custom => Mathf.Lerp(normalizedFrom, normalizedTo, t),
                NormalizedMappingPreset.NotUsed => 0f,
                _ => t
            };
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

        public GoboWheelProfile goboWheel;
    }

    [Serializable]
    public class FixtureFrostBinding
    {
        [Tooltip("同じ灯体に複数のフロスト機構がある場合の1始まりの番号です。\n\nOne-based instance number when a fixture has multiple frost mechanisms.")]
        [Min(1)]
        public int instance = 1;

        [Tooltip("このフロスト段の光学特性です。未設定時はFixture Typeの共通Frost Profileを使用します。\n\nOptical characteristics for this frost stage. When unset, the Fixture Type's shared Frost Profile is used.")]
        public FrostProfile profile;
    }

    [Serializable]
    public class FixturePanTiltSpeedProfile
    {
        [Tooltip("Pan/Tilt速度プリセットに対応するRange Type。\n\nRange Type that activates this Pan/Tilt speed profile.")]
        public FixtureRangeType preset = FixtureRangeType.PanTiltSpeedStandard;

        [Min(0f)]
        [Tooltip("Panの最大角速度（deg/sec）。0の場合はDmx Fixture Componentの共通速度設定を使用。\n\nMaximum Pan angular speed in deg/sec. Zero uses the shared Dmx Fixture Component speed setting.")]
        public float panMaxDegPerSec;

        [Min(0f)]
        [Tooltip("Tiltの最大角速度（deg/sec）。0の場合はDmx Fixture Componentの共通速度設定を使用。\n\nMaximum Tilt angular speed in deg/sec. Zero uses the shared Dmx Fixture Component speed setting.")]
        public float tiltMaxDegPerSec;

        [Min(0f)]
        [Tooltip("停止状態から最大速度へ到達する時間（秒）。0の場合は従来どおり一定速度で移動。\n\nSeconds to reach maximum speed from rest. Zero preserves the legacy constant-speed motion.")]
        public float accelerationTime;

        [Min(0f)]
        [Tooltip("最大速度から停止するまでの時間（秒）。0の場合は従来どおり一定速度で移動。\n\nSeconds to stop from maximum speed. Zero preserves the legacy constant-speed motion.")]
        public float decelerationTime;
    }

    [Serializable]
    public class FixtureMode
    {
        [Tooltip("表示用のモード名（例: Mode 3 / Extended 16bit）\nMode name displayed in the Inspector, for example Mode 3 or Extended 16-bit.")]
        public string modeName = "Mode 1";

        [Tooltip("このModeが消費するチャンネル数（確認用）\nNumber of channels consumed by this Mode, for reference.")]
        [Range(1, 512)]
        public int channelCount = 1;

        [Tooltip("Function → channel割当\nFunction-to-channel assignments.")]
        public List<FunctionChannel> channels = new();

        [Header("Channel Elements (GDTF-lite)")]
        [Tooltip("Optional CH-by-CH definition. Element 0 is CH1, Element 1 is CH2, and so on.")]
        public List<FixtureChannelElement> elements = new();

        [Tooltip("Wheel definitions bound by Attribute + Instance. Used by GoboWheel slots now.")]
        public List<FixtureWheelBinding> wheelBindings = new();

        [Tooltip("Frost AttributeのInstanceと光学プロファイルの対応です。複数段のフロストを個別に定義できます。\n\nMaps Frost Attribute instances to optical profiles. Multiple frost stages can be defined independently.")]
        public List<FixtureFrostBinding> frostBindings = new();

        [Header("Pan/Tilt Speed Profiles")]
        [Tooltip("プリセット方式のPan/Tilt速度プロファイル。未設定の灯体はDmx Fixture Componentの既存速度設定を使用。\n\nPan/Tilt speed profiles for preset-based fixtures. Fixtures without profiles use the existing Dmx Fixture Component speed settings.")]
        public List<FixturePanTiltSpeedProfile> panTiltSpeedProfiles = new();

        public bool UsesChannelElements()
        {
            return elements != null && elements.Count > 0;
        }
    }

    [MovedFrom(true, sourceNamespace: "ArtNet.Runtime", sourceClassName: "FixtureDefinition")]
    [CreateAssetMenu(menuName = "ArtNet/DMX/Fixture Type", fileName = "FixtureType")]
    public class FixtureType : ScriptableObject
    {
        public IrisProfile irisProfile;
        [Tooltip("灯体のFocus光学特性です。チャンネル番号とDMX方向はModeのChannel Elementsで定義します。\n\nOptical Focus characteristics for this fixture. Define channel numbers and DMX direction in the Mode Channel Elements.")]
        public FocusProfile focusProfile;
        [Tooltip("モード側で個別指定されていないフロスト段に使用する共通プロファイルです。\n\nShared profile used by frost stages that do not specify a profile in the mode.")]
        public FrostProfile frostProfile;
        [Tooltip("この灯体の共通プリズムプロファイルです。Dmx Fixture Component側で個別に上書きできます。\n\nShared prism profile for this fixture. It can be overridden per fixture in Dmx Fixture Component.")]
        public PrismProfile prismProfile;
        [Header("Identity (for humans/logs)")]
        public string manufacturer;
        public string model;
        public string revision;
        public string displayName;

        [Header("Modes")]
        public List<FixtureMode> modes = new();

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
                var fixtureMode = modes[i];
                if (fixtureMode == null) continue;

                int count = Mathf.Clamp(fixtureMode.channelCount, 1, 512);
                fixtureMode.channelCount = count;

                if (fixtureMode.elements == null)
                    fixtureMode.elements = new List<FixtureChannelElement>();

                if (!forceCreate && fixtureMode.elements.Count == 0)
                    continue;

                while (fixtureMode.elements.Count < count)
                    fixtureMode.elements.Add(new FixtureChannelElement());

                while (fixtureMode.elements.Count > count)
                    fixtureMode.elements.RemoveAt(fixtureMode.elements.Count - 1);
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
