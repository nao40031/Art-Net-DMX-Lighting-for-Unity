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
        // Sync Beam * To Dmx の値。実ライト、Pseudo Beam、VLBは同じ設定に従う。
        public bool syncLightColorToDmx;
        public bool syncLightDimmerToDmx;
        public bool syncLightGoboToDmx;
        public bool syncLightGoboRotationToDmx;
        public bool syncLightZoomToDmx;
        public bool syncBeamPrismToDmx;
        // Prism auxiliary rendering can replace the primary light even when Dimmer sync is disabled.
        public bool forceLightOff;
        public bool goboEnabled;
        public Texture goboTexture;
        public float goboRotationDeg;
        public Vector2 goboOffsetUv;
        // Kept separate so the lens can choose the visual direction of shake
        // without changing the floor projection or beam state.
        public float goboShakeRotationOffsetDeg;
        public Vector2 goboShakeOffsetUv;
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
