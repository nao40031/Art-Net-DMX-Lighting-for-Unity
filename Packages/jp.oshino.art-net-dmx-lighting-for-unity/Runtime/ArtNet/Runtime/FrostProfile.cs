using UnityEngine;

namespace ArtNet.Runtime
{
    /// <summary>Optical calibration for one diffusion frost stage. DMX ranges belong to FixtureType.</summary>
    [CreateAssetMenu(menuName = "ArtNet/DMX/Frost Profile", fileName = "FrostProfile")]
    public sealed class FrostProfile : ScriptableObject
    {
        [Tooltip("ビーム半径の倍率です。角度ではなく投影面の半径へ適用するため、広角でも破綻しにくい設定です。\n\nBeam-radius multiplier. It is applied to the projected radius rather than directly to the angle.")]
        public AnimationCurve beamRadiusScale = AnimationCurve.Linear(0f, 1f, 1f, 1.6f);

        [Tooltip("ビーム境界の柔らかさです。0で元のペナンブラ、1で中心から外周まで滑らかに減衰します。\n\nBeam-edge softness. Zero preserves the original penumbra; one fades smoothly from the center to the edge.")]
        public AnimationCurve edgeSoftness = AnimationCurve.Linear(0f, 0f, 1f, 0.8f);

        [Tooltip("ゴボやアイリスCookieの拡散半径です。\n\nDiffusion radius applied to gobo and iris cookies.")]
        public AnimationCurve cookieBlur = AnimationCurve.Linear(0f, 0f, 1f, 0.018f);

        [Tooltip("レンズ面上の投影模様へ加えるぼかし量です。\n\nAdditional blur applied to projected patterns on the lens surface.")]
        public AnimationCurve lensBlur = AnimationCurve.Linear(0f, 0f, 1f, 0.012f);

        [Tooltip("フロスト挿入による光量透過率です。\n\nLight transmission caused by inserting the frost filter.")]
        public AnimationCurve transmission = AnimationCurve.Linear(0f, 1f, 1f, 0.86f);

        [Range(64, 1024)]
        public int cookieResolution = 256;

        internal float Evaluate(AnimationCurve curve, float amount, float fallback)
        {
            if (curve == null || curve.length == 0) return fallback;
            return curve.Evaluate(Mathf.Clamp01(amount));
        }
    }
}
