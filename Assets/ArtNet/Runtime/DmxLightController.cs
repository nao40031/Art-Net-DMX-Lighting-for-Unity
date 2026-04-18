/*!
 * Copyright (c) 2026 Oshino
 *
 * Released under the MIT license.
 * see https://opensource.org/licenses/MIT
 */

using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArtNet.Runtime
{
    /// <summary>
    /// ArtNetReceiver を購読し、DMXを正規化して LightDriver に渡す（用途ロジックの中心）
    /// </summary>
    public class DmxLightController : MonoBehaviour
    {
        public enum PipelineMode
        {
            Auto,
            ForceGeneric, // Built-in/URP扱い
            ForceHDRP
        }

        [Header("Source")]
        [Tooltip("購読対象の ArtNetReceiver（未設定なら同一GameObjectから取得）")]
        public ArtNetReceiver receiver;

        [Header("Target")]
        public Light targetLight;

        [Tooltip("適用するDriver（Generic/HDRPのどちらかをアタッチして参照）")]
        public MonoBehaviour driverComponent;

        [Header("Pipeline")]
        public PipelineMode pipelineMode = PipelineMode.Auto;

        [Header("Channel Mapping (1-512)")]
        [Range(1, 512)] public int dimmerChannel = 1;
        [Range(1, 512)] public int redChannel = 2;
        [Range(1, 512)] public int greenChannel = 3;
        [Range(1, 512)] public int blueChannel = 4;

        [Header("Optional Filter")]
        [Tooltip("特定Universeのみ処理したい場合に設定。-1 でフィルタ無効")]
        public int onlyUniverse = -1;

        private ILightDriver _driver;

        private void OnEnable()
        {
            if (receiver == null) receiver = GetComponent<ArtNetReceiver>();
            if (receiver != null) receiver.OnDataReceived += Handle;

            ResolveDriver();
            _driver?.Initialize(targetLight);
        }

        private void OnDisable()
        {
            if (receiver != null) receiver.OnDataReceived -= Handle;
        }

        private void ResolveDriver()
        {
            // Inspectorで明示されていればそれを優先
            if (driverComponent is ILightDriver explicitDriver)
            {
                _driver = explicitDriver;
                return;
            }

            // 明示が無ければ、モードとパイプラインで自動選択
            bool wantHdrp = pipelineMode switch
            {
                PipelineMode.ForceHDRP => true,
                PipelineMode.ForceGeneric => false,
                _ => DetectIsHDRP()
            };

#if HAS_HDRP
            if (wantHdrp)
            {
                // 同一GameObject上のHDRPドライバを探す（なければGenericへフォールバック）
                var hdrp = GetComponent<HdrpLightDriver>();
                if (hdrp != null) { _driver = hdrp; return; }
            }
#endif
            var generic = GetComponent<GenericLightDriver>();
            _driver = generic;
        }

        private void Handle(ArtNetData data)
        {
            if (_driver == null || targetLight == null) return;
            if (onlyUniverse >= 0 && data.Universe != onlyUniverse) return;

            byte dimmer = GetChannelByte(data, dimmerChannel);
            byte r = GetChannelByte(data, redChannel);
            byte g = GetChannelByte(data, greenChannel);
            byte b = GetChannelByte(data, blueChannel);

            float dimmer01 = dimmer / 255f;
            var rgb = new Color(r / 255f, g / 255f, b / 255f, 1f);
            var state = new FixtureRenderState
            {
                lightDimmer01 = dimmer01,
                lensDimmer01 = dimmer01,
                color = rgb,
                goboEnabled = false,
                goboTexture = null,
                goboRotationDeg = 0f
            };

            _driver.Apply(state);
        }

        private static byte GetChannelByte(ArtNetData data, int channel1to512)
        {
            if (channel1to512 < 1 || channel1to512 > 512) return 0;
            int idx = channel1to512 - 1;

            if (data.Channels == null || data.Channels.Length <= idx) return 0;

            int v = data.Channels[idx];
            if (v < 0) v = 0;
            if (v > 255) v = 255;
            return (byte)v;
        }

        private static bool DetectIsHDRP()
        {
            // null -> Built-in
            var rp = GraphicsSettings.currentRenderPipeline;
            if (rp == null) return false;

            // パッケージ参照なしで判定（文字列で安全に）
            string typeName = rp.GetType().FullName ?? "";
            return typeName.Contains("HighDefinition", StringComparison.OrdinalIgnoreCase)
                || typeName.Contains("HDRenderPipeline", StringComparison.OrdinalIgnoreCase);
        }
    }
}
