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
    public enum PrismRangeType
    {
        Select,
        Rotation,
        Index,
        NoFunction,
        Open
    }

    [Serializable]
    public class PrismSlotRange
    {
        public string name;

        [Range(0, 255)]
        public int dmxMin = 0;

        [Range(0, 255)]
        public int dmxMax = 0;

        public PrismRangeType type = PrismRangeType.Select;

        public bool Contains(int dmxValue)
        {
            int clamped = Mathf.Clamp(dmxValue, 0, 255);
            int min = Mathf.Min(dmxMin, dmxMax);
            int max = Mathf.Max(dmxMin, dmxMax);
            return clamped >= min && clamped <= max;
        }
    }

    [Serializable]
    public class PrismSlot
    {
        public string name;

        [Range(0, 255)]
        public int dmxMin = 0;

        [Range(0, 255)]
        public int dmxMax = 255;

        public bool isOpen = false;

        [Min(1)]
        public int facetCount = 1;

        [Tooltip("CookieCompositeではUV上の分散距離、AuxiliaryLightsではAuxiliary Spread Multiplierで角度へ変換されます。")]
        [Range(0f, 1f)]
        public float spread = 0.25f;

        [Tooltip("CookieCompositeで1つ1つのゴボ像の大きさを決めます。")]
        [Range(0.05f, 2f)]
        public float facetScale = 0.55f;

        public float rotationOffsetDeg = 0f;

        [Tooltip("プリズム時の明るさ補正。1で補正なし。")]
        [Min(0f)]
        public float intensityScale = 1f;

        public List<PrismSlotRange> additionalRanges = new();

        public bool Contains(int dmxValue)
        {
            int clamped = Mathf.Clamp(dmxValue, 0, 255);
            int min = Mathf.Min(dmxMin, dmxMax);
            int max = Mathf.Max(dmxMin, dmxMax);
            if (clamped >= min && clamped <= max)
                return true;

            if (additionalRanges == null)
                return false;

            for (int i = 0; i < additionalRanges.Count; i++)
            {
                var range = additionalRanges[i];
                if (range != null && range.Contains(clamped))
                    return true;
            }

            return false;
        }
    }

    [CreateAssetMenu(menuName = "ArtNet/DMX/Prism Definition", fileName = "PrismDefinition")]
    public class PrismDefinition : ScriptableObject
    {
        public List<PrismSlot> slots = new();

        public PrismSlot ResolveSlot(int dmxValue)
        {
            int clamped = Mathf.Clamp(dmxValue, 0, 255);
            if (slots == null)
                return null;

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot != null && slot.Contains(clamped))
                    return slot;
            }

            return null;
        }
    }
}
