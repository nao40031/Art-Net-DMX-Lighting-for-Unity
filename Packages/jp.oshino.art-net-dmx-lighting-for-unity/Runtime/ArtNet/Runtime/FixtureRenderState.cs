/*!
 * Copyright (c) 2026 Oshino
 *
 * Released under the MIT license.
 * see https://opensource.org/licenses/MIT
 */

using UnityEngine;

namespace ArtNet.Runtime
{
    public struct FixtureRenderState
    {
        public float lightDimmer01;
        public float lensDimmer01;
        public Color color;
        // Sync Beam Color To Dmx の値。実ライトもPseudo Beamと同じ設定に従う。
        public bool syncLightColorToDmx;
        public bool goboEnabled;
        public Texture goboTexture;
        public float goboRotationDeg;
        public Vector2 goboOffsetUv;
        public Vector2 beamShakeAngleDeg;
        public bool zoomEnabled;
        public float outerSpotAngleDeg;
        public float innerSpotPercent;
        public bool prismEnabled;
        public int prismFacetCount;
        public float prismSpread;
        public float prismRotationDeg;
        public float prismRotationSpeedDegPerSec;
        public float prismIntensityScale;
        public float vlbPrismGoboScale;
    }
}
