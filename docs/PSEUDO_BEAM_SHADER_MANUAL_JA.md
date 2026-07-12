# Pseudo Beam Shader 使用マニュアル

> **対象**: ArtNet for Unity の `Pseudo Beam Shader` モード  
> **用途**: Volumetric Light Beam（VLB）を使わずに、ムービングライトのビーム表現を軽量に描画する

---

## 1. Pseudo Beam Shader とは

`Pseudo Beam Shader` は、円すい形のMeshに加算合成Shaderを適用して、光の筋を疑似的に描く方式です。  
`DmxFixtureComponent` がDMX値を読み取り、色・明るさ・ゴボ・ズーム・プリズムをビームへ反映します。

### できること

- DMXのColor／Dimmerによる色と明るさの同期
- Goboの投影・回転・オフセットの同期
- Zoomに合わせたビーム先端半径の変更
- Prismの分岐、拡がり、回転、明るさの反映
- 2D手続きノイズ、または3D Noise Textureによる霧・揺らぎ表現
- Soft Shellによる外周のにじみ表現

### 制約

- 実際の空間散乱、遮蔽、ボリュームシャドウを計算する方式ではありません。
- 透明・加算合成のため、カメラ角度や重なり方で見え方が変わります。
- 多数の灯体でPrismやSoft Shellを有効にすると、生成されるRenderer数が増えます。

### 描画モードの使い分け

| モード | 向いている用途 |
| --- | --- |
| `Normal` | 通常のLight/Lens表現だけを使う |
| `Pseudo Beam Shader` | 軽量にビームの存在感を出したい。DMXのGobo・Prismも見せたい |
| `Volumetric Light Beam` | VLB導入済みで、VLB固有のボリューム表現を使いたい |

> **重要**: 本マニュアルの設定は `Beam Render Mode = Pseudo Beam Shader` のときに使用します。VLBモードでは `Pseudo Beam Renderer` は使用されません。

---

## 2. 必要なアセット

| 種類 | 標準パス | 役割 |
| --- | --- | --- |
| Shader | `Assets/ArtNet/Runtime/Resources/ArtNet/PseudoBeam.shader` | `ArtNet/Pseudo Beam` Shader本体 |
| Material | `Assets/ArtNet/Materials/PseudoBeam.mat` | ビームの基本見た目を保持するMaterial |
| コンポーネント | `Assets/ArtNet/Runtime/PrismPseudoBeamCone.cs` | Add Componentでは `ArtNet/Pseudo Beam Cone` として表示。円すいMeshを生成・更新する |
| DMX連携 | `Assets/ArtNet/Runtime/DmxFixtureComponent.cs` | DMX値をMaterialPropertyBlockでビームへ反映する |

---

## 3. 最短セットアップ

### 3-1. ビーム用GameObjectを用意する

1. ムービングライトのビーム発射位置に空のGameObjectを作成します。
2. 発射方向がローカル **+Z方向** になるよう向きを合わせます。
3. `ArtNet/Pseudo Beam Cone` を追加します。
4. `PseudoBeam.mat` を `Material` に割り当てます。

`Pseudo Beam Cone` は、必要な `MeshFilter` と `MeshRenderer` を自動で要求します。Meshは実行時／編集時に生成されます。

### 3-2. 円すい形状を設定する

`Pseudo Beam Cone` の **Shape** を設定します。

| 項目 | 意味 | 初期の目安 |
| --- | --- | --- |
| `Length` | ビームの長さ | 10 |
| `Start Radius` | 発射根元の半径 | 0.05 |
| `End Radius` | 先端の半径 | 1.5 |
| `Segments` | 円周の分割数 | 32 |
| `Cap Start` / `Cap End` | 端面を塞ぐか | 通常はOFF |

> **推奨**: まずは `Length = 10`、`Start Radius = 0.05`、`End Radius = 1.5`、`Segments = 32` で開始します。遠景中心ならSegmentsを下げると負荷を抑えられます。

### 3-3. DmxFixtureComponentへ割り当てる

灯体の `DmxFixtureComponent` で、次を設定します。

1. **Beam Render > Beam Render Mode** を `Pseudo Beam Shader` にします。
2. **Pseudo Beam Shader > Pseudo Beam Renderer** に、手順3-1で作成した `MeshRenderer` を割り当てます。
3. 同じビームを複数表示したい場合だけ、**Extra Pseudo Beam Renderers** に追加します。
4. `Sync Pseudo Beam Color To Dmx` と `Sync Pseudo Beam Dimmer To Dmx` をONにします。

これで、FixtureDefinitionが持つDMXの色とDimmerがビームに反映されます。

> **スクリーンショット配置案 1**: `DmxFixtureComponent` の **Beam Render** と **Pseudo Beam Shader** を開いたInspector画面。

---

## 4. DMX連携の設定

### 基本同期

| 項目 | ONにした場合 | 主なShaderプロパティ |
| --- | --- | --- |
| `Sync Pseudo Beam Color To Dmx` | DMXの色を反映 | `_DmxColor` |
| `Sync Pseudo Beam Dimmer To Dmx` | DMXのDimmerを反映 | `_DmxDimmer` |
| `Sync Pseudo Beam Prism To Dmx` | Prismの分岐・回転などを反映 | `_DmxPrism...` |
| `Sync Pseudo Beam Zoom To Dmx` | Zoomから先端半径を計算 | `_BeamEndRadius` |
| `Sync Pseudo Beam Noise Volume To Beam` | 3Dノイズ関連の値を反映 | `_BeamNoiseVolume...` |

標準Shader `ArtNet/Pseudo Beam` を使う場合、各プロパティ名は初期値のまま変更しません。

### Dimmerの補正

| 項目 | 用途 |
| --- | --- |
| `Pseudo Beam Dimmer Scale` | DMX Dimmerに掛ける倍率。全体を強く／弱くしたいときに調整 |
| `Pseudo Beam Dimmer Floor` | 暗転時にも残す明るさの下限。通常は `0` |

> **注意**: `Dimmer Floor` を上げると、DMXのDimmerを0にしてもビームが完全には消えません。演出意図がない限り `0` のままにします。

### Zoomの補正

Zoom同期がONの場合、LightのOuter Spot Angleからビーム先端半径が計算されます。

| 項目 | 用途 |
| --- | --- |
| `Pseudo Beam Zoom Radius Scale` | Zoom由来の先端半径に掛ける倍率 |
| `Pseudo Beam Zoom Min End Radius` | 先端半径の下限 |
| `Pseudo Beam Zoom Max End Radius` | 先端半径の上限 |

ビームの太さがLightの照射角と合わない場合は、まず `Zoom Radius Scale` を調整し、必要に応じて最小・最大値を見直します。

---

## 5. Materialの見た目調整

`PseudoBeam.mat` を複製して灯体・演出ごとに使い分けると、元の基準Materialを保てます。

### 基本の明るさと輪郭

| プロパティ | 役割 | 調整の方向 |
| --- | --- | --- |
| `Beam Intensity` | Material側の基礎強度 | 高いほど明るい |
| `Beam Start Fade` | ビーム開始側のフェード位置 | 大きいほど根元側が薄くなる |
| `Beam End Fade` | ビーム終端側のフェード位置 | 小さいほど早く消える |
| `Beam Falloff Power` | 長さ方向の減衰カーブ | 大きいほど減衰が急になる |
| `Beam Edge Softness` | 外周を柔らかくする度合い | 高いほど輪郭が柔らかい |
| `Beam Edge Power` | 外周の減衰カーブ | 高いほど輪郭が締まる |
| `Beam Tip Opacity` | 先端部の見え方 | 必要な場合だけ微調整 |

### 手続きノイズ

| プロパティ | 役割 |
| --- | --- |
| `Beam Noise Strength` | 2D手続きノイズの強さ |
| `Beam Noise Scale` | 模様の細かさ。大きいほど細かい |
| `Beam Noise Speed` | 流れる速度 |
| `Beam Noise Contrast` | 濃淡の強さ |

細かなちらつきが気になる場合は、まず `Beam Noise Strength` を下げます。速度を上げすぎると、霧ではなく模様が流れて見えやすくなります。

### Gobo

`Sync Beam Gobo To Dmx` がONで、FixtureDefinition側でGoboが有効なとき、次のプロパティへDMX値が反映されます。

| プロパティ | 役割 |
| --- | --- |
| `_GoboTexture` | Gobo Texture |
| `_GoboRotationDeg` | 回転角度 |
| `_GoboOffset` | UVオフセット |
| `_GoboEnabled` | Goboの有効状態 |

Material側では `Gobo Influence`、`Beam Gobo Influence`、`Beam Gobo Contrast`、`Beam Gobo Min Light` を使い、模様の強さや暗部のつぶれ方を調整します。

---

## 6. 3D Noise Textureで霧感を追加する

3D Noise Textureを使うと、ビーム断面と長さ方向の両方に濃淡を付けられます。

### 仕組み

標準Shaderは、コーンMeshのローカル座標を次のように扱います。

- **ビーム長方向**: `z / Beam Length` を `0〜1` の深度として使います。
- **断面方向**: その深度におけるビーム半径で `x / y` を正規化し、断面のUVとして使います。
- **3Dノイズ座標**: 「断面UV × Radial Scale」と「深度 × Length Scale」を組み合わせてTexture3Dを参照します。
- **時間変化**: Scroll Direction × Speed × 時間を3Dノイズ座標に加算します。

このため、Texture3Dを使うと単に表面の模様を流すのではなく、ビームの太さと長さに沿った霧の密度むらを作れます。`Beam Noise Strength` は最終的なノイズの掛かり具合、`Noise Volume Strength` は手続きノイズから3Dノイズへ置き換える割合です。

### 設定手順

1. `Pseudo Beam Noise Volume` に `Texture3D` を設定します。
2. `Pseudo Beam Noise Volume Enabled` をONにします。
3. `Strength`、`Scale`、`Radial Scale`、`Length Scale` を調整します。
4. 必要に応じて `Speed`、`Offset`、スクロール方向を設定します。

### 主な項目

| 項目 | 効果 |
| --- | --- |
| `Strength` | 3Dノイズを混ぜる強さ |
| `Gobo Scale` | Gobo表示中だけ3Dノイズの強さへ掛ける倍率 |
| `Prism Rotation Scale` | Open GoboでPrism公転中だけ3Dノイズの強さへ掛ける倍率 |
| `Scale` | 全体の模様の細かさ |
| `Radial Scale` | 断面方向の細かさ |
| `Length Scale` | 長さ方向の細かさ |
| `Speed` | スクロール速度 |
| `Contrast` | 濃淡の強さ |
| `Offset` | 灯体ごとの模様ずらし |
| `Scroll Space` | Local（灯体に追従）／World（世界座標を維持） |
| `Scroll Axis` | X／Y／Z軸 |
| `Scroll Reverse` | スクロール方向の反転 |

> **実用的な初期値**: `Strength = 1`、`Scale = 1`、`Radial Scale = 4`、`Length Scale = 12`、`Speed = 0.1`。模様が細かすぎる場合は各Scaleを下げます。

### フォグ感を作る調整順

1. **まずTexture3Dを確認する**: コントラストが極端すぎない、繰り返しても継ぎ目が目立ちにくいノイズを使います。
2. **大きさを決める**: `Scale = 1` のまま `Radial Scale` と `Length Scale` を調整します。値を上げるほど模様は細かくなります。
3. **濃淡を決める**: `Contrast` を上げると霧の切れ目がはっきりします。最初は `1` から少しずつ上げます。
4. **混ざり方を決める**: `Strength` を上げて3Dノイズ寄りにします。表面の細かな揺れも残したい場合は、Materialの `Beam Noise Strength` を低〜中程度に残します。
5. **動きを付ける**: `Speed` は `0.05〜0.2` 程度から開始します。速すぎると煙ではなくテクスチャのスクロールに見えます。
6. **灯体ごとの差を付ける**: 同じノイズが全灯体で同期して見える場合は、`Offset` を灯体ごとに少し変えます。

### フォグ表現レシピ

| 目的 | 目安 | 補足 |
| --- | --- | --- |
| 薄い会場ヘイズ | Strength 0.35〜0.6、Contrast 1〜2、Speed 0.05〜0.1 | 濃淡を控えめにしてビームの形を保つ |
| 煙の流れを見せる | Strength 0.7〜1、Contrast 2〜4、Speed 0.1〜0.2 | `Length Scale` を高めにすると長さ方向の変化が増える |
| 太いビームの霧むら | Radial Scale 1〜3、Length Scale 4〜10 | 大きな塊を作りやすい |
| 細いビームの細かな揺らぎ | Radial Scale 4〜8、Length Scale 10〜16 | 細かすぎてちらつく場合はScaleを下げる |

### Gobo・Prismとノイズの使い分け

- **Gobo表示中**: `Gobo Scale` は、Goboが有効なときだけ3Dノイズ強度へ掛かる倍率です。ゴボ模様を優先したい場合は下げ、霧感を保ちたい場合は `1` に近づけます。
- **Open Gobo + Prism回転中**: `Prism Rotation Scale` は、Prism公転中だけ3Dノイズ強度へ掛かる倍率です。模様が回転に引きずられて見える場合は `0` にします。標準値も `0` です。
- **Local / World**: 灯体の向きに合わせて霧を流したいときは `Local`、ステージ空間に固定した風向きを見せたいときは `World` を選びます。

> **スクリーンショット配置案 2**: 3D Noise Textureを割り当てたInspectorと、ON/OFF比較のGameビュー。

---

## 7. Prism

`Sync Pseudo Beam Prism To Dmx` をONにすると、FixtureDefinitionのPrism状態をビームへ反映します。

### 仕組み

Pseudo BeamのPrism機能は、**分岐の配置**と**断面のFacet表現**を分けて処理します。

1. `Pseudo Beam Renderer` をテンプレートとして使用します。
2. Prismが有効になると、必要な数の `PseudoBeamFacet` を実行時に生成します。
3. 各Facetはテンプレートと同じMesh・Materialを共有し、円周上に等間隔で並びます。
4. `Spread` をX/Y方向の回転角へ変換して、各Facetのビーム方向を外側へ振ります。
5. `Rotation` はFacet全体の並びを回転させます。
6. Shader側では、円すいの円周UVにFacetマスクを描き、`Facet Sharpness` で分岐ビームの輪郭感を補助します。

Prism表示中はテンプレートRendererを非表示にし、生成された分岐ビームを表示します。各Facetの明るさは、分岐数に応じて配分されます。

```text
DMX Prism値
  ├─ C# : Facet数を確保し、各コーンをSpread/Rotationで配置
  └─ Shader : 各コーンの円周方向にFacetマスクを適用
                 ↓
           分岐したPseudo Beamとして描画
```

| DMX連携項目 | 内容 |
| --- | --- |
| `Prism Enabled` | Prismの有効／無効 |
| `Prism Facet Count` | 分岐数 |
| `Prism Spread` | 分岐の拡がり |
| `Prism Rotation` | 分岐の回転 |
| `Prism Intensity` | 分岐ビームの強度 |

### Shaderパラメータ

| パラメータ | 役割 | 調整の方向 |
| --- | --- | --- |
| `_DmxPrismEnabled` | Shader内のFacetマスクの有効化 | 通常はDMX同期に任せる |
| `_DmxPrismFacetCount` | Shader内で扱うFacet数 | 標準Shaderでは1〜8に丸められる |
| `_DmxPrismSpread` | Facetマスクの幅にも影響するSpread値 | 高いほど各Facetは細く分かれて見える |
| `_DmxPrismRotation` | 円周方向のFacetマスクの回転 | DMX同期に任せる |
| `_DmxPrismIntensity` | ビーム強度へ掛かる倍率 | 高いほど明るい |
| `_FacetSharpness` | 円周Facetマスクの鋭さ | 高いほど境界が締まる |

> **注意**: `_DmxPrismFacetCount` はShaderでは最大8ですが、実際に生成できる補助Facet数は `DmxFixtureComponent` 側の上限にも従います。多数分岐の見た目・負荷は、FixtureDefinitionとComponent設定を合わせて確認してください。

### 調整のコツ

- まずFacet Countを少なくして、想定どおり分岐することを確認します。
- 分岐が広すぎる場合は、FixtureDefinitionのPrism Spreadまたは関連するDMX値を見直します。
- 分岐の根元が硬く見える場合は `Facet Sharpness` を下げ、ぼやけすぎる場合は上げます。
- Open GoboでPrismを回す場合、3Dノイズが不自然に見えれば `Pseudo Beam Noise Volume Prism Rotation Scale` を下げます。標準値は `0` です。

> **スクリーンショット配置案 3**: Prism OFF／Facet Count 3／Facet Count 6 のGameビュー比較と、FixtureDefinition側のPrism値。

---

## 8. Soft Shell（にじみ）

`Enable Pseudo Beam Soft Shell` をONにすると、メインビームの外側に薄い補助ビームを自動生成します。輪郭が硬く見える場合に有効です。

| 項目 | 役割 | 初期の目安 |
| --- | --- | --- |
| `Radius Scale` | 補助ビームの半径倍率 | 1.35 |
| `Intensity` | 補助ビームの強度 | 0.2 |
| `Edge Softness` | 補助ビームの輪郭の柔らかさ | 0.9 |
| `Noise Strength` | 補助ビームのノイズ強度 | 0.25 |

> **注意**: Soft Shellはビームごとに追加Rendererを使います。灯体数が多い場面では、必要な灯体だけでONにしてください。

---

## 9. 推奨レシピ

### A. まず動かすための基本ビーム

- Beam Render Mode: `Pseudo Beam Shader`
- Color / Dimmer / Zoom同期: ON
- Prism / Noise Volume / Soft Shell: OFF
- Cone: Length 10、Start Radius 0.05、End Radius 1.5、Segments 32
- Material: Beam Intensity 0.4前後から開始

### B. 薄いヘイズのある会場風

- 基本ビームに加え、`Beam Noise Strength` を低めに設定
- 3D Noise Textureを使用する場合はSpeedを低く保つ
- Soft ShellをONにし、Intensityは0.1〜0.2程度から開始

### C. Gobo・Prismを見せる演出

- `Sync Beam Gobo To Dmx`: ON
- `Sync Pseudo Beam Prism To Dmx`: ON
- Goboの暗部が強すぎる場合は、Materialの `Beam Gobo Min Light` を上げる
- Prism回転時にノイズが目立つ場合は、`Prism Rotation Scale` を0に近づける

---

## 10. トラブルシューティング

### ビームが表示されない

1. `Beam Render Mode` が `Pseudo Beam Shader` か確認します。
2. `Pseudo Beam Renderer` に正しい `MeshRenderer` が割り当てられているか確認します。
3. RendererのMaterialが `ArtNet/Pseudo Beam` Shaderを使用しているか確認します。
4. Coneのローカル+Z方向が発射方向を向いているか確認します。
5. DMX Dimmerが0ではないか、`Sync Pseudo Beam Dimmer To Dmx` がONか確認します。

### 色や明るさがDMXに追従しない

- `Sync Pseudo Beam Color To Dmx`／`Sync Pseudo Beam Dimmer To Dmx` を確認します。
- `Pseudo Beam Color Property` が `_DmxColor`、`Pseudo Beam Dimmer Property` が `_DmxDimmer` になっているか確認します。
- カスタムShaderを使う場合は、そのShader側のプロパティ名に合わせます。

### Zoomしても太さが変わらない

- `Sync Pseudo Beam Zoom To Dmx` をONにします。
- LightのZoom設定とOuter Spot Angleが更新されているか確認します。
- `Pseudo Beam Zoom Radius Scale` が0ではないか確認します。

### Goboが表示されない／見えにくい

- `Sync Beam Gobo To Dmx` をONにします。
- FixtureDefinitionのGobo設定と、現在のDMX値を確認します。
- `Gobo Influence` と `Beam Gobo Influence` を上げます。
- 暗部がつぶれる場合は `Beam Gobo Min Light` を少し上げます。

### Prismが増えない、または負荷が高い

- `Sync Pseudo Beam Prism To Dmx` をONにします。
- FixtureDefinitionのPrism定義・DMX値・Facet Countを確認します。
- Prism有効時は分岐数に応じてRendererが増えます。遠景灯体や多数の灯体ではFacet Countを抑えます。

### 3Dノイズが効かない

- `Pseudo Beam Noise Volume` にTexture3Dが設定されているか確認します。
- `Pseudo Beam Noise Volume Enabled` と `Sync Pseudo Beam Noise Volume To Beam` をONにします。
- MaterialのShaderプロパティ名が標準値と一致しているか確認します。

---

## 11. パフォーマンスの注意

- 通常時は、基本的に1灯体あたり1つのビームRendererです。
- Extra Pseudo Beam Renderers、Prism、Soft Shellを有効にするとRenderer数が増えます。
- 多数の灯体では、遠景の灯体でSegmentsを下げる、Soft Shellを限定する、PrismのFacet数を抑えることを検討します。
- 3D Noise Textureは、必要な演出でのみ有効にします。

---
