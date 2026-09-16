# Art-Net DMX Lighting for Unity

## 導入と更新

Package Managerから更新する通常の導入では、次のGit URLを入力します。

```text
https://github.com/nao40031/Art-Net-DMX-Lighting-for-Unity.git?path=/Packages/jp.oshino.art-net-dmx-lighting-for-unity#upm/release
```

一度このURLで導入すれば、以後はPackage Managerの **Update** ボタンで `upm/release` に公開された最新版を取得できます。

特定バージョンに固定する場合は、代わりにタグ付きURLを使います。タグは自動で進まないため、更新時はURL内のタグを新しい版へ変更してください。

```text
https://github.com/nao40031/Art-Net-DMX-Lighting-for-Unity.git?path=/Packages/jp.oshino.art-net-dmx-lighting-for-unity#v0.1.9
```

すでにタグ付きURLを導入している場合は、一度だけ **Add package from git URL...** で上記の `#upm/release` URLへ切り替えてください。以後のリリースは **Update** から導入できます。

Art-Net/DMX を受信し、Fixture 定義に基づいて Unity のライト、Pan/Tilt、カラー、ゴボを制御するパッケージです。

## 固定版の導入

Unity の **Package Manager** で **Add package from git URL...** を選び、次を入力します。

```text
https://github.com/nao40031/Art-Net-DMX-Lighting-for-Unity.git?path=/Packages/jp.oshino.art-net-dmx-lighting-for-unity#v0.1.9
```

特定バージョンで固定して利用する場合に使います。更新時は、必要なバージョンのタグへURLを差し替えてください。

## まず試す

`Runtime/LightAsset/yamakara Light_やまかライト/Prefab/HDRP` 内のPrefabは、VLBなしで使用するHDRP版です。シーンに配置して `DmxFixtureComponent` のFixture・Universe・Start Addressを設定してください。

1. `Runtime/ArtNet/Prefabs/ArtNet.prefab` をHierarchyへドラッグ＆ドロップします。
2. 標準Prefabを配置し、`DmxFixtureComponent` のDMX設定を確認します。
3. `ArtNet Core` が灯体を自動検出して、ライブArt-Netを適用します。

`ArtNet.prefab` には、通常の受信を担当する `ArtNet Core`、DMXをAnimationClipへ記録する `ArtNet Recorder`、AnimationClipまたはTimelineを再生する `ArtNet Timeline Playback` が含まれます。初期状態は **Live / Record** プリセットです。Timeline再生を行うときは、`DmxTimelinePlayback` Inspectorの **Apply Timeline Playback** を実行してください。

## Timelineで複数Universeを再生する

`ArtNet Timeline Playback/Universes/Universe 0` は、Universe 0用の `ArtNetChannels` と `Animator` を持ちます。Universeを追加する場合は、`Runtime/ArtNet/Prefabs/ArtNet Universe.prefab` を `Universes` の子へ配置し、`ArtNetChannels` の **Universe** を重複しない番号へ設定してください。次に `DmxTimelinePlayback` Inspectorの **Auto Discover in Children** を押して、Sourcesへ登録します。Universe番号が重複している場合はSourcesを変更せず、Consoleとポップアップに該当Object名を表示します。

## VLB版（任意）

VLB版Prefabはパッケージ本体に同梱されています。以下のPrefabにはVLB SD / HDと必要な設定があらかじめ適用済みです。Volumetric Light Beam（VLB）を正規の配布元から導入済みのプロジェクトでは、そのままシーンへ配置して使用できます。

```text
Packages/Art-Net DMX Lighting for Unity/Runtime/LightAsset/yamakara Light_やまかライト/Prefab/HDRP/VLB/MovingLight_withGobo(MAC Ultra)_VLB_HDRP.prefab
Packages/Art-Net DMX Lighting for Unity/Runtime/LightAsset/yamakara Light_やまかライト/Prefab/URP/VLB/MovingLight_withGobo(MAC Ultra)_VLB(SD)_URP.prefab
Packages/Art-Net DMX Lighting for Unity/Runtime/LightAsset/yamakara Light_やまかライト/Prefab/URP/VLB/MovingLight_withGobo(MAC Ultra)_VLB(HD)_URP.prefab
Packages/Art-Net DMX Lighting for Unity/Runtime/LightAsset/yamakara Light_やまかライト/Prefab/URP/VLB/MovingLight_withGobo(L3Spot)_VLB(SD)_URP.prefab
Packages/Art-Net DMX Lighting for Unity/Runtime/LightAsset/yamakara Light_やまかライト/Prefab/URP/VLB/MovingLight_withGobo(L3Spot)_VLB(HD)_URP.prefab
Packages/Art-Net DMX Lighting for Unity/Runtime/LightAsset/ParLight/Prefab with Lights/HDRP/VLB/ParLight_ver12_WithLigh&Dmx_VLB_HDRP.prefab
Packages/Art-Net DMX Lighting for Unity/Runtime/LightAsset/ParLight/Prefab with Lights/URP/VLB/ParLight_ver12_WithLigh&Dmx_VLB_URP.prefab
```

VLBはこのパッケージの依存関係・同梱物ではありません。VLBを導入せずにVLB版Prefabを配置するとMissing Scriptが表示される可能性があります。VLBなしでも、`Prefab/HDRP` のHDRP版PrefabとArt-Net/DMX機能は利用できます。

通常Prefabや独自Prefabへ新たにVLBを追加する場合だけ、`Art-Net > VLB > URP Setup` を使用します。Setup TargetへPrefab AssetまたはルートGameObjectを登録し、SD / HDを選択して **Apply VLB SD/HD to Target Lights** を実行してください。上記のVLB版Prefabに対して、この操作を行う必要はありません。

### Beam DMX Sync

`DmxFixtureComponent` の **Beam Render** には、Light / Pseudo Beam / VLBで共通のDMX同期設定があります。現在の **Beam Render Mode** に応じて対象表現へ適用されます。

- **Sync Beam Color To Dmx**: OFFではPrefabに保存された初期色を維持します。ParLight VLB Prefabは暖色初期値を使うためOFFです。
- **Sync Beam Dimmer To Dmx**: DimmerによるLight・ビーム光量の同期。
- **Sync Beam Gobo To Dmx** / **Sync Beam Gobo Rotation To Dmx**: Light Cookie、Pseudo Beam、VLB Cookieへのゴボ同期。
- **Sync Beam Zoom To Dmx**: Spot Angleとビーム形状の同期。
- **Sync Beam Prism To Dmx**: Pseudo BeamまたはVLBのプリズム表現の同期。Normalモードでは効果はありません。

### Gobo Lens Material Setup

ゴボ付きムービングライトでは、レンズ面へDMXの色・ディマー・ゴボテクスチャ・回転を反映できます。HDRPとURPにはそれぞれ専用の共有マテリアルがあり、実行中のマテリアル差し替えは行いません。必要な場合だけ、Prefabまたはシーン上の灯体を選択して事前セットアップしてください。

1. ゴボ対応の `DmxFixtureComponent` を選択します。
2. Inspectorの **Lens (ShaderGraph DMX Sync)** を開き、**Lens Gobo** の各設定を確認します。
3. **Gobo Lens Material Setup** の **Setup Gobo Lens Material** を押します。
   - 現在のRender Pipelineを自動判定し、HDRPでは `GoboLensSurface_HDRP`、URPでは `GoboLensSurface_URP` を登録済みRendererスロットへ割り当てます。
   - 元のマテリアルは **Original Material** に保存され、必要に応じて **Restore Original Materials** で戻せます。
4. **Validate Gobo Lens Setup** を押し、現在のパイプラインに合うマテリアルが設定されていることをConsoleで確認します。

同梱の `MovingLight_withGobo` Prefab（HDRP 3種、URP 4種）はあらかじめ設定済みです。**Lens Material Bindings** に `Assigned Gobo Lens Material` が表示されていれば割り当て済みで、未設定時は `None (Material)` と表示されます。

**Lens Gobo** の主な調整項目:

- **Gobo Lens Emission**: レンズの発光強度。
- **Gobo Lens Scale**: レンズUV内でのゴボ模様の大きさ。`1` が基準です。
- **Lens Gobo Blur**: ゴボ模様のぼかし。`0` はシャープで、範囲は `0–0.02` です。
- **Gobo Lens Horizontal / Vertical Offset**: レンズUV中央を基準にした模様位置の手動調整。
- **Lens Gobo Rotation Direction**: レンズ面の回転方向。`Normal` は床面投影と同方向、`Reverse` は逆方向です。
- **Lens Prism Gobo Mode**: プリズム有効時、Facet数・拡散量・プリズム回転に追従して、レンズ面のゴボを縮小コピーします。コピー間隔は **Prism Gobo Spacing Mode**（Manual / Auto）と **Manual Prism Gobo Spacing Scale** の最終倍率に連動します。ONが既定です。OFFでは単一ゴボ表示を維持します。
- **Lens Aperture Feather**: レンズ開口の端に近づくほどゴボ模様を弱める幅。`0` ではメッシュ境界で明確に見切れます。
- **Gobo Lens Hotspot Strength**: ゴボ有効時にもレンズ中央へ残すハイライト強度。

#### English summary

Use **Setup Gobo Lens Material** to assign the correct shared lens material for the active HDRP or URP pipeline. The operation is an Editor-time setup only; no material replacement occurs at runtime. Use **Validate Gobo Lens Setup** to verify assignments, and **Restore Original Materials** to revert them. Included `MovingLight_withGobo` prefabs are already configured.

## Editor Tools

### Generic Prefab Replacer

`Tools > Generic > Replace Targets With Template...` は、シーン内の対象をPrefab Assetまたはシーン上のテンプレートへ一括置換します。名前の連番またはRoot配下の条件で対象を抽出でき、必要に応じて子Transformのローカル位置・回転・スケールを引き継げます。

置換処理は元のGameObjectを削除して新しいインスタンスを生成するため、他のComponentやシーンオブジェクトから参照されている場合は参照が切れる可能性があります。実行前にシーンを保存し、置換結果を確認してください。

**English:** Use `Tools > Generic > Replace Targets With Template...` to replace matching scene objects with a Prefab Asset or scene template. The operation deletes the original objects, so references from other objects may be lost.

### MagicQ Calibration CSV Exporter

Hierarchyで灯体または親オブジェクトを選択し、`Tools > MagicQ > Export MagicQ Calibration CSV...` を実行すると、UnityのTransform情報をMagicQ向けCSVへ出力できます。座標スケール、Z反転、World/Local回転、並び順、デフォルトのManufacturer・Model・Modeを出力前に変更できます。

初期値の `Betopper`、`LM70S`、`9ch` はサンプル値です。使用する灯体に合わせてExportウィンドウで変更してください。設定はEditorPrefsへ保存されます。

**English:** Select fixtures or their parent in the Hierarchy, then use `Tools > MagicQ > Export MagicQ Calibration CSV...`. The default `Betopper`, `LM70S`, and `9ch` values are editable examples and are saved in EditorPrefs.

## 前提

- Unity 6000.0以降
- HDRP 17.0.4以降（パッケージ依存関係として自動導入されます）
- Timeline再生機能を利用する場合はUnity Timeline

## ライセンス

本パッケージ内のスクリプトおよびUnityアセットはMIT Licenseです。VLBおよびその他の外部アセットは、それぞれのライセンスに従ってください。
