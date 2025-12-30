using UnityEngine;

namespace ArtNet.Runtime
{
    /// <summary>
    /// Built-in / URP 向け：Light.intensity と Light.color を使うドライバ
    /// </summary>
    public class GenericLightDriver : MonoBehaviour, ILightDriver
    {
        [Header("Generic Light Params")]
        [Tooltip("Dimmer=1.0 のときの最大 Intensity（Built-in/URPのスケール）")]
        public float maxIntensity = 2.0f;

        [Tooltip("Dimmerのカーブ補正（線形のままでよければ未設定でOK）")]
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
