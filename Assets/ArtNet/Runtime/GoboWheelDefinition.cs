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

        public bool Contains(int dmxValue)
        {
            return dmxValue >= dmxMin && dmxValue <= dmxMax;
        }
    }

    [CreateAssetMenu(menuName = "ArtNet/DMX/Gobo Wheel Definition", fileName = "GoboWheelDefinition")]
    public class GoboWheelDefinition : ScriptableObject
    {
        [Tooltip("DMX値の範囲ごとに割り当てるゴボスロット定義。")]
        public List<GoboSlot> slots = new();

        public GoboSlot ResolveSlot(int dmxValue)
        {
            int clamped = Mathf.Clamp(dmxValue, 0, 255);
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
