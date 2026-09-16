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
        private Color _initialColor;
        private float _initialIntensity;
        private Texture _initialCookie;
        private Vector2 _initialSpotAngles;
        private bool _hasInitialColor;

        public float InitialIntensity => _hasInitialColor ? _initialIntensity : (_light != null ? _light.intensity : 0f);

        public void Initialize(Light targetLight)
        {
            if (_light != targetLight)
                _hasInitialColor = false;

            _light = targetLight;
            if (_light != null && !_hasInitialColor)
            {
                _initialColor = _light.color;
                _initialIntensity = _light.intensity;
                _initialCookie = _light.cookie;
                _initialSpotAngles = new Vector2(_light.spotAngle, _light.innerSpotAngle);
                _hasInitialColor = true;
            }
        }

        public void Apply(FixtureRenderState state)
        {
            if (_light == null) return;

            float d = ApplyCurve(state.lightDimmer01);
            _light.intensity = state.forceLightOff ? 0f : (state.syncLightDimmerToDmx ? d * maxIntensity : _initialIntensity);
            _light.color = state.syncLightColorToDmx ? state.color : _initialColor;
            _light.cookie = state.syncLightGoboToDmx ? (state.goboEnabled ? state.goboTexture : null) : _initialCookie;

            if (state.syncLightZoomToDmx && state.zoomEnabled)
            {
                float outer = Mathf.Clamp(state.outerSpotAngleDeg, 0.1f, 179f);
                float inner01 = Mathf.Clamp01(state.innerSpotPercent / 100f);
                _light.spotAngle = outer;
                _light.innerSpotAngle = outer * inner01;
            }
            else if (!state.syncLightZoomToDmx)
            {
                _light.spotAngle = _initialSpotAngles.x;
                _light.innerSpotAngle = _initialSpotAngles.y;
            }
        }

        private float ApplyCurve(float x)
        {
            if (dimmerCurve == null || dimmerCurve.length == 0) return Mathf.Clamp01(x);
            return Mathf.Clamp01(dimmerCurve.Evaluate(Mathf.Clamp01(x)));
        }
    }
}
