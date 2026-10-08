using System.Collections.Generic;
using UnityEngine;
#if HAS_HDRP
using UnityEngine.Rendering.HighDefinition;
#endif

namespace ArtNet.Runtime
{
    public partial class DmxFixtureComponent
    {
        public bool syncFrostToDmx = true;
        [Tooltip("灯体単位で上書きする共通Frost Profileです。未設定時はFixture TypeまたはModeのFrost Profileを使用します。\n\nOptional per-fixture shared Frost Profile override. Otherwise uses the Fixture Type or Mode Frost Profile.")]
        public FrostProfile frostProfile;

        private sealed class ResolvedFrostBinding
        {
            public int instance;
            public FrostProfile profile;
        }

        private readonly List<ResolvedFrostBinding> _resolvedFrostBindings = new();
        private readonly Dictionary<Light, FrostCookie> _frostCookies = new();
        private bool _frostEnabled;
        private float _frostAmount01;
        private float _frostBeamRadiusScale = 1f;
        private float _frostEdgeSoftness;
        private float _frostCookieBlur;
        private float _frostLensBlur;
        private float _frostTransmission = 1f;
        private int _frostCookieResolution = 256;
        private bool _frostSpotBaseCaptured;
        private float _frostBaseOuterSpotAngle;
        private float _frostBaseInnerSpotPercent;

        private FrostProfile DefaultFrostProfile => frostProfile != null ? frostProfile : (fixture != null ? fixture.frostProfile : null);

        private void ResolveFrostBindings(FixtureMode fixtureMode)
        {
            _resolvedFrostBindings.Clear();
            if (fixtureMode == null) return;

            if (fixtureMode.frostBindings != null)
            {
                for (int i = 0; i < fixtureMode.frostBindings.Count; i++)
                {
                    var binding = fixtureMode.frostBindings[i];
                    if (binding == null) continue;
                    AddResolvedFrostBinding(Mathf.Max(1, binding.instance), binding.profile);
                }
            }

            if (fixtureMode.elements == null) return;
            for (int i = 0; i < fixtureMode.elements.Count; i++)
            {
                var element = fixtureMode.elements[i];
                if (element == null || element.attribute != FixtureAttribute.Frost) continue;
                AddResolvedFrostBinding(Mathf.Max(1, element.instance), null);
            }
        }

        private void AddResolvedFrostBinding(int instance, FrostProfile profile)
        {
            for (int i = 0; i < _resolvedFrostBindings.Count; i++)
            {
                if (_resolvedFrostBindings[i].instance != instance) continue;
                if (profile != null) _resolvedFrostBindings[i].profile = profile;
                return;
            }
            _resolvedFrostBindings.Add(new ResolvedFrostBinding { instance = instance, profile = profile });
        }

        private void ReadFrost(int[] data)
        {
            ResetFrostState();
            if (!syncFrostToDmx) return;
            for (int i = 0; i < _resolvedFrostBindings.Count; i++)
            {
                var binding = _resolvedFrostBindings[i];
                if (TryReadElement01(data, FixtureAttribute.Frost, binding.instance, FixtureChannelRole.Value, out float amount))
                    AccumulateFrost(amount, frostProfile != null ? frostProfile : (binding.profile != null ? binding.profile : DefaultFrostProfile));
            }
        }

        private void ReadFrost(byte[] data)
        {
            ResetFrostState();
            if (!syncFrostToDmx) return;
            for (int i = 0; i < _resolvedFrostBindings.Count; i++)
            {
                var binding = _resolvedFrostBindings[i];
                if (TryReadElement01(data, FixtureAttribute.Frost, binding.instance, FixtureChannelRole.Value, out float amount))
                    AccumulateFrost(amount, frostProfile != null ? frostProfile : (binding.profile != null ? binding.profile : DefaultFrostProfile));
            }
        }

        private void ReadLegacyFrost(int[] data)
        {
            ResetFrostState();
            if (!syncFrostToDmx || !TryGetRelativeChannel(FixtureFunction.Frost, out int relative)) return;
            AccumulateFrost(DmxValueUtils.ByteTo01(Read8Abs(data, startAddress + relative - 1)), DefaultFrostProfile);
        }

        private void ReadLegacyFrost(byte[] data)
        {
            ResetFrostState();
            if (!syncFrostToDmx || !TryGetRelativeChannel(FixtureFunction.Frost, out int relative)) return;
            AccumulateFrost(DmxValueUtils.ByteTo01(Read8Abs(data, startAddress + relative - 1)), DefaultFrostProfile);
        }

        private void ResetFrostState()
        {
            _frostEnabled = false;
            _frostAmount01 = 0f;
            _frostBeamRadiusScale = 1f;
            _frostEdgeSoftness = 0f;
            _frostCookieBlur = 0f;
            _frostLensBlur = 0f;
            _frostTransmission = 1f;
            _frostCookieResolution = 256;
        }

        private void AccumulateFrost(float amount, FrostProfile profile)
        {
            amount = Mathf.Clamp01(amount);
            if (amount <= 0.000001f) return;
            _frostEnabled = true;
            _frostAmount01 = Mathf.Max(_frostAmount01, amount);
            if (profile == null)
            {
                _frostBeamRadiusScale *= Mathf.Lerp(1f, 1.6f, amount);
                _frostEdgeSoftness = Mathf.Max(_frostEdgeSoftness, amount * 0.8f);
                _frostCookieBlur = Mathf.Min(0.08f, _frostCookieBlur + amount * 0.018f);
                _frostLensBlur = Mathf.Min(0.02f, _frostLensBlur + amount * 0.012f);
                _frostTransmission *= Mathf.Lerp(1f, 0.86f, amount);
                return;
            }
            _frostBeamRadiusScale *= Mathf.Max(0.01f, profile.Evaluate(profile.beamRadiusScale, amount, 1f));
            _frostEdgeSoftness = Mathf.Max(_frostEdgeSoftness, Mathf.Clamp01(profile.Evaluate(profile.edgeSoftness, amount, 0f)));
            _frostCookieBlur = Mathf.Min(0.08f, _frostCookieBlur + Mathf.Max(0f, profile.Evaluate(profile.cookieBlur, amount, 0f)));
            _frostLensBlur = Mathf.Min(0.02f, _frostLensBlur + Mathf.Max(0f, profile.Evaluate(profile.lensBlur, amount, 0f)));
            _frostTransmission *= Mathf.Clamp01(profile.Evaluate(profile.transmission, amount, 1f));
            _frostCookieResolution = Mathf.Max(_frostCookieResolution, profile.cookieResolution);
        }

        private void GetFrostBaseSpotAngles(out float outer, out float innerPercent)
        {
            if (!_frostSpotBaseCaptured)
            {
                _frostBaseOuterSpotAngle = targetLight != null ? targetLight.spotAngle : maxOuterSpotAngle;
                _frostBaseInnerSpotPercent = targetLight != null && targetLight.spotAngle > 0f
                    ? targetLight.innerSpotAngle / targetLight.spotAngle * 100f
                    : 0f;
                _frostSpotBaseCaptured = true;
            }
            outer = _frostBaseOuterSpotAngle;
            innerPercent = _frostBaseInnerSpotPercent;
        }

        private void ReleaseFrostSpotBase()
        {
            _frostSpotBaseCaptured = false;
        }

        private static void ApplyFrostToSpotAngles(ref float outer, ref float innerPercent, float radiusScale, float edgeSoftness)
        {
            outer = Mathf.Clamp(outer, 0.1f, 179f);
            float footprint = Mathf.Tan(outer * 0.5f * Mathf.Deg2Rad) * Mathf.Max(0.01f, radiusScale);
            outer = Mathf.Clamp(2f * Mathf.Atan(footprint) * Mathf.Rad2Deg, 0.1f, 179f);
            innerPercent = Mathf.Lerp(Mathf.Clamp(innerPercent, 0f, 100f), 0f, Mathf.Clamp01(edgeSoftness));
        }

        private Texture ApplyFrostCookie(Light light, Texture source, FixtureRenderState state)
        {
            if (light == null || light.type != LightType.Spot) return source;
            if (!state.frostEnabled || state.frostCookieBlur <= 0.000001f)
            {
                if (_frostCookies.TryGetValue(light, out var previous) && source == previous.Output) source = previous.Source;
                return source;
            }
            if (!_frostCookies.TryGetValue(light, out var cookie))
            {
                cookie = new FrostCookie();
                _frostCookies.Add(light, cookie);
            }
            return cookie.Apply(source, state.frostCookieBlur, _frostCookieResolution);
        }

        private void ApplyPrimaryFrost(FixtureRenderState state)
        {
            if (!state.frostEnabled && _frostCookies.Count == 0) return;
            var lights = GatherTargetLights();
            foreach (var light in lights)
            {
                if (light == null || light.type != LightType.Spot) continue;
                Texture result = ApplyFrostCookie(light, light.cookie, state);
                SetFrostLightCookie(light, result);
            }
        }

        private static void SetFrostLightCookie(Light light, Texture cookie)
        {
            light.cookie = cookie;
#if HAS_HDRP
            if (light.TryGetComponent<HDAdditionalLightData>(out var hd))
                hd.SetCookie(cookie != null ? cookie : Texture2D.whiteTexture);
#endif
        }

        private void ReleaseFrost()
        {
            foreach (var pair in _frostCookies)
            {
                if (pair.Key != null && pair.Key.cookie == pair.Value.Output)
                    SetFrostLightCookie(pair.Key, pair.Value.Source);
                pair.Value.Dispose();
            }
            _frostCookies.Clear();
            _resolvedFrostBindings.Clear();
            ResetFrostState();
            ReleaseFrostSpotBase();
        }
    }
}
