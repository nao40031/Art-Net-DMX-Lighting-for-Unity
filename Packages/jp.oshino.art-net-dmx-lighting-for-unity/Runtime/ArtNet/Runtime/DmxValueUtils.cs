using UnityEngine;

namespace ArtNet.Runtime
{
    public static class DmxValueUtils
    {
        public static float ByteTo01(int v) => Mathf.Clamp01(v / 255f);
        public static int ClampByte(int v) => Mathf.Clamp(v, 0, 255);

        public static int Read8(int[] dmx512, int ch1Based)
        {
            if (dmx512 == null) return 0;
            int idx = ch1Based - 1;
            if (idx < 0 || idx >= dmx512.Length) return 0;
            return ClampByte(dmx512[idx]);
        }

        /// <summary>
        /// 16bit値を (coarse<<8) | fine として読む。
        /// fineが無い場合は coarse を上位8bitとして扱う（0..65280）。
        /// </summary>
        public static int Read16(int[] dmx512, int coarseCh, int fineChOrMinus1)
        {
            int coarse = Read8(dmx512, coarseCh);
            if (fineChOrMinus1 <= 0) return coarse << 8;
            int fine = Read8(dmx512, fineChOrMinus1);
            return (coarse << 8) | fine; // 0..65535
        }
    }
}
