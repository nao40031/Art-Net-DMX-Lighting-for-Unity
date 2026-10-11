using System;
using UnityEngine;

namespace ArtNet.Runtime
{
    public enum ColorTemperatureMapping
    {
        FilterTint = 0,
        AbsoluteKelvin = 1
    }

    [Serializable]
    public sealed class ColorFilterResponse
    {
        [Tooltip("フィルターを最大挿入したときのRGB透過率です。\n\nRGB transmittance with the filter fully inserted.")]
        public Color fullTransmission = Color.white;

        [Tooltip("DMXの正規化値からフィルター挿入率への応答です。\n\nResponse from normalized DMX to filter insertion.")]
        public AnimationCurve insertionCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        public Color Evaluate(float amount01)
        {
            float amount = Mathf.Clamp01(amount01);
            if (insertionCurve != null && insertionCurve.length > 0)
                amount = Mathf.Clamp01(insertionCurve.Evaluate(amount));
            return Color.Lerp(Color.white, new Color(Mathf.Clamp01(fullTransmission.r),
                Mathf.Clamp01(fullTransmission.g), Mathf.Clamp01(fullTransmission.b), 1f), amount);
        }
    }

    [Serializable]
    public sealed class ColorTemperatureResponse
    {
        [Tooltip("フィルター色か、DMX範囲を絶対色温度（K）として扱うかを選びます。\n\nUse a filter tint or map the DMX range to absolute color temperature in kelvin.")]
        public ColorTemperatureMapping mapping = ColorTemperatureMapping.FilterTint;

        [Tooltip("Filter Tint方式のフィルター透過特性です。\n\nFilter transmission when using Filter Tint mapping.")]
        public ColorFilterResponse filter = new ColorFilterResponse();

        [Tooltip("DMX値0に対応する色温度（K）です。\n\nColor temperature in kelvin at DMX zero.")]
        [Min(1000f)] public float kelvinAtZero = 6500f;
        [Tooltip("DMX最大値に対応する色温度（K）です。\n\nColor temperature in kelvin at maximum DMX.")]
        [Min(1000f)] public float kelvinAtFull = 3200f;
        [Tooltip("補正前の光源色温度（K）です。\n\nSource color temperature in kelvin before correction.")]
        [Min(1000f)] public float sourceKelvin = 6500f;

        public Color Evaluate(float amount01)
        {
            if (mapping == ColorTemperatureMapping.FilterTint)
                return filter != null ? filter.Evaluate(amount01) : Color.white;

            float kelvin = Mathf.Lerp(Mathf.Max(1000f, kelvinAtZero), Mathf.Max(1000f, kelvinAtFull), Mathf.Clamp01(amount01));
            Color source = Mathf.CorrelatedColorTemperatureToRGB(Mathf.Max(1000f, sourceKelvin));
            Color target = Mathf.CorrelatedColorTemperatureToRGB(kelvin);
            float r = source.r > 0.0001f ? target.r / source.r : 1f;
            float g = source.g > 0.0001f ? target.g / source.g : 1f;
            float b = source.b > 0.0001f ? target.b / source.b : 1f;
            return new Color(Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(b), 1f);
        }
    }

    [CreateAssetMenu(menuName = "ArtNet/DMX/Color Mix Profile", fileName = "ColorMixProfile")]
    public sealed class ColorMixProfile : ScriptableObject
    {
        [Tooltip("Cyanフィルターの透過特性です。\n\nCyan filter transmission response.")]
        public ColorFilterResponse cyan = new ColorFilterResponse { fullTransmission = Color.cyan };
        [Tooltip("Magentaフィルターの透過特性です。\n\nMagenta filter transmission response.")]
        public ColorFilterResponse magenta = new ColorFilterResponse { fullTransmission = Color.magenta };
        [Tooltip("Yellowフィルターの透過特性です。\n\nYellow filter transmission response.")]
        public ColorFilterResponse yellow = new ColorFilterResponse { fullTransmission = Color.yellow };
        [Tooltip("CTCチャンネルの色温度補正です。\n\nColor-temperature correction for the CTC channel.")]
        public ColorTemperatureResponse ctc = new ColorTemperatureResponse
        {
            filter = new ColorFilterResponse { fullTransmission = new Color(1f, 0.78f, 0.55f, 1f) }
        };
        [Tooltip("CTOチャンネルの暖色補正です。\n\nWarm correction for the CTO channel.")]
        public ColorTemperatureResponse cto = new ColorTemperatureResponse
        {
            filter = new ColorFilterResponse { fullTransmission = new Color(1f, 0.78f, 0.55f, 1f) }
        };
        [Tooltip("CTBチャンネルの寒色補正です。\n\nCool correction for the CTB channel.")]
        public ColorTemperatureResponse ctb = new ColorTemperatureResponse
        {
            filter = new ColorFilterResponse { fullTransmission = new Color(0.65f, 0.82f, 1f, 1f) },
            kelvinAtFull = 9000f
        };

        public Color EvaluateCmy(float cyan01, float magenta01, float yellow01)
        {
            Color c = cyan != null ? cyan.Evaluate(cyan01) : Color.white;
            Color m = magenta != null ? magenta.Evaluate(magenta01) : Color.white;
            Color y = yellow != null ? yellow.Evaluate(yellow01) : Color.white;
            return new Color(c.r * m.r * y.r, c.g * m.g * y.g, c.b * m.b * y.b, 1f);
        }

        public Color EvaluateTemperature(FixtureAttribute attribute, float amount01)
        {
            ColorTemperatureResponse response = attribute == FixtureAttribute.CTO ? cto :
                attribute == FixtureAttribute.CTB ? ctb : ctc;
            return response != null ? response.Evaluate(amount01) : Color.white;
        }
    }
}
