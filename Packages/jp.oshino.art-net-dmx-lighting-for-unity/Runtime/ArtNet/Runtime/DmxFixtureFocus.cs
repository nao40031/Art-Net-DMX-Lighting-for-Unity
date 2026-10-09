using System.Collections.Generic;
using UnityEngine;
#if HAS_HDRP
using UnityEngine.Rendering.HighDefinition;
#endif

namespace ArtNet.Runtime
{
    public partial class DmxFixtureComponent
    {
        [Header("Focus (all light / beam modes)")]
        public bool syncFocusToDmx = true;

        [Tooltip("灯体単位で上書きするFocus Profileです。未設定時はFixture Typeの共有Focus Profileを使用します。\n\nOptional per-fixture Focus Profile override. Otherwise uses the Fixture Type's shared Focus Profile.")]
        public FocusProfile focusProfile;

        [Tooltip("複数のFocus属性を使う場合の1始まりの番号です。\n\nOne-based instance number when multiple Focus attributes are defined.")]
        [Min(1)] public int focusInstance = 1;

        [Tooltip("Focusの鮮明さを評価する代表投影距離（m）です。Targetが設定されている場合は使用しません。\n\nRepresentative projection distance in meters used to evaluate focus. Ignored when a Target is assigned.")]
        [Min(0.01f)] public float focusReferenceDistanceMeters = 10f;

        [Tooltip("任意の代表投影位置です。灯体からこのTransformまでの距離をFocus評価に使用します。\n\nOptional representative projection point. Focus is evaluated using the distance from the fixture to this Transform.")]
        public Transform focusTarget;

        private readonly Dictionary<Light, FocusCookie> _focusCookies = new();
        private bool _focusHasInput;
        private bool _focusInitialized;
        private float _focusTarget01;
        private float _focusCurrent01;
        private float _focusDefocus01;
        private float _focusCookieBlur;
        private float _focusLensBlur;
        private float _focusEdgeSoftness;

        private FocusProfile ActiveFocusProfile => focusProfile != null
            ? focusProfile
            : (fixture != null ? fixture.focusProfile : null);

        public float CurrentFocusPosition => _focusCurrent01;
        public float CurrentFocusDistanceMeters => ActiveFocusProfile != null
            ? ActiveFocusProfile.EvaluateFocalDistanceMeters(_focusCurrent01)
            : 0f;

        private bool FocusActive => syncFocusToDmx && _focusHasInput && ActiveFocusProfile != null;

        private void ReadFocus(int[] data)
        {
            bool hasValue = TryReadElement01(data, FixtureAttribute.Focus, focusInstance,
                FixtureChannelRole.Value, out float value, out bool usedRangeMapping);
            if (!hasValue)
                hasValue = TryReadElement01(data, FixtureAttribute.Focus, focusInstance,
                    FixtureChannelRole.Position, out value, out usedRangeMapping);
            SetFocusInput(hasValue, value, usedRangeMapping);
        }

        private void ReadFocus(byte[] data)
        {
            bool hasValue = TryReadElement01(data, FixtureAttribute.Focus, focusInstance,
                FixtureChannelRole.Value, out float value, out bool usedRangeMapping);
            if (!hasValue)
                hasValue = TryReadElement01(data, FixtureAttribute.Focus, focusInstance,
                    FixtureChannelRole.Position, out value, out usedRangeMapping);
            SetFocusInput(hasValue, value, usedRangeMapping);
        }

        private void ReadLegacyFocus(int[] data)
        {
            bool hasValue = TryGetRelativeChannel(FixtureFunction.Focus, out int relative);
            float value = hasValue
                ? DmxValueUtils.ByteTo01(Read8Abs(data, startAddress + relative - 1))
                : 0f;
            SetFocusInput(hasValue, value, false);
        }

        private void ReadLegacyFocus(byte[] data)
        {
            bool hasValue = TryGetRelativeChannel(FixtureFunction.Focus, out int relative);
            float value = hasValue
                ? DmxValueUtils.ByteTo01(Read8Abs(data, startAddress + relative - 1))
                : 0f;
            SetFocusInput(hasValue, value, false);
        }

        private void SetFocusInput(bool hasValue, float value, bool usedRangeMapping)
        {
            var profile = ActiveFocusProfile;
            _focusHasInput = syncFocusToDmx && profile != null && hasValue;
            if (!_focusHasInput)
            {
                _focusInitialized = false;
                return;
            }

            value = Mathf.Clamp01(value);
            if (!usedRangeMapping && profile.invertUnmappedPosition)
                value = 1f - value;

            _focusTarget01 = value;
            if (!_focusInitialized || !Application.isPlaying)
            {
                _focusCurrent01 = value;
                _focusInitialized = true;
            }
        }

        private void UpdateFocus()
        {
            if (!FocusActive)
            {
                _focusDefocus01 = 0f;
                _focusCookieBlur = 0f;
                _focusLensBlur = 0f;
                _focusEdgeSoftness = 0f;
                return;
            }

            var profile = ActiveFocusProfile;
            float travelSeconds = _focusTarget01 >= _focusCurrent01
                ? profile.nearToFarSeconds
                : profile.farToNearSeconds;
            if (travelSeconds <= 0f || !Application.isPlaying)
                _focusCurrent01 = _focusTarget01;
            else
                _focusCurrent01 = Mathf.MoveTowards(_focusCurrent01, _focusTarget01,
                    Time.deltaTime / Mathf.Max(0.0001f, travelSeconds));

            RefreshFocusOptics();
        }

        private void RefreshFocusOptics()
        {
            if (!FocusActive)
            {
                _focusDefocus01 = 0f;
                _focusCookieBlur = 0f;
                _focusLensBlur = 0f;
                _focusEdgeSoftness = 0f;
                return;
            }

            var profile = ActiveFocusProfile;
            _focusDefocus01 = profile.EvaluateDefocus01(_focusCurrent01, GetFocusReferenceDistanceMeters());
            _focusCookieBlur = _focusDefocus01 * Mathf.Max(0f, profile.maximumCookieBlur);
            _focusLensBlur = _focusDefocus01 * Mathf.Max(0f, profile.maximumLensBlur);
            _focusEdgeSoftness = _focusDefocus01 * Mathf.Clamp01(profile.maximumEdgeSoftness);
        }

        private float GetFocusReferenceDistanceMeters()
        {
            if (focusTarget == null)
                return Mathf.Max(0.01f, focusReferenceDistanceMeters);

            Transform origin = targetLight != null ? targetLight.transform : transform;
            return Mathf.Max(0.01f, Vector3.Distance(origin.position, focusTarget.position));
        }

        private static void ApplyFocusToSpotAngles(ref float innerPercent, float edgeSoftness)
        {
            innerPercent = Mathf.Lerp(Mathf.Clamp(innerPercent, 0f, 100f), 0f,
                Mathf.Clamp01(edgeSoftness));
        }

        private Texture ApplyFocusCookie(Light light, Texture source, FixtureRenderState state)
        {
            if (light == null || light.type != LightType.Spot) return source;
            if (!state.focusEnabled || state.focusCookieBlur <= 0.000001f)
            {
                if (_focusCookies.TryGetValue(light, out var previous) && source == previous.Output)
                    source = previous.Source;
                return source;
            }

            if (!_focusCookies.TryGetValue(light, out var cookie))
            {
                cookie = new FocusCookie();
                _focusCookies.Add(light, cookie);
            }

            return cookie.Apply(source, state.focusCookieBlur, ActiveFocusProfile.cookieResolution);
        }

        private void ApplyPrimaryFocus(FixtureRenderState state)
        {
            if (!state.focusEnabled && _focusCookies.Count == 0) return;
            var lights = GatherTargetLights();
            foreach (var light in lights)
            {
                if (light == null || light.type != LightType.Spot) continue;
                Texture result = ApplyFocusCookie(light, light.cookie, state);
                SetFocusLightCookie(light, result);
            }
        }

        private static void SetFocusLightCookie(Light light, Texture cookie)
        {
            light.cookie = cookie;
#if HAS_HDRP
            if (light.TryGetComponent<HDAdditionalLightData>(out var hd))
                hd.SetCookie(cookie != null ? cookie : Texture2D.whiteTexture);
#endif
        }

        private void ReleaseFocus()
        {
            foreach (var pair in _focusCookies)
            {
                if (pair.Key != null && pair.Key.cookie == pair.Value.Output)
                    SetFocusLightCookie(pair.Key, pair.Value.Source);
                pair.Value.Dispose();
            }

            _focusCookies.Clear();
            _focusHasInput = false;
            _focusInitialized = false;
            _focusTarget01 = 0f;
            _focusCurrent01 = 0f;
            _focusDefocus01 = 0f;
            _focusCookieBlur = 0f;
            _focusLensBlur = 0f;
            _focusEdgeSoftness = 0f;
        }
    }
}
