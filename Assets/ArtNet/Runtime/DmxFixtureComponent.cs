/*!
 * Copyright (c) 2026 Oshino
 *
 * Released under the MIT license.
 * see https://opensource.org/licenses/MIT
 */

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

#if HAS_HDRP
using UnityEngine.Rendering.HighDefinition;
#endif

namespace ArtNet.Runtime
{
    [DisallowMultipleComponent]
    public class DmxFixtureComponent : MonoBehaviour
    {
        // ------------------------------------------------------------
        // Basic
        // ------------------------------------------------------------

        [Header("Addressing (RigController expects these)")]
        [Tooltip("DMX Universe番号。送信側と合わせてください（0/1始まりはRigController設定に合わせる）。")]
        public int universe = 0;

        [Tooltip("DMX開始アドレス（1-512）。")]
        [Range(1, 512)]
        public int startAddress = 1;

        [Header("Profile (Fixture/Mode)")]
        public FixtureDefinition fixture;

        // RigControllerが参照するため public。Inspector上はMode名表示でも内部値はindex。
        [SerializeField] public int mode = 0;

        // ------------------------------------------------------------
        // Targets
        // ------------------------------------------------------------

        [Header("Targets")]
        public Light targetLight;
        [Tooltip("複数ライトを対象にする場合に設定。ここに登録した全Lightへ同じDMXを適用します。")]
        public List<Light> targetLights = new();
        public Transform panTransform;
        public Transform tiltTransform;


        // ------------------------------------------------------------
        // Lens (ShaderGraph DMX Sync)
        // ------------------------------------------------------------

        [Header("Lens (ShaderGraph DMX Sync)")]
        [Tooltip("レンズ面のRenderer。ShaderGraph側に _DmxColor(Color) / _DmxDimmer(Float) がある前提です。")]
        [SerializeField] private Renderer lensRenderer;

        [Tooltip("レンズが複数Rendererに分かれる場合の追加登録（任意）。")]
        [SerializeField] private Renderer[] extraLensRenderers;

        [Tooltip("レンズへDMX同期を行います。")]
        [SerializeField] private bool syncLensToDmx = true;
        [SerializeField] private bool syncLensColorToDmx = true;
        [SerializeField] private bool syncLensDimmerToDmx = true;

        [Tooltip("ShaderGraphのColorプロパティ名（例: _DmxColor）。")]
        [SerializeField] private string lensColorProperty = "_DmxColor";

        [Tooltip("ShaderGraphのFloatプロパティ名（例: _DmxDimmer）。")]
        [SerializeField] private string lensDimmerProperty = "_DmxDimmer";

        [Tooltip("Dimmer(0-1)に掛ける倍率。レンズの明るさ調整用。")]
        [SerializeField, Min(0f)] private float lensDimmerScale = 1.0f;

        // ------------------------------------------------------------
        // Gobo
        // ------------------------------------------------------------

        [Header("Gobo")]
        [Tooltip("DMX値とゴボテクスチャを対応付ける定義。")]
        [SerializeField] private GoboWheelDefinition goboWheel;

        [Tooltip("ライトのcookieへゴボを同期します。")]
        [SerializeField] private bool syncLightCookieToGobo = true;

        [Tooltip("レンズ表現へゴボを同期します。")]
        [SerializeField] private bool syncLensGoboToDmx = true;

        [Tooltip("ビーム表現へゴボを同期します。")]
        [SerializeField] private bool syncBeamGoboToDmx = true;

        [Tooltip("GoboRotation=255 のときの角速度（deg/sec）。")]
        [SerializeField, Min(0f)] private float maxGoboRotateDegPerSec = 360f;

        [Tooltip("Gobo WheelのShake範囲をゴボ回転角の往復揺れとして反映します。")]
        [SerializeField] private bool enableGoboShake = true;

        [Tooltip("ゴボ回転をLight Cookie用Transformのロール回転へ同期します。")]
        [SerializeField] private bool syncGoboRotationToCookieTransform = true;

        [Tooltip("Light Cookieを回転させるTransform。通常はSpot Light本体のTransformを指定します。")]
        [SerializeField] private Transform goboCookieRollTransform;

        [Tooltip("Cookie Transformを回転させるローカル軸。通常はZ軸です。")]
        [SerializeField] private Vector3 goboCookieRollAxis = Vector3.forward;

        [Tooltip("Cookie Transformへ加算する固定ロール角度補正（deg）。")]
        [SerializeField] private float goboCookieRollOffsetDeg = 0f;

        [Tooltip("レンズ用ゴボTextureプロパティ名。")]
        [SerializeField] private string lensGoboTextureProperty = "_GoboTexture";

        [Tooltip("レンズ用ゴボ回転プロパティ名。")]
        [SerializeField] private string lensGoboRotationProperty = "_GoboRotationDeg";

        [Tooltip("レンズ用ゴボ有効プロパティ名。")]
        [SerializeField] private string lensGoboEnabledProperty = "_GoboEnabled";

        [Tooltip("ビーム用ゴボTextureプロパティ名。")]
        [SerializeField] private string beamGoboTextureProperty = "_GoboTexture";

        [Tooltip("ビーム用ゴボ回転プロパティ名。")]
        [SerializeField] private string beamGoboRotationProperty = "_GoboRotationDeg";

        [Tooltip("ビーム用ゴボ有効プロパティ名。")]
        [SerializeField] private string beamGoboEnabledProperty = "_GoboEnabled";

        // ------------------------------------------------------------
        // Pseudo Beam (for Volumetrics OFF)
        // ------------------------------------------------------------

        public enum BeamRenderMode
        {
            Normal,
            PseudoBeamShader,
            VolumetricLightBeam
        }

        [Header("Beam Render")]
        [Tooltip("照明ビームの描画方式。VLBのHD/SDはPrefabに付与されたコンポーネントで自動判定します。")]
        [SerializeField] private BeamRenderMode beamRenderMode = BeamRenderMode.Normal;

        [Header("VLB Overrides")]
        [Tooltip("有効時、VolumetricLightBeamHDのIntensity Multiplierをこの値で上書きします。")]
        [SerializeField] private bool overrideVlbHdIntensityMultiplier = true;

        [SerializeField, Min(0f)] private float vlbHdIntensityMultiplier = 0.01f;

        [Tooltip("有効時、VolumetricLightBeamSDのIntensity Multiplierをこの値で上書きします。")]
        [SerializeField] private bool overrideVlbSdIntensityMultiplier = true;

        [SerializeField, Min(0f)] private float vlbSdIntensityMultiplier = 0.01f;

        [Tooltip("有効時、VolumetricLightBeamHDのHDRP Exposure Weightをこの値で上書きします。")]
        [SerializeField] private bool overrideVlbHdrpExposureWeight = true;

        [SerializeField, Range(0f, 1f)] private float vlbHdrpExposureWeight = 0f;

        [SerializeField, HideInInspector] private bool syncPseudoBeamToDmx = false;
        [SerializeField, HideInInspector] private bool _beamRenderModeMigrated;
        [SerializeField, HideInInspector] private bool _vlbPipelineDefaultsInitialized;

        [Header("Pseudo Beam Shader")]
        [Tooltip("Pseudo Beam Shader用の代表Renderer。VLBモードでは使用しません。")]
        [InspectorName("Pseudo Beam Renderer")]
        [SerializeField] private Renderer beamRenderer;

        [Tooltip("Pseudo Beam Shader用Rendererが複数ある場合の追加Renderer。VLBモードでは使用しません。")]
        [InspectorName("Extra Pseudo Beam Renderers")]
        [SerializeField] private Renderer[] extraBeamRenderers;

        [InspectorName("Sync Pseudo Beam Color To Dmx")]
        [SerializeField] private bool syncBeamColorToDmx = true;
        [InspectorName("Sync Pseudo Beam Dimmer To Dmx")]
        [SerializeField] private bool syncBeamDimmerToDmx = true;
        [InspectorName("Sync Pseudo Beam Prism To Dmx")]
        [SerializeField] private bool syncBeamPrismToDmx = true;
        [InspectorName("Sync Pseudo Beam Zoom To Dmx")]
        [SerializeField] private bool syncBeamZoomToDmx = true;
        [InspectorName("Sync Pseudo Beam Noise Volume To Beam")]
        [SerializeField] private bool syncBeamNoiseVolumeToBeam = true;

        private enum BeamNoiseVolumeScrollSpace { Local, World }
        private enum BeamNoiseVolumeScrollAxis { X, Y, Z }

        [Tooltip("ビームマテリアルのColorプロパティ名。例: _DmxColor / _BaseColor")]
        [InspectorName("Pseudo Beam Color Property")]
        [SerializeField] private string beamColorProperty = "_DmxColor";

        [Tooltip("ビームマテリアルの強度プロパティ名。例: _DmxDimmer / _BeamIntensity")]
        [InspectorName("Pseudo Beam Dimmer Property")]
        [SerializeField] private string beamDimmerProperty = "_DmxDimmer";

        [Tooltip("ビームマテリアルのPrism有効プロパティ名。")]
        [InspectorName("Pseudo Beam Prism Enabled Property")]
        [SerializeField] private string beamPrismEnabledProperty = "_DmxPrismEnabled";

        [Tooltip("ビームマテリアルのPrism Facet数プロパティ名。")]
        [InspectorName("Pseudo Beam Prism Facet Count Property")]
        [SerializeField] private string beamPrismFacetCountProperty = "_DmxPrismFacetCount";

        [Tooltip("ビームマテリアルのPrism Spreadプロパティ名。")]
        [InspectorName("Pseudo Beam Prism Spread Property")]
        [SerializeField] private string beamPrismSpreadProperty = "_DmxPrismSpread";

        [Tooltip("ビームマテリアルのPrism Rotationプロパティ名。")]
        [InspectorName("Pseudo Beam Prism Rotation Property")]
        [SerializeField] private string beamPrismRotationProperty = "_DmxPrismRotation";

        [Tooltip("ビームマテリアルのPrism強度プロパティ名。")]
        [InspectorName("Pseudo Beam Prism Intensity Property")]
        [SerializeField] private string beamPrismIntensityProperty = "_DmxPrismIntensity";

        [Tooltip("ビームマテリアルのBeam Lengthプロパティ名。")]
        [InspectorName("Pseudo Beam Length Property")]
        [SerializeField] private string beamLengthProperty = "_BeamLength";

        [Tooltip("ビームマテリアルのBeam Start Radiusプロパティ名。")]
        [InspectorName("Pseudo Beam Start Radius Property")]
        [SerializeField] private string beamStartRadiusProperty = "_BeamStartRadius";

        [Tooltip("ビームマテリアルのBeam End Radiusプロパティ名。")]
        [InspectorName("Pseudo Beam End Radius Property")]
        [SerializeField] private string beamEndRadiusProperty = "_BeamEndRadius";

        [Tooltip("MewNoiseGen等で生成した3DノイズTexture。疑似ビームのフォグ濃淡に使用します。")]
        [InspectorName("Pseudo Beam Noise Volume")]
        [SerializeField] private Texture3D beamNoiseVolume;

        [Tooltip("3DノイズTextureを疑似ビームへ適用します。Texture未設定時は自動で無効扱いになります。")]
        [InspectorName("Pseudo Beam Noise Volume Enabled")]
        [SerializeField] private bool beamNoiseVolumeEnabled = false;

        [Tooltip("既存の手続きノイズと3DノイズTextureのブレンド量。")]
        [InspectorName("Pseudo Beam Noise Volume Strength")]
        [SerializeField, Range(0f, 1f)] private float beamNoiseVolumeStrength = 1f;

        [Tooltip("Open以外のゴボがビームに適用されている時に3DノイズTextureの強さへ掛ける倍率。0でゴボ時のみ3Dノイズを無効化、1で通常時と同じ強さです。")]
        [UnityEngine.Serialization.FormerlySerializedAs("beamNoiseVolumePrismScale")]
        [InspectorName("Pseudo Beam Noise Volume Gobo Scale")]
        [SerializeField, Range(0f, 1f)] private float beamNoiseVolumeGoboScale = 1f;

        [Tooltip("Open GoboでPrismが公転している時に3DノイズTextureの強さへ掛ける倍率。0で公転中のみ3Dノイズを無効化、1で通常時と同じ強さです。")]
        [InspectorName("Pseudo Beam Noise Volume Prism Rotation Scale")]
        [SerializeField, Range(0f, 1f)] private float beamNoiseVolumePrismRotationScale = 0f;

        [Tooltip("3DノイズTextureの空間スケール。大きいほど細かい模様になります。")]
        [InspectorName("Pseudo Beam Noise Volume Scale")]
        [SerializeField, Min(0.001f)] private float beamNoiseVolumeScale = 1f;

        [Tooltip("3DノイズTextureのビーム断面方向スケール。大きいほど断面方向の模様が細かくなります。")]
        [InspectorName("Pseudo Beam Noise Volume Radial Scale")]
        [SerializeField, Min(0.001f)] private float beamNoiseVolumeRadialScale = 4f;

        [Tooltip("3DノイズTextureのビーム長方向スケール。大きいほど長さ方向の模様が細かくなります。")]
        [InspectorName("Pseudo Beam Noise Volume Length Scale")]
        [SerializeField, Min(0.001f)] private float beamNoiseVolumeLengthScale = 12f;

        [Tooltip("3DノイズTextureのZ方向スクロール速度。")]
        [InspectorName("Pseudo Beam Noise Volume Speed")]
        [SerializeField] private float beamNoiseVolumeSpeed = 0.1f;

        [Tooltip("3DノイズTextureのコントラスト。")]
        [InspectorName("Pseudo Beam Noise Volume Contrast")]
        [SerializeField, Min(0.01f)] private float beamNoiseVolumeContrast = 1f;

        [Tooltip("3DノイズTextureのサンプリングオフセット。灯体ごとの柄ずらしに使用します。")]
        [InspectorName("Pseudo Beam Noise Volume Offset")]
        [SerializeField] private Vector3 beamNoiseVolumeOffset = Vector3.zero;

        [Tooltip("3DノイズTextureのスクロール方向をLocal軸基準にするかWorld軸基準にするか。")]
        [InspectorName("Pseudo Beam Noise Volume Scroll Space")]
        [SerializeField] private BeamNoiseVolumeScrollSpace beamNoiseVolumeScrollSpace = BeamNoiseVolumeScrollSpace.Local;

        [Tooltip("3DノイズTextureをスクロールさせる軸。World指定時はムービングの向きが変わってもWorld軸方向を維持します。")]
        [InspectorName("Pseudo Beam Noise Volume Scroll Axis")]
        [SerializeField] private BeamNoiseVolumeScrollAxis beamNoiseVolumeScrollAxis = BeamNoiseVolumeScrollAxis.Z;

        [Tooltip("3DノイズTextureのスクロール方向を反転します。")]
        [InspectorName("Pseudo Beam Noise Volume Scroll Reverse")]
        [SerializeField] private bool beamNoiseVolumeScrollReverse = false;

        [Tooltip("ビームマテリアルの3DノイズTextureプロパティ名。")]
        [InspectorName("Pseudo Beam Noise Volume Property")]
        [SerializeField] private string beamNoiseVolumeProperty = "_BeamNoiseVolume";

        [Tooltip("ビームマテリアルの3Dノイズ有効プロパティ名。")]
        [InspectorName("Pseudo Beam Noise Volume Enabled Property")]
        [SerializeField] private string beamNoiseVolumeEnabledProperty = "_BeamNoiseVolumeEnabled";

        [Tooltip("ビームマテリアルの3Dノイズブレンド強度プロパティ名。")]
        [InspectorName("Pseudo Beam Noise Volume Strength Property")]
        [SerializeField] private string beamNoiseVolumeStrengthProperty = "_BeamNoiseVolumeStrength";

        [Tooltip("ビームマテリアルの3Dノイズスケールプロパティ名。")]
        [InspectorName("Pseudo Beam Noise Volume Scale Property")]
        [SerializeField] private string beamNoiseVolumeScaleProperty = "_BeamNoiseVolumeScale";

        [Tooltip("ビームマテリアルの3Dノイズ断面方向スケールプロパティ名。")]
        [InspectorName("Pseudo Beam Noise Volume Radial Scale Property")]
        [SerializeField] private string beamNoiseVolumeRadialScaleProperty = "_BeamNoiseVolumeRadialScale";

        [Tooltip("ビームマテリアルの3Dノイズ長さ方向スケールプロパティ名。")]
        [InspectorName("Pseudo Beam Noise Volume Length Scale Property")]
        [SerializeField] private string beamNoiseVolumeLengthScaleProperty = "_BeamNoiseVolumeLengthScale";

        [Tooltip("ビームマテリアルの3Dノイズ速度プロパティ名。")]
        [InspectorName("Pseudo Beam Noise Volume Speed Property")]
        [SerializeField] private string beamNoiseVolumeSpeedProperty = "_BeamNoiseVolumeSpeed";

        [Tooltip("ビームマテリアルの3Dノイズコントラストプロパティ名。")]
        [InspectorName("Pseudo Beam Noise Volume Contrast Property")]
        [SerializeField] private string beamNoiseVolumeContrastProperty = "_BeamNoiseVolumeContrast";

        [Tooltip("ビームマテリアルの3Dノイズオフセットプロパティ名。")]
        [InspectorName("Pseudo Beam Noise Volume Offset Property")]
        [SerializeField] private string beamNoiseVolumeOffsetProperty = "_BeamNoiseVolumeOffset";

        [Tooltip("ビームマテリアルの3Dノイズスクロール方向プロパティ名。")]
        [InspectorName("Pseudo Beam Noise Volume Scroll Direction Property")]
        [SerializeField] private string beamNoiseVolumeScrollDirectionProperty = "_BeamNoiseVolumeScrollDirection";

        [Tooltip("ビーム強度に掛ける倍率。")]
        [InspectorName("Pseudo Beam Dimmer Scale")]
        [SerializeField, Min(0f)] private float beamDimmerScale = 1.0f;

        [Tooltip("ビーム強度の下限値。暗転時の残光調整用。")]
        [InspectorName("Pseudo Beam Dimmer Floor")]
        [SerializeField, Range(0f, 1f)] private float beamDimmerFloor = 0f;

        [Tooltip("Zoomから計算した疑似ビーム先端半径へ掛ける倍率。")]
        [InspectorName("Pseudo Beam Zoom Radius Scale")]
        [SerializeField, Min(0f)] private float beamZoomRadiusScale = 1f;

        [Tooltip("Zoom連動時の疑似ビーム先端半径の最小値。")]
        [InspectorName("Pseudo Beam Zoom Min End Radius")]
        [SerializeField, Min(0.001f)] private float beamZoomMinEndRadius = 0.01f;

        [Tooltip("Zoom連動時の疑似ビーム先端半径の最大値。")]
        [InspectorName("Pseudo Beam Zoom Max End Radius")]
        [SerializeField, Min(0.001f)] private float beamZoomMaxEndRadius = 25f;

        [Tooltip("疑似ビームの外側に薄いにじみ用レイヤーを追加します。")]
        [InspectorName("Enable Pseudo Beam Soft Shell")]
        [SerializeField] private bool enableBeamSoftShell = false;

        [Tooltip("にじみ用ビームの半径倍率。")]
        [InspectorName("Pseudo Beam Soft Shell Radius Scale")]
        [SerializeField, Min(1f)] private float beamSoftShellRadiusScale = 1.35f;

        [Tooltip("にじみ用ビームのMaterial側Beam Intensity。")]
        [InspectorName("Pseudo Beam Soft Shell Intensity")]
        [SerializeField, Min(0f)] private float beamSoftShellIntensity = 0.2f;

        [Tooltip("にじみ用ビームのEdge Softness上書き値。")]
        [InspectorName("Pseudo Beam Soft Shell Edge Softness")]
        [SerializeField, Range(0f, 1f)] private float beamSoftShellEdgeSoftness = 0.9f;

        [Tooltip("にじみ用ビームのNoise Strength上書き値。")]
        [InspectorName("Pseudo Beam Soft Shell Noise Strength")]
        [SerializeField, Range(0f, 1f)] private float beamSoftShellNoiseStrength = 0.25f;

        // ------------------------------------------------------------
        // Zoom
        // ------------------------------------------------------------

        [Header("Zoom")]
        [Tooltip("DMXのZoom値をLightのOuter Spot Angleへ同期します。")]
        [SerializeField] private bool syncZoomToLight = true;

        [Tooltip("Zoom最小時のOuter Spot Angle。")]
        [SerializeField, Range(0.1f, 179f)] private float minOuterSpotAngle = 5f;

        [Tooltip("Zoom最大時のOuter Spot Angle。")]
        [SerializeField, Range(0.1f, 179f)] private float maxOuterSpotAngle = 50f;

        [Tooltip("DMX Zoom値の向きを反転します。")]
        [SerializeField] private bool invertZoom = false;

        [Tooltip("Outer Spot Angleに対するInner Spotの割合。HDRPではinnerSpotPercentとして使用します。")]
        [SerializeField, Range(0f, 100f)] private float zoomInnerSpotPercent = 80f;

        // ------------------------------------------------------------
        // Prism
        // ------------------------------------------------------------

        public enum PrismRenderMode
        {
            None = 0,
            CookieComposite = 1,
            AuxiliaryLights = 2,
            CookieCompositeAndAuxiliaryLights = 3
        }

        public enum AuxiliaryCookieRotationMode
        {
            CompositeTextureRoll = 0,
            TransformRoll = 1,
            Auto = 2
        }

        public enum PrismDrawMode
        {
            ProjectionOnly = 0,
            FullAuxiliaryLights = 1,
            ProjectionAndShaderBeam = 2
        }

        [Header("Prism")]
        [Tooltip("DMX値とプリズムスロットを対応付ける定義。")]
        [SerializeField] private PrismDefinition prismDefinition;

        [Tooltip("プリズム機能を有効にします。オフの場合、Prism DefinitionとDMX値が設定されていてもプリズム描画は行いません。")]
        [SerializeField] private bool enablePrism = true;

        [Tooltip("プリズムの描画モード。Projection Onlyは床・壁のCookie投影中心、Full Auxiliary Lightsは高品質、Projection + Shader Beamは将来のShaderビーム分割用です。")]
        [SerializeField] private PrismDrawMode prismDrawMode = PrismDrawMode.ProjectionOnly;

        [Tooltip("旧プリズム描画方式。既存Prefabの互換性維持用です。")]
        [SerializeField, HideInInspector] private PrismRenderMode prismRenderMode = PrismRenderMode.CookieComposite;

        [Tooltip("旧Projection Only互換用。現在のProjection OnlyではPrimary Lightを抑制し、補助ライト側で投影します。")]
        [SerializeField, HideInInspector, Range(0f, 1f)] private float projectionOnlyPrimaryIntensityScale = 0.65f;

        [Tooltip("Projection Only時に補助HDRPライトのVolumetric Dimmerを0にします。")]
        [SerializeField] private bool disableAuxiliaryVolumetricInProjectionOnly = true;

        [Tooltip("AuxiliaryLightsでのGobo Rotation方式。AutoはHDRPならTransform Roll、それ以外はComposite Texture Rollです。")]
        [SerializeField] private AuxiliaryCookieRotationMode auxiliaryCookieRotationMode = AuxiliaryCookieRotationMode.CompositeTextureRoll;

        [Tooltip("Prism Rotation=最大時の角速度（deg/sec）。")]
        [SerializeField, Min(0f)] private float maxPrismRotateDegPerSec = 360f;

        [Tooltip("CookieComposite用のRenderTextureサイズ。")]
        [SerializeField, Min(16)] private int prismCompositeCookieSize = 512;

        [Tooltip("角度変化がこの値未満ならCookieを再合成しません。0で毎回更新。")]
        [SerializeField, Min(0f)] private float prismCompositeRotationStepDeg = 1f;

        [Tooltip("CookieCompositeで扱う最大Facet数。")]
        [SerializeField, Range(1, 16)] private int maxPrismCompositeFacets = 8;

        [Tooltip("AuxiliaryLightsで扱う最大Facet数。")]
        [SerializeField, Range(1, 16)] private int maxPrismAuxiliaryFacets = 8;

        [Tooltip("PrismSlot.spreadをAuxiliary Lightの角度へ変換する倍率。")]
        [SerializeField, Min(0f)] private float auxiliarySpreadMultiplierDeg = 10f;

        [Tooltip("Auxiliary Lightの明るさに掛ける追加倍率。")]
        [SerializeField, Min(0f)] private float auxiliaryIntensityScale = 1f;

        [Tooltip("補助SpotLightのVolumetricだけに掛ける倍率。床や壁へのCookie投影の明るさは変えず、Full Auxiliary Lightsの空間ビームだけを調整します。")]
        [SerializeField, Range(0f, 30f)] private float auxiliaryVolumetricIntensityScale = 1f;

        [Tooltip("プリズム時の明るさ分散量。0で分散なし、1でFacet数に応じて完全分散します。")]
        [SerializeField, Range(0f, 1f)] private float prismBrightnessDistribution = 1f;

        private enum PrismZoomCorrectionMode
        {
            Off,
            Auto,
            Custom
        }

        [Tooltip("プリズム分割されたゴボ同士の距離をZoomに連動して補正します。Offは補正なし、AutoはSpot Angleから自動補正、Customは下の倍率をそのまま使います。")]
        [SerializeField] private PrismZoomCorrectionMode prismZoomCorrection = PrismZoomCorrectionMode.Off;

        [Tooltip("プリズム分割されたゴボ同士の距離倍率。AutoではZoom補正後の倍率、Customでは直接の距離倍率として使用します。")]
        [SerializeField, Range(0f, 3f)] private float prismGoboSpacingScale = 1f;

        [Tooltip("VLBのプリズム時に、各ゴボ投影の外形サイズをSpot Angleで調整します。1で通常のSpot Lightに合わせた基準サイズです。")]
        [InspectorName("Prism Gobo Scale (VLB)")]
        [SerializeField, Range(0.1f, 3f)] private float prismGoboScaleVlb = 1f;

        [Tooltip("Auxiliary LightのShadowを有効化します。初期OFF推奨。")]
        [SerializeField] private bool auxiliaryLightShadows = false;

        [Tooltip("Cookie合成Shader。未設定の場合はResources/Shader.Findから解決します。")]
        [SerializeField] private Shader prismCookieShader;

        // ------------------------------------------------------------
        // Light Response (LED / Halogen)
        // ------------------------------------------------------------

        public enum LightResponseMode { Led, Halogen }

        [Header("Light Response")]
        public LightResponseMode lightResponseMode = LightResponseMode.Led;

        [Header("Halogen Response (seconds)")]
        [Tooltip("ソース(レンズ)の立ち上がり時間（0→1秒）。")]
        [SerializeField, Min(0f)] private float halogenSourceRiseTime = 0.06f;

        [Tooltip("ソース(レンズ)の立ち下がり時間（1→0秒）。")]
        [SerializeField, Min(0f)] private float halogenSourceFallTime = 0.10f;

        [Tooltip("ソースON後、ビーム(ライト)が点灯するまでの遅延。")]
        [SerializeField, Min(0f)] private float halogenBeamOnDelay = 0.04f;

        [Tooltip("ソースOFF後、ビーム(ライト)が消灯するまでの遅延。")]
        [SerializeField, Min(0f)] private float halogenBeamOffDelay = 0.04f;

        [Tooltip("ビーム(ライト)の立ち上がり時間（0→1秒）。")]
        [SerializeField, Min(0f)] private float halogenBeamRiseTime = 0.06f;

        [Tooltip("ビーム(ライト)の立ち下がり時間（1→0秒）。")]
        [SerializeField, Min(0f)] private float halogenBeamFallTime = 0.08f;

        // MPBで per-renderer 上書きし、マテリアルインスタンス増加を避ける
        private MaterialPropertyBlock _lensMpb;
        private int _lensColorId;
        private int _lensDimmerId;
        private int _lensGoboTextureId;
        private int _lensGoboRotationId;
        private int _lensGoboEnabledId;
        private int _lensGoboOffsetId;
        private bool _lensPropertyIdsReady;

        private MaterialPropertyBlock _beamMpb;
        private int _beamColorId;
        private int _beamDimmerId;
        private int _beamGoboTextureId;
        private int _beamGoboRotationId;
        private int _beamGoboEnabledId;
        private int _beamGoboOffsetId;
        private int _beamPrismEnabledId;
        private int _beamPrismFacetCountId;
        private int _beamPrismSpreadId;
        private int _beamPrismRotationId;
        private int _beamPrismIntensityId;
        private int _beamLengthId;
        private int _beamStartRadiusId;
        private int _beamEndRadiusId;
        private int _beamIntensityId;
        private int _beamEdgeSoftnessId;
        private int _beamNoiseStrengthId;
        private int _beamNoiseVolumeId;
        private int _beamNoiseVolumeEnabledId;
        private int _beamNoiseVolumeStrengthId;
        private int _beamNoiseVolumeScaleId;
        private int _beamNoiseVolumeRadialScaleId;
        private int _beamNoiseVolumeLengthScaleId;
        private int _beamNoiseVolumeSpeedId;
        private int _beamNoiseVolumeContrastId;
        private int _beamNoiseVolumeOffsetId;
        private int _beamNoiseVolumeScrollDirectionId;
        private bool _beamPropertyIdsReady;
        [Header("Pan/Tilt Range (degrees)")]
        public float panRangeDeg = 540f;
        public float tiltRangeDeg = 270f;

        // ------------------------------------------------------------
        // Pipeline / Driver (Built-in / URP / HDRP)
        // ------------------------------------------------------------

        public enum PipelineMode { Auto, ForceGeneric, ForceHDRP }

        [Header("Pipeline / Driver")]
        public PipelineMode pipelineMode = PipelineMode.Auto;

        [Tooltip("明示的に使用するドライバ（GenericLightDriver / HdrpLightDriver など）。")]
        public MonoBehaviour driverOverride;

        [Tooltip("ドライバが無い場合に自動追加します（同一GameObject）。")]
        public bool autoAddDriverIfMissing = true;

        [Header("Driver Intensity Defaults")]
        [Tooltip("GenericLightDriver.maxIntensity の既定値。")]
        public float genericMaxIntensity = 10f;

        [Tooltip("HdrpLightDriver.maxIntensity の既定値。")]
        public float hdrpMaxIntensity = 9870f;

        [Tooltip("trueの場合、Initialize時に上記maxIntensityをドライバへ上書きします。")]
        public bool overrideDriverMaxIntensity = true;

        // ------------------------------------------------------------
        // Pan/Tilt Axis / Tuning
        // ------------------------------------------------------------

        public enum LocalAxis { X, Y, Z, MinusX, MinusY, MinusZ }

        [Header("Pan/Tilt Axis / Tuning")]
        [Tooltip("Panのローカル回転軸。")]
        public LocalAxis panAxis = LocalAxis.Z; // default: Z axis

        [Tooltip("Tiltのローカル回転軸。")]
        public LocalAxis tiltAxis = LocalAxis.X;

        public bool panInvert = false;
        public bool tiltInvert = false;

        [Tooltip("Panの角度オフセット（度）。")]
        public float panOffsetDeg = 0f;

        [Tooltip("Tiltの角度オフセット（度）。")]
        public float tiltOffsetDeg = 0f;

        [Range(0f, 30f)]
        [Tooltip("Pan/Tiltの追従スムージング。0=即時、値を上げるほど滑らか。")]
        public float panTiltSmoothing = 12f;


        [Tooltip("DMX更新(例:40Hz)と描画更新の差を毎フレーム補間で滑らかにします。")]
        public bool enableContinuousPanTiltUpdate = true;

        [Header("Edit Mode Preview")]
        [Tooltip("EditモードのTimelineプレビュー時にPan/Tiltと光量を即時反映します。")]
        public bool applyImmediateInEditMode = true;

        [Header("Pan/Tilt Speed (deg/sec)  ※PanTiltSpeedがある機種のみ有効")]
        [Tooltip("PanTiltSpeed=0 のときの角速度（deg/sec）。")]
        public float panTiltSpeedMinDegPerSec = 30f;

        [Tooltip("PanTiltSpeed=255 のときの角速度（deg/sec）。")]
        public float panTiltSpeedMaxDegPerSec = 720f;

        [Header("Reset Threshold")]
        [Tooltip("Reset ch がこの値以上のとき Reset を実行します（機種に合わせて調整）。")]
        [Range(0, 255)]
        public int resetTriggerThreshold = 250;

        [Header("Auto Resolve")]
        [SerializeField] private bool autoResolveOnValidate = true;

        // ------------------------------------------------------------
        // Monitoring (migrated from DmxDebugMonitor, no editor extension)
        // ------------------------------------------------------------

        [Header("Monitoring (DMX)")]
        [Tooltip("このFixtureのDMXモニタを有効化します（Inspector表示/周期ログ）。")]
        [SerializeField] private bool monitorEnabled = true;

        [Header("Monitor Channels (Absolute 1-512)")]
        [Tooltip("Universe内の絶対チャンネル（1-512）を監視します。")]
        [Range(1, 512)] public int monitorCh1 = 1;
        [Range(1, 512)] public int monitorCh2 = 2;
        [Range(1, 512)] public int monitorCh3 = 3;
        [Range(1, 512)] public int monitorCh4 = 4;
        [Range(1, 512)] public int monitorCh5 = 5;

        [Header("Monitoring (Auto / Relative)")]
        [Tooltip("FixtureDefinitionのModeで解決されたFixtureFunctionを自動で一覧表示します。")]
        [SerializeField] private bool monitorIncludeResolvedFunctions = true;

        [Tooltip("startAddress基準の相対ch（1=Start）を追加監視します（例: 1,2,3,10）。")]
        [SerializeField] private List<int> monitorExtraRelativeChannels = new();

        [Header("Debug Values (ReadOnly)")]
        [SerializeField, Range(0, 255)] private int ch1;
        [SerializeField, Range(0, 255)] private int ch2;
        [SerializeField, Range(0, 255)] private int ch3;
        [SerializeField, Range(0, 255)] private int ch4;
        [SerializeField, Range(0, 255)] private int ch5;

        [Serializable]
        public struct DmxMonitorItem
        {
            public bool useElementSchema;
            public FixtureFunction function; // 0 の場合は function未指定。UIでは relXX 表示
            public string label;
            public FixtureAttribute attribute;
            public int instance;
            public FixtureChannelRole role;
            public FixtureByteRole byteRole;
            public int relativeCh;           // fixture内の相対ch（1=startAddress）
            public int absoluteCh;           // 1-based within universe
            [Range(0, 255)] public int value;
        }

        [Header("Debug Values (Resolved Function -> Value)")]
        [SerializeField] private List<DmxMonitorItem> monitorItems = new();

        [Header("Periodic Logging (Flood Protection)")]
        [Tooltip("一定間隔で受信データを要約ログ出力します（ログ洪水対策あり）。")]
        [SerializeField] private bool enablePeriodicLog = false;

        [Tooltip("ログ出力間隔（秒）。例: 1.0で1秒ごと。")]
        [SerializeField, Min(0.1f)] private float logIntervalSec = 1.0f;

        [Tooltip("受信が無い間はログを出さない（無駄ログ抑制）。")]
        [SerializeField] private bool logOnlyWhenDataArrived = true;

        [Tooltip("ログ先頭にヘッダー（Universe/Start/Modeなど）を含めます。")]
        [SerializeField] private bool includeHeaderInLog = true;

        private float _nextLogTime;
        private bool _hasNewDataSinceLastLog;

        // ------------------------------------------------------------
        // Runtime
        // ------------------------------------------------------------

        [NonSerialized] public ILightDriver runtimeDriver;
        [NonSerialized] public bool isInitialized;
        [NonSerialized] public List<ILightDriver> runtimeDrivers = new();

        private Quaternion _panBaseLocalRot;
        private Quaternion _tiltBaseLocalRot;
        private bool _movementBaseCaptured;

#if UNITY_EDITOR
        [NonSerialized] private bool _previewLightCaptured;
        [NonSerialized] private List<Light> _previewLightTargets = new();
        [NonSerialized] private List<float> _previewLightIntensity = new();
        [NonSerialized] private List<Color> _previewLightColor = new();
#endif

        private bool _hasDmxTargets;
        private float _targetDimmer01;
        private Color _targetColor = Color.white;
        private float _lensTargetDimmer01;
        private float _lightTargetDimmer01;
        private float _currentLensDimmer01;
        private float _currentLightDimmer01;
        private bool _lightTargetPending;
        private float _lightTargetPendingAt;
        private float _pendingLightTargetDimmer01;
        private bool _lensTargetPending;
        private float _lensTargetPendingAt;
        private float _pendingLensTargetDimmer01;
        private bool _goboEnabled;
        private Texture _goboTexture;
        private float _goboRotationDeg;
        private float _goboRotationOffsetDeg;
        private float _goboRotationSpeedDegPerSec;
        private bool _goboShakeEnabled;
        private float _goboShakePhaseRad;
        private float _goboShakeOffsetDeg;
        private float _goboShakeAmplitudeDeg;
        private float _goboShakePositionAmplitudeUv;
        private float _goboShakeBeamAngleAmplitudeDeg;
        private Vector2 _goboShakePositionOffsetUv;
        private Vector2 _goboShakeBeamAngleOffsetDeg;
        private float _goboShakeSpeedHz;
        private GoboShakeMotionMode _goboShakeMotionMode = GoboShakeMotionMode.Rotation;
        private GoboShakePositionAxis _goboShakePositionAxis = GoboShakePositionAxis.Horizontal;
        private bool _goboShakeAffectBeam;
        private bool _goboShakeApplyWithPrism;
        private bool _goboShakeApplyWithZoom;
        private bool _goboShakeAllowDuringGoboRotation = true;
        private bool _zoomEnabled;
        private float _zoomOuterSpotAngleDeg;
        private float _zoomInnerSpotPercent;
        private bool _prismEnabled;
        private int _prismFacetCount = 1;
        private float _prismSpread;
        private float _prismFacetScale = 1f;
        private float _prismRotationDeg;
        private float _prismRotationOffsetDeg;
        private float _prismRotationSpeedDegPerSec;
        private float _prismIntensityScale = 1f;
        private RenderTexture _prismCompositeCookie;
        private RenderTexture _prismRotatedGoboCookie;
        private RenderTexture _goboOffsetCookie;
        private Material _prismCookieMaterial;
        private static System.Reflection.MethodInfo _textureIncrementUpdateCountMethod;
        private static bool _textureIncrementUpdateCountMethodResolved;
        private Texture _lastPrismCompositeSource;
        private Texture _lastPrismRotatedSource;
        private Texture _lastGoboOffsetSource;
        private float _lastPrismCompositeGoboRotation = float.NaN;
        private float _lastPrismCompositePrismRotation = float.NaN;
        private float _lastPrismRotatedGoboRotation = float.NaN;
        private Vector2 _lastPrismCompositeGoboOffset = new Vector2(float.NaN, float.NaN);
        private Vector2 _lastPrismRotatedGoboOffset = new Vector2(float.NaN, float.NaN);
        private Vector2 _lastGoboOffset = new Vector2(float.NaN, float.NaN);
        private int _lastPrismCompositeFacetCount = -1;
        private float _lastPrismCompositeSpread = float.NaN;
        private float _lastPrismCompositeFacetScale = float.NaN;
        private float _lastPrismCompositeIntensityScale = float.NaN;
        private int _lastPrismCookieSize = -1;
        private int _lastPrismRotatedCookieSize = -1;
        private int _lastGoboOffsetCookieSize = -1;
        private readonly List<PrismAuxiliaryLight> _prismAuxiliaryLights = new();
        private readonly List<PrismPseudoBeamFacet> _prismPseudoBeamFacets = new();
        private VlbBeamAdapter _vlbBeamAdapter;
        private Transform _prismAuxRoot;
        private Transform _prismPseudoBeamRoot;
        private Transform _singlePseudoBeamSoftShell;
        private PrismPseudoBeamCone _singlePseudoBeamSoftCone;
        private MeshRenderer _singlePseudoBeamSoftRenderer;
        private Transform _goboCookieRollBaseTransform;
        private Quaternion _goboCookieRollBaseLocalRot;
        private bool _hasGoboCookieRollBaseLocalRot;

#if HAS_HDRP
        private readonly List<HdrpVolumetricDimmerDefault> _primaryVolumetricDimmerDefaults = new();
#endif

        private class PrismAuxiliaryLight
        {
            public Transform directionPivot;
            public Transform cookieRollPivot;
            public Light light;
            public Component vlbHd;
            public Component vlbCookieHd;
#if HAS_HDRP
            public HDAdditionalLightData hd;
#endif
        }

#if HAS_HDRP
        private class HdrpVolumetricDimmerDefault
        {
            public HDAdditionalLightData hd;
            public float value;
        }
#endif

        private class PrismPseudoBeamFacet
        {
            public Transform directionPivot;
            public MeshFilter filter;
            public MeshRenderer renderer;
            public PrismPseudoBeamCone softCone;
            public MeshRenderer softRenderer;
        }

        // --- Pan/Tilt continuous interpolation targets (updated by DMX, applied every frame) ---
        private bool _hasPanTarget;
        private bool _hasTiltTarget;
        private Quaternion _panTargetLocalRot;
        private Quaternion _tiltTargetLocalRot;
        private float _panTargetMaxDegPerSec = -1f;
        private float _tiltTargetMaxDegPerSec = -1f;
        private float _panTargetSmoothing = 0.15f;
        private float _tiltTargetSmoothing = 0.15f;

        private readonly Dictionary<FixtureFunction, int> _relativeMap = new();
        private readonly Dictionary<ElementKey, ElementBinding> _elementMap = new();
        private readonly Dictionary<WheelKey, GoboWheelDefinition> _goboWheelMap = new();
        private bool _usesElementMode;
        [SerializeField] private string _activeProfileLabel;
        [SerializeField] private string _activeModeLabel;

        private struct ElementKey : IEquatable<ElementKey>
        {
            public FixtureAttribute attribute;
            public int instance;
            public FixtureChannelRole role;

            public ElementKey(FixtureAttribute attribute, int instance, FixtureChannelRole role)
            {
                this.attribute = attribute;
                this.instance = Mathf.Max(1, instance);
                this.role = role;
            }

            public bool Equals(ElementKey other)
                => attribute == other.attribute && instance == other.instance && role == other.role;

            public override bool Equals(object obj)
                => obj is ElementKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = (int)attribute;
                    hash = (hash * 397) ^ instance;
                    hash = (hash * 397) ^ (int)role;
                    return hash;
                }
            }
        }

        private struct WheelKey : IEquatable<WheelKey>
        {
            public FixtureAttribute attribute;
            public int instance;

            public WheelKey(FixtureAttribute attribute, int instance)
            {
                this.attribute = attribute;
                this.instance = Mathf.Max(1, instance);
            }

            public bool Equals(WheelKey other)
                => attribute == other.attribute && instance == other.instance;

            public override bool Equals(object obj)
                => obj is WheelKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((int)attribute * 397) ^ instance;
                }
            }
        }

        private struct ElementBinding
        {
            public int singleRel;
            public int coarseRel;
            public int fineRel;
            public FixtureChannelElement singleElement;
            public FixtureChannelElement coarseElement;

            public FixtureChannelElement PrimaryElement => singleElement ?? coarseElement;
        }

        [Serializable]
        public struct ResolvedItem
        {
            public FixtureFunction function;
            public bool useElementSchema;
            public string label;
            public FixtureAttribute attribute;
            public int instance;
            public FixtureChannelRole role;
            public FixtureByteRole byteRole;
            public int relativeChannel; // 1-based within fixture
            public int absoluteChannel; // 1-based within universe
        }

        [SerializeField] private List<ResolvedItem> _resolvedItems = new();

        // ------------------------------------------------------------
        // RigController integration
        // ------------------------------------------------------------

        public bool IsValid
        {
            get
            {
                if (fixture == null) return false;
                if (fixture.modes == null || fixture.modes.Count == 0) return false;
                if (mode < 0 || mode >= fixture.modes.Count) return false;

                int count = Mathf.Clamp(fixture.modes[mode].channelCount, 1, 512);
                if (startAddress < 1 || startAddress > 512) return false;
                if (startAddress + count - 1 > 512) return false;
                return true;
            }
        }

        public void Initialize() => ResolveAll();
        public void Initialize(bool _) => ResolveAll();

        [ContextMenu("Auto Attach Target Light (Find in Children)")]
        public void Context_AutoAttachTargetLight()
        {
            if (targetLight == null)
                targetLight = GetComponentInChildren<Light>(true);
        }

        [ContextMenu("Auto Attach Target Lights (Find in Children)")]
        public void Context_AutoAttachTargetLights()
        {
            var lights = GetComponentsInChildren<Light>(true);
            targetLights = new List<Light>(lights);
            if (targetLight == null && lights.Length > 0)
                targetLight = lights[0];
        }

        [ContextMenu("Auto Attach Beam Renderers (Name contains Beam)")]
        public void Context_AutoAttachBeamRenderers()
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            var found = new List<Renderer>();

            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                if (r == lensRenderer) continue;

                var n = r.gameObject.name;
                if (n.IndexOf("beam", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("volum", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    found.Add(r);
                }
            }

            if (found.Count == 0)
                return;

            beamRenderer = found[0];
            if (found.Count > 1)
            {
                extraBeamRenderers = new Renderer[found.Count - 1];
                for (int i = 1; i < found.Count; i++)
                    extraBeamRenderers[i - 1] = found[i];
            }
            else
            {
                extraBeamRenderers = Array.Empty<Renderer>();
            }
        }

        public void ApplyFromUniverseBuffer(int[] universe512) => ApplyFromUniverseBufferInternal(universe512);

        public void ApplyFromUniverseBuffer(byte[] universe512) => ApplyFromUniverseBufferInternal(universe512);

        // ------------------------------------------------------------
        // Unity lifecycle
        // ------------------------------------------------------------

        private void Reset()
        {
            ApplyVlbPipelineDefaults();
            Context_AutoAttachTargetLight();
            CaptureMovementBaseIfNeeded(force: true);
        }

        private void OnEnable()
        {
            MigrateLegacyBeamRenderMode();
            ResolveAll();

            if (Application.isPlaying)
                CaptureMovementBaseIfNeeded(force: true);

            _nextLogTime = Time.unscaledTime + Mathf.Max(0.1f, logIntervalSec);
            _hasNewDataSinceLastLog = false;
        }

        private void OnValidate()
        {
            MigrateLegacyBeamRenderMode();
            InitializeVlbPipelineDefaultsIfNeeded();
            if (!autoResolveOnValidate) return;
            ResolveAll();
        }

        private void OnDisable()
        {
            RestorePrimaryVolumetricDefaults();
            DisablePrismAuxiliaryLights();
            DisableVlbBeam();
        }

        private void OnDestroy()
        {
            RestorePrimaryVolumetricDefaults();
            ReleasePrismResources();
        }

        private void MigrateLegacyBeamRenderMode()
        {
            if (_beamRenderModeMigrated)
                return;

            if (syncPseudoBeamToDmx)
                beamRenderMode = BeamRenderMode.PseudoBeamShader;

            syncPseudoBeamToDmx = beamRenderMode == BeamRenderMode.PseudoBeamShader;
            _beamRenderModeMigrated = true;
        }

        [ContextMenu("Apply VLB Pipeline Defaults")]
        public void ApplyVlbPipelineDefaults()
        {
            var pipeline = DetectCurrentRenderPipeline();
            vlbHdIntensityMultiplier = pipeline == RenderPipelineKind.HDRP ? 0.00001f : 0.01f;
            vlbSdIntensityMultiplier = 0.01f;
            vlbHdrpExposureWeight = 0f;
            overrideVlbHdIntensityMultiplier = true;
            overrideVlbSdIntensityMultiplier = true;
            overrideVlbHdrpExposureWeight = true;
            _vlbPipelineDefaultsInitialized = true;
        }

        private void InitializeVlbPipelineDefaultsIfNeeded()
        {
            if (_vlbPipelineDefaultsInitialized)
                return;

            ApplyVlbPipelineDefaults();
        }

        private enum RenderPipelineKind
        {
            BuiltIn,
            URP,
            HDRP
        }

        private static RenderPipelineKind DetectCurrentRenderPipeline()
        {
            var pipeline = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
            if (pipeline == null)
                return RenderPipelineKind.BuiltIn;

            string typeName = pipeline.GetType().FullName ?? pipeline.GetType().Name;
            if (typeName.Contains("HighDefinition"))
                return RenderPipelineKind.HDRP;
            if (typeName.Contains("Universal"))
                return RenderPipelineKind.URP;

            return RenderPipelineKind.BuiltIn;
        }

        private void Update()
        {
            UpdatePanTiltMotion();
            UpdateGoboMotion();
            UpdatePrismMotion();
            UpdateLightResponse();

            if (!monitorEnabled) return;
            if (!enablePeriodicLog) return;

            if (Time.unscaledTime < _nextLogTime) return;

            if (logOnlyWhenDataArrived && !_hasNewDataSinceLastLog)
            {
                _nextLogTime = Time.unscaledTime + Mathf.Max(0.1f, logIntervalSec);
                return;
            }

            Debug.Log(BuildMonitorSummaryLog(), this);

            _hasNewDataSinceLastLog = false;
            _nextLogTime = Time.unscaledTime + Mathf.Max(0.1f, logIntervalSec);
        }

        // ------------------------------------------------------------
        // Debug / Labels
        // ------------------------------------------------------------

        public string ActiveProfileLabel => _activeProfileLabel;
        public string ActiveModeLabel => _activeModeLabel;

        public IReadOnlyList<(FixtureFunction function, int relativeCh, int absoluteCh)> GetResolved()
        {
            var result = new List<(FixtureFunction, int, int)>(_resolvedItems.Count);
            foreach (var it in _resolvedItems) result.Add((it.function, it.relativeChannel, it.absoluteChannel));
            return result;
        }

        // ------------------------------------------------------------
        // Resolve Mapping + Driver
        // ------------------------------------------------------------

        private void ResolveAll()
        {
            ResolveMapping();
            if (monitorEnabled) RebuildMonitorItemsSkeleton();
            ResolveAndInitializeDrivers();
            CaptureMovementBaseIfNeeded(force: false);
        }

        public void ResolveMapping()
        {
            _relativeMap.Clear();
            _elementMap.Clear();
            _goboWheelMap.Clear();
            _resolvedItems.Clear();
            _usesElementMode = false;

            if (fixture == null)
            {
                _activeProfileLabel = "(No Fixture)";
                _activeModeLabel = "(No Mode)";
                return;
            }

            _activeProfileLabel = fixture.GetDisplayLabel();

            if (fixture.modes == null || fixture.modes.Count == 0)
            {
                _activeModeLabel = "(No Modes)";
                return;
            }

            mode = Mathf.Clamp(mode, 0, fixture.modes.Count - 1);
            var md = fixture.modes[mode];

            _activeModeLabel = string.IsNullOrWhiteSpace(md.modeName) ? $"Mode {mode}" : md.modeName;

            if (md.UsesChannelElements())
            {
                BuildElementMapping(md);
                if (_elementMap.Count > 0)
                {
                    _usesElementMode = true;
                    return;
                }
            }

            if (md.channels == null) return;

            foreach (var fc in md.channels)
            {
                if (fc == null) continue;

                int rel = Mathf.Clamp(fc.channel, 1, 512);
                _relativeMap[fc.function] = rel;

                int abs = startAddress + rel - 1;
                _resolvedItems.Add(new ResolvedItem
                {
                    useElementSchema = false,
                    function = fc.function,
                    label = fc.function.ToString(),
                    relativeChannel = rel,
                    absoluteChannel = abs
                });
            }
        }

        private void BuildElementMapping(FixtureModeDefinition md)
        {
            int count = Mathf.Clamp(md.channelCount, 1, 512);

            for (int i = 0; i < count; i++)
            {
                var element = (md.elements != null && i < md.elements.Count) ? md.elements[i] : null;
                int rel = i + 1;
                int instance = element != null ? Mathf.Max(1, element.instance) : 1;
                var attribute = element != null ? element.attribute : FixtureAttribute.NoFeature;
                var role = element != null ? element.role : FixtureChannelRole.Value;
                var byteRole = element != null ? element.byteRole : FixtureByteRole.Single;

                _resolvedItems.Add(new ResolvedItem
                {
                    useElementSchema = true,
                    function = default,
                    label = BuildElementLabel(attribute, instance, role, byteRole),
                    attribute = attribute,
                    instance = instance,
                    role = role,
                    byteRole = byteRole,
                    relativeChannel = rel,
                    absoluteChannel = startAddress + rel - 1
                });

                if (element == null) continue;
                if (element.attribute == FixtureAttribute.NoFeature) continue;

                var key = new ElementKey(element.attribute, instance, element.role);

                if (!_elementMap.TryGetValue(key, out var binding))
                    binding = default;

                switch (element.byteRole)
                {
                    case FixtureByteRole.Single:
                        binding.singleRel = rel;
                        binding.singleElement = element;
                        break;
                    case FixtureByteRole.Coarse:
                        binding.coarseRel = rel;
                        binding.coarseElement = element;
                        break;
                    case FixtureByteRole.Fine:
                        binding.fineRel = rel;
                        break;
                }

                _elementMap[key] = binding;
            }

            if (md.wheelBindings == null) return;

            for (int i = 0; i < md.wheelBindings.Count; i++)
            {
                var binding = md.wheelBindings[i];
                if (binding == null) continue;
                if (binding.goboWheel == null) continue;

                var key = new WheelKey(binding.attribute, binding.instance);
                _goboWheelMap[key] = binding.goboWheel;
            }
        }

        private void RebuildMonitorItemsSkeleton()
        {
            if (monitorItems == null) monitorItems = new List<DmxMonitorItem>();
            monitorItems.Clear();

            // 1) FixtureDefinition modeで解決した関数群を追加
            if (monitorIncludeResolvedFunctions && _resolvedItems != null)
            {
                for (int i = 0; i < _resolvedItems.Count; i++)
                {
                    var r = _resolvedItems[i];
                    monitorItems.Add(new DmxMonitorItem
                    {
                        useElementSchema = r.useElementSchema,
                        function = r.function,
                        label = r.label,
                        attribute = r.attribute,
                        instance = r.instance,
                        role = r.role,
                        byteRole = r.byteRole,
                        relativeCh = Mathf.Clamp(r.relativeChannel, 1, 512),
                        absoluteCh = Mathf.Clamp(r.absoluteChannel, 1, 512),
                        value = 0
                    });
                }
            }

            // 2) 追加の相対chを追記（重複除外）
            if (monitorExtraRelativeChannels != null)
            {
                for (int i = 0; i < monitorExtraRelativeChannels.Count; i++)
                {
                    int rel = monitorExtraRelativeChannels[i];
                    if (rel < 1) continue;

                    bool dup = false;
                    for (int k = 0; k < monitorItems.Count; k++)
                    {
                        if (monitorItems[k].relativeCh == rel)
                        {
                            dup = true;
                            break;
                        }
                    }
                    if (dup) continue;

                    int abs = startAddress + rel - 1;
                    monitorItems.Add(new DmxMonitorItem
                    {
                        useElementSchema = false,
                        function = default,
                        label = $"rel{rel}",
                        relativeCh = rel,
                        absoluteCh = Mathf.Clamp(abs, 1, 512),
                        value = 0
                    });
                }
            }

            // 表示を安定させるため relativeCh 昇順に並べる（件数が少ないため O(n^2) で十分）
            for (int i = 0; i < monitorItems.Count - 1; i++)
            {
                for (int j = i + 1; j < monitorItems.Count; j++)
                {
                    if (monitorItems[j].relativeCh < monitorItems[i].relativeCh)
                    {
                        var tmp = monitorItems[i];
                        monitorItems[i] = monitorItems[j];
                        monitorItems[j] = tmp;
                    }
                }
            }
        }

        public bool TryGetRelativeChannel(FixtureFunction function, out int relative1Based)
            => _relativeMap.TryGetValue(function, out relative1Based);

        private void ResolveAndInitializeDrivers()
        {
            isInitialized = false;
            runtimeDriver = null;

            if (targetLight == null && (targetLights == null || targetLights.Count == 0))
                Context_AutoAttachTargetLight();

            if (runtimeDrivers == null) runtimeDrivers = new List<ILightDriver>();
            runtimeDrivers.Clear();

            var targets = GatherTargetLights();
            if (targets.Count == 0)
                return;

            var usedDrivers = new HashSet<ILightDriver>();
            var overrideDriver = driverOverride as ILightDriver;

            if (driverOverride != null && overrideDriver == null)
                Debug.LogWarning($"[DmxFixtureComponent] driverOverride is set but does not implement ILightDriver: {driverOverride.GetType().Name}", this);

#if HAS_HDRP
            var hdrpOnSelf = GetComponents<HdrpLightDriver>();
#endif
            var genericOnSelf = GetComponents<GenericLightDriver>();

            bool warnedOverrideReuse = false;
            foreach (var light in targets)
            {
                if (light == null) continue;

                bool detectedHdrp = false;
#if HAS_HDRP
                detectedHdrp = (light.GetComponent<HDAdditionalLightData>() != null);
#endif

                bool wantHdrp = pipelineMode switch
                {
                    PipelineMode.ForceHDRP => true,
                    PipelineMode.ForceGeneric => false,
                    _ => detectedHdrp
                };

                ILightDriver driver = null;

                // 1) override (first light only)
                if (overrideDriver != null && !usedDrivers.Contains(overrideDriver))
                {
                    driver = overrideDriver;
                }
                else if (overrideDriver != null && !warnedOverrideReuse)
                {
                    Debug.LogWarning("[DmxFixtureComponent] driverOverride is set but multiple targetLights were found. " +
                                     "driverOverride will be used for the first light only.", this);
                    warnedOverrideReuse = true;
                }

                // 2) resolve from SAME GameObject (unused components only)
                if (driver == null)
                {
#if HAS_HDRP
                    if (wantHdrp)
                    {
                        for (int i = 0; i < hdrpOnSelf.Length; i++)
                        {
                            if (!usedDrivers.Contains(hdrpOnSelf[i]))
                            {
                                driver = hdrpOnSelf[i];
                                break;
                            }
                        }
                    }
#endif
                    if (driver == null)
                    {
                        for (int i = 0; i < genericOnSelf.Length; i++)
                        {
                            if (!usedDrivers.Contains(genericOnSelf[i]))
                            {
                                driver = genericOnSelf[i];
                                break;
                            }
                        }
                    }
                }

                // 2.5) legacy fallback: targetLight側にあるDriverを探す
                if (driver == null)
                {
#if HAS_HDRP
                    var hdrpOnLight = light.GetComponent<HdrpLightDriver>();
                    if (hdrpOnLight != null && !usedDrivers.Contains(hdrpOnLight))
                    {
                        driver = hdrpOnLight;
                        Debug.LogWarning("[DmxFixtureComponent] Found LightDriver on targetLight GameObject (legacy). " +
                                         "Now we recommend attaching LightDriver to the same GameObject as DmxFixtureComponent.", this);
                    }
#endif
                    if (driver == null)
                    {
                        var genericOnLight = light.GetComponent<GenericLightDriver>();
                        if (genericOnLight != null && !usedDrivers.Contains(genericOnLight))
                        {
                            driver = genericOnLight;
                            Debug.LogWarning("[DmxFixtureComponent] Found LightDriver on targetLight GameObject (legacy). " +
                                             "Now we recommend attaching LightDriver to the same GameObject as DmxFixtureComponent.", this);
                        }
                    }
                }

                // 3) auto add to SAME GameObject
                if (driver == null && autoAddDriverIfMissing)
                {
#if HAS_HDRP
                    if (wantHdrp)
                        driver = gameObject.AddComponent<HdrpLightDriver>();
                    else
                        driver = gameObject.AddComponent<GenericLightDriver>();
#else
                    if (wantHdrp)
                        Debug.LogWarning($"[DmxFixtureComponent] HDRP is requested but HAS_HDRP is not enabled. Using GenericLightDriver: {name}", this);

                    driver = gameObject.AddComponent<GenericLightDriver>();
#endif
                }

                if (driver == null) continue;

                usedDrivers.Add(driver);
                driver.Initialize(light);

                if (overrideDriverMaxIntensity)
                {
                    if (driver is GenericLightDriver gd)
                        gd.maxIntensity = genericMaxIntensity;

#if HAS_HDRP
                    if (driver is HdrpLightDriver hd)
                        hd.maxIntensity = hdrpMaxIntensity;
#endif
                }

                runtimeDrivers.Add(driver);
            }

            runtimeDriver = runtimeDrivers.Count > 0 ? runtimeDrivers[0] : null;
            isInitialized = runtimeDrivers.Count > 0;
        }

        private List<Light> GatherTargetLights()
        {
            var result = new List<Light>();

            if (targetLights != null)
            {
                for (int i = 0; i < targetLights.Count; i++)
                {
                    var l = targetLights[i];
                    if (l == null) continue;
                    if (!result.Contains(l)) result.Add(l);
                }
            }

            if (targetLight != null && !result.Contains(targetLight))
                result.Add(targetLight);

            return result;
        }

        private void CaptureMovementBaseIfNeeded(bool force)
        {
            if (!force && _movementBaseCaptured) return;

            if (panTransform != null) _panBaseLocalRot = panTransform.localRotation;
            if (tiltTransform != null) _tiltBaseLocalRot = tiltTransform.localRotation;

            _movementBaseCaptured = true;
        }

        // ------------------------------------------------------------
        // Apply core
        // ------------------------------------------------------------

        private void ApplyFromUniverseBufferInternal(int[] universe512)
        {
            if (universe512 == null) return;
            if (!IsValid) return;

            if (targetLight == null) Context_AutoAttachTargetLight();

            if (_usesElementMode)
            {
                ApplyElementMode(universe512);
                UpdateMonitorValues(universe512);
                ApplyImmediateInEditMode();
                return;
            }

            // --- Reset ---
            if (TryGetRelativeChannel(FixtureFunction.Reset, out int resetRel))
            {
                int resetAbs = startAddress + resetRel - 1;
                int resetVal = Read8Abs(universe512, resetAbs);
                if (resetVal >= resetTriggerThreshold)
                    PerformReset();
            }

            // --- Dimmer ---
            float dim01 = 1f;
            if (TryGetRelativeChannel(FixtureFunction.Dimmer, out int dimRel))
            {
                int dimAbs = startAddress + dimRel - 1;
                dim01 = DmxValueUtils.ByteTo01(Read8Abs(universe512, dimAbs));
            }

            // --- RGB (+White) ---
            Color rgb = Color.white;

            bool hasR = TryGetRelativeChannel(FixtureFunction.Red, out int rRel);
            bool hasG = TryGetRelativeChannel(FixtureFunction.Green, out int gRel);
            bool hasB = TryGetRelativeChannel(FixtureFunction.Blue, out int bRel);

            if (hasR && hasG && hasB)
            {
                float r = DmxValueUtils.ByteTo01(Read8Abs(universe512, startAddress + rRel - 1));
                float g = DmxValueUtils.ByteTo01(Read8Abs(universe512, startAddress + gRel - 1));
                float b = DmxValueUtils.ByteTo01(Read8Abs(universe512, startAddress + bRel - 1));

                rgb = new Color(r, g, b, 1f);

                // Whiteがある場合は加算して簡易ミックス
                if (TryGetRelativeChannel(FixtureFunction.White, out int wRel))
                {
                    float w = DmxValueUtils.ByteTo01(Read8Abs(universe512, startAddress + wRel - 1));
                    rgb = rgb + Color.white * w;
                }
            }

            int goboValue = 0;
            if (TryGetRelativeChannel(FixtureFunction.Gobo, out int goboRel))
                goboValue = Read8Abs(universe512, startAddress + goboRel - 1);

            int goboRotationValue = 127;
            if (TryGetRelativeChannel(FixtureFunction.GoboRotation, out int goboRotationRel))
                goboRotationValue = Read8Abs(universe512, startAddress + goboRotationRel - 1);

            UpdateGoboTargetsFromDmx(goboValue, goboRotationValue);
            int prismValue = 0;
            if (TryGetRelativeChannel(FixtureFunction.Prism, out int prismRel))
                prismValue = Read8Abs(universe512, startAddress + prismRel - 1);
            UpdatePrismTargetsFromDmx(prismValue, false, 0f, false, 0f);
            UpdateLightTargetsFromDmx(dim01, rgb);

            // --- Pan/Tilt Speed（ある機種のみ適用） ---
            bool hasSpeed = TryGetRelativeChannel(FixtureFunction.PanTiltSpeed, out int spRel);
            float maxDegPerSec = panTiltSpeedMaxDegPerSec;
            if (hasSpeed)
            {
                float sp01 = DmxValueUtils.ByteTo01(Read8Abs(universe512, startAddress + spRel - 1)); // slow -> fast
                maxDegPerSec = Mathf.Lerp(panTiltSpeedMinDegPerSec, panTiltSpeedMaxDegPerSec, sp01);
            }

            // --- Pan ---
            if (panTransform != null && TryGetRelativeChannel(FixtureFunction.PanCoarse, out int panCoarseRel))
            {
                int panFineRel = TryGetRelativeChannel(FixtureFunction.PanFine, out int pf) ? pf : -1;

                int panAbsCoarse = startAddress + panCoarseRel - 1;
                int panAbsFine = (panFineRel > 0) ? (startAddress + panFineRel - 1) : -1;

                int pan16 = Read16Abs(universe512, panAbsCoarse, panAbsFine);
                float pan01 = pan16 / 65535f;
                float panDeg = (pan01 - 0.5f) * panRangeDeg;

                if (panInvert) panDeg = -panDeg;
                panDeg += panOffsetDeg;

                SetPanTarget(panDeg, hasSpeed ? maxDegPerSec : -1f, panTiltSmoothing);
}

            // --- Tilt ---
            if (tiltTransform != null && TryGetRelativeChannel(FixtureFunction.TiltCoarse, out int tiltCoarseRel))
            {
                int tiltFineRel = TryGetRelativeChannel(FixtureFunction.TiltFine, out int tf) ? tf : -1;

                int tiltAbsCoarse = startAddress + tiltCoarseRel - 1;
                int tiltAbsFine = (tiltFineRel > 0) ? (startAddress + tiltFineRel - 1) : -1;

                int tilt16 = Read16Abs(universe512, tiltAbsCoarse, tiltAbsFine);
                float tilt01 = tilt16 / 65535f;
                float tiltDeg = (tilt01 - 0.5f) * tiltRangeDeg;

                if (tiltInvert) tiltDeg = -tiltDeg;
                tiltDeg += tiltOffsetDeg;

                SetTiltTarget(tiltDeg, hasSpeed ? maxDegPerSec : -1f, panTiltSmoothing);
}

            // --- Monitoring update (per-fixture realtime values) ---
            UpdateMonitorValues(universe512);
            ApplyImmediateInEditMode();
        }

        private void ApplyFromUniverseBufferInternal(byte[] universe512)
        {
            if (universe512 == null) return;
            if (!IsValid) return;

            if (targetLight == null) Context_AutoAttachTargetLight();

            if (_usesElementMode)
            {
                ApplyElementMode(universe512);
                UpdateMonitorValues(universe512);
                ApplyImmediateInEditMode();
                return;
            }

            // --- Reset ---
            if (TryGetRelativeChannel(FixtureFunction.Reset, out int resetRel))
            {
                int resetAbs = startAddress + resetRel - 1;
                int resetVal = Read8Abs(universe512, resetAbs);
                if (resetVal >= resetTriggerThreshold)
                    PerformReset();
            }

            // --- Dimmer ---
            float dim01 = 1f;
            if (TryGetRelativeChannel(FixtureFunction.Dimmer, out int dimRel))
            {
                int dimAbs = startAddress + dimRel - 1;
                dim01 = DmxValueUtils.ByteTo01(Read8Abs(universe512, dimAbs));
            }

            // --- RGB (+White) ---
            Color rgb = Color.white;

            bool hasR = TryGetRelativeChannel(FixtureFunction.Red, out int rRel);
            bool hasG = TryGetRelativeChannel(FixtureFunction.Green, out int gRel);
            bool hasB = TryGetRelativeChannel(FixtureFunction.Blue, out int bRel);

            if (hasR && hasG && hasB)
            {
                float r = DmxValueUtils.ByteTo01(Read8Abs(universe512, startAddress + rRel - 1));
                float g = DmxValueUtils.ByteTo01(Read8Abs(universe512, startAddress + gRel - 1));
                float b = DmxValueUtils.ByteTo01(Read8Abs(universe512, startAddress + bRel - 1));

                rgb = new Color(r, g, b, 1f);

                // Whiteがある場合は加算して簡易ミックス
                if (TryGetRelativeChannel(FixtureFunction.White, out int wRel))
                {
                    float w = DmxValueUtils.ByteTo01(Read8Abs(universe512, startAddress + wRel - 1));
                    rgb = rgb + Color.white * w;
                }
            }

            int goboValue = 0;
            if (TryGetRelativeChannel(FixtureFunction.Gobo, out int goboRel))
                goboValue = Read8Abs(universe512, startAddress + goboRel - 1);

            int goboRotationValue = 127;
            if (TryGetRelativeChannel(FixtureFunction.GoboRotation, out int goboRotationRel))
                goboRotationValue = Read8Abs(universe512, startAddress + goboRotationRel - 1);

            UpdateGoboTargetsFromDmx(goboValue, goboRotationValue);
            int prismValue = 0;
            if (TryGetRelativeChannel(FixtureFunction.Prism, out int prismRel))
                prismValue = Read8Abs(universe512, startAddress + prismRel - 1);
            UpdatePrismTargetsFromDmx(prismValue, false, 0f, false, 0f);
            UpdateLightTargetsFromDmx(dim01, rgb);

            // --- Pan/Tilt Speed（ある機種のみ適用） ---
            bool hasSpeed = TryGetRelativeChannel(FixtureFunction.PanTiltSpeed, out int spRel);
            float maxDegPerSec = panTiltSpeedMaxDegPerSec;
            if (hasSpeed)
            {
                float sp01 = DmxValueUtils.ByteTo01(Read8Abs(universe512, startAddress + spRel - 1)); // slow -> fast
                maxDegPerSec = Mathf.Lerp(panTiltSpeedMinDegPerSec, panTiltSpeedMaxDegPerSec, sp01);
            }

            // --- Pan ---
            if (panTransform != null && TryGetRelativeChannel(FixtureFunction.PanCoarse, out int panCoarseRel))
            {
                int panFineRel = TryGetRelativeChannel(FixtureFunction.PanFine, out int pf) ? pf : -1;

                int panAbsCoarse = startAddress + panCoarseRel - 1;
                int panAbsFine = (panFineRel > 0) ? (startAddress + panFineRel - 1) : -1;

                int pan16 = Read16Abs(universe512, panAbsCoarse, panAbsFine);
                float pan01 = pan16 / 65535f;
                float panDeg = (pan01 - 0.5f) * panRangeDeg;

                if (panInvert) panDeg = -panDeg;
                panDeg += panOffsetDeg;

                SetPanTarget(panDeg, hasSpeed ? maxDegPerSec : -1f, panTiltSmoothing);
}

            // --- Tilt ---
            if (tiltTransform != null && TryGetRelativeChannel(FixtureFunction.TiltCoarse, out int tiltCoarseRel))
            {
                int tiltFineRel = TryGetRelativeChannel(FixtureFunction.TiltFine, out int tf) ? tf : -1;

                int tiltAbsCoarse = startAddress + tiltCoarseRel - 1;
                int tiltAbsFine = (tiltFineRel > 0) ? (startAddress + tiltFineRel - 1) : -1;

                int tilt16 = Read16Abs(universe512, tiltAbsCoarse, tiltAbsFine);
                float tilt01 = tilt16 / 65535f;
                float tiltDeg = (tilt01 - 0.5f) * tiltRangeDeg;

                if (tiltInvert) tiltDeg = -tiltDeg;
                tiltDeg += tiltOffsetDeg;

                SetTiltTarget(tiltDeg, hasSpeed ? maxDegPerSec : -1f, panTiltSmoothing);
}

            // --- Monitoring update (per-fixture realtime values) ---
            UpdateMonitorValues(universe512);
            ApplyImmediateInEditMode();
        }


        private void ApplyElementMode(int[] universe512)
        {
            if (TryElementValueMatchesRange(universe512, FixtureAttribute.Control, 1, FixtureChannelRole.Control, FixtureRangeType.Reset, out _) ||
                TryElementValueMatchesRange(universe512, FixtureAttribute.Control, 1, FixtureChannelRole.Value, FixtureRangeType.Reset, out _))
            {
                PerformReset();
                return;
            }

            float dim01 = TryReadElement01(universe512, FixtureAttribute.Dimmer, 1, FixtureChannelRole.Value, out float dim)
                ? dim
                : 1f;

            if (TryElementValueMatchesRange(universe512, FixtureAttribute.Strobe, 1, FixtureChannelRole.Value, FixtureRangeType.Closed, out _))
                dim01 = 0f;

            Color rgb = ReadElementColor(universe512);

            UpdateZoomTargetsFromDmx(TryReadElement01(universe512, FixtureAttribute.Zoom, 1, FixtureChannelRole.Value, out float zoom01, out bool zoomUsesRangeMapping), zoom01, zoomUsesRangeMapping);

            int goboValue = TryReadElementRaw8(universe512, FixtureAttribute.GoboWheel, 1, FixtureChannelRole.SelectMode, out int goboRaw)
                ? goboRaw
                : 0;

            int goboRotationValue = 127;
            bool hasGoboRotationSpeedOverride = false;
            float goboRotationSpeedOverride = 0f;
            if (TryReadElementRawForRange(universe512, FixtureAttribute.GoboWheel, 1, FixtureChannelRole.PositionOrRotation, out int goboRotationRaw, out int goboRotationRawMax, out var goboRotationElement) ||
                TryReadElementRawForRange(universe512, FixtureAttribute.GoboWheel, 1, FixtureChannelRole.Rotation, out goboRotationRaw, out goboRotationRawMax, out goboRotationElement))
            {
                if (TryMapGoboRotationRangeToSpeed(goboRotationElement, goboRotationRaw, goboRotationRawMax, out goboRotationSpeedOverride))
                {
                    hasGoboRotationSpeedOverride = true;
                }
                else
                {
                    goboRotationValue = Mathf.Clamp(Mathf.RoundToInt((goboRotationRaw / Mathf.Max(1f, goboRotationRawMax)) * 255f), 0, 255);
                }
            }

            UpdateGoboTargetsFromDmx(goboValue, goboRotationValue, ResolveGoboWheelDefinition(1), hasGoboRotationSpeedOverride, goboRotationSpeedOverride);
            UpdatePrismTargetsFromElementDmx(universe512);
            UpdateLightTargetsFromDmx(dim01, rgb);

            if (panTransform != null && TryReadElement01(universe512, FixtureAttribute.Pan, 1, FixtureChannelRole.Position, out float pan01))
            {
                float panDeg = (pan01 - 0.5f) * panRangeDeg;
                if (panInvert) panDeg = -panDeg;
                panDeg += panOffsetDeg;
                SetPanTarget(panDeg, -1f, panTiltSmoothing);
            }

            if (tiltTransform != null && TryReadElement01(universe512, FixtureAttribute.Tilt, 1, FixtureChannelRole.Position, out float tilt01))
            {
                float tiltDeg = (tilt01 - 0.5f) * tiltRangeDeg;
                if (tiltInvert) tiltDeg = -tiltDeg;
                tiltDeg += tiltOffsetDeg;
                SetTiltTarget(tiltDeg, -1f, panTiltSmoothing);
            }
        }

        private void ApplyElementMode(byte[] universe512)
        {
            if (TryElementValueMatchesRange(universe512, FixtureAttribute.Control, 1, FixtureChannelRole.Control, FixtureRangeType.Reset, out _) ||
                TryElementValueMatchesRange(universe512, FixtureAttribute.Control, 1, FixtureChannelRole.Value, FixtureRangeType.Reset, out _))
            {
                PerformReset();
                return;
            }

            float dim01 = TryReadElement01(universe512, FixtureAttribute.Dimmer, 1, FixtureChannelRole.Value, out float dim)
                ? dim
                : 1f;

            if (TryElementValueMatchesRange(universe512, FixtureAttribute.Strobe, 1, FixtureChannelRole.Value, FixtureRangeType.Closed, out _))
                dim01 = 0f;

            Color rgb = ReadElementColor(universe512);

            UpdateZoomTargetsFromDmx(TryReadElement01(universe512, FixtureAttribute.Zoom, 1, FixtureChannelRole.Value, out float zoom01, out bool zoomUsesRangeMapping), zoom01, zoomUsesRangeMapping);

            int goboValue = TryReadElementRaw8(universe512, FixtureAttribute.GoboWheel, 1, FixtureChannelRole.SelectMode, out int goboRaw)
                ? goboRaw
                : 0;

            int goboRotationValue = 127;
            bool hasGoboRotationSpeedOverride = false;
            float goboRotationSpeedOverride = 0f;
            if (TryReadElementRawForRange(universe512, FixtureAttribute.GoboWheel, 1, FixtureChannelRole.PositionOrRotation, out int goboRotationRaw, out int goboRotationRawMax, out var goboRotationElement) ||
                TryReadElementRawForRange(universe512, FixtureAttribute.GoboWheel, 1, FixtureChannelRole.Rotation, out goboRotationRaw, out goboRotationRawMax, out goboRotationElement))
            {
                if (TryMapGoboRotationRangeToSpeed(goboRotationElement, goboRotationRaw, goboRotationRawMax, out goboRotationSpeedOverride))
                {
                    hasGoboRotationSpeedOverride = true;
                }
                else
                {
                    goboRotationValue = Mathf.Clamp(Mathf.RoundToInt((goboRotationRaw / Mathf.Max(1f, goboRotationRawMax)) * 255f), 0, 255);
                }
            }

            UpdateGoboTargetsFromDmx(goboValue, goboRotationValue, ResolveGoboWheelDefinition(1), hasGoboRotationSpeedOverride, goboRotationSpeedOverride);
            UpdatePrismTargetsFromElementDmx(universe512);
            UpdateLightTargetsFromDmx(dim01, rgb);

            if (panTransform != null && TryReadElement01(universe512, FixtureAttribute.Pan, 1, FixtureChannelRole.Position, out float pan01))
            {
                float panDeg = (pan01 - 0.5f) * panRangeDeg;
                if (panInvert) panDeg = -panDeg;
                panDeg += panOffsetDeg;
                SetPanTarget(panDeg, -1f, panTiltSmoothing);
            }

            if (tiltTransform != null && TryReadElement01(universe512, FixtureAttribute.Tilt, 1, FixtureChannelRole.Position, out float tilt01))
            {
                float tiltDeg = (tilt01 - 0.5f) * tiltRangeDeg;
                if (tiltInvert) tiltDeg = -tiltDeg;
                tiltDeg += tiltOffsetDeg;
                SetTiltTarget(tiltDeg, -1f, panTiltSmoothing);
            }
        }

        private bool TryReadElement01(int[] universe512, FixtureAttribute attribute, int instance, FixtureChannelRole role, out float value01)
        {
            return TryReadElement01(universe512, attribute, instance, role, out value01, out _);
        }

        private bool TryReadElement01(int[] universe512, FixtureAttribute attribute, int instance, FixtureChannelRole role, out float value01, out bool usedRangeMapping)
        {
            value01 = 0f;
            usedRangeMapping = false;
            if (!_elementMap.TryGetValue(new ElementKey(attribute, instance, role), out var binding))
                return false;

            if (binding.singleRel > 0)
            {
                int raw8 = Read8Abs(universe512, startAddress + binding.singleRel - 1);
                if (TryNormalizeElementRange(binding.PrimaryElement, raw8, 255, out value01))
                {
                    usedRangeMapping = true;
                    return true;
                }

                value01 = DmxValueUtils.ByteTo01(raw8);
                return true;
            }

            if (binding.coarseRel > 0)
            {
                int coarseAbs = startAddress + binding.coarseRel - 1;
                int fineAbs = binding.fineRel > 0 ? startAddress + binding.fineRel - 1 : -1;
                int raw16 = Read16Abs(universe512, coarseAbs, fineAbs);
                if (TryNormalizeElementRange(binding.PrimaryElement, raw16, 65535, out value01))
                {
                    usedRangeMapping = true;
                    return true;
                }

                value01 = raw16 / 65535f;
                return true;
            }

            return false;
        }

        private bool TryReadElement01(byte[] universe512, FixtureAttribute attribute, int instance, FixtureChannelRole role, out float value01)
        {
            return TryReadElement01(universe512, attribute, instance, role, out value01, out _);
        }

        private bool TryReadElement01(byte[] universe512, FixtureAttribute attribute, int instance, FixtureChannelRole role, out float value01, out bool usedRangeMapping)
        {
            value01 = 0f;
            usedRangeMapping = false;
            if (!_elementMap.TryGetValue(new ElementKey(attribute, instance, role), out var binding))
                return false;

            if (binding.singleRel > 0)
            {
                int raw8 = Read8Abs(universe512, startAddress + binding.singleRel - 1);
                if (TryNormalizeElementRange(binding.PrimaryElement, raw8, 255, out value01))
                {
                    usedRangeMapping = true;
                    return true;
                }

                value01 = DmxValueUtils.ByteTo01(raw8);
                return true;
            }

            if (binding.coarseRel > 0)
            {
                int coarseAbs = startAddress + binding.coarseRel - 1;
                int fineAbs = binding.fineRel > 0 ? startAddress + binding.fineRel - 1 : -1;
                int raw16 = Read16Abs(universe512, coarseAbs, fineAbs);
                if (TryNormalizeElementRange(binding.PrimaryElement, raw16, 65535, out value01))
                {
                    usedRangeMapping = true;
                    return true;
                }

                value01 = raw16 / 65535f;
                return true;
            }

            return false;
        }

        private static bool TryNormalizeElementRange(FixtureChannelElement element, int rawValue, int defaultDmxMax, out float value01)
        {
            value01 = 0f;
            if (element == null || element.ranges == null)
                return false;

            FixtureChannelRange bestRange = null;
            int bestWidth = int.MaxValue;

            for (int i = 0; i < element.ranges.Count; i++)
            {
                var range = element.ranges[i];
                if (range == null) continue;
                if (!range.Contains(rawValue)) continue;
                if (!range.HasExplicitMapping(defaultDmxMax)) continue;

                int width = Mathf.Abs(range.dmxMax - range.dmxMin);
                if (bestRange == null || width < bestWidth)
                {
                    bestRange = range;
                    bestWidth = width;
                }
            }

            if (bestRange == null)
                return false;

            value01 = Mathf.Clamp01(bestRange.Normalize(rawValue));
            return true;
        }

        private bool TryReadElementRaw8(int[] universe512, FixtureAttribute attribute, int instance, FixtureChannelRole role, out int value)
        {
            value = 0;
            if (!_elementMap.TryGetValue(new ElementKey(attribute, instance, role), out var binding))
                return false;

            int rel = binding.singleRel > 0 ? binding.singleRel : binding.coarseRel;
            if (rel <= 0) return false;

            value = Read8Abs(universe512, startAddress + rel - 1);
            return true;
        }

        private bool TryReadElementRaw8(byte[] universe512, FixtureAttribute attribute, int instance, FixtureChannelRole role, out int value)
        {
            value = 0;
            if (!_elementMap.TryGetValue(new ElementKey(attribute, instance, role), out var binding))
                return false;

            int rel = binding.singleRel > 0 ? binding.singleRel : binding.coarseRel;
            if (rel <= 0) return false;

            value = Read8Abs(universe512, startAddress + rel - 1);
            return true;
        }

        private bool TryReadElementRaw16(int[] universe512, FixtureAttribute attribute, int instance, FixtureChannelRole role, out int value, out FixtureChannelElement element)
        {
            value = 0;
            element = null;
            if (!_elementMap.TryGetValue(new ElementKey(attribute, instance, role), out var binding))
                return false;

            element = binding.PrimaryElement;

            if (binding.coarseRel > 0)
            {
                int coarseAbs = startAddress + binding.coarseRel - 1;
                int fineAbs = binding.fineRel > 0 ? startAddress + binding.fineRel - 1 : -1;
                value = Read16Abs(universe512, coarseAbs, fineAbs);
                return true;
            }

            if (binding.singleRel > 0)
            {
                value = Read8Abs(universe512, startAddress + binding.singleRel - 1) * 257;
                return true;
            }

            return false;
        }

        private bool TryReadElementRaw16(byte[] universe512, FixtureAttribute attribute, int instance, FixtureChannelRole role, out int value, out FixtureChannelElement element)
        {
            value = 0;
            element = null;
            if (!_elementMap.TryGetValue(new ElementKey(attribute, instance, role), out var binding))
                return false;

            element = binding.PrimaryElement;

            if (binding.coarseRel > 0)
            {
                int coarseAbs = startAddress + binding.coarseRel - 1;
                int fineAbs = binding.fineRel > 0 ? startAddress + binding.fineRel - 1 : -1;
                value = Read16Abs(universe512, coarseAbs, fineAbs);
                return true;
            }

            if (binding.singleRel > 0)
            {
                value = Read8Abs(universe512, startAddress + binding.singleRel - 1) * 257;
                return true;
            }

            return false;
        }

        private bool TryReadElementRawForRange(int[] universe512, FixtureAttribute attribute, int instance, FixtureChannelRole role, out int value, out int maxValue, out FixtureChannelElement element)
        {
            value = 0;
            maxValue = 255;
            element = null;
            if (!_elementMap.TryGetValue(new ElementKey(attribute, instance, role), out var binding))
                return false;

            element = binding.PrimaryElement;

            if (binding.coarseRel > 0)
            {
                int coarseAbs = startAddress + binding.coarseRel - 1;
                int fineAbs = binding.fineRel > 0 ? startAddress + binding.fineRel - 1 : -1;
                value = Read16Abs(universe512, coarseAbs, fineAbs);
                maxValue = 65535;
                return true;
            }

            if (binding.singleRel > 0)
            {
                value = Read8Abs(universe512, startAddress + binding.singleRel - 1);
                maxValue = 255;
                return true;
            }

            return false;
        }

        private bool TryReadElementRawForRange(byte[] universe512, FixtureAttribute attribute, int instance, FixtureChannelRole role, out int value, out int maxValue, out FixtureChannelElement element)
        {
            value = 0;
            maxValue = 255;
            element = null;
            if (!_elementMap.TryGetValue(new ElementKey(attribute, instance, role), out var binding))
                return false;

            element = binding.PrimaryElement;

            if (binding.coarseRel > 0)
            {
                int coarseAbs = startAddress + binding.coarseRel - 1;
                int fineAbs = binding.fineRel > 0 ? startAddress + binding.fineRel - 1 : -1;
                value = Read16Abs(universe512, coarseAbs, fineAbs);
                maxValue = 65535;
                return true;
            }

            if (binding.singleRel > 0)
            {
                value = Read8Abs(universe512, startAddress + binding.singleRel - 1);
                maxValue = 255;
                return true;
            }

            return false;
        }

        private bool TryMapGoboRotationRangeToSpeed(FixtureChannelElement element, int rawValue, int rawMax, out float speedDegPerSec)
        {
            speedDegPerSec = 0f;
            if (element == null || element.ranges == null)
                return false;

            FixtureChannelRange bestRange = null;
            int bestWidth = int.MaxValue;

            for (int i = 0; i < element.ranges.Count; i++)
            {
                var range = element.ranges[i];
                if (range == null) continue;
                if (!range.Contains(rawValue)) continue;
                if (!IsGoboRotationRangeType(range.type)) continue;

                int width = Mathf.Abs(range.dmxMax - range.dmxMin);
                if (bestRange == null || width < bestWidth)
                {
                    bestRange = range;
                    bestWidth = width;
                }
            }

            if (bestRange == null)
                return false;

            switch (bestRange.type)
            {
                case FixtureRangeType.RotationCW:
                    speedDegPerSec = -MapGoboRotationRangeMagnitude(bestRange, rawValue, rawMax, true);
                    return true;
                case FixtureRangeType.RotationCCW:
                    speedDegPerSec = MapGoboRotationRangeMagnitude(bestRange, rawValue, rawMax, false);
                    return true;
                case FixtureRangeType.NoFunction:
                case FixtureRangeType.Open:
                case FixtureRangeType.Closed:
                case FixtureRangeType.Indexed:
                    speedDegPerSec = 0f;
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsGoboRotationRangeType(FixtureRangeType type)
        {
            return type == FixtureRangeType.RotationCW ||
                   type == FixtureRangeType.RotationCCW ||
                   type == FixtureRangeType.NoFunction ||
                   type == FixtureRangeType.Open ||
                   type == FixtureRangeType.Closed ||
                   type == FixtureRangeType.Indexed;
        }

        private float MapGoboRotationRangeMagnitude(FixtureChannelRange range, int rawValue, int rawMax, bool fastToSlow)
        {
            if (range.HasExplicitMapping(rawMax))
                return Mathf.Clamp01(range.Normalize(rawValue)) * maxGoboRotateDegPerSec;

            int min = Mathf.Min(range.dmxMin, range.dmxMax);
            int max = Mathf.Max(range.dmxMin, range.dmxMax);
            if (max <= min)
                return 0f;

            float t = Mathf.InverseLerp(min, max, rawValue);
            return fastToSlow
                ? Mathf.Lerp(maxGoboRotateDegPerSec, 0f, t)
                : Mathf.Lerp(0f, maxGoboRotateDegPerSec, t);
        }

        private bool TryMapPrismRotationRange(FixtureChannelElement element, int rawValue, int rawMax, out float speedDegPerSec, out bool indexed, out float indexDeg)
        {
            speedDegPerSec = 0f;
            indexed = false;
            indexDeg = 0f;
            if (element == null || element.ranges == null)
                return false;

            FixtureChannelRange bestRange = null;
            int bestWidth = int.MaxValue;

            for (int i = 0; i < element.ranges.Count; i++)
            {
                var range = element.ranges[i];
                if (range == null) continue;
                if (!range.Contains(rawValue)) continue;
                if (!IsPrismRotationRangeType(range.type)) continue;

                int width = Mathf.Abs(range.dmxMax - range.dmxMin);
                if (bestRange == null || width < bestWidth)
                {
                    bestRange = range;
                    bestWidth = width;
                }
            }

            if (bestRange == null)
                return false;

            switch (bestRange.type)
            {
                case FixtureRangeType.RotationCW:
                    speedDegPerSec = -MapPrismRotationRangeMagnitude(bestRange, rawValue, rawMax, true);
                    return true;
                case FixtureRangeType.RotationCCW:
                    speedDegPerSec = MapPrismRotationRangeMagnitude(bestRange, rawValue, rawMax, false);
                    return true;
                case FixtureRangeType.Indexed:
                    indexed = true;
                    indexDeg = MapPrismIndexRangeDeg(bestRange, rawValue, rawMax);
                    return true;
                case FixtureRangeType.NoFunction:
                case FixtureRangeType.Open:
                case FixtureRangeType.Closed:
                    speedDegPerSec = 0f;
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsPrismRotationRangeType(FixtureRangeType type)
        {
            return type == FixtureRangeType.RotationCW ||
                   type == FixtureRangeType.RotationCCW ||
                   type == FixtureRangeType.NoFunction ||
                   type == FixtureRangeType.Open ||
                   type == FixtureRangeType.Closed ||
                   type == FixtureRangeType.Indexed;
        }

        private float MapPrismRotationRangeMagnitude(FixtureChannelRange range, int rawValue, int rawMax, bool fastToSlow)
        {
            if (range.HasExplicitMapping(rawMax))
                return Mathf.Clamp01(range.Normalize(rawValue)) * maxPrismRotateDegPerSec;

            int min = Mathf.Min(range.dmxMin, range.dmxMax);
            int max = Mathf.Max(range.dmxMin, range.dmxMax);
            if (max <= min)
                return 0f;

            float t = Mathf.InverseLerp(min, max, rawValue);
            return fastToSlow
                ? Mathf.Lerp(maxPrismRotateDegPerSec, 0f, t)
                : Mathf.Lerp(0f, maxPrismRotateDegPerSec, t);
        }

        private static float MapPrismIndexRangeDeg(FixtureChannelRange range, int rawValue, int rawMax)
        {
            if (range.HasExplicitMapping(rawMax))
                return Mathf.Clamp01(range.Normalize(rawValue)) * 360f;

            int min = Mathf.Min(range.dmxMin, range.dmxMax);
            int max = Mathf.Max(range.dmxMin, range.dmxMax);
            if (max <= min)
                return 0f;

            return Mathf.InverseLerp(min, max, rawValue) * 360f;
        }

        private bool TryElementValueMatchesRange(int[] universe512, FixtureAttribute attribute, int instance, FixtureChannelRole role, FixtureRangeType type, out int rawValue)
        {
            rawValue = 0;
            if (!_elementMap.TryGetValue(new ElementKey(attribute, instance, role), out var binding))
                return false;

            var element = binding.PrimaryElement;
            if (element == null) return false;
            if (!TryReadElementRaw8(universe512, attribute, instance, role, out rawValue))
                return false;

            return ElementHasRangeType(element, rawValue, type);
        }

        private bool TryElementValueMatchesRange(byte[] universe512, FixtureAttribute attribute, int instance, FixtureChannelRole role, FixtureRangeType type, out int rawValue)
        {
            rawValue = 0;
            if (!_elementMap.TryGetValue(new ElementKey(attribute, instance, role), out var binding))
                return false;

            var element = binding.PrimaryElement;
            if (element == null) return false;
            if (!TryReadElementRaw8(universe512, attribute, instance, role, out rawValue))
                return false;

            return ElementHasRangeType(element, rawValue, type);
        }

        private static bool ElementHasRangeType(FixtureChannelElement element, int rawValue, FixtureRangeType type)
        {
            if (element.ranges == null) return false;

            for (int i = 0; i < element.ranges.Count; i++)
            {
                var range = element.ranges[i];
                if (range == null) continue;
                if (range.type != type) continue;
                if (range.Contains(rawValue)) return true;
            }

            return false;
        }

        private Color ReadElementColor(int[] universe512)
        {
            bool hasC = TryReadElement01(universe512, FixtureAttribute.Cyan, 1, FixtureChannelRole.Value, out float c);
            bool hasM = TryReadElement01(universe512, FixtureAttribute.Magenta, 1, FixtureChannelRole.Value, out float m);
            bool hasY = TryReadElement01(universe512, FixtureAttribute.Yellow, 1, FixtureChannelRole.Value, out float y);

            Color rgb;
            if (hasC || hasM || hasY)
            {
                rgb = new Color(1f - (hasC ? c : 0f), 1f - (hasM ? m : 0f), 1f - (hasY ? y : 0f), 1f);
            }
            else
            {
                bool hasR = TryReadElement01(universe512, FixtureAttribute.Red, 1, FixtureChannelRole.Value, out float r);
                bool hasG = TryReadElement01(universe512, FixtureAttribute.Green, 1, FixtureChannelRole.Value, out float g);
                bool hasB = TryReadElement01(universe512, FixtureAttribute.Blue, 1, FixtureChannelRole.Value, out float b);
                rgb = (hasR || hasG || hasB) ? new Color(hasR ? r : 0f, hasG ? g : 0f, hasB ? b : 0f, 1f) : Color.white;
            }

            if (TryReadElement01(universe512, FixtureAttribute.White, 1, FixtureChannelRole.Value, out float w))
                rgb += Color.white * w;

            if (TryReadElement01(universe512, FixtureAttribute.CTC, 1, FixtureChannelRole.Value, out float ctc01))
                rgb = ApplyCtcApproximation(rgb, ctc01);

            return ClampColor(rgb);
        }

        private Color ReadElementColor(byte[] universe512)
        {
            bool hasC = TryReadElement01(universe512, FixtureAttribute.Cyan, 1, FixtureChannelRole.Value, out float c);
            bool hasM = TryReadElement01(universe512, FixtureAttribute.Magenta, 1, FixtureChannelRole.Value, out float m);
            bool hasY = TryReadElement01(universe512, FixtureAttribute.Yellow, 1, FixtureChannelRole.Value, out float y);

            Color rgb;
            if (hasC || hasM || hasY)
            {
                rgb = new Color(1f - (hasC ? c : 0f), 1f - (hasM ? m : 0f), 1f - (hasY ? y : 0f), 1f);
            }
            else
            {
                bool hasR = TryReadElement01(universe512, FixtureAttribute.Red, 1, FixtureChannelRole.Value, out float r);
                bool hasG = TryReadElement01(universe512, FixtureAttribute.Green, 1, FixtureChannelRole.Value, out float g);
                bool hasB = TryReadElement01(universe512, FixtureAttribute.Blue, 1, FixtureChannelRole.Value, out float b);
                rgb = (hasR || hasG || hasB) ? new Color(hasR ? r : 0f, hasG ? g : 0f, hasB ? b : 0f, 1f) : Color.white;
            }

            if (TryReadElement01(universe512, FixtureAttribute.White, 1, FixtureChannelRole.Value, out float w))
                rgb += Color.white * w;

            if (TryReadElement01(universe512, FixtureAttribute.CTC, 1, FixtureChannelRole.Value, out float ctc01))
                rgb = ApplyCtcApproximation(rgb, ctc01);

            return ClampColor(rgb);
        }

        private static Color ApplyCtcApproximation(Color color, float ctc01)
        {
            Color warmTint = new Color(1f, 0.78f, 0.55f, 1f);
            Color tint = Color.Lerp(Color.white, warmTint, Mathf.Clamp01(ctc01));
            return new Color(color.r * tint.r, color.g * tint.g, color.b * tint.b, 1f);
        }

        private static Color ClampColor(Color color)
        {
            return new Color(Mathf.Clamp01(color.r), Mathf.Clamp01(color.g), Mathf.Clamp01(color.b), 1f);
        }

        private GoboWheelDefinition ResolveGoboWheelDefinition(int instance)
        {
            if (_goboWheelMap.TryGetValue(new WheelKey(FixtureAttribute.GoboWheel, instance), out var wheel) && wheel != null)
                return wheel;

            return goboWheel;
        }

        private void UpdateMonitorValues(int[] universe512)
        {
            if (!monitorEnabled) return;

            // 5ch quick monitor (absolute)
            ch1 = Read8Abs(universe512, monitorCh1);
            ch2 = Read8Abs(universe512, monitorCh2);
            ch3 = Read8Abs(universe512, monitorCh3);
            ch4 = Read8Abs(universe512, monitorCh4);
            ch5 = Read8Abs(universe512, monitorCh5);

            // Function / relative list
            if (monitorItems != null)
            {
                for (int i = 0; i < monitorItems.Count; i++)
                {
                    var it = monitorItems[i];
                    it.absoluteCh = Mathf.Clamp(startAddress + it.relativeCh - 1, 1, 512);
                    it.value = Read8Abs(universe512, it.absoluteCh);
                    monitorItems[i] = it;
                }
            }

            _hasNewDataSinceLastLog = true;
        }

        private void UpdateMonitorValues(byte[] universe512)
        {
            if (!monitorEnabled) return;

            // 5ch quick monitor (absolute)
            ch1 = Read8Abs(universe512, monitorCh1);
            ch2 = Read8Abs(universe512, monitorCh2);
            ch3 = Read8Abs(universe512, monitorCh3);
            ch4 = Read8Abs(universe512, monitorCh4);
            ch5 = Read8Abs(universe512, monitorCh5);

            // Function / relative list
            if (monitorItems != null)
            {
                for (int i = 0; i < monitorItems.Count; i++)
                {
                    var it = monitorItems[i];
                    it.absoluteCh = Mathf.Clamp(startAddress + it.relativeCh - 1, 1, 512);
                    it.value = Read8Abs(universe512, it.absoluteCh);
                    monitorItems[i] = it;
                }
            }

            _hasNewDataSinceLastLog = true;
        }


        private string BuildMonitorSummaryLog()
        {
            var sb = new StringBuilder(256);
            sb.Append("[DmxFixtureComponent Monitor] ");

            if (includeHeaderInLog)
            {
                sb.Append(name);
                sb.Append(" | Uni="); sb.Append(universe);
                sb.Append(", Start="); sb.Append(startAddress);
                sb.Append(", Profile="); sb.Append(_activeProfileLabel);
                sb.Append(", Mode="); sb.Append(_activeModeLabel);
                sb.Append(" | ");
            }

            sb.Append("ABS ");
            sb.Append("ch"); sb.Append(monitorCh1); sb.Append('='); sb.Append(ch1); sb.Append(", ");
            sb.Append("ch"); sb.Append(monitorCh2); sb.Append('='); sb.Append(ch2); sb.Append(", ");
            sb.Append("ch"); sb.Append(monitorCh3); sb.Append('='); sb.Append(ch3); sb.Append(", ");
            sb.Append("ch"); sb.Append(monitorCh4); sb.Append('='); sb.Append(ch4); sb.Append(", ");
            sb.Append("ch"); sb.Append(monitorCh5); sb.Append('='); sb.Append(ch5);

            if (monitorItems != null && monitorItems.Count > 0)
            {
                sb.Append(" | REL/FUNC ");
                for (int i = 0; i < monitorItems.Count; i++)
                {
                    var it = monitorItems[i];

                    // function未指定なら relXX で表示
                    string label = !string.IsNullOrWhiteSpace(it.label)
                        ? it.label
                        : (Convert.ToInt32(it.function) == 0)
                        ? $"rel{it.relativeCh}"
                        : it.function.ToString();

                    sb.Append(label);
                    sb.Append('(');
                    sb.Append(it.relativeCh);
                    sb.Append("->");
                    sb.Append(it.absoluteCh);
                    sb.Append(")=");
                    sb.Append(it.value);
                    if (i != monitorItems.Count - 1) sb.Append(", ");
                }
            }

            return sb.ToString();
        }

        private static string BuildElementLabel(FixtureAttribute attribute, int instance, FixtureChannelRole role, FixtureByteRole byteRole)
        {
            return $"{attribute}{Mathf.Max(1, instance)}.{role}.{byteRole}";
        }

        public void ResetPanTiltToBase()
        {
            CaptureMovementBaseIfNeeded(force: false);
            if (panTransform != null) panTransform.localRotation = _panBaseLocalRot;
            if (tiltTransform != null) tiltTransform.localRotation = _tiltBaseLocalRot;
            _hasPanTarget = false;
            _hasTiltTarget = false;
        }
        public void RecapturePanTiltBase()
        {
            CaptureMovementBaseIfNeeded(force: true);
        }

#if UNITY_EDITOR
        public void CapturePreviewLightBase()
        {
            if (_previewLightCaptured) return;
            _previewLightTargets.Clear();
            _previewLightIntensity.Clear();
            _previewLightColor.Clear();

            var lights = GatherTargetLights();
            for (int i = 0; i < lights.Count; i++)
            {
                var l = lights[i];
                if (l == null) continue;
                _previewLightTargets.Add(l);
                _previewLightIntensity.Add(l.intensity);
                _previewLightColor.Add(l.color);
            }

            _previewLightCaptured = true;
        }

        public void RestorePreviewLightBase()
        {
            if (!_previewLightCaptured) return;

            for (int i = 0; i < _previewLightTargets.Count; i++)
            {
                var l = _previewLightTargets[i];
                if (l == null) continue;
                if (i < _previewLightIntensity.Count) l.intensity = _previewLightIntensity[i];
                if (i < _previewLightColor.Count) l.color = _previewLightColor[i];
            }

            _previewLightTargets.Clear();
            _previewLightIntensity.Clear();
            _previewLightColor.Clear();
            _previewLightCaptured = false;
        }

        [ContextMenu("Restore Pan/Tilt From Prefab")]
        public void RestorePanTiltFromPrefab()
        {
            if (panTransform != null)
            {
                var srcPan = PrefabUtility.GetCorrespondingObjectFromSource(panTransform) as Transform;
                if (srcPan != null) panTransform.localRotation = srcPan.localRotation;
            }
            if (tiltTransform != null)
            {
                var srcTilt = PrefabUtility.GetCorrespondingObjectFromSource(tiltTransform) as Transform;
                if (srcTilt != null) tiltTransform.localRotation = srcTilt.localRotation;
            }
            CaptureMovementBaseIfNeeded(force: true);
        }

        [ContextMenu("Restore Light From Prefab")]
        public void RestoreLightFromPrefab()
        {
            var lights = GatherTargetLights();
            for (int i = 0; i < lights.Count; i++)
            {
                var l = lights[i];
                if (l == null) continue;
                var src = PrefabUtility.GetCorrespondingObjectFromSource(l) as Light;
                if (src == null) continue;
                l.color = src.color;
                l.intensity = src.intensity;
            }
        }
#endif

        private void PerformReset()
        {
            if (panTransform != null) panTransform.localRotation = _panBaseLocalRot;
            if (tiltTransform != null) tiltTransform.localRotation = _tiltBaseLocalRot;

            _hasDmxTargets = true;
            _targetDimmer01 = 0f;
            _targetColor = Color.white;
            _lensTargetDimmer01 = 0f;
            _lightTargetDimmer01 = 0f;
            _currentLensDimmer01 = 0f;
            _currentLightDimmer01 = 0f;
            _lightTargetPending = false;
            _lensTargetPending = false;
            _goboEnabled = false;
            _goboTexture = null;
            _goboRotationDeg = 0f;
            _goboRotationOffsetDeg = 0f;
            _goboRotationSpeedDegPerSec = 0f;
            _goboShakeEnabled = false;
            _goboShakePhaseRad = 0f;
            _goboShakeOffsetDeg = 0f;
            _goboShakeAmplitudeDeg = 0f;
            _goboShakePositionAmplitudeUv = 0f;
            _goboShakeBeamAngleAmplitudeDeg = 0f;
            _goboShakePositionOffsetUv = Vector2.zero;
            _goboShakeBeamAngleOffsetDeg = Vector2.zero;
            _goboShakeSpeedHz = 0f;
            _goboShakeMotionMode = GoboShakeMotionMode.Rotation;
            _goboShakePositionAxis = GoboShakePositionAxis.Horizontal;
            _goboShakeAffectBeam = false;
            _goboShakeApplyWithPrism = false;
            _goboShakeApplyWithZoom = false;
            _goboShakeAllowDuringGoboRotation = true;
            _prismEnabled = false;
            _prismFacetCount = 1;
            _prismSpread = 0f;
            _prismFacetScale = 1f;
            _prismRotationDeg = 0f;
            _prismRotationOffsetDeg = 0f;
            _prismRotationSpeedDegPerSec = 0f;
            _prismIntensityScale = 1f;
            DisablePrismAuxiliaryLights();

            ApplyLightAndLens(0f, 0f, Color.white);
        }

        private void UpdateLightTargetsFromDmx(float dim01, Color rgb)
        {
            _hasDmxTargets = true;
            _targetColor = rgb;

            float prevTarget = _targetDimmer01;
            _targetDimmer01 = Mathf.Clamp01(dim01);

            if (lightResponseMode == LightResponseMode.Led)
            {
                _lensTargetDimmer01 = _targetDimmer01;
                _lightTargetDimmer01 = _targetDimmer01;
                _lightTargetPending = false;
                _lensTargetPending = false;
                _currentLensDimmer01 = _lensTargetDimmer01;
                _currentLightDimmer01 = _lightTargetDimmer01;
                ApplyLightAndLens(_currentLightDimmer01, _currentLensDimmer01, _targetColor);
                return;
            }

            bool isRising = _targetDimmer01 > prevTarget + 0.0001f;
            bool isFalling = _targetDimmer01 < prevTarget - 0.0001f;

            if (isRising)
            {
                _lensTargetDimmer01 = _targetDimmer01;

                if (halogenBeamOnDelay <= 0f)
                {
                    _lightTargetDimmer01 = _targetDimmer01;
                    _lightTargetPending = false;
                }
                else
                {
                    _pendingLightTargetDimmer01 = _targetDimmer01;
                    _lightTargetPendingAt = Time.time + halogenBeamOnDelay;
                    _lightTargetPending = true;
                }

                _lensTargetPending = false;
                return;
            }

            if (isFalling)
            {
                _lightTargetDimmer01 = _targetDimmer01;
                _lightTargetPending = false;

                if (halogenBeamOffDelay <= 0f)
                {
                    _lensTargetDimmer01 = _targetDimmer01;
                    _lensTargetPending = false;
                }
                else
                {
                    _pendingLensTargetDimmer01 = _targetDimmer01;
                    _lensTargetPendingAt = Time.time + halogenBeamOffDelay;
                    _lensTargetPending = true;
                }
                return;
            }

            _lensTargetDimmer01 = _targetDimmer01;
            _lightTargetDimmer01 = _targetDimmer01;
            _lightTargetPending = false;
            _lensTargetPending = false;
        }

        private void UpdateLightResponse()
        {
            if (!_hasDmxTargets) return;

            if (lightResponseMode == LightResponseMode.Led)
            {
                _currentLensDimmer01 = _lensTargetDimmer01;
                _currentLightDimmer01 = _lightTargetDimmer01;
            }
            else
            {
                if (_lightTargetPending && Time.time >= _lightTargetPendingAt)
                {
                    _lightTargetDimmer01 = _pendingLightTargetDimmer01;
                    _lightTargetPending = false;
                }

                if (_lensTargetPending && Time.time >= _lensTargetPendingAt)
                {
                    _lensTargetDimmer01 = _pendingLensTargetDimmer01;
                    _lensTargetPending = false;
                }

                _currentLensDimmer01 = MoveDimmer(_currentLensDimmer01, _lensTargetDimmer01, halogenSourceRiseTime, halogenSourceFallTime);
                _currentLightDimmer01 = MoveDimmer(_currentLightDimmer01, _lightTargetDimmer01, halogenBeamRiseTime, halogenBeamFallTime);
            }

            ApplyLightAndLens(_currentLightDimmer01, _currentLensDimmer01, _targetColor);
        }

        private void ApplyImmediateInEditMode()
        {
#if UNITY_EDITOR
            if (Application.isPlaying) return;
            if (!applyImmediateInEditMode) return;

            if (lightResponseMode == LightResponseMode.Halogen)
            {
                _lensTargetDimmer01 = _targetDimmer01;
                _lightTargetDimmer01 = _targetDimmer01;
                _lightTargetPending = false;
                _lensTargetPending = false;
            }

            _currentLensDimmer01 = _lensTargetDimmer01;
            _currentLightDimmer01 = _lightTargetDimmer01;
            ApplyLightAndLens(_currentLightDimmer01, _currentLensDimmer01, _targetColor);

            if (panTransform != null && _hasPanTarget)
                panTransform.localRotation = _panTargetLocalRot;
            if (tiltTransform != null && _hasTiltTarget)
                tiltTransform.localRotation = _tiltTargetLocalRot;
#endif
        }

        private void ApplyLightAndLens(float lightDim01, float lensDim01, Color rgb)
        {
            var state = BuildRenderState(lightDim01, lensDim01, rgb);
            ApplyPrismAuxiliaryLights(state);

            var driverState = state;
            bool suppressPrimaryLight = ShouldSuppressPrimaryLightForAuxiliaryOnly() ||
                                        (!IsVlbBeamRenderMode() && state.prismEnabled && IsProjectionAndShaderBeamMode());
            if (suppressPrimaryLight)
            {
                driverState.lightDimmer01 = 0f;
                driverState.goboEnabled = false;
                driverState.goboTexture = null;
            }
            else if (ShouldSuppressPrimaryGoboForVlbPrism())
            {
                driverState.goboEnabled = false;
                driverState.goboTexture = null;
            }
            if (!syncLightCookieToGobo)
            {
                driverState.goboEnabled = false;
                driverState.goboTexture = null;
            }
            else if (driverState.goboEnabled && driverState.goboTexture != null && driverState.goboOffsetUv.sqrMagnitude > 0.0000001f)
            {
                Texture offsetCookie = GetOffsetGoboCookie(driverState.goboTexture, driverState.goboOffsetUv);
                if (offsetCookie != null)
                {
                    driverState.goboTexture = offsetCookie;
                    driverState.goboOffsetUv = Vector2.zero;
                }
            }

            if (isInitialized && runtimeDrivers != null && runtimeDrivers.Count > 0)
            {
                for (int i = 0; i < runtimeDrivers.Count; i++)
                    runtimeDrivers[i]?.Apply(driverState);
            }
            else
            {
                ApplyDirectToLights(driverState);
            }

            ApplyPrimaryVolumetricForAlternativeBeamModes(suppressPrimaryLight);
            ApplyVlbBeamDmx(state);
            ApplyLensDmx(state);
            var beamState = state;
            if (suppressPrimaryLight && !IsProjectionAndShaderBeamMode())
            {
                beamState.lightDimmer01 = 0f;
                beamState.lensDimmer01 = 0f;
                beamState.goboEnabled = false;
                beamState.goboTexture = null;
            }
            ApplyPseudoBeamDmx(beamState);
        }

        private bool IsPseudoBeamRenderMode()
        {
            return beamRenderMode == BeamRenderMode.PseudoBeamShader;
        }

        private bool IsVlbBeamRenderMode()
        {
            return beamRenderMode == BeamRenderMode.VolumetricLightBeam;
        }

        private bool UsesAlternativeBeamRenderMode()
        {
            return IsPseudoBeamRenderMode() || IsVlbBeamRenderMode();
        }

        private static float MoveDimmer(float current, float target, float riseTime, float fallTime)
        {
            if (Mathf.Approximately(current, target)) return target;

            float dt = Mathf.Max(0.0001f, Time.deltaTime);
            float time = (target > current) ? riseTime : fallTime;
            if (time <= 0f) return target;

            float step = dt / time;
            return Mathf.MoveTowards(current, target, step);
        }

        private void ApplyDirectToLights(FixtureRenderState state)
        {
            var targets = GatherTargetLights();
            for (int i = 0; i < targets.Count; i++)
            {
                var l = targets[i];
                if (l == null) continue;
                l.color = state.color;
                l.intensity = state.lightDimmer01 * 10f;
                l.cookie = state.goboEnabled ? state.goboTexture : null;
            }
        }

// ------------------------------------------------------------
        // Lens DMX sync helpers
        // ------------------------------------------------------------

                private void EnsureLensPropertyIds()
        {
            if (_lensPropertyIdsReady && _lensMpb != null) return;

            // propertyが空だと PropertyToID で0になるので、最低限ガード
            if (string.IsNullOrWhiteSpace(lensColorProperty)) lensColorProperty = "_DmxColor";
            if (string.IsNullOrWhiteSpace(lensDimmerProperty)) lensDimmerProperty = "_DmxDimmer";
            if (string.IsNullOrWhiteSpace(lensGoboTextureProperty)) lensGoboTextureProperty = "_GoboTexture";
            if (string.IsNullOrWhiteSpace(lensGoboRotationProperty)) lensGoboRotationProperty = "_GoboRotationDeg";
            if (string.IsNullOrWhiteSpace(lensGoboEnabledProperty)) lensGoboEnabledProperty = "_GoboEnabled";

            _lensColorId = Shader.PropertyToID(lensColorProperty);
            _lensDimmerId = Shader.PropertyToID(lensDimmerProperty);
            _lensGoboTextureId = Shader.PropertyToID(lensGoboTextureProperty);
            _lensGoboRotationId = Shader.PropertyToID(lensGoboRotationProperty);
            _lensGoboEnabledId = Shader.PropertyToID(lensGoboEnabledProperty);
            _lensGoboOffsetId = Shader.PropertyToID("_GoboOffset");
            if (_lensMpb == null) _lensMpb = new MaterialPropertyBlock();
            _lensPropertyIdsReady = true;
        }

        private void ApplyLensDmx(FixtureRenderState state)
        {
            if (!syncLensToDmx) return;
            if (!syncLensColorToDmx && !syncLensDimmerToDmx && !syncLensGoboToDmx) return;

            // Renderer未設定なら何もしない
            if (lensRenderer == null && (extraLensRenderers == null || extraLensRenderers.Length == 0))
                return;

            EnsureLensPropertyIds();

            if (_lensMpb == null) return;

            float d = Mathf.Clamp01(state.lensDimmer01) * Mathf.Max(0f, lensDimmerScale);

            // 共通のMPBに値をセットし、各Rendererに適用
            _lensMpb.Clear();
            if (syncLensColorToDmx)
                _lensMpb.SetColor(_lensColorId, state.color);
            if (syncLensDimmerToDmx)
                _lensMpb.SetFloat(_lensDimmerId, d);
            if (syncLensGoboToDmx)
            {
                _lensMpb.SetTexture(_lensGoboTextureId, state.goboEnabled && state.goboTexture != null ? state.goboTexture : Texture2D.whiteTexture);
                _lensMpb.SetFloat(_lensGoboRotationId, state.goboRotationDeg);
                _lensMpb.SetFloat(_lensGoboEnabledId, state.goboEnabled ? 1f : 0f);
                _lensMpb.SetVector(_lensGoboOffsetId, new Vector4(state.goboOffsetUv.x, state.goboOffsetUv.y, 0f, 0f));
            }

            if (lensRenderer != null)
                lensRenderer.SetPropertyBlock(_lensMpb);

            if (extraLensRenderers != null)
            {
                for (int i = 0; i < extraLensRenderers.Length; i++)
                {
                    var r = extraLensRenderers[i];
                    if (r != null) r.SetPropertyBlock(_lensMpb);
                }
            }
        }

        private void EnsureBeamPropertyIds()
        {
            if (_beamPropertyIdsReady && _beamMpb != null) return;

            if (string.IsNullOrWhiteSpace(beamColorProperty)) beamColorProperty = "_DmxColor";
            if (string.IsNullOrWhiteSpace(beamDimmerProperty)) beamDimmerProperty = "_DmxDimmer";
            if (string.IsNullOrWhiteSpace(beamGoboTextureProperty)) beamGoboTextureProperty = "_GoboTexture";
            if (string.IsNullOrWhiteSpace(beamGoboRotationProperty)) beamGoboRotationProperty = "_GoboRotationDeg";
            if (string.IsNullOrWhiteSpace(beamGoboEnabledProperty)) beamGoboEnabledProperty = "_GoboEnabled";
            if (string.IsNullOrWhiteSpace(beamPrismEnabledProperty)) beamPrismEnabledProperty = "_DmxPrismEnabled";
            if (string.IsNullOrWhiteSpace(beamPrismFacetCountProperty)) beamPrismFacetCountProperty = "_DmxPrismFacetCount";
            if (string.IsNullOrWhiteSpace(beamPrismSpreadProperty)) beamPrismSpreadProperty = "_DmxPrismSpread";
            if (string.IsNullOrWhiteSpace(beamPrismRotationProperty)) beamPrismRotationProperty = "_DmxPrismRotation";
            if (string.IsNullOrWhiteSpace(beamPrismIntensityProperty)) beamPrismIntensityProperty = "_DmxPrismIntensity";
            if (string.IsNullOrWhiteSpace(beamLengthProperty)) beamLengthProperty = "_BeamLength";
            if (string.IsNullOrWhiteSpace(beamStartRadiusProperty)) beamStartRadiusProperty = "_BeamStartRadius";
            if (string.IsNullOrWhiteSpace(beamEndRadiusProperty)) beamEndRadiusProperty = "_BeamEndRadius";
            if (string.IsNullOrWhiteSpace(beamNoiseVolumeProperty)) beamNoiseVolumeProperty = "_BeamNoiseVolume";
            if (string.IsNullOrWhiteSpace(beamNoiseVolumeEnabledProperty)) beamNoiseVolumeEnabledProperty = "_BeamNoiseVolumeEnabled";
            if (string.IsNullOrWhiteSpace(beamNoiseVolumeStrengthProperty)) beamNoiseVolumeStrengthProperty = "_BeamNoiseVolumeStrength";
            if (string.IsNullOrWhiteSpace(beamNoiseVolumeScaleProperty)) beamNoiseVolumeScaleProperty = "_BeamNoiseVolumeScale";
            if (string.IsNullOrWhiteSpace(beamNoiseVolumeRadialScaleProperty)) beamNoiseVolumeRadialScaleProperty = "_BeamNoiseVolumeRadialScale";
            if (string.IsNullOrWhiteSpace(beamNoiseVolumeLengthScaleProperty)) beamNoiseVolumeLengthScaleProperty = "_BeamNoiseVolumeLengthScale";
            if (string.IsNullOrWhiteSpace(beamNoiseVolumeSpeedProperty)) beamNoiseVolumeSpeedProperty = "_BeamNoiseVolumeSpeed";
            if (string.IsNullOrWhiteSpace(beamNoiseVolumeContrastProperty)) beamNoiseVolumeContrastProperty = "_BeamNoiseVolumeContrast";
            if (string.IsNullOrWhiteSpace(beamNoiseVolumeOffsetProperty)) beamNoiseVolumeOffsetProperty = "_BeamNoiseVolumeOffset";
            if (string.IsNullOrWhiteSpace(beamNoiseVolumeScrollDirectionProperty)) beamNoiseVolumeScrollDirectionProperty = "_BeamNoiseVolumeScrollDirection";

            _beamColorId = Shader.PropertyToID(beamColorProperty);
            _beamDimmerId = Shader.PropertyToID(beamDimmerProperty);
            _beamGoboTextureId = Shader.PropertyToID(beamGoboTextureProperty);
            _beamGoboRotationId = Shader.PropertyToID(beamGoboRotationProperty);
            _beamGoboEnabledId = Shader.PropertyToID(beamGoboEnabledProperty);
            _beamGoboOffsetId = Shader.PropertyToID("_GoboOffset");
            _beamPrismEnabledId = Shader.PropertyToID(beamPrismEnabledProperty);
            _beamPrismFacetCountId = Shader.PropertyToID(beamPrismFacetCountProperty);
            _beamPrismSpreadId = Shader.PropertyToID(beamPrismSpreadProperty);
            _beamPrismRotationId = Shader.PropertyToID(beamPrismRotationProperty);
            _beamPrismIntensityId = Shader.PropertyToID(beamPrismIntensityProperty);
            _beamLengthId = Shader.PropertyToID(beamLengthProperty);
            _beamStartRadiusId = Shader.PropertyToID(beamStartRadiusProperty);
            _beamEndRadiusId = Shader.PropertyToID(beamEndRadiusProperty);
            _beamIntensityId = Shader.PropertyToID("_BeamIntensity");
            _beamEdgeSoftnessId = Shader.PropertyToID("_BeamEdgeSoftness");
            _beamNoiseStrengthId = Shader.PropertyToID("_BeamNoiseStrength");
            _beamNoiseVolumeId = Shader.PropertyToID(beamNoiseVolumeProperty);
            _beamNoiseVolumeEnabledId = Shader.PropertyToID(beamNoiseVolumeEnabledProperty);
            _beamNoiseVolumeStrengthId = Shader.PropertyToID(beamNoiseVolumeStrengthProperty);
            _beamNoiseVolumeScaleId = Shader.PropertyToID(beamNoiseVolumeScaleProperty);
            _beamNoiseVolumeRadialScaleId = Shader.PropertyToID(beamNoiseVolumeRadialScaleProperty);
            _beamNoiseVolumeLengthScaleId = Shader.PropertyToID(beamNoiseVolumeLengthScaleProperty);
            _beamNoiseVolumeSpeedId = Shader.PropertyToID(beamNoiseVolumeSpeedProperty);
            _beamNoiseVolumeContrastId = Shader.PropertyToID(beamNoiseVolumeContrastProperty);
            _beamNoiseVolumeOffsetId = Shader.PropertyToID(beamNoiseVolumeOffsetProperty);
            _beamNoiseVolumeScrollDirectionId = Shader.PropertyToID(beamNoiseVolumeScrollDirectionProperty);
            if (_beamMpb == null) _beamMpb = new MaterialPropertyBlock();
            _beamPropertyIdsReady = true;
        }

        private void ApplyPseudoBeamDmx(FixtureRenderState state)
        {
            bool pseudoBeamMode = IsPseudoBeamRenderMode();
            bool forcePseudoBeamForPrism = pseudoBeamMode && IsProjectionAndShaderBeamMode() && state.prismEnabled;
            if (!pseudoBeamMode && !forcePseudoBeamForPrism)
            {
                DisablePrismPseudoBeamFacets();
                DisableSinglePseudoBeamSoftShell();
                SetTemplateBeamRenderersEnabled(false);
                return;
            }

            if (!syncBeamColorToDmx && !syncBeamDimmerToDmx && !syncBeamGoboToDmx && !syncBeamPrismToDmx && !syncBeamNoiseVolumeToBeam)
            {
                DisablePrismPseudoBeamFacets();
                DisableSinglePseudoBeamSoftShell();
                SetTemplateBeamRenderersEnabled(true);
                return;
            }

            if (beamRenderer == null && (extraBeamRenderers == null || extraBeamRenderers.Length == 0))
            {
                DisableSinglePseudoBeamSoftShell();
                return;
            }

            EnsureBeamPropertyIds();
            if (_beamMpb == null) return;

            if (IsProjectionAndShaderBeamMode() && state.prismEnabled && beamRenderer is MeshRenderer)
            {
                DisableSinglePseudoBeamSoftShell();
                ApplyPrismPseudoBeamFacets(state);
                return;
            }

            DisablePrismPseudoBeamFacets();
            SetTemplateBeamRenderersEnabled(true);
            ApplyPseudoBeamRenderer(beamRenderer, state, 1f, false);
            ApplySinglePseudoBeamSoftShell(state);

            if (extraBeamRenderers != null)
            {
                for (int i = 0; i < extraBeamRenderers.Length; i++)
                    ApplyPseudoBeamRenderer(extraBeamRenderers[i], state, 1f, false);
            }
        }

        private void ApplyVlbBeamDmx(FixtureRenderState state)
        {
            if (!IsVlbBeamRenderMode() || state.prismEnabled)
            {
                DisableVlbBeam();
                return;
            }

            var targets = GatherTargetLights();
            if (_vlbBeamAdapter == null)
                _vlbBeamAdapter = new VlbBeamAdapter();

            _vlbBeamAdapter.Apply(targets, state, syncLightCookieToGobo, BuildVlbOverrides(), this);
        }

        private void DisableVlbBeam()
        {
            var targets = GatherTargetLights();
            if (_vlbBeamAdapter == null)
                _vlbBeamAdapter = new VlbBeamAdapter();

            _vlbBeamAdapter.Disable(targets);
        }

        private VlbBeamAdapter.Overrides BuildVlbOverrides()
        {
            return new VlbBeamAdapter.Overrides
            {
                overrideHdIntensityMultiplier = overrideVlbHdIntensityMultiplier,
                hdIntensityMultiplier = vlbHdIntensityMultiplier,
                overrideSdIntensityMultiplier = overrideVlbSdIntensityMultiplier,
                sdIntensityMultiplier = vlbSdIntensityMultiplier,
                overrideHdrpExposureWeight = overrideVlbHdrpExposureWeight,
                hdrpExposureWeight = vlbHdrpExposureWeight
            };
        }

        private void ApplyPseudoBeamRenderer(Renderer renderer, FixtureRenderState state, float dimmerMultiplier, bool forceSingleBeamMask, bool softShell = false)
        {
            if (renderer == null)
                return;

            float d = Mathf.Clamp01(state.lightDimmer01) * Mathf.Max(0f, beamDimmerScale);
            d *= Mathf.Max(0f, dimmerMultiplier);
            d = Mathf.Max(Mathf.Clamp01(beamDimmerFloor), d);

            _beamMpb.Clear();
            if (syncBeamColorToDmx)
                _beamMpb.SetColor(_beamColorId, state.color);
            if (syncBeamDimmerToDmx)
                _beamMpb.SetFloat(_beamDimmerId, d);
            if (syncBeamGoboToDmx)
            {
                _beamMpb.SetTexture(_beamGoboTextureId, state.goboEnabled && state.goboTexture != null ? state.goboTexture : Texture2D.whiteTexture);
                _beamMpb.SetFloat(_beamGoboRotationId, state.goboRotationDeg);
                _beamMpb.SetFloat(_beamGoboEnabledId, state.goboEnabled ? 1f : 0f);
                _beamMpb.SetVector(_beamGoboOffsetId, new Vector4(state.goboOffsetUv.x, state.goboOffsetUv.y, 0f, 0f));
            }
            if (syncBeamPrismToDmx)
            {
                _beamMpb.SetFloat(_beamPrismEnabledId, !forceSingleBeamMask && state.prismEnabled ? 1f : 0f);
                _beamMpb.SetFloat(_beamPrismFacetCountId, Mathf.Max(1, state.prismFacetCount));
                _beamMpb.SetFloat(_beamPrismSpreadId, Mathf.Max(0f, state.prismSpread));
                _beamMpb.SetFloat(_beamPrismRotationId, state.prismRotationDeg);
                _beamMpb.SetFloat(_beamPrismIntensityId, Mathf.Max(0f, state.prismIntensityScale));
            }
            if (!softShell && renderer is MeshRenderer meshRenderer)
                UpdatePseudoBeamTemplateShape(meshRenderer, state);

            ApplyPseudoBeamShapeProperties(renderer);
            ApplyPseudoBeamNoiseVolumeProperties(renderer, state);
            if (softShell)
            {
                _beamMpb.SetFloat(_beamIntensityId, Mathf.Max(0f, beamSoftShellIntensity));
                _beamMpb.SetFloat(_beamEdgeSoftnessId, Mathf.Clamp01(beamSoftShellEdgeSoftness));
                _beamMpb.SetFloat(_beamNoiseStrengthId, Mathf.Clamp01(beamSoftShellNoiseStrength));
            }

            renderer.SetPropertyBlock(_beamMpb);
        }

        private void ApplyPseudoBeamNoiseVolumeProperties(Renderer renderer, FixtureRenderState state)
        {
            if (!syncBeamNoiseVolumeToBeam)
                return;

            bool enabled = beamNoiseVolumeEnabled && beamNoiseVolume != null;
            if (beamNoiseVolume != null)
                _beamMpb.SetTexture(_beamNoiseVolumeId, beamNoiseVolume);
            _beamMpb.SetFloat(_beamNoiseVolumeEnabledId, enabled ? 1f : 0f);
            float effectiveBeamNoiseVolumeStrength = beamNoiseVolumeStrength;
            bool hasActiveBeamGobo = syncBeamGoboToDmx && state.goboEnabled && state.goboTexture != null;
            bool prismOrbitingWithoutGobo = state.prismEnabled && !hasActiveBeamGobo && Mathf.Abs(state.prismRotationSpeedDegPerSec) > 0.001f;
            if (hasActiveBeamGobo)
                effectiveBeamNoiseVolumeStrength *= beamNoiseVolumeGoboScale;
            else if (prismOrbitingWithoutGobo)
                effectiveBeamNoiseVolumeStrength *= beamNoiseVolumePrismRotationScale;
            _beamMpb.SetFloat(_beamNoiseVolumeStrengthId, Mathf.Clamp01(effectiveBeamNoiseVolumeStrength));
            _beamMpb.SetFloat(_beamNoiseVolumeScaleId, Mathf.Max(0.001f, beamNoiseVolumeScale));
            _beamMpb.SetFloat(_beamNoiseVolumeRadialScaleId, Mathf.Max(0.001f, beamNoiseVolumeRadialScale));
            _beamMpb.SetFloat(_beamNoiseVolumeLengthScaleId, Mathf.Max(0.001f, beamNoiseVolumeLengthScale));
            _beamMpb.SetFloat(_beamNoiseVolumeSpeedId, beamNoiseVolumeSpeed);
            _beamMpb.SetFloat(_beamNoiseVolumeContrastId, Mathf.Max(0.01f, beamNoiseVolumeContrast));
            _beamMpb.SetVector(_beamNoiseVolumeOffsetId, new Vector4(beamNoiseVolumeOffset.x, beamNoiseVolumeOffset.y, beamNoiseVolumeOffset.z, 0f));
            Vector3 scrollDirection = ResolveBeamNoiseVolumeScrollDirection(renderer);
            _beamMpb.SetVector(_beamNoiseVolumeScrollDirectionId, new Vector4(scrollDirection.x, scrollDirection.y, scrollDirection.z, 0f));
        }

        private Vector3 ResolveBeamNoiseVolumeScrollDirection(Renderer renderer)
        {
            Vector3 direction = beamNoiseVolumeScrollAxis switch
            {
                BeamNoiseVolumeScrollAxis.X => Vector3.right,
                BeamNoiseVolumeScrollAxis.Y => Vector3.up,
                _ => Vector3.forward
            };

            if (beamNoiseVolumeScrollReverse)
                direction = -direction;

            if (beamNoiseVolumeScrollSpace == BeamNoiseVolumeScrollSpace.World && renderer != null && renderer.transform != null)
                direction = renderer.transform.InverseTransformDirection(direction);

            if (direction.sqrMagnitude <= 0.000001f)
                direction = Vector3.forward;

            return direction.normalized;
        }

        private void ApplyPseudoBeamShapeProperties(Renderer renderer)
        {
            var cone = renderer != null ? renderer.GetComponent<PrismPseudoBeamCone>() : null;
            if (cone == null && beamRenderer != null)
                cone = beamRenderer.GetComponent<PrismPseudoBeamCone>();

            float length = cone != null ? cone.Length : 1f;
            float startRadius = cone != null ? cone.StartRadius : 0.01f;
            float endRadius = cone != null ? cone.EndRadius : 1f;
            _beamMpb.SetFloat(_beamLengthId, Mathf.Max(0.01f, length));
            _beamMpb.SetFloat(_beamStartRadiusId, Mathf.Max(0f, startRadius));
            _beamMpb.SetFloat(_beamEndRadiusId, Mathf.Max(0.001f, endRadius));
        }

        private void ApplyPrismPseudoBeamFacets(FixtureRenderState state)
        {
            var templateRenderer = beamRenderer as MeshRenderer;
            if (templateRenderer == null)
                return;

            var templateFilter = templateRenderer.GetComponent<MeshFilter>();
            if (templateFilter == null || templateFilter.sharedMesh == null)
                return;

            int count = Mathf.Clamp(state.prismFacetCount, 1, Mathf.Max(1, maxPrismAuxiliaryFacets));
            EnsurePrismPseudoBeamFacets(templateRenderer, count);
            SetTemplateBeamRenderersEnabled(false);

            Mesh sharedMesh = UpdatePseudoBeamTemplateShape(templateRenderer, state);
            if (sharedMesh == null)
                sharedMesh = templateFilter.sharedMesh;

            float prismRotationDeg = state.prismRotationDeg;
            float spreadDeg = Mathf.Max(0f, state.prismSpread * auxiliarySpreadMultiplierDeg * GetPrismGoboSpacingScale(state));
            Transform templateTransform = templateRenderer.transform;
            Transform templateParent = templateTransform.parent;

            if (_prismPseudoBeamRoot != null && _prismPseudoBeamRoot.parent != templateParent)
                _prismPseudoBeamRoot.SetParent(templateParent, false);

            if (_prismPseudoBeamRoot != null)
            {
                _prismPseudoBeamRoot.localPosition = Vector3.zero;
                _prismPseudoBeamRoot.localRotation = Quaternion.identity;
                _prismPseudoBeamRoot.localScale = Vector3.one;
            }

            for (int i = 0; i < _prismPseudoBeamFacets.Count; i++)
            {
                var facet = _prismPseudoBeamFacets[i];
                bool active = i < count;
                if (facet?.directionPivot != null)
                    facet.directionPivot.gameObject.SetActive(active);

                if (!active || facet == null || facet.renderer == null || facet.filter == null)
                    continue;

                float angle = ((360f * i) / count) + prismRotationDeg;
                float angleRad = angle * Mathf.Deg2Rad;
                float xDeg = Mathf.Sin(angleRad) * spreadDeg;
                float yDeg = Mathf.Cos(angleRad) * spreadDeg;

                facet.directionPivot.localPosition = templateTransform.localPosition;
                facet.directionPivot.localRotation = templateTransform.localRotation * Quaternion.Euler(xDeg + state.beamShakeAngleDeg.x, yDeg + state.beamShakeAngleDeg.y, 0f);
                facet.directionPivot.localScale = Vector3.one;

                Transform facetTransform = facet.renderer.transform;
                facetTransform.localPosition = Vector3.zero;
                facetTransform.localRotation = Quaternion.identity;
                facetTransform.localScale = templateTransform.localScale;

                facet.filter.sharedMesh = sharedMesh;
                facet.renderer.sharedMaterials = templateRenderer.sharedMaterials;
                facet.renderer.enabled = true;
                facet.renderer.gameObject.layer = templateRenderer.gameObject.layer;
                facet.renderer.renderingLayerMask = templateRenderer.renderingLayerMask;
                facet.renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                facet.renderer.receiveShadows = false;

                float facetDimmerMultiplier = GetPrismBrightnessDistributionScale(count);
                ApplyPseudoBeamRenderer(facet.renderer, state, facetDimmerMultiplier, true);
                ApplyPrismPseudoBeamFacetSoftShell(facet, templateRenderer, state, facetDimmerMultiplier);
            }
        }

        private Mesh UpdatePseudoBeamTemplateShape(MeshRenderer templateRenderer, FixtureRenderState state)
        {
            var templateFilter = templateRenderer.GetComponent<MeshFilter>();
            var cone = templateRenderer.GetComponent<PrismPseudoBeamCone>();
            if (cone == null || templateFilter == null)
                return templateFilter != null ? templateFilter.sharedMesh : null;

            if (!syncBeamZoomToDmx)
                return templateFilter.sharedMesh;

            float length = Mathf.Max(0.01f, cone.Length);
            float startRadius = Mathf.Max(0f, cone.StartRadius);
            float maxRadius = Mathf.Max(beamZoomMinEndRadius, beamZoomMaxEndRadius);
            float outer = GetPseudoBeamOuterSpotAngle(state);
            float endRadius = Mathf.Tan(outer * 0.5f * Mathf.Deg2Rad) * length * Mathf.Max(0f, beamZoomRadiusScale);
            endRadius = Mathf.Clamp(endRadius, Mathf.Max(0.001f, beamZoomMinEndRadius), maxRadius);
            cone.SetRuntimeShape(length, startRadius, endRadius);
            return templateFilter.sharedMesh;
        }

        private float GetPseudoBeamOuterSpotAngle(FixtureRenderState state)
        {
            if (state.zoomEnabled)
                return Mathf.Clamp(state.outerSpotAngleDeg, 0.1f, 179f);

            if (targetLight != null)
                return Mathf.Clamp(targetLight.spotAngle, 0.1f, 179f);

            return Mathf.Clamp(maxOuterSpotAngle, 0.1f, 179f);
        }

        private void ApplySinglePseudoBeamSoftShell(FixtureRenderState state)
        {
            var templateRenderer = beamRenderer as MeshRenderer;
            if (!enableBeamSoftShell || templateRenderer == null)
            {
                DisableSinglePseudoBeamSoftShell();
                return;
            }

            EnsureSinglePseudoBeamSoftShell(templateRenderer);
            if (_singlePseudoBeamSoftShell == null || _singlePseudoBeamSoftRenderer == null || _singlePseudoBeamSoftCone == null)
                return;

            CopySoftShellTransformAndShape(_singlePseudoBeamSoftShell, _singlePseudoBeamSoftRenderer, _singlePseudoBeamSoftCone, templateRenderer);
            _singlePseudoBeamSoftShell.gameObject.SetActive(true);
            _singlePseudoBeamSoftRenderer.enabled = true;
            ApplyPseudoBeamRenderer(_singlePseudoBeamSoftRenderer, state, 1f, false, true);
        }

        private void ApplyPrismPseudoBeamFacetSoftShell(PrismPseudoBeamFacet facet, MeshRenderer templateRenderer, FixtureRenderState state, float dimmerMultiplier)
        {
            if (facet == null || facet.softRenderer == null || facet.softCone == null)
                return;

            if (!enableBeamSoftShell)
            {
                facet.softRenderer.gameObject.SetActive(false);
                return;
            }

            CopySoftShellTransformAndShape(facet.softRenderer.transform, facet.softRenderer, facet.softCone, templateRenderer);
            facet.softRenderer.transform.localPosition = Vector3.zero;
            facet.softRenderer.transform.localRotation = Quaternion.identity;
            facet.softRenderer.transform.localScale = templateRenderer.transform.localScale;
            facet.softRenderer.gameObject.SetActive(true);
            facet.softRenderer.enabled = true;
            ApplyPseudoBeamRenderer(facet.softRenderer, state, dimmerMultiplier, true, true);
        }

        private void CopySoftShellTransformAndShape(Transform softTransform, MeshRenderer softRenderer, PrismPseudoBeamCone softCone, MeshRenderer templateRenderer)
        {
            if (softTransform == null || softRenderer == null || softCone == null || templateRenderer == null)
                return;

            Transform templateTransform = templateRenderer.transform;
            softTransform.localPosition = templateTransform.localPosition;
            softTransform.localRotation = templateTransform.localRotation;
            softTransform.localScale = templateTransform.localScale;
            softRenderer.sharedMaterials = templateRenderer.sharedMaterials;
            softRenderer.gameObject.layer = templateRenderer.gameObject.layer;
            softRenderer.renderingLayerMask = templateRenderer.renderingLayerMask;
            softRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            softRenderer.receiveShadows = false;

            var templateCone = templateRenderer.GetComponent<PrismPseudoBeamCone>();
            if (templateCone == null)
                return;

            float length = Mathf.Max(0.01f, templateCone.Length);
            float startRadius = Mathf.Max(0f, templateCone.StartRadius);
            float endRadius = Mathf.Max(0.001f, templateCone.EndRadius) * Mathf.Max(1f, beamSoftShellRadiusScale);
            softCone.SetRuntimeShape(length, startRadius, endRadius);
        }

        private void EnsureSinglePseudoBeamSoftShell(MeshRenderer templateRenderer)
        {
            if (_singlePseudoBeamSoftShell != null)
                return;

            var softGo = new GameObject("PrismPseudoBeamSoftShell");
            softGo.hideFlags = HideFlags.DontSave;
            Transform parent = templateRenderer != null ? templateRenderer.transform.parent : transform;
            softGo.transform.SetParent(parent, false);
            _singlePseudoBeamSoftShell = softGo.transform;
            _singlePseudoBeamSoftCone = softGo.AddComponent<PrismPseudoBeamCone>();
            _singlePseudoBeamSoftRenderer = softGo.GetComponent<MeshRenderer>();
            if (_singlePseudoBeamSoftRenderer != null)
            {
                _singlePseudoBeamSoftRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _singlePseudoBeamSoftRenderer.receiveShadows = false;
            }
        }

        private void DisableSinglePseudoBeamSoftShell()
        {
            if (_singlePseudoBeamSoftShell != null)
                _singlePseudoBeamSoftShell.gameObject.SetActive(false);
        }

        private void ReleaseSinglePseudoBeamSoftShell()
        {
            if (_singlePseudoBeamSoftShell == null)
                return;

            if (Application.isPlaying)
                Destroy(_singlePseudoBeamSoftShell.gameObject);
            else
                DestroyImmediate(_singlePseudoBeamSoftShell.gameObject);

            _singlePseudoBeamSoftShell = null;
            _singlePseudoBeamSoftCone = null;
            _singlePseudoBeamSoftRenderer = null;
        }

        private void EnsurePrismPseudoBeamFacets(MeshRenderer templateRenderer, int count)
        {
            count = Mathf.Clamp(count, 1, Mathf.Max(1, maxPrismAuxiliaryFacets));

            Transform parent = templateRenderer != null && templateRenderer.transform != null
                ? templateRenderer.transform.parent
                : transform;

            if (_prismPseudoBeamRoot == null)
            {
                var root = new GameObject("PrismPseudoBeamFacetRoot");
                root.hideFlags = HideFlags.DontSave;
                _prismPseudoBeamRoot = root.transform;
                _prismPseudoBeamRoot.SetParent(parent, false);
            }

            while (_prismPseudoBeamFacets.Count < count)
                _prismPseudoBeamFacets.Add(CreatePrismPseudoBeamFacet(templateRenderer, _prismPseudoBeamFacets.Count));
        }

        private PrismPseudoBeamFacet CreatePrismPseudoBeamFacet(MeshRenderer templateRenderer, int index)
        {
            var directionGo = new GameObject($"PseudoBeamFacetDirectionPivot_{index}");
            directionGo.hideFlags = HideFlags.DontSave;
            directionGo.transform.SetParent(_prismPseudoBeamRoot, false);

            var beamGo = new GameObject($"PseudoBeamFacet_{index}");
            beamGo.hideFlags = HideFlags.DontSave;
            beamGo.transform.SetParent(directionGo.transform, false);

            var filter = beamGo.AddComponent<MeshFilter>();
            var renderer = beamGo.AddComponent<MeshRenderer>();

            if (templateRenderer != null)
            {
                var templateFilter = templateRenderer.GetComponent<MeshFilter>();
                if (templateFilter != null)
                    filter.sharedMesh = templateFilter.sharedMesh;
                renderer.sharedMaterials = templateRenderer.sharedMaterials;
                renderer.lightProbeUsage = templateRenderer.lightProbeUsage;
                renderer.reflectionProbeUsage = templateRenderer.reflectionProbeUsage;
                renderer.probeAnchor = templateRenderer.probeAnchor;
                renderer.renderingLayerMask = templateRenderer.renderingLayerMask;
                beamGo.layer = templateRenderer.gameObject.layer;
            }

            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var softGo = new GameObject($"PseudoBeamFacetSoft_{index}");
            softGo.hideFlags = HideFlags.DontSave;
            softGo.transform.SetParent(directionGo.transform, false);
            var softCone = softGo.AddComponent<PrismPseudoBeamCone>();
            var softRenderer = softGo.GetComponent<MeshRenderer>();
            if (softRenderer != null && templateRenderer != null)
            {
                softRenderer.sharedMaterials = templateRenderer.sharedMaterials;
                softRenderer.lightProbeUsage = templateRenderer.lightProbeUsage;
                softRenderer.reflectionProbeUsage = templateRenderer.reflectionProbeUsage;
                softRenderer.probeAnchor = templateRenderer.probeAnchor;
                softRenderer.renderingLayerMask = templateRenderer.renderingLayerMask;
                softGo.layer = templateRenderer.gameObject.layer;
                softRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                softRenderer.receiveShadows = false;
                softGo.SetActive(false);
            }

            return new PrismPseudoBeamFacet
            {
                directionPivot = directionGo.transform,
                filter = filter,
                renderer = renderer,
                softCone = softCone,
                softRenderer = softRenderer
            };
        }

        private void DisablePrismPseudoBeamFacets()
        {
            for (int i = 0; i < _prismPseudoBeamFacets.Count; i++)
            {
                var facet = _prismPseudoBeamFacets[i];
                if (facet?.directionPivot != null)
                    facet.directionPivot.gameObject.SetActive(false);
            }
        }

        private void ReleasePrismPseudoBeamFacets()
        {
            for (int i = 0; i < _prismPseudoBeamFacets.Count; i++)
            {
                var facet = _prismPseudoBeamFacets[i];
                if (facet?.directionPivot == null)
                    continue;

                if (Application.isPlaying)
                    Destroy(facet.directionPivot.gameObject);
                else
                    DestroyImmediate(facet.directionPivot.gameObject);
            }

            _prismPseudoBeamFacets.Clear();
            ReleaseSinglePseudoBeamSoftShell();

            if (_prismPseudoBeamRoot != null)
            {
                if (Application.isPlaying)
                    Destroy(_prismPseudoBeamRoot.gameObject);
                else
                    DestroyImmediate(_prismPseudoBeamRoot.gameObject);
                _prismPseudoBeamRoot = null;
            }
        }

        private void SetTemplateBeamRenderersEnabled(bool enabled)
        {
            if (beamRenderer != null)
                beamRenderer.enabled = enabled;

            if (extraBeamRenderers != null)
            {
                for (int i = 0; i < extraBeamRenderers.Length; i++)
                {
                    var r = extraBeamRenderers[i];
                    if (r != null)
                        r.enabled = enabled;
                }
            }
        }



        // ------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------

        private static int Read8Abs(int[] universe512, int absCh1Based)
        {
            int idx = absCh1Based - 1;
            if (idx < 0 || idx >= universe512.Length) return 0;
            return DmxValueUtils.ClampByte(universe512[idx]);
        }

        private static int Read8Abs(byte[] universe512, int absCh1Based)
        {
            int idx = absCh1Based - 1;
            if (idx < 0 || idx >= universe512.Length) return 0;
            return universe512[idx];
        }


        private static int Read16Abs(int[] universe512, int absCoarseCh1Based, int absFineCh1BasedOrMinus1)
        {
            int coarse = Read8Abs(universe512, absCoarseCh1Based);
            if (absFineCh1BasedOrMinus1 <= 0) return coarse << 8;
            int fine = Read8Abs(universe512, absFineCh1BasedOrMinus1);
            return (coarse << 8) | fine;
        }

        private static int Read16Abs(byte[] universe512, int absCoarseCh1Based, int absFineCh1BasedOrMinus1)
        {
            int coarse = Read8Abs(universe512, absCoarseCh1Based);
            if (absFineCh1BasedOrMinus1 <= 0) return coarse << 8;
            int fine = Read8Abs(universe512, absFineCh1BasedOrMinus1);
            return (coarse << 8) | fine;
        }

        private FixtureRenderState BuildRenderState(float lightDim01, float lensDim01, Color rgb)
        {
            float goboRotationDeg = GetDisplayGoboRotationDeg();
            Vector2 goboOffsetUv = GetDisplayGoboOffsetUv();
            bool goboEnabled = _goboEnabled && _goboTexture != null;
            Texture goboTextureForRender = _goboTexture;

            if (ShouldUsePrismCookieComposite())
            {
                Texture source = goboEnabled ? _goboTexture : Texture2D.whiteTexture;
                Texture composite = GetPrismCompositeCookie(source, goboRotationDeg, GetDisplayPrismRotationDeg(), goboOffsetUv);
                if (composite != null)
                {
                    goboEnabled = true;
                    goboTextureForRender = composite;
                    goboRotationDeg = 0f;
                    goboOffsetUv = Vector2.zero;
                }
            }
            return new FixtureRenderState
            {
                lightDimmer01 = lightDim01,
                lensDimmer01 = lensDim01,
                color = rgb,
                goboEnabled = goboEnabled && goboTextureForRender != null,
                goboTexture = goboTextureForRender,
                goboRotationDeg = goboRotationDeg,
                goboOffsetUv = goboOffsetUv,
                beamShakeAngleDeg = GetDisplayBeamShakeAngleDeg(),
                zoomEnabled = _zoomEnabled,
                outerSpotAngleDeg = _zoomOuterSpotAngleDeg,
                innerSpotPercent = _zoomInnerSpotPercent,
                prismEnabled = IsPrismDrawingEnabled(),
                prismFacetCount = _prismFacetCount,
                prismSpread = _prismSpread,
                prismRotationDeg = GetDisplayPrismRotationDeg(),
                prismRotationSpeedDegPerSec = _prismRotationSpeedDegPerSec,
                prismIntensityScale = _prismIntensityScale,
                vlbPrismGoboScale = prismGoboScaleVlb
            };
        }

        private float GetDisplayGoboRotationDeg()
        {
            return Mathf.Repeat(_goboRotationDeg + _goboRotationOffsetDeg + _goboShakeOffsetDeg, 360f);
        }

        private Vector2 GetDisplayGoboOffsetUv()
        {
            return ShouldApplyCurrentGoboShake() ? _goboShakePositionOffsetUv : Vector2.zero;
        }

        private Vector2 GetDisplayBeamShakeAngleDeg()
        {
            return ShouldApplyCurrentGoboShake() ? _goboShakeBeamAngleOffsetDeg : Vector2.zero;
        }

        private void UpdateZoomTargetsFromDmx(bool hasZoom, float zoom01, bool usesRangeMapping)
        {
            if (!syncZoomToLight || !hasZoom)
            {
                _zoomEnabled = false;
                return;
            }

            float z = Mathf.Clamp01(zoom01);
            if (invertZoom && !usesRangeMapping) z = 1f - z;

            float min = Mathf.Clamp(minOuterSpotAngle, 0.1f, 179f);
            float max = Mathf.Clamp(maxOuterSpotAngle, 0.1f, 179f);

            _zoomEnabled = true;
            _zoomOuterSpotAngleDeg = Mathf.Lerp(min, max, z);
            _zoomInnerSpotPercent = Mathf.Clamp(zoomInnerSpotPercent, 0f, 100f);
        }

        private void UpdatePrismTargetsFromElementDmx(int[] universe512)
        {
            int prismValue = TryReadElementRaw8(universe512, FixtureAttribute.Prism, 1, FixtureChannelRole.SelectMode, out int prismRaw)
                ? prismRaw
                : 0;

            bool hasRotationOverride = false;
            bool hasIndexOverride = false;
            float rotationSpeedOverride = 0f;
            float indexDegOverride = 0f;

            if (TryReadElementRawForRange(universe512, FixtureAttribute.Prism, 1, FixtureChannelRole.PositionOrRotation, out int prismRotationRaw, out int prismRotationRawMax, out var prismRotationElement) ||
                TryReadElementRawForRange(universe512, FixtureAttribute.Prism, 1, FixtureChannelRole.Rotation, out prismRotationRaw, out prismRotationRawMax, out prismRotationElement))
            {
                if (TryMapPrismRotationRange(prismRotationElement, prismRotationRaw, prismRotationRawMax, out rotationSpeedOverride, out hasIndexOverride, out indexDegOverride))
                    hasRotationOverride = true;
            }

            UpdatePrismTargetsFromDmx(prismValue, hasRotationOverride, rotationSpeedOverride, hasIndexOverride, indexDegOverride);
        }

        private void UpdatePrismTargetsFromElementDmx(byte[] universe512)
        {
            int prismValue = TryReadElementRaw8(universe512, FixtureAttribute.Prism, 1, FixtureChannelRole.SelectMode, out int prismRaw)
                ? prismRaw
                : 0;

            bool hasRotationOverride = false;
            bool hasIndexOverride = false;
            float rotationSpeedOverride = 0f;
            float indexDegOverride = 0f;

            if (TryReadElementRawForRange(universe512, FixtureAttribute.Prism, 1, FixtureChannelRole.PositionOrRotation, out int prismRotationRaw, out int prismRotationRawMax, out var prismRotationElement) ||
                TryReadElementRawForRange(universe512, FixtureAttribute.Prism, 1, FixtureChannelRole.Rotation, out prismRotationRaw, out prismRotationRawMax, out prismRotationElement))
            {
                if (TryMapPrismRotationRange(prismRotationElement, prismRotationRaw, prismRotationRawMax, out rotationSpeedOverride, out hasIndexOverride, out indexDegOverride))
                    hasRotationOverride = true;
            }

            UpdatePrismTargetsFromDmx(prismValue, hasRotationOverride, rotationSpeedOverride, hasIndexOverride, indexDegOverride);
        }

        private void UpdatePrismTargetsFromDmx(int prismValue, bool overridePrismRotationSpeed, float prismRotationSpeedOverride, bool overridePrismIndex, float prismIndexDeg)
        {
            _prismEnabled = false;
            _prismFacetCount = 1;
            _prismSpread = 0f;
            _prismFacetScale = 1f;
            _prismRotationOffsetDeg = 0f;
            _prismIntensityScale = 1f;

            if (prismDefinition != null)
            {
                var slot = prismDefinition.ResolveSlot(prismValue);
                if (slot != null)
                {
                    _prismEnabled = !slot.isOpen && slot.facetCount > 1;
                    int maxConfiguredFacets = Mathf.Max(1, Mathf.Max(maxPrismCompositeFacets, maxPrismAuxiliaryFacets));
                    _prismFacetCount = Mathf.Clamp(slot.facetCount, 1, maxConfiguredFacets);
                    _prismSpread = Mathf.Max(0f, slot.spread);
                    _prismFacetScale = Mathf.Max(0.0001f, slot.facetScale);
                    _prismRotationOffsetDeg = slot.rotationOffsetDeg;
                    _prismIntensityScale = Mathf.Max(0f, slot.intensityScale);
                }
            }

            if (overridePrismIndex)
            {
                _prismRotationDeg = Mathf.Repeat(prismIndexDeg, 360f);
                _prismRotationSpeedDegPerSec = 0f;
                return;
            }

            _prismRotationSpeedDegPerSec = overridePrismRotationSpeed ? prismRotationSpeedOverride : 0f;
        }

        private void UpdatePrismMotion()
        {
            if (!Mathf.Approximately(_prismRotationSpeedDegPerSec, 0f))
                _prismRotationDeg = Mathf.Repeat(_prismRotationDeg + (_prismRotationSpeedDegPerSec * Time.deltaTime), 360f);
        }

        private void UpdateGoboTargetsFromDmx(int goboValue, int goboRotationValue, GoboWheelDefinition wheelDefinition = null, bool overrideGoboRotationSpeed = false, float goboRotationSpeedOverride = 0f)
        {
            _goboEnabled = false;
            _goboTexture = null;
            _goboRotationOffsetDeg = 0f;
            _goboShakeEnabled = false;
            _goboShakeAmplitudeDeg = 0f;
            _goboShakePositionAmplitudeUv = 0f;
            _goboShakeBeamAngleAmplitudeDeg = 0f;
            _goboShakeSpeedHz = 0f;
            _goboShakeMotionMode = GoboShakeMotionMode.Rotation;
            _goboShakePositionAxis = GoboShakePositionAxis.Horizontal;
            _goboShakeAffectBeam = false;
            _goboShakeApplyWithPrism = false;
            _goboShakeApplyWithZoom = false;
            _goboShakeAllowDuringGoboRotation = true;

            var wheel = wheelDefinition != null ? wheelDefinition : goboWheel;
            if (wheel != null)
            {
                if (wheel.TryResolveSlotRange(goboValue, out var slot, out var range))
                {
                    _goboEnabled = !slot.isOpen && slot.texture != null;
                    _goboTexture = slot.texture;
                    _goboRotationOffsetDeg = slot.rotationOffsetDeg;

                    if (enableGoboShake && wheel.TryResolveShakeProfile(range, out var shakeProfile))
                    {
                        _goboShakeEnabled = true;
                        float shake01 = GetGoboRangeNormalized(range, goboValue);
                        float zoomScale = CalculateGoboShakeZoomScale(shakeProfile);
                        _goboShakeMotionMode = shakeProfile.motionMode;
                        _goboShakePositionAxis = shakeProfile.positionAxis;
                        _goboShakeAffectBeam = shakeProfile.affectBeam;
                        _goboShakeApplyWithPrism = shakeProfile.applyWithPrism;
                        _goboShakeApplyWithZoom = shakeProfile.applyWithZoom;
                        _goboShakeAllowDuringGoboRotation = shakeProfile.allowDuringGoboRotation;
                        _goboShakeAmplitudeDeg = UsesRotationShake(shakeProfile.motionMode)
                            ? MapGoboShakeRangeToAmplitude(shakeProfile.amplitudeMinDeg, shakeProfile.amplitudeMaxDeg, shakeProfile.amplitudeMapping, shake01) * zoomScale
                            : 0f;
                        _goboShakePositionAmplitudeUv = UsesPositionShake(shakeProfile.motionMode)
                            ? MapGoboShakeRangeToAmplitude(shakeProfile.positionAmplitudeMin, shakeProfile.positionAmplitudeMax, shakeProfile.amplitudeMapping, shake01) * zoomScale
                            : 0f;
                        _goboShakeBeamAngleAmplitudeDeg = shakeProfile.affectBeam
                            ? MapGoboShakeRangeToAmplitude(shakeProfile.beamAngleAmplitudeMinDeg, shakeProfile.beamAngleAmplitudeMaxDeg, shakeProfile.amplitudeMapping, shake01) * zoomScale
                            : 0f;
                        _goboShakeSpeedHz = MapGoboShakeRangeToSpeed(shakeProfile, shake01);
                    }
                }
            }

            if (!_goboShakeEnabled)
            {
                _goboShakeOffsetDeg = 0f;
                _goboShakePositionOffsetUv = Vector2.zero;
                _goboShakeBeamAngleOffsetDeg = Vector2.zero;
            }

            _goboRotationSpeedDegPerSec = overrideGoboRotationSpeed ? goboRotationSpeedOverride : MapGoboRotationDmxToSpeed(goboRotationValue);
        }

        private void UpdateGoboMotion()
        {
            if (!Mathf.Approximately(_goboRotationSpeedDegPerSec, 0f))
                _goboRotationDeg = Mathf.Repeat(_goboRotationDeg + (_goboRotationSpeedDegPerSec * Time.deltaTime), 360f);

            if (ShouldApplyCurrentGoboShake() &&
                (_goboShakeAmplitudeDeg > 0f || _goboShakePositionAmplitudeUv > 0f || _goboShakeBeamAngleAmplitudeDeg > 0f) &&
                _goboShakeSpeedHz > 0f)
            {
                _goboShakePhaseRad = Mathf.Repeat(_goboShakePhaseRad + (Mathf.PI * 2f * _goboShakeSpeedHz * Time.deltaTime), Mathf.PI * 2f);
                float wave = Mathf.Sin(_goboShakePhaseRad);
                _goboShakeOffsetDeg = wave * _goboShakeAmplitudeDeg;
                Vector2 axis = ResolveGoboShakePositionAxis(_goboShakePositionAxis);
                _goboShakePositionOffsetUv = axis * (wave * _goboShakePositionAmplitudeUv);
                _goboShakeBeamAngleOffsetDeg = _goboShakeAffectBeam ? axis * (wave * _goboShakeBeamAngleAmplitudeDeg) : Vector2.zero;
            }
            else
            {
                _goboShakeOffsetDeg = 0f;
                _goboShakePositionOffsetUv = Vector2.zero;
                _goboShakeBeamAngleOffsetDeg = Vector2.zero;
            }

            ApplyGoboCookieRollTransform();
        }

        private void ApplyGoboCookieRollTransform()
        {
            if (ShouldPrismOwnGoboCookieRotation())
            {
                RestoreGoboCookieRollTransform();
                return;
            }

            if (!syncGoboRotationToCookieTransform)
                return;

            if (goboCookieRollTransform == null)
            {
                _goboCookieRollBaseTransform = null;
                _hasGoboCookieRollBaseLocalRot = false;
                return;
            }

            if (!_hasGoboCookieRollBaseLocalRot || _goboCookieRollBaseTransform != goboCookieRollTransform)
            {
                _goboCookieRollBaseTransform = goboCookieRollTransform;
                _goboCookieRollBaseLocalRot = goboCookieRollTransform.localRotation;
                _hasGoboCookieRollBaseLocalRot = true;
            }

            Vector3 axis = goboCookieRollAxis.sqrMagnitude > 0.0001f ? goboCookieRollAxis.normalized : Vector3.forward;
            float rollDeg = Mathf.Repeat(GetDisplayGoboRotationDeg() + goboCookieRollOffsetDeg, 360f);
            goboCookieRollTransform.localRotation = _goboCookieRollBaseLocalRot * Quaternion.AngleAxis(rollDeg, axis);
        }

        private float MapGoboRotationDmxToSpeed(int dmxValue)
        {
            int clamped = Mathf.Clamp(dmxValue, 0, 255);
            if (clamped <= 126)
            {
                float t = clamped / 126f;
                return Mathf.Lerp(-maxGoboRotateDegPerSec, 0f, t);
            }

            if (clamped <= 128)
                return 0f;

            float t2 = (clamped - 129) / 126f;
            return Mathf.Lerp(0f, maxGoboRotateDegPerSec, t2);
        }

        private static float GetGoboRangeNormalized(GoboSlotRange range, int dmxValue)
        {
            if (range == null)
                return 0f;

            int min = Mathf.Clamp(Mathf.Min(range.dmxMin, range.dmxMax), 0, 255);
            int max = Mathf.Clamp(Mathf.Max(range.dmxMin, range.dmxMax), 0, 255);
            return max > min ? Mathf.InverseLerp(min, max, Mathf.Clamp(dmxValue, min, max)) : 1f;
        }

        private static float MapGoboShakeRangeToSpeed(GoboShakeProfile profile, float normalized)
        {
            float minSpeed = Mathf.Max(0f, profile.speedMinHz);
            float maxSpeed = Mathf.Max(minSpeed, profile.speedMaxHz);
            return Mathf.Lerp(minSpeed, maxSpeed, Mathf.Clamp01(normalized));
        }

        private static float MapGoboShakeRangeToAmplitude(float minValue, float maxValue, GoboShakeAmplitudeMapping mapping, float normalized)
        {
            float minAmplitude = Mathf.Max(0f, minValue);
            float maxAmplitude = Mathf.Max(0f, maxValue);
            float t = Mathf.Clamp01(normalized);

            switch (mapping)
            {
                case GoboShakeAmplitudeMapping.SlowLargeFastSmall:
                    return Mathf.Lerp(maxAmplitude, minAmplitude, t);
                case GoboShakeAmplitudeMapping.SlowSmallFastLarge:
                    return Mathf.Lerp(minAmplitude, maxAmplitude, t);
                case GoboShakeAmplitudeMapping.Fixed:
                default:
                    return maxAmplitude;
            }
        }

        private static bool UsesRotationShake(GoboShakeMotionMode mode)
        {
            return mode == GoboShakeMotionMode.Rotation || mode == GoboShakeMotionMode.RotationAndPosition;
        }

        private static bool UsesPositionShake(GoboShakeMotionMode mode)
        {
            return mode == GoboShakeMotionMode.Position || mode == GoboShakeMotionMode.RotationAndPosition;
        }

        private static Vector2 ResolveGoboShakePositionAxis(GoboShakePositionAxis axis)
        {
            return axis switch
            {
                GoboShakePositionAxis.Vertical => Vector2.up,
                GoboShakePositionAxis.Both => new Vector2(1f, 1f).normalized,
                _ => Vector2.right
            };
        }

        private bool ShouldApplyCurrentGoboShake()
        {
            if (!_goboShakeEnabled)
                return false;

            if (!_goboShakeApplyWithPrism && IsPrismDrawingEnabled())
                return false;

            if (!_goboShakeAllowDuringGoboRotation && Mathf.Abs(_goboRotationSpeedDegPerSec) > 0.001f)
                return false;

            return true;
        }

        private float CalculateGoboShakeZoomScale(GoboShakeProfile profile)
        {
            if (profile == null || !profile.applyWithZoom || !_zoomEnabled)
                return 1f;

            float min = Mathf.Clamp(minOuterSpotAngle, 0.1f, 179f);
            float max = Mathf.Clamp(maxOuterSpotAngle, 0.1f, 179f);
            float t = max > min ? Mathf.InverseLerp(min, max, _zoomOuterSpotAngleDeg) : 0.5f;
            float narrowScale = Mathf.Max(0f, profile.zoomShakeScaleMin);
            float wideScale = Mathf.Max(narrowScale, profile.zoomShakeScaleMax);
            return Mathf.Lerp(narrowScale, wideScale, t);
        }

        private bool ShouldUsePrismCookieComposite()
        {
            return false;
        }

        private bool ShouldUsePrismAuxiliaryLights()
        {
            return IsPrismDrawingEnabled();
        }

        private bool ShouldSuppressPrimaryLightForAuxiliaryOnly()
        {
            return IsPrismDrawingEnabled();
        }

        private bool ShouldSuppressPrimaryGoboForVlbPrism()
        {
            return IsPrismDrawingEnabled() && IsVlbBeamRenderMode();
        }

        private bool ShouldPrismOwnGoboCookieRotation()
        {
            return IsPrismDrawingEnabled();
        }

        private bool IsPrismDrawingEnabled()
        {
            return enablePrism && _prismEnabled;
        }

        private bool IsProjectionOnlyPrismMode()
        {
            return IsPrismDrawingEnabled() &&
                   (prismDrawMode == PrismDrawMode.ProjectionOnly ||
                    prismDrawMode == PrismDrawMode.ProjectionAndShaderBeam);
        }

        private bool IsProjectionAndShaderBeamMode()
        {
            return IsPrismDrawingEnabled() &&
                   prismDrawMode == PrismDrawMode.ProjectionAndShaderBeam;
        }

        private float GetDisplayPrismRotationDeg()
        {
            return Mathf.Repeat(-(_prismRotationDeg + _prismRotationOffsetDeg), 360f);
        }

        private void RestoreGoboCookieRollTransform()
        {
            if (goboCookieRollTransform == null)
                return;

            if (_hasGoboCookieRollBaseLocalRot && _goboCookieRollBaseTransform == goboCookieRollTransform)
                goboCookieRollTransform.localRotation = _goboCookieRollBaseLocalRot;
        }

        private Texture GetPrismCompositeCookie(Texture source, float goboRotationDeg, float prismRotationDeg, Vector2 goboOffsetUv)
        {
            if (!_prismEnabled || source == null)
                return source;

            int size = Mathf.Clamp(prismCompositeCookieSize, 16, 4096);
            int facetCount = Mathf.Clamp(_prismFacetCount, 1, Mathf.Max(1, maxPrismCompositeFacets));
            float spread = Mathf.Clamp01(_prismSpread);
            float facetScale = Mathf.Max(0.0001f, _prismFacetScale);
            float intensity = Mathf.Max(0f, _prismIntensityScale);

            _prismCompositeCookie = EnsurePrismRenderTexture(_prismCompositeCookie, size, "ArtNet Prism Composite Cookie");

            if (!NeedsPrismCompositeUpdate(source, size, facetCount, spread, facetScale, intensity, goboRotationDeg, prismRotationDeg, goboOffsetUv))
                return _prismCompositeCookie;

            if (!RenderPrismCookie(source, _prismCompositeCookie, facetCount, spread, facetScale, goboRotationDeg, prismRotationDeg, intensity, goboOffsetUv))
                return source;

            _lastPrismCompositeSource = source;
            _lastPrismCookieSize = size;
            _lastPrismCompositeFacetCount = facetCount;
            _lastPrismCompositeSpread = spread;
            _lastPrismCompositeFacetScale = facetScale;
            _lastPrismCompositeIntensityScale = intensity;
            _lastPrismCompositeGoboRotation = goboRotationDeg;
            _lastPrismCompositePrismRotation = prismRotationDeg;
            _lastPrismCompositeGoboOffset = goboOffsetUv;

            return _prismCompositeCookie;
        }

        private Texture GetRotatedGoboCookie(Texture source, float goboRotationDeg, Vector2 goboOffsetUv)
        {
            if (source == null)
                return null;

            int size = Mathf.Clamp(prismCompositeCookieSize, 16, 4096);
            if (_lastPrismRotatedSource != null && _lastPrismRotatedSource != source)
            {
                if (_prismRotatedGoboCookie != null)
                {
                    _prismRotatedGoboCookie.Release();
                    if (Application.isPlaying)
                        Destroy(_prismRotatedGoboCookie);
                    else
                        DestroyImmediate(_prismRotatedGoboCookie);
                    _prismRotatedGoboCookie = null;
                }

                _lastPrismRotatedGoboRotation = float.NaN;
                _lastPrismRotatedGoboOffset = new Vector2(float.NaN, float.NaN);
                _lastPrismRotatedCookieSize = -1;
            }

            _prismRotatedGoboCookie = EnsurePrismRenderTexture(_prismRotatedGoboCookie, size, "ArtNet Prism Rotated Gobo Cookie");

            if (_lastPrismRotatedSource == source &&
                _prismRotatedGoboCookie != null &&
                _lastPrismRotatedCookieSize == size &&
                !AngleChangedEnough(_lastPrismRotatedGoboRotation, goboRotationDeg) &&
                !OffsetChangedEnough(_lastPrismRotatedGoboOffset, goboOffsetUv))
            {
                return _prismRotatedGoboCookie;
            }

            if (!RenderPrismCookie(source, _prismRotatedGoboCookie, 1, 0f, 1f, goboRotationDeg, 0f, 1f, goboOffsetUv))
                return source;

            _lastPrismRotatedSource = source;
            _lastPrismRotatedGoboRotation = goboRotationDeg;
            _lastPrismRotatedGoboOffset = goboOffsetUv;
            _lastPrismRotatedCookieSize = size;

            return _prismRotatedGoboCookie;
        }

        private Texture GetOffsetGoboCookie(Texture source, Vector2 goboOffsetUv)
        {
            if (source == null)
                return null;

            if (goboOffsetUv.sqrMagnitude <= 0.0000001f)
                return source;

            int size = Mathf.Clamp(prismCompositeCookieSize, 16, 4096);
            if (_lastGoboOffsetSource != null && _lastGoboOffsetSource != source)
            {
                ReleaseRenderTexture(_goboOffsetCookie);
                _goboOffsetCookie = null;
                _lastGoboOffset = new Vector2(float.NaN, float.NaN);
                _lastGoboOffsetCookieSize = -1;
            }

            _goboOffsetCookie = EnsurePrismRenderTexture(_goboOffsetCookie, size, "ArtNet Gobo Offset Cookie");

            if (_lastGoboOffsetSource == source &&
                _goboOffsetCookie != null &&
                _lastGoboOffsetCookieSize == size &&
                !OffsetChangedEnough(_lastGoboOffset, goboOffsetUv))
            {
                return _goboOffsetCookie;
            }

            if (!RenderPrismCookie(source, _goboOffsetCookie, 1, 0f, 1f, 0f, 0f, 1f, goboOffsetUv))
                return source;

            _lastGoboOffsetSource = source;
            _lastGoboOffset = goboOffsetUv;
            _lastGoboOffsetCookieSize = size;

            return _goboOffsetCookie;
        }

        private bool NeedsPrismCompositeUpdate(Texture source, int size, int facetCount, float spread, float facetScale, float intensity, float goboRotationDeg, float prismRotationDeg, Vector2 goboOffsetUv)
        {
            if (_prismCompositeCookie == null)
                return true;

            if (_lastPrismCompositeSource != source ||
                _lastPrismCookieSize != size ||
                _lastPrismCompositeFacetCount != facetCount ||
                !Mathf.Approximately(_lastPrismCompositeSpread, spread) ||
                !Mathf.Approximately(_lastPrismCompositeFacetScale, facetScale) ||
                !Mathf.Approximately(_lastPrismCompositeIntensityScale, intensity))
            {
                return true;
            }

            return AngleChangedEnough(_lastPrismCompositeGoboRotation, goboRotationDeg) ||
                   AngleChangedEnough(_lastPrismCompositePrismRotation, prismRotationDeg) ||
                   OffsetChangedEnough(_lastPrismCompositeGoboOffset, goboOffsetUv);
        }

        private static bool OffsetChangedEnough(Vector2 previous, Vector2 current)
        {
            if (float.IsNaN(previous.x) || float.IsNaN(previous.y))
                return true;

            return (previous - current).sqrMagnitude >= 0.0000001f;
        }

        private bool AngleChangedEnough(float previousDeg, float currentDeg)
        {
            if (float.IsNaN(previousDeg))
                return true;

            float step = Mathf.Max(0f, prismCompositeRotationStepDeg);
            if (step <= 0f)
                return true;

            return Mathf.Abs(Mathf.DeltaAngle(previousDeg, currentDeg)) >= step;
        }

        private RenderTexture EnsurePrismRenderTexture(RenderTexture current, int size, string textureName)
        {
            if (current != null && current.width == size && current.height == size)
                return current;

            if (current != null)
                ReleaseRenderTexture(current);

            var rt = new RenderTexture(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
            {
                name = textureName,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false
            };
            rt.Create();
            return rt;
        }

        private bool RenderPrismCookie(Texture source, RenderTexture target, int facetCount, float spread, float facetScale, float goboRotationDeg, float prismRotationDeg, float intensityScale, Vector2 goboOffsetUv)
        {
            if (source == null || target == null)
                return false;

            if (!EnsurePrismCookieMaterial())
                return false;

            _prismCookieMaterial.SetTexture("_GoboTex", source);
            _prismCookieMaterial.SetFloat("_FacetCount", Mathf.Clamp(facetCount, 1, 8));
            _prismCookieMaterial.SetFloat("_Spread", Mathf.Max(0f, spread));
            _prismCookieMaterial.SetFloat("_FacetScale", Mathf.Max(0.0001f, facetScale));
            _prismCookieMaterial.SetFloat("_GoboRotationRad", goboRotationDeg * Mathf.Deg2Rad);
            _prismCookieMaterial.SetFloat("_PrismRotationRad", prismRotationDeg * Mathf.Deg2Rad);
            _prismCookieMaterial.SetFloat("_IntensityScale", Mathf.Max(0f, intensityScale));
            _prismCookieMaterial.SetVector("_GoboOffset", new Vector4(goboOffsetUv.x, goboOffsetUv.y, 0f, 0f));
            Graphics.Blit(source, target, _prismCookieMaterial);
            MarkTextureContentUpdated(target);
            return true;
        }

        private static void MarkTextureContentUpdated(Texture texture)
        {
            if (texture == null)
                return;

            if (!_textureIncrementUpdateCountMethodResolved)
            {
                _textureIncrementUpdateCountMethodResolved = true;
                _textureIncrementUpdateCountMethod = typeof(Texture).GetMethod(
                    "IncrementUpdateCount",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic);
            }

            _textureIncrementUpdateCountMethod?.Invoke(texture, null);
        }

        private bool EnsurePrismCookieMaterial()
        {
            if (_prismCookieMaterial != null)
                return true;

            Shader shader = prismCookieShader;
            if (shader == null)
                shader = Resources.Load<Shader>("ArtNet/PrismCookieComposite");
            if (shader == null)
                shader = Shader.Find("Hidden/ArtNet/PrismCookieComposite");
            if (shader == null)
                return false;

            _prismCookieMaterial = new Material(shader)
            {
                hideFlags = HideFlags.DontSave
            };
            return true;
        }

        private void ApplyPrismAuxiliaryLights(FixtureRenderState state)
        {
            if (!ShouldUsePrismAuxiliaryLights())
            {
                DisablePrismAuxiliaryLights();
                return;
            }

            int count = Mathf.Clamp(_prismFacetCount, 1, Mathf.Max(1, maxPrismAuxiliaryFacets));
            EnsurePrismAuxiliaryLights(count);

            Transform sourceTransform = targetLight != null ? targetLight.transform : transform;
            if (_prismAuxRoot != null && sourceTransform != null)
            {
                _prismAuxRoot.position = sourceTransform.position;
                _prismAuxRoot.rotation = sourceTransform.rotation;
            }

            float prismRotationDeg = GetDisplayPrismRotationDeg();
            float goboRotationDeg = GetDisplayGoboRotationDeg();
                float spreadDeg = Mathf.Max(0f, _prismSpread * auxiliarySpreadMultiplierDeg * GetPrismGoboSpacingScale(state));
            var rotationMode = ResolveAuxiliaryCookieRotationMode();
            Texture sourceCookie = _goboEnabled && _goboTexture != null ? _goboTexture : Texture2D.whiteTexture;
            Texture cookie = rotationMode == AuxiliaryCookieRotationMode.TransformRoll
                ? GetOffsetGoboCookie(sourceCookie, state.goboOffsetUv)
                : GetRotatedGoboCookie(sourceCookie, goboRotationDeg, state.goboOffsetUv);

            for (int i = 0; i < _prismAuxiliaryLights.Count; i++)
            {
                var aux = _prismAuxiliaryLights[i];
                bool active = i < count;
                if (aux?.directionPivot != null)
                    aux.directionPivot.gameObject.SetActive(active);

                if (!active || aux == null || aux.light == null)
                    continue;

                float angle = ((360f * i) / count) + prismRotationDeg;
                float angleRad = angle * Mathf.Deg2Rad;
                float xDeg = Mathf.Sin(angleRad) * spreadDeg;
                float yDeg = Mathf.Cos(angleRad) * spreadDeg;

                aux.directionPivot.localRotation = Quaternion.Euler(xDeg + state.beamShakeAngleDeg.x, yDeg + state.beamShakeAngleDeg.y, 0f);
                aux.cookieRollPivot.localRotation = rotationMode == AuxiliaryCookieRotationMode.TransformRoll
                    ? Quaternion.AngleAxis(goboRotationDeg + goboCookieRollOffsetDeg, Vector3.forward)
                    : Quaternion.identity;

                ApplyPrismAuxiliaryLightState(aux, state, cookie, count);
            }
        }

        private AuxiliaryCookieRotationMode ResolveAuxiliaryCookieRotationMode()
        {
            if (auxiliaryCookieRotationMode != AuxiliaryCookieRotationMode.Auto)
                return auxiliaryCookieRotationMode;

#if HAS_HDRP
            if (PrimaryLightHasHdrpData())
                return AuxiliaryCookieRotationMode.TransformRoll;
#endif
            return AuxiliaryCookieRotationMode.CompositeTextureRoll;
        }

        private void ApplyPrimaryVolumetricForAlternativeBeamModes(bool suppressPrimaryLight)
        {
#if HAS_HDRP
            bool disableVolumetricOnly = !suppressPrimaryLight && UsesAlternativeBeamRenderMode();
            var targets = GatherTargetLights();
            for (int i = 0; i < targets.Count; i++)
            {
                var light = targets[i];
                if (light == null)
                    continue;

                var hd = light.GetComponent<HDAdditionalLightData>();
                if (hd == null)
                    continue;

                float defaultValue = CapturePrimaryVolumetricDefault(hd);
                TrySetHdrpVolumetricDimmer(hd, disableVolumetricOnly ? 0f : defaultValue);
            }
#endif
        }

#if HAS_HDRP
        private float CapturePrimaryVolumetricDefault(HDAdditionalLightData hd)
        {
            for (int i = 0; i < _primaryVolumetricDimmerDefaults.Count; i++)
            {
                var entry = _primaryVolumetricDimmerDefaults[i];
                if (entry != null && entry.hd == hd)
                    return entry.value;
            }

            float defaultValue = TryGetHdrpVolumetricDimmer(hd, out float value) ? value : 1f;
            _primaryVolumetricDimmerDefaults.Add(new HdrpVolumetricDimmerDefault
            {
                hd = hd,
                value = defaultValue
            });
            return defaultValue;
        }

        private void RestorePrimaryVolumetricDefaults()
        {
            for (int i = 0; i < _primaryVolumetricDimmerDefaults.Count; i++)
            {
                var entry = _primaryVolumetricDimmerDefaults[i];
                if (entry?.hd != null)
                    TrySetHdrpVolumetricDimmer(entry.hd, entry.value);
            }
        }

        private bool PrimaryLightHasHdrpData()
        {
            return targetLight != null && targetLight.GetComponent<HDAdditionalLightData>() != null;
        }

        private static bool TryGetHdrpVolumetricDimmer(HDAdditionalLightData hd, out float value)
        {
            value = 1f;
            if (hd == null)
                return false;

            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic;

            var property = typeof(HDAdditionalLightData).GetProperty("volumetricDimmer", flags);
            if (property != null && property.CanRead && property.PropertyType == typeof(float))
            {
                value = (float)property.GetValue(hd);
                return true;
            }

            var field = typeof(HDAdditionalLightData).GetField("volumetricDimmer", flags);
            if (field != null && field.FieldType == typeof(float))
            {
                value = (float)field.GetValue(hd);
                return true;
            }

            return false;
        }

        private static void TrySetHdrpVolumetricDimmer(HDAdditionalLightData hd, float value)
        {
            if (hd == null)
                return;

            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic;

            var property = typeof(HDAdditionalLightData).GetProperty("volumetricDimmer", flags);
            if (property != null && property.CanWrite && property.PropertyType == typeof(float))
            {
                property.SetValue(hd, value);
                return;
            }

            var field = typeof(HDAdditionalLightData).GetField("volumetricDimmer", flags);
            if (field != null && field.FieldType == typeof(float))
                field.SetValue(hd, value);
        }

#else
        private void RestorePrimaryVolumetricDefaults()
        {
        }

#endif

        private void EnsurePrismAuxiliaryLights(int count)
        {
            count = Mathf.Clamp(count, 1, Mathf.Max(1, maxPrismAuxiliaryFacets));

            if (_prismAuxRoot == null)
            {
                var root = new GameObject("PrismAuxRoot");
                root.hideFlags = HideFlags.DontSave;
                _prismAuxRoot = root.transform;
                _prismAuxRoot.SetParent(transform, false);
            }

            while (_prismAuxiliaryLights.Count < count)
                _prismAuxiliaryLights.Add(CreatePrismAuxiliaryLight(_prismAuxiliaryLights.Count));
        }

        private PrismAuxiliaryLight CreatePrismAuxiliaryLight(int index)
        {
            var directionGo = new GameObject($"FacetDirectionPivot_{index}");
            directionGo.hideFlags = HideFlags.DontSave;
            directionGo.transform.SetParent(_prismAuxRoot, false);

            var rollGo = new GameObject($"CookieRollPivot_{index}");
            rollGo.hideFlags = HideFlags.DontSave;
            rollGo.transform.SetParent(directionGo.transform, false);

            var lightGo = new GameObject($"FacetLight_{index}");
            lightGo.hideFlags = HideFlags.DontSave;
            lightGo.transform.SetParent(rollGo.transform, false);

            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Spot;
            light.shadows = auxiliaryLightShadows ? LightShadows.Soft : LightShadows.None;

#if HAS_HDRP
            HDAdditionalLightData hd = null;
            if (targetLight != null && targetLight.GetComponent<HDAdditionalLightData>() != null)
                hd = lightGo.AddComponent<HDAdditionalLightData>();
#endif

            return new PrismAuxiliaryLight
            {
                directionPivot = directionGo.transform,
                cookieRollPivot = rollGo.transform,
                light = light,
#if HAS_HDRP
                hd = hd
#endif
            };
        }

        private float GetPrismBrightnessDistributionScale(int facetCount)
        {
            int safeFacetCount = Mathf.Max(1, facetCount);
            float fullyDistributedScale = 1f / safeFacetCount;
            return Mathf.Lerp(1f, fullyDistributedScale, Mathf.Clamp01(prismBrightnessDistribution));
        }

        private float GetVlbPrismGoboSpotAngleScale(FixtureRenderState state)
        {
            if (!IsVlbBeamRenderMode())
                return 1f;

            return state.vlbPrismGoboScale > 0f
                ? Mathf.Clamp(state.vlbPrismGoboScale, 0.1f, 3f)
                : 1f;
        }

        private float GetPrismGoboSpacingScale(FixtureRenderState state)
        {
            float customScale = Mathf.Max(0f, prismGoboSpacingScale);

            switch (prismZoomCorrection)
            {
                case PrismZoomCorrectionMode.Auto:
                    if (!state.zoomEnabled)
                        return customScale;

                    float currentAngle = Mathf.Clamp(state.outerSpotAngleDeg, 0.1f, 179f);
                    float minAngle = Mathf.Clamp(minOuterSpotAngle, 0.1f, 179f);
                    float maxAngle = Mathf.Clamp(maxOuterSpotAngle, 0.1f, 179f);
                    float referenceAngle = Mathf.Clamp((minAngle + maxAngle) * 0.5f, 0.1f, 179f);
                    float currentFootprint = Mathf.Tan(currentAngle * 0.5f * Mathf.Deg2Rad);
                    float referenceFootprint = Mathf.Max(0.0001f, Mathf.Tan(referenceAngle * 0.5f * Mathf.Deg2Rad));
                    return Mathf.Max(0f, currentFootprint / referenceFootprint * customScale);

                case PrismZoomCorrectionMode.Custom:
                    return customScale;

                case PrismZoomCorrectionMode.Off:
                default:
                    return 1f;
            }
        }

        private void ApplyPrismAuxiliaryLightState(PrismAuxiliaryLight aux, FixtureRenderState state, Texture cookie, int facetCount)
        {
            if (aux == null || aux.light == null)
                return;

            Light light = aux.light;
            bool hasHdrp = false;
#if HAS_HDRP
            hasHdrp = aux.hd != null;
#endif
            bool projectionOnly = IsProjectionOnlyPrismMode();
            float maxIntensity = hasHdrp ? hdrpMaxIntensity : genericMaxIntensity;
            float perFacetScale = auxiliaryIntensityScale * Mathf.Max(0f, _prismIntensityScale) * GetPrismBrightnessDistributionScale(facetCount);
            float intensity = Mathf.Clamp01(state.lightDimmer01) * maxIntensity * perFacetScale;

            light.enabled = true;
            light.type = LightType.Spot;
            light.color = state.color;
            light.intensity = intensity;
            light.cookie = cookie;
            light.shadows = (!projectionOnly && auxiliaryLightShadows) ? LightShadows.Soft : LightShadows.None;

            if (targetLight != null)
            {
                light.range = targetLight.range;
                light.cullingMask = targetLight.cullingMask;
                light.renderingLayerMask = targetLight.renderingLayerMask;
            }

            if (state.zoomEnabled)
            {
                float spotAngleScale = GetVlbPrismGoboSpotAngleScale(state);
                float outer = Mathf.Clamp(state.outerSpotAngleDeg * spotAngleScale, 0.1f, 179f);
                float inner01 = Mathf.Clamp01(state.innerSpotPercent / 100f);
                light.spotAngle = outer;
                light.innerSpotAngle = outer * inner01;
            }
            else if (targetLight != null)
            {
                float spotAngleScale = GetVlbPrismGoboSpotAngleScale(state);
                float baseOuter = Mathf.Clamp(targetLight.spotAngle, 0.1f, 179f);
                float inner01 = Mathf.Clamp01(targetLight.innerSpotAngle / baseOuter);
                float outer = Mathf.Clamp(baseOuter * spotAngleScale, 0.1f, 179f);
                light.spotAngle = outer;
                light.innerSpotAngle = outer * inner01;
            }

#if HAS_HDRP
            if (aux.hd != null)
            {
                aux.hd.SetColor(state.color);
                aux.hd.intensity = intensity;
                aux.hd.SetCookie(cookie != null ? cookie : Texture2D.whiteTexture);
                float volumetricDimmer = IsVlbBeamRenderMode() || (projectionOnly && disableAuxiliaryVolumetricInProjectionOnly)
                    ? 0f
                    : Mathf.Max(0f, auxiliaryVolumetricIntensityScale);
                TrySetHdrpVolumetricDimmer(aux.hd, volumetricDimmer);
                if (state.zoomEnabled)
                {
                    float spotAngleScale = GetVlbPrismGoboSpotAngleScale(state);
                    aux.hd.SetSpotAngle(Mathf.Clamp(state.outerSpotAngleDeg * spotAngleScale, 0.1f, 179f));
                    aux.hd.innerSpotPercent = Mathf.Clamp(state.innerSpotPercent, 0f, 100f);
                }
                else if (targetLight != null)
                {
                    float spotAngleScale = GetVlbPrismGoboSpotAngleScale(state);
                    float baseOuter = Mathf.Clamp(targetLight.spotAngle, 0.1f, 179f);
                    float inner01 = Mathf.Clamp01(targetLight.innerSpotAngle / baseOuter);
                    aux.hd.SetSpotAngle(Mathf.Clamp(baseOuter * spotAngleScale, 0.1f, 179f));
                    aux.hd.innerSpotPercent = inner01 * 100f;
                }

                if (targetLight != null)
                    aux.hd.range = targetLight.range;
            }
#endif

            ApplyPrismAuxiliaryVlbState(aux, state, cookie);
        }

        private void ApplyPrismAuxiliaryVlbState(PrismAuxiliaryLight aux, FixtureRenderState state, Texture cookie)
        {
            if (aux == null || aux.light == null)
                return;

            if (!IsVlbBeamRenderMode())
            {
                DisablePrismAuxiliaryVlb(aux);
                return;
            }

            EnsurePrismAuxiliaryVlb(aux);
            if (aux.vlbHd == null)
                return;

            var overrides = BuildVlbOverrides();
            var template = targetLight != null ? VlbBeamAdapter.GetHd(targetLight.gameObject) : null;
            VlbBeamAdapter.ApplyPrismHd(aux.vlbHd, aux.light, template, overrides);

            if (aux.vlbCookieHd != null)
            {
                bool hasCookie = syncLightCookieToGobo && state.goboEnabled && cookie != null;
                VlbBeamAdapter.ApplyPrismCookieHd(aux.vlbCookieHd, hasCookie, cookie, state.lightDimmer01, GetTemplateVlbCookieScale());
            }
        }

        private Vector2 GetTemplateVlbCookieScale()
        {
            var template = targetLight != null ? VlbBeamAdapter.GetCookieHd(targetLight.gameObject) : null;
            if (template != null)
                return VlbBeamAdapter.GetCookieScale(template);
            return Vector2.one;
        }

        private void EnsurePrismAuxiliaryVlb(PrismAuxiliaryLight aux)
        {
            if (aux == null || aux.light == null)
                return;

            var gameObject = aux.light.gameObject;
            if (aux.vlbHd == null)
                aux.vlbHd = VlbBeamAdapter.GetHd(gameObject);
            if (aux.vlbHd == null)
                aux.vlbHd = VlbBeamAdapter.EnsureHd(gameObject);

            if (aux.vlbCookieHd == null)
                aux.vlbCookieHd = VlbBeamAdapter.GetCookieHd(gameObject);
            if (aux.vlbCookieHd == null)
                aux.vlbCookieHd = VlbBeamAdapter.EnsureCookieHd(gameObject);
        }

        private void DisablePrismAuxiliaryVlb(PrismAuxiliaryLight aux)
        {
            if (aux == null)
                return;

            VlbBeamAdapter.Disable(aux.vlbHd);
            VlbBeamAdapter.Disable(aux.vlbCookieHd);
        }

        private void DisablePrismAuxiliaryLights()
        {
            for (int i = 0; i < _prismAuxiliaryLights.Count; i++)
            {
                var aux = _prismAuxiliaryLights[i];
                if (aux?.directionPivot != null)
                    aux.directionPivot.gameObject.SetActive(false);
                if (aux?.light != null)
                    aux.light.enabled = false;
                DisablePrismAuxiliaryVlb(aux);
            }

            DisablePrismPseudoBeamFacets();
            DisableSinglePseudoBeamSoftShell();
            SetTemplateBeamRenderersEnabled(true);
        }

        private void ReleasePrismResources()
        {
            ReleasePrismPseudoBeamFacets();
            ReleaseRenderTexture(_prismCompositeCookie);
            ReleaseRenderTexture(_prismRotatedGoboCookie);
            ReleaseRenderTexture(_goboOffsetCookie);
            _prismCompositeCookie = null;
            _prismRotatedGoboCookie = null;
            _goboOffsetCookie = null;

            if (_prismCookieMaterial != null)
            {
                DestroyUnityObject(_prismCookieMaterial);
                _prismCookieMaterial = null;
            }

            if (_prismAuxRoot != null)
            {
                DestroyUnityObject(_prismAuxRoot.gameObject);
                _prismAuxRoot = null;
            }

            _prismAuxiliaryLights.Clear();
        }

        private static void ReleaseRenderTexture(RenderTexture rt)
        {
            if (rt == null)
                return;

            rt.Release();
            DestroyUnityObject(rt);
        }

        private static void DestroyUnityObject(UnityEngine.Object obj)
        {
            if (obj == null)
                return;

            if (Application.isPlaying)
                Destroy(obj);
            else
                DestroyImmediate(obj);
        }


        
        // ------------------------------------------------------------
        // Pan/Tilt continuous interpolation (target set on DMX update, applied every frame)
        // ------------------------------------------------------------

        private void SetPanTarget(float panDeg, float maxDegPerSec, float smoothing01)
        {
            _panTargetLocalRot = _panBaseLocalRot * Quaternion.AngleAxis(panDeg, AxisToVector(panAxis));
            _hasPanTarget = true;
            _panTargetMaxDegPerSec = maxDegPerSec;
            _panTargetSmoothing = smoothing01;
        }

        private void SetTiltTarget(float tiltDeg, float maxDegPerSec, float smoothing01)
        {
            _tiltTargetLocalRot = _tiltBaseLocalRot * Quaternion.AngleAxis(tiltDeg, AxisToVector(tiltAxis));
            _hasTiltTarget = true;
            _tiltTargetMaxDegPerSec = maxDegPerSec;
            _tiltTargetSmoothing = smoothing01;
        }

        private void UpdatePanTiltMotion()
        {
            if (!enableContinuousPanTiltUpdate) return;

            // 最初のDMX目標値が来るまでは何もしない
            if (panTransform != null && _hasPanTarget)
            {
                ApplyRotationToTarget(panTransform, _panTargetLocalRot, _panTargetMaxDegPerSec, _panTargetSmoothing);
            }

            if (tiltTransform != null && _hasTiltTarget)
            {
                ApplyRotationToTarget(tiltTransform, _tiltTargetLocalRot, _tiltTargetMaxDegPerSec, _tiltTargetSmoothing);
            }
        }

        private static void ApplyRotationToTarget(Transform t, Quaternion targetLocalRot, float maxDegPerSec, float smoothing01)
        {
            if (t == null) return;

            if (maxDegPerSec > 0f)
            {
                float dt = Mathf.Max(0.0001f, Time.deltaTime);
                float step = maxDegPerSec * dt;
                t.localRotation = Quaternion.RotateTowards(t.localRotation, targetLocalRot, step);
                return;
            }

            if (smoothing01 <= 0f)
            {
                t.localRotation = targetLocalRot;
                return;
            }

            float dt2 = Mathf.Max(0.0001f, Time.deltaTime);
            // smoothing01 is treated as 0..1
            float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(smoothing01), dt2 * 60f);
            t.localRotation = Quaternion.Slerp(t.localRotation, targetLocalRot, k);
        }

private static Vector3 AxisToVector(LocalAxis a) => a switch
        {
            LocalAxis.X => Vector3.right,
            LocalAxis.Y => Vector3.up,
            LocalAxis.Z => Vector3.forward,
            LocalAxis.MinusX => -Vector3.right,
            LocalAxis.MinusY => -Vector3.up,
            LocalAxis.MinusZ => -Vector3.forward,
            _ => Vector3.forward
        };

        private static void ApplyAxisRotation(
            Transform t,
            Quaternion baseLocalRotation,
            LocalAxis axis,
            float degrees,
            float speedDegPerSecOrMinus1,
            float smoothing)
        {
            var ax = AxisToVector(axis);
            var target = baseLocalRotation * Quaternion.AngleAxis(degrees, ax);

            // Speed優先（PanTiltSpeedがある場合）
            if (speedDegPerSecOrMinus1 > 0f)
            {
                t.localRotation = Quaternion.RotateTowards(t.localRotation, target, speedDegPerSecOrMinus1 * Time.deltaTime);
                return;
            }

            // Smoothing
            if (smoothing <= 0f)
            {
                t.localRotation = target;
                return;
            }

            float k = Mathf.Clamp01(Time.deltaTime * smoothing);
            t.localRotation = Quaternion.Slerp(t.localRotation, target, k);
        }
    }
}
