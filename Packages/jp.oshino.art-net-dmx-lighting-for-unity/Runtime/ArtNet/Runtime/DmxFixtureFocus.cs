using System.Collections.Generic;
using UnityEngine;
#if HAS_HDRP
using UnityEngine.Rendering.HighDefinition;
#endif

namespace ArtNet.Runtime
{
    public enum FocusControlMode
    {
        Manual,
        AutoTarget,
        AutoRaycast
    }

    public partial class DmxFixtureComponent
    {
        [Header("Focus (all light / beam modes)")]
        public bool syncFocusToDmx = true;

        [Tooltip("灯体単位で上書きするFocus Profileです。未設定時はFixture Typeの共有Focus Profileを使用します。\n\nOptional per-fixture Focus Profile override. Otherwise uses the Fixture Type's shared Focus Profile.")]
        public FocusProfile focusProfile;

        [Tooltip("複数のFocus属性を使う場合の1始まりの番号です。\n\nOne-based instance number when multiple Focus attributes are defined.")]
        [Min(1)] public int focusInstance = 1;

        [Tooltip("Focusの制御方法です。ManualはDMX値をそのまま使用し、Auto TargetとAuto Raycastは描画用Focusを距離から自動計算します。\n\nFocus control method. Manual uses the DMX value directly. Auto Target and Auto Raycast calculate the rendering focus from distance.")]
        public FocusControlMode focusControlMode = FocusControlMode.Manual;

        [Tooltip("Focus Profileの既定制御モードを、このFixtureだけで上書きします。\n\nOverrides the Focus Profile's default control mode for this fixture only.")]
        public bool overrideFocusControlMode;

        [Tooltip("Focusの鮮明さを評価する代表投影距離（m）です。Targetが設定されている場合は使用しません。\n\nRepresentative projection distance in meters used to evaluate focus. Ignored when a Target is assigned.")]
        [Min(0.01f)] public float focusReferenceDistanceMeters = 10f;

        [Tooltip("手動Focusの代表投影位置、またはAuto Targetの追従対象です。\n\nRepresentative projection point for Manual Focus, or the tracking target for Auto Target.")]
        public Transform focusTarget;

        [Tooltip("Auto Raycastで照射面を探す最大距離（m）です。\n\nMaximum distance in meters used to find a projection surface in Auto Raycast mode.")]
        [Min(0.01f)] public float autoFocusRaycastMaxDistanceMeters = 100f;

        [Tooltip("Auto Raycastで検出対象にするレイヤーです。\n\nLayers considered as projection surfaces in Auto Raycast mode.")]
        public LayerMask autoFocusRaycastLayers = ~0;

        [Tooltip("対象距離の変化がこの値未満の場合は現在の自動Focus距離を維持します。\n\nKeeps the current auto-focus distance while target movement is below this value.")]
        [Min(0f)] public float autoFocusDistanceDeadbandMeters = 0.01f;

        private readonly Dictionary<Light, FocusCookie> _focusCookies = new();
        private bool _focusHasInput;
        private bool _autoFocusHasTarget;
        private bool _focusInitialized;
        private float _focusTarget01;
        private float _focusCurrent01;
        private float _focusDefocus01;
        private float _focusCookieBlur;
        private float _focusLensBlur;
        private float _focusEdgeSoftness;
        private float _autoFocusDistanceMeters;

        private FocusProfile ActiveFocusProfile => focusProfile != null
            ? focusProfile
            : (fixture != null ? fixture.focusProfile : null);

        private FocusControlMode ActiveFocusControlMode => overrideFocusControlMode
            ? focusControlMode
            : (ActiveFocusProfile != null ? ActiveFocusProfile.defaultControlMode : FocusControlMode.Manual);

        public float CurrentFocusPosition => _focusCurrent01;
        public float CurrentFocusDistanceMeters => ActiveFocusProfile != null
            ? ActiveFocusProfile.EvaluateFocalDistanceMeters(_focusCurrent01)
            : 0f;

        private bool FocusActive => syncFocusToDmx && ActiveFocusProfile != null &&
                                    (_focusHasInput || _autoFocusHasTarget);

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
                if (ActiveFocusControlMode == FocusControlMode.Manual)
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
            var profile = ActiveFocusProfile;
            if (!syncFocusToDmx || profile == null)
            {
                _autoFocusHasTarget = false;
                _focusDefocus01 = 0f;
                _focusCookieBlur = 0f;
                _focusLensBlur = 0f;
                _focusEdgeSoftness = 0f;
                return;
            }

            _autoFocusHasTarget = TryGetAutoFocusDistanceMeters(out float autoFocusDistance);
            if (_autoFocusHasTarget)
                _autoFocusDistanceMeters = autoFocusDistance;

            if (!_focusHasInput && !_autoFocusHasTarget)
            {
                _focusDefocus01 = 0f;
                _focusCookieBlur = 0f;
                _focusLensBlur = 0f;
                _focusEdgeSoftness = 0f;
                return;
            }

            float requestedFocus01 = _autoFocusHasTarget
                ? profile.EvaluateFocusPositionForDistanceMeters(_autoFocusDistanceMeters)
                : _focusTarget01;
            if (!_focusInitialized)
            {
                _focusCurrent01 = requestedFocus01;
                _focusInitialized = true;
            }

            float travelSeconds = requestedFocus01 >= _focusCurrent01
                ? profile.nearToFarSeconds
                : profile.farToNearSeconds;
            if (travelSeconds <= 0f || !Application.isPlaying)
                _focusCurrent01 = requestedFocus01;
            else
                _focusCurrent01 = Mathf.MoveTowards(_focusCurrent01, requestedFocus01,
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

        private bool TryGetAutoFocusDistanceMeters(out float distanceMeters)
        {
            distanceMeters = 0f;
            FocusControlMode controlMode = ActiveFocusControlMode;
            if (controlMode == FocusControlMode.Manual) return false;

            Transform origin = targetLight != null ? targetLight.transform : transform;
            if (controlMode == FocusControlMode.AutoTarget)
            {
                if (focusTarget == null) return false;
                distanceMeters = Vector3.Distance(origin.position, focusTarget.position);
            }
            else
            {
                if (!Physics.Raycast(origin.position, origin.forward, out RaycastHit hit,
                        Mathf.Max(0.01f, autoFocusRaycastMaxDistanceMeters), autoFocusRaycastLayers,
                        QueryTriggerInteraction.Ignore))
                    return false;
                distanceMeters = hit.distance;
            }

            distanceMeters = Mathf.Max(0.01f, distanceMeters);
            if (_autoFocusHasTarget &&
                Mathf.Abs(distanceMeters - _autoFocusDistanceMeters) < autoFocusDistanceDeadbandMeters)
                distanceMeters = _autoFocusDistanceMeters;
            return true;
        }

        private float GetFocusReferenceDistanceMeters()
        {
            if (_autoFocusHasTarget)
                return _autoFocusDistanceMeters;
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
            _autoFocusHasTarget = false;
            _focusInitialized = false;
            _focusTarget01 = 0f;
            _focusCurrent01 = 0f;
            _focusDefocus01 = 0f;
            _focusCookieBlur = 0f;
            _focusLensBlur = 0f;
            _focusEdgeSoftness = 0f;
            _autoFocusDistanceMeters = 0f;
        }
    }
}
