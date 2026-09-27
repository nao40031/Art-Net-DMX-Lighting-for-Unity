/*!
 * Copyright (c) 2026 Oshino
 *
 * Released under the MIT license.
 * see https://opensource.org/licenses/MIT
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace ArtNet.Runtime
{
    public enum GoboRangeType
    {
        Select,
        Rotation,
        Shake,
        WheelScroll,
        NoFunction,
        Open
    }

    public enum GoboShakeAmplitudeMapping
    {
        Fixed,
        SlowLargeFastSmall,
        SlowSmallFastLarge
    }

    public enum GoboShakeMotionMode
    {
        Rotation,
        Position,
        RotationAndPosition
    }

    public enum GoboShakePositionAxis
    {
        Horizontal,
        Vertical,
        Both
    }

    [Serializable]
    public class GoboShakeProfile
    {
        [Tooltip("Enable this Shake range.")]
        public bool enabled = true;

        [Header("Speed")]
        [Tooltip("Shake speed at the range minimum value in Hz.")]
        [Min(0f)]
        public float speedMinHz = 0.4f;

        [Tooltip("Shake speed at the range maximum value in Hz.")]
        [Min(0f)]
        public float speedMaxHz = 10f;

        [Header("Motion")]
        public GoboShakeMotionMode motionMode = GoboShakeMotionMode.Rotation;
        public GoboShakePositionAxis positionAxis = GoboShakePositionAxis.Horizontal;
        public bool affectBeam = true;
        public bool applyWithPrism = true;
        public bool applyWithZoom = true;
        public bool allowDuringGoboRotation = true;

        [Header("Rotation Amplitude")]
        [Tooltip("Minimum rotational shake amplitude in degrees.")]
        [Min(0f)]
        public float amplitudeMinDeg = 10f;

        [Tooltip("Maximum rotational shake amplitude in degrees. Fixed mapping uses this value.")]
        [Min(0f)]
        public float amplitudeMaxDeg = 360f;

        [Header("Position Amplitude")]
        [Tooltip("Minimum positional shake amplitude in gobo UV units.")]
        [Min(0f)]
        public float positionAmplitudeMin = 0.015f;

        [Tooltip("Maximum positional shake amplitude in gobo UV units.")]
        [Min(0f)]
        public float positionAmplitudeMax = 0.06f;

        [Header("Beam Angle Amplitude")]
        [Tooltip("Minimum beam direction shake amplitude in degrees.")]
        [Min(0f)]
        public float beamAngleAmplitudeMinDeg = 0.2f;

        [Tooltip("Maximum beam direction shake amplitude in degrees.")]
        [Min(0f)]
        public float beamAngleAmplitudeMaxDeg = 1f;

        [Header("Zoom Scale")]
        [Tooltip("Shake multiplier at the narrowest zoom side.")]
        [Min(0f)]
        public float zoomShakeScaleMin = 0.5f;

        [Tooltip("Shake multiplier at the widest zoom side.")]
        [Min(0f)]
        public float zoomShakeScaleMax = 1.5f;

        [Tooltip("How the DMX value inside the Shake range maps to amplitude.")]
        public GoboShakeAmplitudeMapping amplitudeMapping = GoboShakeAmplitudeMapping.Fixed;
    }

    [Serializable]
    public class GoboSlotRange
    {
        public string name;

        [Range(0, 255)]
        public int dmxMin = 0;

        [Range(0, 255)]
        public int dmxMax = 0;

        public GoboRangeType type = GoboRangeType.Select;

        [Tooltip("Shake settings used when Type is Shake.")]
        [FormerlySerializedAs("shakeOverride")]
        public GoboShakeProfile shake = new();

        public bool Contains(int dmxValue)
        {
            return dmxValue >= dmxMin && dmxValue <= dmxMax;
        }
    }

    [Serializable]
    public class GoboSlot
    {
        public string name;

        [Range(0, 255)]
        public int dmxMin = 0;

        [Range(0, 255)]
        public int dmxMax = 255;

        public Texture2D texture;
        public bool isOpen = false;
        public float rotationOffsetDeg = 0f;
        public List<GoboSlotRange> additionalRanges = new();

        public bool Contains(int dmxValue)
        {
            if (dmxValue >= dmxMin && dmxValue <= dmxMax)
                return true;

            if (additionalRanges == null)
                return false;

            for (int i = 0; i < additionalRanges.Count; i++)
            {
                var range = additionalRanges[i];
                if (range != null && range.Contains(dmxValue))
                    return true;
            }

            return false;
        }
    }

    [CreateAssetMenu(menuName = "ArtNet/DMX/Gobo Wheel Definition", fileName = "GoboWheelDefinition")]
    public class GoboWheelDefinition : ScriptableObject
    {
        [Tooltip("DMX value ranges mapped to gobo slots.")]
        public List<GoboSlot> slots = new();

        public GoboSlot ResolveSlot(int dmxValue)
        {
            return TryResolveSlotRange(dmxValue, out var slot, out _) ? slot : null;
        }

        public bool TryResolveSlotRange(int dmxValue, out GoboSlot slot, out GoboSlotRange range)
        {
            int clamped = Mathf.Clamp(dmxValue, 0, 255);
            for (int i = 0; i < slots.Count; i++)
            {
                slot = slots[i];
                if (slot == null)
                    continue;

                if (clamped >= slot.dmxMin && clamped <= slot.dmxMax)
                {
                    range = null;
                    return true;
                }

                if (slot.additionalRanges == null)
                    continue;

                for (int j = 0; j < slot.additionalRanges.Count; j++)
                {
                    range = slot.additionalRanges[j];
                    if (range != null && range.Contains(clamped))
                        return true;
                }
            }

            slot = null;
            range = null;
            return false;
        }

        public bool TryResolveShakeProfile(GoboSlotRange range, out GoboShakeProfile profile)
        {
            profile = null;
            if (range == null || range.type != GoboRangeType.Shake)
                return false;

            profile = range.shake ?? new GoboShakeProfile();
            profile.speedMinHz = Mathf.Max(0f, profile.speedMinHz);
            profile.speedMaxHz = Mathf.Max(profile.speedMinHz, profile.speedMaxHz);
            profile.amplitudeMinDeg = Mathf.Max(0f, profile.amplitudeMinDeg);
            profile.amplitudeMaxDeg = Mathf.Max(0f, profile.amplitudeMaxDeg);
            profile.positionAmplitudeMin = Mathf.Max(0f, profile.positionAmplitudeMin);
            profile.positionAmplitudeMax = Mathf.Max(0f, profile.positionAmplitudeMax);
            profile.beamAngleAmplitudeMinDeg = Mathf.Max(0f, profile.beamAngleAmplitudeMinDeg);
            profile.beamAngleAmplitudeMaxDeg = Mathf.Max(0f, profile.beamAngleAmplitudeMaxDeg);
            profile.zoomShakeScaleMin = Mathf.Max(0f, profile.zoomShakeScaleMin);
            profile.zoomShakeScaleMax = Mathf.Max(profile.zoomShakeScaleMin, profile.zoomShakeScaleMax);
            return profile.enabled;
        }
    }
}
