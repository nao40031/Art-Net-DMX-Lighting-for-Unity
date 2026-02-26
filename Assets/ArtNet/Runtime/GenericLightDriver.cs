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

        public void Apply(float dimmer01, Color rgb)
        {
            if (_light == null) return;

            float d = ApplyCurve(dimmer01);
            _light.intensity = d * maxIntensity;
            _light.color = rgb;
        }

        private float ApplyCurve(float x)
        {
            if (dimmerCurve == null || dimmerCurve.length == 0) return Mathf.Clamp01(x);
            return Mathf.Clamp01(dimmerCurve.Evaluate(Mathf.Clamp01(x)));
        }
    }
}
