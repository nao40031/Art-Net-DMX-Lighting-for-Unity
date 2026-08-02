/*!
 * Copyright (c) 2026 Oshino
 *
 * Released under the MIT license.
 * see https://opensource.org/licenses/MIT
 */

using UnityEngine;

namespace ArtNet.Runtime
{
    /// <summary>
    /// Built-in / URP light driver.
    /// Applies DMX dimmer/color to Light.intensity and Light.color.
    /// </summary>
    public class GenericLightDriver : MonoBehaviour, ILightDriver
    {
        [Header("Generic Light Params")]
        [Tooltip("Intensity at Dimmer=1.0")]
        public float maxIntensity = 2.0f;

        [Tooltip("Optional dimmer response curve (0..1 -> 0..1)")]
        public AnimationCurve dimmerCurve;

        private Light _light;

        public void Initialize(Light targetLight)
        {
            _light = targetLight;
        }

        public void Apply(FixtureRenderState state)
        {
            if (_light == null) return;

            float d = ApplyCurve(state.lightDimmer01);
            _light.intensity = d * maxIntensity;
            _light.color = state.color;
            _light.cookie = state.goboEnabled ? state.goboTexture : null;

            if (state.zoomEnabled)
            {
                float outer = Mathf.Clamp(state.outerSpotAngleDeg, 0.1f, 179f);
                float inner01 = Mathf.Clamp01(state.innerSpotPercent / 100f);
                _light.spotAngle = outer;
                _light.innerSpotAngle = outer * inner01;
            }
        }

        private float ApplyCurve(float x)
        {
            if (dimmerCurve == null || dimmerCurve.length == 0) return Mathf.Clamp01(x);
            return Mathf.Clamp01(dimmerCurve.Evaluate(Mathf.Clamp01(x)));
        }
    }
}
