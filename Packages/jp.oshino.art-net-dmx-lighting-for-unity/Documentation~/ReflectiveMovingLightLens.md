# Reflective MAC Ultra lens

The original lens Shader Graphs remain available as `SG_LensFakeDepth_URP.shadergraph` and `SG_LensFakeDepth_HDRP.shadergraph`. The MAC Ultra gobo prefabs use separate reflective variants, `SG_LensReflective_URP.shadergraph` and `SG_LensReflective_HDRP.shadergraph`, through `MovingLight_LensReflective_URP.mat` and `MovingLight_LensReflective_HDRP.mat`. The additive gobo surface remains the second material on the lens renderer.

The reflective variants use the pipeline-specific Lit targets. The lens material starts with metallic 0.6 and smoothness 0.94; these values control the environment reflection and can be tuned per fixture. The existing DMX color and dimmer remain connected to lens emission, and the separate gobo material remains DMX-controlled. The HDRP reflective Shader Graph opts in to receiving screen-space reflections.

For a visible reflection, provide an environment to reflect. A reflection probe that captures the intended surroundings should include the lens in its influence volume. In URP, enable Box Projection in the URP asset and on the probe when nearby objects should appear with positional parallax. For HDRP screen-space reflections, the HDRP asset, camera frame settings, and scene volume must also enable SSR; the material setting alone is insufficient. SSR can only reflect objects visible to the camera. Reflection probes remain the fallback for off-screen surroundings.

The demo scenes do not automatically include a reflection probe. A material or Shader Graph import/compile pass does not confirm the visual result; inspect the lens at multiple angles with gobo enabled and disabled in both pipelines.

## MAC Ultra center emitter and Bloom

汎用マテリアル `GoboLensSurface_MovingLight_HDRP.mat` / `GoboLensSurface_MovingLight_URP.mat` は、ゴボとは別の中心円Emissionを持ちます。`Source Emission`（0–16）で明るさ、`Source Inner Radius` / `Source Outer Radius` で円の広がりを調整できます。EmissionはDMXカラー・ディマー・シャッターに連動し、アイリスの影響も受けます。ゴボ模様の明るさは `Gobo Emission` で別に調整してください。

Bloomはマテリアルだけでは発生しません。導入先シーンでHDR対応のカメラとGlobal VolumeのBloomを有効にしてください。URPではカメラのPost ProcessingとVolume Layer Maskも確認します。開始値はThreshold 2、Intensity 0.5、Scatter 0.65です。Bloomは同じシーンの他の明るい部分にも作用するため、導入先の照明・露出に合わせて調整してください。パッケージはシーンのVolume設定を自動変更しません。

The general-purpose moving-light gobo materials have an independent center emitter beneath the gobo overlay. `Source Emission` (0–16) sets its strength; `Source Inner Radius` and `Source Outer Radius` shape it. The emitter follows DMX color, dimmer, shutter, and iris. Adjust `Gobo Emission` separately for the pattern. Bloom requires an HDR-capable camera and an enabled Bloom override in the destination scene's Volume; in URP, also enable camera post-processing and include the Volume layer. Start with threshold 2, intensity 0.5, and scatter 0.65, then tune for the scene. The package does not automatically change consumer scenes.

`Source Parallax` adds view-dependent offset to the center emitter only. A positive or negative value changes the offset direction; the MAC Ultra materials start at `0.035`. The gobo, lens-surface hotspot, reflection, and glass coating remain fixed, making the emitter appear recessed beneath the gobo. Keep this value small (roughly -0.08 to 0.08) because it is a lightweight UV-based depth illusion rather than real geometry.

Open Goboでは、中心から外側へ弱まる発光の上に、レンズ内側の細い輪郭線と中心寄りの小さなハイライトを重ねます。`Open Lens Center Level` / `Open Lens Edge Level` で面の明暗差、`Open Lens Rim Emission` / `Open Lens Highlight Emission` で輪郭線とハイライトを調整します。これらはゴボ表示時には適用されません。青いガラス材質は変更せず、外側の発光を抑えることで質感を見せます。

With Open Gobo, the surface emission falls from center to edge, with a thin inner rim and a small near-center glint. `Open Lens Center Level` and `Open Lens Edge Level` set the field contrast; `Open Lens Rim Emission` and `Open Lens Highlight Emission` tune the two accents. The effect fades out when a gobo is active. The glass material is unchanged; reduced outer emission reveals more of its blue tint.

## Lens halo without post-processing

MAC Ultraのムービングライトプレハブ（通常/VLB HD/VLB SD、HDRP/URP）は、標準で `MovingLight_LensGlossy_HDRP/URP.mat` をレンズ表面に使用します。透明な表面で比較したい場合は `MovingLight_LensClear_HDRP/URP.mat` または対応する `_LensClear` プレハブを使用してください。ゴボと中心光源のマテリアルは両バリエーションで共通です。

The MAC Ultra moving-light prefabs (regular/VLB HD/VLB SD, HDRP/URP) use `MovingLight_LensGlossy_HDRP/URP.mat` as their default lens coating. For a clear surface, assign `MovingLight_LensClear_HDRP/URP.mat` or use the matching `_LensClear` prefab. Both variants share the gobo and center-emitter materials.

MAC UltraのHDRP/URPプレハブには、カメラのポストプロセスに依存しないレンズ周辺光を追加しました。再生時にレンズ前面へ小さな加算Quadを生成し、DMXカラー・ディマー・シャッターとアイリス開口に連動します。ゴボ、反射、ガラスコーティングとは別のマテリアルなので、ゴボ模様を隠さずに調整できます。`Dmx Fixture Component` の `Enable Lens Halo`、`Lens Halo Size` と `MovingLight_LensHalo_HDRP/URP.mat` の `Halo Intensity`、`Halo Tint` で調整してください。このQuadは再生時のみ生成されます。

The MAC Ultra HDRP/URP prefabs now generate a small additive quad in front of the lens during Play mode. It follows DMX color, dimmer, shutter and iris aperture without relying on camera post-processing. It uses a separate material from the gobo, reflection and glass coating. Adjust `Enable Lens Halo`, `Lens Halo Size`, `Halo Intensity`, and `Halo Tint`. The quad is runtime-only and is a local glow, not a full-screen Bloom substitute.

## Optional packaged Bloom profiles

Package ManagerのSamplesから、使用中のパイプラインに合う `MAC Ultra Lens Bloom (HDRP)` または `(URP)` をImportしてください。取り込んだ `MACUltraLensBloom_HDRP/URP` Volume ProfileをシーンのGlobal Volumeへ割り当てるか、既存のVolume ProfileへBloomのThreshold 2、Intensity 0.5、Scatter 0.65を転記します。既存Volumeを置き換えると他の効果が失われるため、既存設定がある場合はBloom Overrideだけを追加してください。URPではHDRとカメラのPost Processingを有効にし、カメラのVolume Layer MaskにGlobal Volumeのレイヤーを含めます。HDRPでもカメラのFrame Settingsでポストプロセスを有効にします。サンプルはシーンやカメラを自動変更しません。

In Package Manager, import the `MAC Ultra Lens Bloom (HDRP)` or `(URP)` sample matching the project. Assign the imported Volume Profile to a Global Volume, or copy its Bloom values into an existing profile. If the scene already has a profile, add only the Bloom override rather than replacing its other effects. Enable HDR and camera post-processing in URP and include the Global Volume layer in the camera's Volume Layer Mask; enable post-processing in HDRP camera Frame Settings. The sample never changes scene or camera settings automatically.
