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
        public bool goboEnabled;
        public Texture goboTexture;
        public float goboRotationDeg;
        public bool zoomEnabled;
        public float outerSpotAngleDeg;
        public float innerSpotPercent;
    }
}
