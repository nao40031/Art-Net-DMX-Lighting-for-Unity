using UnityEngine;

namespace ArtNet.Runtime
{
    /// <summary>Optical and motion calibration for a motorized focus lens.</summary>
    [CreateAssetMenu(menuName = "ArtNet/DMX/Focus Profile", fileName = "FocusProfile")]
    public sealed class FocusProfile : ScriptableObject
    {
        [Tooltip("正規化Focus 0で合焦する最短距離（m）です。\n\nNearest focal distance in meters at normalized Focus 0.")]
        [Min(0.01f)] public float nearFocusDistanceMeters = 2f;

        [Tooltip("正規化Focus 1で合焦する最長距離（m）です。Far Is Infinityが有効な場合、終点は無限遠となりこの値は使用しません。\n\nFarthest focal distance in meters at normalized Focus 1. When Far Is Infinity is enabled, the endpoint is infinity and this value is not used.")]
        [Min(0.01f)] public float farFocusDistanceMeters = 50f;

        [Tooltip("Focus 1を無限遠として扱います。\n\nTreats normalized Focus 1 as optical infinity.")]
        public bool farIsInfinity = true;

        [Tooltip("正規化Focus位置から焦点距離への応答です。出力0がNear、1がFarです。\n\nResponse from normalized Focus position to focal distance. Output 0 is Near and 1 is Far.")]
        public AnimationCurve focusDistanceCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Tooltip("Channel Elementに明示的なRange Mappingがない場合だけFocus方向を反転します。\n\nInverts Focus only when the Channel Element has no explicit range mapping.")]
        public bool invertUnmappedPosition;

        [Tooltip("このディオプター差でDefocusを1として扱います。小さいほど焦点ずれへ敏感になります。\n\nDiopter difference treated as full defocus. Smaller values make focus more sensitive.")]
        [Min(0.0001f)] public float fullBlurDiopterDifference = 0.5f;

        [Tooltip("正規化した焦点ずれから視覚効果量へのカーブです。\n\nCurve from normalized defocus to visual effect strength.")]
        public AnimationCurve defocusCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("最大Cookieぼかし半径です。\n\nMaximum cookie blur radius.")]
        [Range(0f, 0.08f)] public float maximumCookieBlur = 0.02f;

        [Tooltip("最大レンズ面ゴボぼかし半径です。\n\nMaximum lens-surface gobo blur radius.")]
        [Range(0f, 0.02f)] public float maximumLensBlur = 0.01f;

        [Tooltip("最大ビーム外周Softnessです。Focusは照射角を変えず、内側の境界だけを柔らかくします。\n\nMaximum beam-edge softness. Focus softens the inner boundary without changing the beam angle.")]
        [Range(0f, 1f)] public float maximumEdgeSoftness = 0.75f;

        [Tooltip("NearからFarまで移動する秒数です。0で即時反映します。\n\nSeconds to travel from Near to Far. Zero applies immediately.")]
        [Min(0f)] public float nearToFarSeconds;

        [Tooltip("FarからNearまで移動する秒数です。0で即時反映します。\n\nSeconds to travel from Far to Near. Zero applies immediately.")]
        [Min(0f)] public float farToNearSeconds;

        [Range(64, 1024)] public int cookieResolution = 256;

        public float EvaluateFocalDistanceMeters(float focus01)
        {
            float near = Mathf.Max(0.01f, nearFocusDistanceMeters);
            float far = Mathf.Max(near, farFocusDistanceMeters);
            float t = EvaluateCurve(focusDistanceCurve, focus01, focus01);
            float nearDiopters = 1f / near;
            float farDiopters = farIsInfinity ? 0f : 1f / far;
            float diopters = Mathf.Lerp(nearDiopters, farDiopters, t);
            return diopters <= 0.000001f ? float.PositiveInfinity : 1f / diopters;
        }

        public float EvaluateDefocus01(float focus01, float referenceDistanceMeters)
        {
            float referenceDiopters = 1f / Mathf.Max(0.01f, referenceDistanceMeters);
            float focalDistance = EvaluateFocalDistanceMeters(focus01);
            float focalDiopters = float.IsInfinity(focalDistance) ? 0f : 1f / Mathf.Max(0.01f, focalDistance);
            float normalized = Mathf.Abs(focalDiopters - referenceDiopters) /
                               Mathf.Max(0.0001f, fullBlurDiopterDifference);
            normalized = Mathf.Clamp01(normalized);
            return Mathf.Clamp01(EvaluateCurve(defocusCurve, normalized, normalized));
        }

        private static float EvaluateCurve(AnimationCurve curve, float input, float fallback)
        {
            return curve == null || curve.length == 0 ? fallback : curve.Evaluate(Mathf.Clamp01(input));
        }
    }
}
