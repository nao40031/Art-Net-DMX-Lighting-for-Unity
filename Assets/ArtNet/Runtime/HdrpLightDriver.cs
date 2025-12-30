#if HAS_HDRP
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace ArtNet.Runtime
{
    /// <summary>
    /// HDRP向け：HDAdditionalLightData.SetIntensity を使って物理単位で制御するドライバ
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
        [Tooltip("Dimmer=1.0 のときの最大光量（下の単位で解釈）")]
        public float maxIntensity = 9870f;   // ★変更（以前: 5000f）

        [Tooltip("HDRPの物理単位")]
        public HdrpUnit unit = HdrpUnit.Lumen;

        [Tooltip("Dimmerのカーブ補正（線形のままでよければ未設定でOK）")]
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

            // HDRP追加データが取れれば物理単位で設定、取れなければfallback
            if (_hd != null)
            {
                try
                {
                    _hd.SetIntensity(intensity, ToLightUnit(unit));
                }
                catch
                {
                    // バージョン/ライトタイプによっては失敗し得るので、最後の保険
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
