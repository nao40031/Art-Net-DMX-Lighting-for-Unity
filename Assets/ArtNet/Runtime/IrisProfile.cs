using UnityEngine;

namespace ArtNet.Runtime
{
    public enum IrisNoFunction { Hold, Open, PositionChannel }

    /// <summary>Optical/motion calibration only. Channel numbers and ranges belong to FixtureDefinition.</summary>
    [CreateAssetMenu(menuName = "ArtNet/DMX/Iris Profile", fileName = "IrisProfile")]
    public sealed class IrisProfile : ScriptableObject
    {
        [Tooltip("Minimum diameter / full diameter. Zero permits blackout; not a measured fixture default.")]
        [Range(0f, 1f)] public float minimumDiameter = 0.05f;
        public AnimationCurve apertureCurve = AnimationCurve.Linear(0, 0, 1, 1);
        [Min(0)] public float openingSeconds;
        [Min(0)] public float closingSeconds;
        [Tooltip("Fallback direction only when the position channel has no explicit ranges.")]
        public bool invertUnmappedPosition;
        public IrisNoFunction noFunction = IrisNoFunction.Hold;
        [Min(0)] public float minimumHz = 0.2f;
        [Min(0)] public float maximumHz = 3f;
        public AnimationCurve speedCurve = AnimationCurve.Linear(0, 0, 1, 1);
        [Tooltip("One cycle: normalized time -> aperture. Provisional pulse, not a manufacturer measurement.")]
        public AnimationCurve pulse = new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.8f, 1), new Keyframe(1, 0));
        public Vector2 pulseAperture = new Vector2(0, 1);
        [Range(0, 0.25f)] public float feather = 0.01f;
        [Tooltip("0 = circle; 3..32 = regular polygon approximation, not physical blade geometry.")]
        [Range(0, 32)] public int blades;
        [Range(0, 1)] public float lensInfluence;
        [Range(64, 1024)] public int cookieResolution = 256;
    }
}
