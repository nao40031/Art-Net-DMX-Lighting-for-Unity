/*!
 * Copyright (c) 2026 Oshino
 *
 * Released under the MIT license.
 * see https://opensource.org/licenses/MIT
 */

#if HAS_HDRP
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace ArtNet.Runtime
{
    /// <summary>
    /// HDRP light driver.
    /// Applies DMX dimmer/color to Light and HDAdditionalLightData.
    /// </summary>
    public class HdrpLightDriver : MonoBehaviour, ILightDriver
    {
        public enum HdrpUnit
        {
            Lumen,
            Candela,
            Lux
        }

        [Header("HDRP Light Params")]
        [Tooltip("Intensity at Dimmer=1.0")]
        public float maxIntensity = 9870f;

        [Tooltip("HDRP light unit")]
        public HdrpUnit unit = HdrpUnit.Lumen;

        [Tooltip("Optional dimmer response curve (0..1 -> 0..1)")]
        public AnimationCurve dimmerCurve;

        private Light _light;
        private HDAdditionalLightData _hd;
        private Color _initialColor;
        private bool _hasInitialColor;

        public void Initialize(Light targetLight)
        {
            if (_light != targetLight)
                _hasInitialColor = false;

            _light = targetLight;
            _hd = (_light != null) ? _light.GetComponent<HDAdditionalLightData>() : null;
            if (_light != null && !_hasInitialColor)
            {
                _initialColor = _light.color;
                _hasInitialColor = true;
            }
        }

        public void Apply(FixtureRenderState state)
        {
            if (_light == null) return;

            float d = ApplyCurve(state.lightDimmer01);
            float intensity = d * maxIntensity;
            Texture cookie = state.goboEnabled ? state.goboTexture : Texture2D.whiteTexture;

            _light.color = state.syncLightColorToDmx ? state.color : _initialColor;

            if (_hd != null)
            {
                try
                {
                    _hd.SetIntensity(intensity, ToLightUnit(unit));
                    _hd.SetCookie(cookie);
                    if (state.zoomEnabled)
                    {
                        _hd.SetSpotAngle(Mathf.Clamp(state.outerSpotAngleDeg, 0.1f, 179f));
                        _hd.innerSpotPercent = Mathf.Clamp(state.innerSpotPercent, 0f, 100f);
                    }
                }
                catch
                {
                    _light.intensity = intensity;
                    _light.cookie = state.goboEnabled ? state.goboTexture : null;
                    ApplyGenericZoom(state);
                }
            }
            else
            {
                _light.intensity = intensity;
                _light.cookie = state.goboEnabled ? state.goboTexture : null;
                ApplyGenericZoom(state);
            }
        }

        private void ApplyGenericZoom(FixtureRenderState state)
        {
            if (!state.zoomEnabled || _light == null) return;

            float outer = Mathf.Clamp(state.outerSpotAngleDeg, 0.1f, 179f);
            float inner01 = Mathf.Clamp01(state.innerSpotPercent / 100f);
            _light.spotAngle = outer;
            _light.innerSpotAngle = outer * inner01;
        }

        private static UnityEngine.Rendering.LightUnit ToLightUnit(HdrpUnit u)
        {
            return u switch
            {
                HdrpUnit.Candela => UnityEngine.Rendering.LightUnit.Candela,
                HdrpUnit.Lux => UnityEngine.Rendering.LightUnit.Lux,
                _ => UnityEngine.Rendering.LightUnit.Lumen
            };
        }

        private float ApplyCurve(float x)
        {
            if (dimmerCurve == null || dimmerCurve.length == 0) return Mathf.Clamp01(x);
            return Mathf.Clamp01(dimmerCurve.Evaluate(Mathf.Clamp01(x)));
        }
    }
}
#endif
