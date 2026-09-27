using System.Collections.Generic;
using UnityEngine;
#if HAS_HDRP
using UnityEngine.Rendering.HighDefinition;
#endif

namespace ArtNet.Runtime
{
    public partial class DmxFixtureComponent
    {
        [Header("Iris (all light / beam modes)")]
        public bool syncIrisToDmx = true;
        [Tooltip("Optional per-fixture override. Otherwise uses Fixture Definition's shared Iris Profile.")]
        public IrisProfile irisProfile;
        [Min(1)] public int irisInstance = 1;
        private readonly IrisController _iris = new IrisController();
        private readonly Dictionary<Light, IrisCookie> _irisCookies = new Dictionary<Light, IrisCookie>();
        private readonly HashSet<(Renderer renderer, int slot)> _irisRenderers = new HashSet<(Renderer, int)>();
        private static readonly int IrisShapeId = Shader.PropertyToID("_IrisShape");
        private static readonly int IrisLensInfluenceId = Shader.PropertyToID("_IrisLensInfluence");
        private IrisProfile ActiveIrisProfile => irisProfile != null ? irisProfile : (fixture != null ? fixture.irisProfile : null);
        public float CurrentIrisAperture => _iris.Aperture;
        private Vector4 IrisShape => new Vector4(_iris.Diameter(ActiveIrisProfile),
            ActiveIrisProfile != null ? ActiveIrisProfile.feather : 0,
            ActiveIrisProfile != null ? ActiveIrisProfile.blades : 0, 0);

        private void ReadIris(int[] data)
        {
            bool hp = TryReadElementRawForRange(data, FixtureAttribute.Iris, irisInstance, FixtureChannelRole.Value, out int p, out int pm, out var pe);
            if (!hp) hp = TryReadElementRawForRange(data, FixtureAttribute.Iris, irisInstance, FixtureChannelRole.Position, out p, out pm, out pe);
            bool hs = TryReadElementRawForRange(data, FixtureAttribute.Iris, irisInstance, FixtureChannelRole.SelectMode, out int s, out int sm, out var se);
            bool hv = TryReadElement01(data, FixtureAttribute.Iris, irisInstance, FixtureChannelRole.Speed, out float speed);
            _iris.SetInput(syncIrisToDmx ? ActiveIrisProfile : null, hp, p, pm, pe, hs, s, sm, se, hv, speed, !Application.isPlaying);
        }
        private void ReadIris(byte[] data)
        {
            bool hp = TryReadElementRawForRange(data, FixtureAttribute.Iris, irisInstance, FixtureChannelRole.Value, out int p, out int pm, out var pe);
            if (!hp) hp = TryReadElementRawForRange(data, FixtureAttribute.Iris, irisInstance, FixtureChannelRole.Position, out p, out pm, out pe);
            bool hs = TryReadElementRawForRange(data, FixtureAttribute.Iris, irisInstance, FixtureChannelRole.SelectMode, out int s, out int sm, out var se);
            bool hv = TryReadElement01(data, FixtureAttribute.Iris, irisInstance, FixtureChannelRole.Speed, out float speed);
            _iris.SetInput(syncIrisToDmx ? ActiveIrisProfile : null, hp, p, pm, pe, hs, s, sm, se, hv, speed, !Application.isPlaying);
        }
        private void UpdateIris()
        {
            if (!syncIrisToDmx || ActiveIrisProfile == null) _iris.Reset();
            else _iris.Tick(Time.deltaTime, ActiveIrisProfile);
        }

        // Called after normal drivers, preserving custom cookies when gobo sync is disabled.
        private Texture ApplyIrisCookie(Light light, Texture source, FixtureRenderState state)
        {
            if (light == null || light.type != LightType.Spot) return source;
            if (!state.irisEnabled)
            {
                if (_irisCookies.TryGetValue(light, out var previous) && source == previous.Output) source = previous.Source;
                return source;
            }
            if (!_irisCookies.TryGetValue(light, out var cookie))
            { cookie = new IrisCookie(); _irisCookies.Add(light, cookie); }
            return cookie.Apply(source, state.irisShape, ActiveIrisProfile.cookieResolution);
        }

        private void ApplyPrimaryIris(FixtureRenderState state)
        {
            if (!state.irisEnabled && _irisCookies.Count == 0) return;
            var lights = GatherTargetLights();
            foreach (var light in lights)
            {
                if (light == null || light.type != LightType.Spot) continue;
                Texture result = ApplyIrisCookie(light, light.cookie, state);
                SetIrisLightCookie(light, result);
            }
        }
        private static void SetIrisLightCookie(Light light, Texture cookie)
        {
            light.cookie = cookie;
#if HAS_HDRP
            if (light.TryGetComponent<HDAdditionalLightData>(out var hd))
                hd.SetCookie(cookie != null ? cookie : Texture2D.whiteTexture);
#endif
        }
        private void ReleaseIris()
        {
            if (_irisRenderers.Count > 0)
            {
                var block = new MaterialPropertyBlock();
                foreach (var entry in _irisRenderers)
                {
                    if (entry.renderer == null) continue;
                    block.Clear();
                    if (entry.slot < 0) entry.renderer.GetPropertyBlock(block);
                    else entry.renderer.GetPropertyBlock(block, entry.slot);
                    block.SetVector(IrisShapeId, new Vector4(1, 0, 0, 0));
                    block.SetFloat(IrisLensInfluenceId, 0);
                    if (entry.slot < 0) entry.renderer.SetPropertyBlock(block);
                    else entry.renderer.SetPropertyBlock(block, entry.slot);
                }
                _irisRenderers.Clear();
            }
            foreach (var pair in _irisCookies)
            {
                if (pair.Key != null && pair.Key.cookie == pair.Value.Output)
                    SetIrisLightCookie(pair.Key, pair.Value.Source);
                pair.Value.Dispose();
            }
            _irisCookies.Clear();
            _iris.Reset();
        }
    }
}
