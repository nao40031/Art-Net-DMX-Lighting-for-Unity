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

        public void Initialize(Light targetLight)
        {
            _light = targetLight;
            _hd = (_light != null) ? _light.GetComponent<HDAdditionalLightData>() : null;
        }

        public void Apply(float dimmer01, Color rgb)
        {
            if (_light == null) return;

            float d = ApplyCurve(dimmer01);
            float intensity = d * maxIntensity;

            _light.color = rgb;

            if (_hd != null)
            {
                try
                {
                    _hd.SetIntensity(intensity, ToLightUnit(unit));
                }
                catch
                {
                    _light.intensity = intensity;
                }
            }
            else
            {
                _light.intensity = intensity;
            }
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
