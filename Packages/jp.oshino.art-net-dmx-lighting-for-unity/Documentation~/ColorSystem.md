# CMY・色温度・Color Wheel

この機能は特定の灯体のチャンネル表をコードへ固定しません。Fixture Type の Mode に DMX チャンネルを、Color Mix Profile と Color Wheel Profile に光学特性を定義します。既存の Fixture Type / Prefab に新しい Profile を設定しなければ、従来の RGB・CMY・CTC の近似挙動を維持します。

## 何を表すか

- CMY は白色光から各色成分を減らす独立したフィルターです。Cyan は赤、Magenta は緑、Yellow は青を主に減らします。最大挿入時の透過色と DMX 応答を各 Profile で調整できます。
- CTO は暖色側、CTB は寒色側の補正フィルターです。CTC は機種ごとに意味が異なるため、色フィルターとしての応答か、DMX 値から絶対色温度（K）への割当を Profile で選びます。DMX 値から Kelvin を自動推定しません。
- Color Wheel は離散的な色スロットを持つ機械式ホイールです。Open、固定色、2色の境界、連続色、正逆回転、ランダム、停止を DMX 範囲ごとに定義できます。

処理順序は RGB または CMY → White 加算 → CTC / CTO / CTB → Color Wheel → 既存のライト・ビーム・レンズ反映です。各フィルターの RGB 値は透過率として乗算し、`neutralTransmission` はそれに追加で掛ける無彩色の減光率です。同じ減光を RGB と `neutralTransmission` の両方に設定しないでください。

## 設定

1. Fixture Type の Mode に Channel Elements を設定します。CMY、CTC、CTO、CTB は `Value`、Color Wheel は `SelectMode` または `Value` を使います。Coarse / Fine の2チャンネルで16bitにもできます。
2. CMY や色温度の実機に合わせた校正が必要なら Color Mix Profile を作り、Fixture Type の `Color Mix Profile` へ割り当てます。未設定時の CMY は単純な `(1-C, 1-M, 1-Y)`、CTC は従来の暖色近似です。
3. Color Wheel Profile の `ranges` に実機DMX表の値域を重複しないよう登録します。16bitの範囲表なら `Use 16 Bit Ranges` を有効にします。Mode の `wheelBindings` に `ColorWheel` と Instance を指定して Profile を割り当てます。Instance 1 の共通フォールバックは Dmx Fixture Component の `Color Wheel` にも置けます。
4. 回転・ランダム範囲の循環対象は `Slot` と `Split` の範囲です。回転速度はその範囲の最小/最大値に対応する `cyclesPerSecondAtMin/Max` で指定します。`Stop` は直前の透過色を保持します。

古い Function Channels 方式でも `Cyan` / `Magenta` / `Yellow` / `CTC` / `CTO` / `CTB` と `ColorWheel` を8bitで利用できます。Color Wheel には Component 側の共通 Profile を設定してください。既存の列挙値とチャンネル番号は変更していません。

## Martin MAC Ultra Performance — Basic 48ch

`MACUltraPerformance_FixtureType` には、Basic 48ch モード専用の Color Mix Profile と Color Wheel Profile を割り当てています。CH 4–5 は Cyan 16bit、CH 6–7 は Magenta 16bit、CH 8–9 は Yellow 16bit、CH 10–11 は CTC 16bit、CH 12 は8bit Color Wheelです。CTC は DMX 0 = 6000 K、最大 = 2850 K として扱います。

CH 12 は、Open→Spectral Enhancement（1–14）、各固定色と連続遷移（15–104）、Open（105–160）、固定色の短い選択域（161–192）、CW 回転（193–214）、停止（215–221）、CCW 回転（222–243）、ランダム高速／中速／低速（244–255）をDMX表どおりに定義しています。回転の速度値はメーカーが絶対回転数を公開していないため、視覚上の初期値です。実機の回転周期を測定したら、Profile の `cyclesPerSecondAtMin/Max` を置き換えてください。

色ホイールの名称と波長表記は機器資料に従っています。Spectral Enhancement、Blue 508 SP、Deep Green 500–544 SP、Orange 591 SP、Navy Blue 440 SP、Deep Red 629 SPです。公開資料には各フィルターの完全な分光透過率がないため、Profile のRGB色は視覚的な初期近似、`neutralTransmission` は第三者の実測光束比（65%、4.3%、25%、24%、0.4%、4.1%）を初期値にしています。分光計・測色計または実機をカメラで比較して最終校正してください。

参考: [Martin MAC Ultra Performance user/safety/installation manual](https://www.martin.com/en-US/site_elements/mac-ultra-performance-user-safety-and-installation-manual)、[MAC Ultra Performance photometric test (Mike Wood / Lighting&Sound America)](https://www.lightingandsoundamerica.com/reprint/LSA_2022_09_Martin_MAC_Ultra_Performance.pdf)。

## 表現上の限界

`Split` は現時点で2色の面積比による単一色近似です。照射面・空間ビーム・レンズ面を二領域に分ける描画ではありません。回転中は隣接スロット間を色補間します。実機のスペクトル、フィルター透過光量、White LED の色温度、ゴボ／プリズムとの相互作用は Profile を現物で測定・校正する必要があります。Unity の Kelvin 色も視覚的な近似であり、分光計測の代替ではありません。

参考: [GDTF Specification](https://github.com/mvrdevelopment/spec/blob/main/gdtf-spec.md)、[Cold River CMY灯体 DMX表](https://cold-river.com/manual4/CR1C139/CR1C139_DMX_EN.pdf)。
