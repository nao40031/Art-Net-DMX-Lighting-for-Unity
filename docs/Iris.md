# Iris

## 設計とZoomとの違い

Irisは開口の外周を遮り、ゴボの倍率を変えずに照射範囲を絞ります。Zoomは投影角とゴボの倍率を変えます。この実装ではUnity LightのSpot AngleはIrisから変更しません。両者は同時に使用できます。

機種固有のチャンネル番号・DMX範囲はFixture Definition、光学的な最小径・動作速度はIris Profileに分離しています。MAC Ultra専用分岐はランタイムにありません。既存FixtureにProfileを設定しない限り、Irisは従来の描画を変更しません。

## 設定

1. ProjectのCreate > ArtNet > DMX > Iris Profileでプロファイルを作成します。
2. Fixture Definitionの`Iris Profile`へ割り当てます。個体差を持たせる場合はDmx Fixture Component側の`Iris Profile`で上書きできます。
3. Fixtureの使用ModeのChannel Elementsで該当チャンネルを`Attribute = Iris`、`Instance = 1`、`Role = Value`または`Position`にします。
4. 8bitは`Byte Role = Single`。16bitは上位／下位の各チャンネルを`Coarse`／`Fine`にし、Attribute・Instance・Roleを一致させます。範囲はCoarse側に16bit整数で登録します。
5. Dmx Fixture Componentの`Sync Iris To DMX`を有効にします。複数Iris定義がある場合は`Iris Instance`を選択します。
6. 既存のRig／TimelineからDMXを入力します。パルスと移動時間の確認はPlay Modeで行います。

### 範囲と動作

| Range Type | 動作 |
|---|---|
| None / Indexed | 範囲を開度に変換。正規化0=最小、1=全開 |
| Open / Closed | 全開／最小開口 |
| Pulse / Iris Pulse Reverse | Profileの波形を順方向／逆方向に再生 |
| Iris Hold | 現在の開度を保持 |
| No Function | ProfileのHold / Open / Position Channelに従う |

`Mapping Preset = Inverted`で方向を反転できます。明示範囲がない場合だけ`Invert Unmapped Position`が使われます。未定義の範囲や未対応Macroは保持し、未知の値を勝手に開度へ変換しません。`SelectMode`を別チャンネルにした構成と、`Speed`による独立速度指定にも対応します。

### Profile

- `Minimum Diameter`: 全開径に対する最小径。0で完全遮光。初期値0.05は仮の値です。
- `Aperture Curve`: 正規化開度から実際の開度への応答曲線。
- `Opening / Closing Seconds`: 全行程の所要時間。0なら即時。
- `Minimum / Maximum Hz`, `Speed Curve`: パルス速度。初期値0.2–3 Hzは仮の値です。
- `Pulse`, `Pulse Aperture`: 1周期の波形と開度範囲。逆パルスは時間方向を反転します。初期波形は実機測定値ではありません。
- `Feather`: Cookie境界のぼかし。Focusの再現ではありません。
- `Blades`: 0=円形、3–32=正多角形近似。機械的な羽根の3D再現ではありません。
- `Lens Influence`: 対応レンズシェーダーへの見た目の反映量。初期値0。レンズのSync Lens To DMXも必要です。
- `Cookie Resolution`: 64–1024、実際には2の累乗。変化したときだけGPUでCookieを再合成します。

## 描画方式

| モード | 実装 |
|---|---|
| 通常Light（URP / HDRP） | 既存ゴボ／Cookie × Irisマスク。対象はSpot Light |
| Pseudo Beam（URP / HDRP） | 円／多角形の開口に合わせてビーム頂点を縮小。ゴボ参照範囲も連動し、Zoom倍率と分離 |
| VLB HD（URP / HDRP） | Lightに適用した合成CookieをVolumetricCookieHDへ反映。遮光はDimmerとは独立 |
| VLB SD（URP / HDRP） | SDにCookie機能がないため、円形開口をビーム角と発光元半径で再現。Unity Lightの投影角は維持 |

VLBは任意依存で、未導入でもランタイムのコンパイルに必須ではありません。HDではCookie機能がVLB設定で有効である必要があります。SDでは多角形・Cookie内のゴボ遮蔽・CookieのFeatherを体積ビームに再現できません（床面の通常Light投影は合成Cookieで遮蔽します）。精密なゴボ付き体積表現にはHDを使用してください。Pseudo Beamも既存のメッシュ式近似であり、実光線追跡ではありません。

プリズムの補助Spot Lightにも個別のIrisマスクを適用します。SDのプリズム体積ビーム生成は従来実装の範囲のままで、新たなHDへの自動変換は行いません。

RenderTexture/Materialはライトごとに再利用し、入力テクスチャ・開度・形状が変わった場合のみ描画します。256×256 ARGB32の出力は約256 KiB/ライト（深度・Mipなし）です。パルス中は毎フレーム、補助ライトを含む出力数分のBlitが発生します。無効化・破棄時に元のCookieを戻し、生成リソースを解放します。

## 参考データ

`Tools > ArtNet > Iris > Create Reference Definitions`で`Assets/ArtNetIrisExamples`に設定例を作成できます。既存ファイルは上書きしません。

- Generic: CH1の8bit、CH1/2の16bit。0=最小、最大値=全開。
- MAC Ultra参考: Basic CH24、Extended CH24/25。これは**Irisチャンネルだけの定義**で、完全なMAC Ultra Fixtureではありません。使用中のFixtureを複製し、対応するIris要素とProfileを転記してください。

| 動作 | Basic | Extended |
|---|---|---|
| 全開→最小 | 0–200 | 0–51400 |
| Pulse 高速→低速 | 201–225 | 51401–57825 |
| Hold | 226–230 | 57826–59110 |
| Reverse Pulse 低速→高速 | 231–255 | 59111–65535 |

この範囲は共有されたMAC Ultra Performance manualを参考にしています。最小径、Hz、羽根数、波形は取扱説明書から確定できないため調整値です。別機種にはその機種の範囲・方向・速度を設定してください。

## 検証とトラブルシュート

`Tools > ArtNet > Iris > Run Validation`で、制御境界・DMX受信・Cookieキャッシュ／復元・GPUマスクのテストを実行できます。一時GameObjectは終了時に破棄します。バッチ実行は`-executeMethod ArtNet.Editor.IrisValidation.Run`（GPUが必要、`-nographics`不可）。

視覚確認はURP/HDRP × Normal/Pseudo Beam/VLBの6構成で、ゴボなし／あり、全開／中間／最小、Zoom固定／変化、プリズム、Dimmer=0、無効化を確認してください。Irisだけを操作したときはゴボの模様が拡縮せず外周が切れ、Zoom操作では模様ごと拡縮することが基準です。

効かない場合はProfile未設定、Mode/Instance/Role/開始アドレス、レンダラのCookie設定、独自Pseudo Beamマテリアルに`_IrisShape`対応があるかを確認します。Point/Directional/Area Lightの開口は対象外です。

Timelineの入力経路も共通ですが、パルス位相はランタイムの経過時間です。任意時刻へのシークで過去のパルス位相を完全再現する機能は含みません。

### 今回の検証環境

Unity 6000.0.48f1。ランタイムC#はHAS_HDRP有効／無効の両条件、変更したエディタC#もコンパイル確認。制御・DMX入力結合・GPU Cookieの40項目をTemp内の独立プロジェクトで検証しました（EditModeではOnDisableを明示呼び出し）。元プロジェクト全体のバッチ実行は、AssetsとPackagesに重複する既存PrefabOverrideReverterWindowのコンパイルエラーにより停止しました。この無関係な重複は変更していません。6構成の実シーン目視検証は未完了です。

このreleaseブランチでは、ランタイム実装は`Assets/ArtNet/Runtime`、設定例生成・検証メニューとInspectorは`Assets/ArtNet/Editor`にあります。主な追加ファイルは`IrisProfile.cs`、`IrisController.cs`、`IrisCookie.cs`、`DmxFixtureIris.cs`とIrisシェーダーです。UPM構成のブランチでは同じ実装をパッケージ内に配置しています。
