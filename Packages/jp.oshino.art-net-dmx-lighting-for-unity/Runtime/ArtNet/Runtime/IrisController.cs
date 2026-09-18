using UnityEngine;

namespace ArtNet.Runtime
{
    /// <summary>Pipeline-independent, allocation-free state machine. Does not know any fixture model.</summary>
    public sealed class IrisController
    {
        public enum Motion { Position, Pulse, ReversePulse, Hold }
        public float Aperture { get; private set; } = 1;
        public Motion Mode { get; private set; }
        public bool Active { get; private set; }
        private float target = 1, phase, speed;

        public void Reset() { Aperture = target = 1; phase = speed = 0; Active = false; Mode = Motion.Position; }

        public void SetInput(IrisProfile profile, bool hasPosition, int position, int positionMax,
            FixtureChannelElement positionElement, bool hasSelect, int selection, int selectionMax,
            FixtureChannelElement selectElement, bool hasSpeed, float speed01, bool immediate)
        {
            Active = profile != null && (hasPosition || hasSelect);
            if (!Active) { Reset(); return; }
            int raw = hasSelect ? selection : position;
            int rawMax = hasSelect ? selectionMax : positionMax;
            var element = hasSelect ? selectElement : positionElement;
            var range = FindRange(element, raw);
            if (range != null && range.mappingPreset == NormalizedMappingPreset.NotUsed)
            { Mode = Motion.Hold; return; }
            var type = range != null ? range.type : FixtureRangeType.None;
            // Explicit gaps hold; they must not turn a macro byte into an aperture.
            if (range == null && element != null && element.ranges != null && element.ranges.Count > 0)
            { Mode = Motion.Hold; return; }
            float value = range != null ? range.Normalize(raw) : Mathf.Clamp01(raw / Mathf.Max(1f, rawMax));
            switch (type)
            {
                case FixtureRangeType.Pulse:
                case FixtureRangeType.IrisPulseReverse:
                    Mode = type == FixtureRangeType.Pulse ? Motion.Pulse : Motion.ReversePulse;
                    speed = Mathf.Lerp(Mathf.Max(0, profile.minimumHz), Mathf.Max(0, profile.maximumHz),
                        Evaluate(profile.speedCurve, hasSpeed ? speed01 : value));
                    return;
                case FixtureRangeType.IrisHold: Mode = Motion.Hold; return;
                case FixtureRangeType.NoFunction:
                    if (profile.noFunction == IrisNoFunction.Hold) { Mode = Motion.Hold; return; }
                    if (profile.noFunction == IrisNoFunction.Open) { SetPosition(1, profile, immediate); return; }
                    break;
                case FixtureRangeType.Open: SetPosition(1, profile, immediate); return;
                case FixtureRangeType.Closed: SetPosition(0, profile, immediate); return;
                case FixtureRangeType.None:
                case FixtureRangeType.Indexed: break;
                default: Mode = Motion.Hold; return;
            }
            if (hasSelect)
            {
                if (!hasPosition) { Mode = Motion.Hold; return; }
                range = FindRange(positionElement, position);
                if (range != null && range.mappingPreset == NormalizedMappingPreset.NotUsed)
                { Mode = Motion.Hold; return; }
                if (range == null && positionElement?.ranges?.Count > 0) { Mode = Motion.Hold; return; }
                value = range != null ? range.Normalize(position) : Mathf.Clamp01(position / Mathf.Max(1f, positionMax));
            }
            if (range == null && profile.invertUnmappedPosition) value = 1 - value;
            SetPosition(value, profile, immediate);
        }

        private void SetPosition(float value, IrisProfile profile, bool immediate)
        {
            Mode = Motion.Position;
            target = Evaluate(profile.apertureCurve, value);
            if (immediate || (target >= Aperture ? profile.openingSeconds : profile.closingSeconds) <= 0)
                Aperture = target;
        }

        public void Tick(float deltaTime, IrisProfile profile)
        {
            if (!Active || profile == null || Mode == Motion.Hold) return;
            if (Mode == Motion.Position)
            {
                float seconds = target >= Aperture ? profile.openingSeconds : profile.closingSeconds;
                Aperture = seconds <= 0 ? target : Mathf.MoveTowards(Aperture, target, Mathf.Max(0, deltaTime) / seconds);
                return;
            }
            phase = Mathf.Repeat(phase + Mathf.Max(0, deltaTime) * speed * (Mode == Motion.ReversePulse ? -1 : 1), 1);
            Aperture = Evaluate(profile.apertureCurve, Mathf.Lerp(Mathf.Clamp01(profile.pulseAperture.x),
                Mathf.Clamp01(profile.pulseAperture.y), Evaluate(profile.pulse, phase)));
        }

        public float Diameter(IrisProfile profile) => Active && profile != null
            ? Mathf.Lerp(Mathf.Clamp01(profile.minimumDiameter), 1, Aperture) : 1;

        private static float Evaluate(AnimationCurve curve, float value) => Mathf.Clamp01(
            curve == null || curve.length == 0 ? value : curve.Evaluate(Mathf.Clamp01(value)));

        public static FixtureChannelRange FindRange(FixtureChannelElement element, int raw)
        {
            if (element?.ranges == null) return null;
            // Narrowest range wins, matching the existing normalized range convention.
            FixtureChannelRange best = null;
            foreach (var range in element.ranges)
                if (range != null && range.Contains(raw) && (best == null ||
                    Mathf.Abs(range.dmxMax - range.dmxMin) < Mathf.Abs(best.dmxMax - best.dmxMin))) best = range;
            return best;
        }
    }
}
