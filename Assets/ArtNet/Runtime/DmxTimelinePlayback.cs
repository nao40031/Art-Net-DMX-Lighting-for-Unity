using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArtNet.Runtime
{
    [DisallowMultipleComponent]
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

        [Header("Target")]
        public DmxRigController rig;

        [Header("Sources")]
        public List<UniverseSource> sources = new();

        [Header("Playback")]
        public bool enablePlayback = true;
        public UpdateTiming updateTiming = UpdateTiming.Update;

        [Tooltip("0なら毎フレーム。例: 40 で 40Hz 更新")]
        [Min(0f)] public float sampleRate = 0f;

        [Tooltip("OnEnable時に値を強制適用（全て0でも一度流す）")]
        public bool forceApplyOnEnable = true;

        [Header("Rig Input Override")]
        public bool overrideRigInputMode = true;
        public DmxRigController.InputMode inputModeWhileEnabled = DmxRigController.InputMode.PlaybackOnly;

        [Header("Debug")]
        public bool logMissingRig = true;

        private readonly List<SourceState> _states = new();
        private float _nextSampleTime;
        private DmxRigController.InputMode _prevMode;
        private bool _modeOverridden;

        private struct SourceState
        {
            public int universe;
            public ArtNetChannels channels;
            public byte[] buffer;
            public bool initialized;
        }

        private void OnEnable()
        {
            ResolveRig();
            RebuildStates();
            _nextSampleTime = 0f;

            if (overrideRigInputMode && rig != null)
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

        private void OnDisable()
        {
            if (_modeOverridden && rig != null)
                rig.inputMode = _prevMode;
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
                RebuildStates();
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
            if (sources == null) return;

            for (int i = 0; i < sources.Count; i++)
            {
                var s = sources[i];
                if (s.channels == null) continue;

                _states.Add(new SourceState
                {
                    universe = s.universe,
                    channels = s.channels,
                    buffer = new byte[512],
                    initialized = false
                });
            }
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

        private void Tick(bool force = false)
        {
            if (!enablePlayback) return;
            if (rig == null) return;

            if (sampleRate > 0f && !force)
            {
                float t = Time.unscaledTime;
                if (t < _nextSampleTime) return;
                _nextSampleTime = t + 1f / sampleRate;
            }

            for (int i = 0; i < _states.Count; i++)
            {
                var st = _states[i];
                if (st.channels == null) continue;

                bool changed = st.channels.CopyTo(st.buffer);
                if (changed || force || !st.initialized)
                {
                    rig.InjectUniverse(st.universe, st.buffer);
                    st.initialized = true;
                    _states[i] = st;
                }
            }
        }
    }
}
