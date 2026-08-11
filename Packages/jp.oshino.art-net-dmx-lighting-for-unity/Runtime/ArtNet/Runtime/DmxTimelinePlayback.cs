/*!
 * Copyright (c) 2026 Oshino
 *
 * Released under the MIT license.
 * see https://opensource.org/licenses/MIT
 */

using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ArtNet.Runtime
{
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class DmxTimelinePlayback : MonoBehaviour
    {
        [Serializable]
        public struct UniverseSource
        {
            public int universe;
            public ArtNetChannels channels;
        }

        public enum UpdateTiming
        {
            Update,
            LateUpdate,
            FixedUpdate
        }

        public enum SourceMode
        {
            Manual,
            AutoDiscoverInChildren
        }

        [Header("Target")]
        public DmxRigController rig;

        [Header("Sources")]
        [Tooltip("Manualは従来どおりSourcesを使用します。Auto Discover In ChildrenはUniverse Root配下のArtNetChannelsを自動登録します。")]
        public SourceMode sourceMode = SourceMode.Manual;

        [Tooltip("Auto Discover In Childrenで検索する親Transform。未設定ならこのGameObject配下を検索します。")]
        public Transform universeRoot;

        [Tooltip("有効にすると、非アクティブなUniverse Objectも再生対象にします。")]
        public bool includeInactiveUniverseSources = false;

        [Tooltip("Manualモードで使用するUniverseとArtNetChannelsの対応表です。")]
        public List<UniverseSource> sources = new();

        [Header("Playback")]
        public bool enablePlayback = true;
        [Tooltip("Enable preview in edit mode (Timeline Preview)")]
        public bool enableInEditMode = true;
        public UpdateTiming updateTiming = UpdateTiming.Update;

        [Tooltip("0�Ȃ疈�t���[���B��: 40 �� 40Hz �X�V")]
        [Min(0f)] public float sampleRate = 0f;
        [Header("Live Compatibility")]
        [Tooltip("Align playback timing with live input conditions.")]
        public bool useLiveCompatiblePlayback = true;
        [Tooltip("Force updateTiming to Update in live-compatible mode.")]
        public bool forceUpdateTimingToUpdate = true;
        [Tooltip("When sampleRate is 0, apply defaultLiveSampleRate automatically.")]
        public bool applyDefaultSampleRateWhenZero = true;
        [Tooltip("Default live-compatible sample rate (typically 40 or 44).")]
        [Min(1f)] public float defaultLiveSampleRate = 40f;
        [Tooltip("OnEnable���ɒl������K�p�i�S��0�ł��x�����j")]
        public bool forceApplyOnEnable = true;

        [Tooltip("When false, initial Tick won't inject if channels unchanged (prevents zeroing on start).")]
        public bool applyOnFirstFrame = true;

        [Tooltip("Force Animator culling to Always Animate (so ArtNetChannels updates even if invisible)")]
        public bool forceAnimatorAlwaysAnimate = true;

        [Tooltip("Reset fixture pan/tilt to base when leaving edit mode preview")]
        public bool resetPanTiltOnExitingEditMode = true;

        [Header("Rig Input Override")]
        public bool overrideRigInputMode = false;
        public DmxRigController.InputMode inputModeWhileEnabled = DmxRigController.InputMode.LiveAndPlayback;

        [Header("Debug")]
        public bool logMissingRig = true;

        private readonly List<SourceState> _states = new();
        private float _nextSampleTime;
        private DmxRigController.InputMode _prevMode;
        private bool _modeOverridden;

#if UNITY_EDITOR
        private bool _previewBaseCaptured;
        private readonly Dictionary<int, PreviewBase> _previewBase = new();
        private struct PreviewBase
        {
            public Quaternion pan;
            public Quaternion tilt;
            public bool hasPan;
            public bool hasTilt;

        }
#endif

        private struct SourceState
        {
            public int universe;
            public ArtNetChannels channels;
            public byte[] buffer;
            public bool initialized;
        }

        private void OnEnable()
        {
            #if UNITY_EDITOR
            if (!Application.isPlaying)
                EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            #endif
            ResolveRig();
            ApplyLiveCompatibilitySettings();
            RebuildStates();
            if (_states.Count == 0 && logMissingRig)
                Debug.LogWarning("[DmxTimelinePlayback] Sources is empty. Add ArtNetChannels to Sources.", this);
            if (!Application.isPlaying && enableInEditMode && rig != null)
                rig.DiscoverAndInitializeAllFixtures();
            _nextSampleTime = 0f;

            if (Application.isPlaying && enablePlayback && overrideRigInputMode && rig != null)
            {
                _prevMode = rig.inputMode;
                rig.inputMode = inputModeWhileEnabled;
                _modeOverridden = true;
            }
            else
            {
                _modeOverridden = false;
            }

            if (forceApplyOnEnable)
            {
                Tick(force: true);
            }
        }

        #if UNITY_EDITOR
        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
                ResetPreviewState();
        }

        private void ResetPreviewState()
        {
            if (!resetPanTiltOnExitingEditMode) return;

            if (_previewBaseCaptured)
            {
                RestorePreviewBase();
                return;
            }

#if UNITY_2023_1_OR_NEWER
            var fixtures = FindObjectsByType<DmxFixtureComponent>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var fixtures = FindObjectsOfType<DmxFixtureComponent>(true);
#endif
            if (fixtures == null) return;
            for (int i = 0; i < fixtures.Length; i++)
            {
                var f = fixtures[i];
                if (f != null)
                {
                    f.ResetPanTiltToBase();
                    f.RestorePreviewLightBase();
                    f.RecapturePanTiltBase();
                }
            }
        }

        private void CapturePreviewBase()
        {
            if (_previewBaseCaptured) return;
            _previewBase.Clear();

#if UNITY_2023_1_OR_NEWER
            var fixtures = FindObjectsByType<DmxFixtureComponent>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var fixtures = FindObjectsOfType<DmxFixtureComponent>(true);
#endif
            if (fixtures == null) return;

            for (int i = 0; i < fixtures.Length; i++)
            {
                var f = fixtures[i];
                if (f == null) continue;

                var state = new PreviewBase();
                if (f.panTransform != null)
                {
                    state.pan = f.panTransform.localRotation;
                    state.hasPan = true;
                }
                if (f.tiltTransform != null)
                {
                    state.tilt = f.tiltTransform.localRotation;
                    state.hasTilt = true;
                }

                f.CapturePreviewLightBase();
                _previewBase[f.GetInstanceID()] = state;
            }

            _previewBaseCaptured = true;
        }

        private void RestorePreviewBase()
        {
            if (!_previewBaseCaptured) return;

#if UNITY_2023_1_OR_NEWER
            var fixtures = FindObjectsByType<DmxFixtureComponent>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var fixtures = FindObjectsOfType<DmxFixtureComponent>(true);
#endif
            if (fixtures != null)
            {
                for (int i = 0; i < fixtures.Length; i++)
                {
                    var f = fixtures[i];
                    if (f == null) continue;

                    if (_previewBase.TryGetValue(f.GetInstanceID(), out var state))
                    {
                        if (state.hasPan && f.panTransform != null)
                            f.panTransform.localRotation = state.pan;
                        if (state.hasTilt && f.tiltTransform != null)
                            f.tiltTransform.localRotation = state.tilt;
                    }
                    
                    f.RestorePreviewLightBase();
                    f.RecapturePanTiltBase();
                }
            }

            _previewBase.Clear();
            _previewBaseCaptured = false;
        }

#endif
        private void OnDisable()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            if (!Application.isPlaying && _previewBaseCaptured)
                RestorePreviewBase();
#endif
            if (_modeOverridden && rig != null)
                rig.inputMode = _prevMode;
        }

        private void OnValidate()
        {
            if (rig == null)
                rig = GetComponent<DmxRigController>();

            ApplyLiveCompatibilitySettings();

            if (!Application.isPlaying)
                RebuildStates();
        }

        private void ApplyLiveCompatibilitySettings()
        {
            if (!useLiveCompatiblePlayback) return;

            if (forceUpdateTimingToUpdate)
                updateTiming = UpdateTiming.Update;

            if (applyDefaultSampleRateWhenZero && sampleRate <= 0f)
                sampleRate = Mathf.Max(1f, defaultLiveSampleRate);
        }
        private void ResolveRig()
        {
            if (rig == null)
                rig = GetComponent<DmxRigController>();

            if (rig == null && logMissingRig)
                Debug.LogWarning("[DmxTimelinePlayback] DmxRigController is missing.", this);
        }

        private void RebuildStates()
        {
            _states.Clear();

            if (sourceMode == SourceMode.AutoDiscoverInChildren)
            {
                RebuildAutoDiscoveredStates();
                return;
            }

            RebuildManualStates();
        }

        private void RebuildManualStates()
        {
            if (sources == null) return;

            for (int i = 0; i < sources.Count; i++)
            {
                var s = sources[i];
                if (s.channels == null) continue;

                AddState(s.universe, s.channels);
            }
        }

        private void RebuildAutoDiscoveredStates()
        {
            var root = universeRoot != null ? universeRoot : transform;
            var channels = root.GetComponentsInChildren<ArtNetChannels>(includeInactiveUniverseSources);
            var usedUniverses = new HashSet<int>();

            for (int i = 0; i < channels.Length; i++)
            {
                var channelSet = channels[i];
                if (channelSet == null) continue;

                if (channelSet.universe < 0)
                {
                    Debug.LogWarning($"[DmxTimelinePlayback] Universe must be 0 or greater: '{channelSet.name}'.", channelSet);
                    continue;
                }

                if (!usedUniverses.Add(channelSet.universe))
                {
                    Debug.LogWarning($"[DmxTimelinePlayback] Duplicate Universe {channelSet.universe} ignored: '{channelSet.name}'.", channelSet);
                    continue;
                }

                AddState(channelSet.universe, channelSet);
            }
        }

        private void AddState(int universe, ArtNetChannels channels)
        {
            if (channels == null) return;

            if (forceAnimatorAlwaysAnimate)
            {
                var anim = channels.GetComponent<Animator>();
                if (anim != null)
                    anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            _states.Add(new SourceState
            {
                universe = universe,
                channels = channels,
                buffer = new byte[512],
                initialized = false
            });
        }

        private void Update()
        {
            if (updateTiming == UpdateTiming.Update)
                Tick();
        }

        private void LateUpdate()
        {
            if (updateTiming == UpdateTiming.LateUpdate)
                Tick();
        }

        private void FixedUpdate()
        {
            if (updateTiming == UpdateTiming.FixedUpdate)
                Tick();
        }
        private static float GetTime()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                return (float)UnityEditor.EditorApplication.timeSinceStartup;
#endif
            return Time.unscaledTime;
        }


        private void Tick(bool force = false)
        {
            if (!enablePlayback) return;
            #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (!AnimationMode.InAnimationMode() && _previewBaseCaptured)
                    RestorePreviewBase();
                if (!enableInEditMode) return;
            }
#else
            if (!Application.isPlaying && !enableInEditMode) return;
#endif
            if (rig == null) return;

            if (sampleRate > 0f && !force)
            {
                float t = GetTime();
                if (t < _nextSampleTime) return;
                _nextSampleTime = t + 1f / sampleRate;
            }

            for (int i = 0; i < _states.Count; i++)
            {
                var st = _states[i];
                if (st.channels == null) continue;

                bool changed = st.channels.CopyTo(st.buffer);
#if UNITY_EDITOR
                if (!Application.isPlaying && enableInEditMode && !_previewBaseCaptured && (changed || force))
                    CapturePreviewBase();
#endif
                bool shouldApply = changed || force || (applyOnFirstFrame && !st.initialized);
                if (shouldApply)
                {
                    rig.InjectUniverse(st.universe, st.buffer);
                    st.initialized = true;
                    _states[i] = st;
                }
            }
        }
    }
}
