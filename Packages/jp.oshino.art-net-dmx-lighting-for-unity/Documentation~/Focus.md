# Focus

## 概要

Focusは、ゴボ、フレーミング、ビーム外周などが鮮明になる投影距離を制御します。Zoomの照射角、Irisの開口、Frostの拡散とは独立しています。意図的に少しぼかしたルックもDMX値どおり保持し、自動的に最もシャープな位置へ補正しません。

Focus controls the projection distance where gobos, framing, and beam edges appear sharp. It is independent of Zoom angle, Iris aperture, and Frost diffusion. Deliberately soft looks are preserved from DMX and are not automatically corrected to the sharpest position.

## 設定

1. `Create > ArtNet > DMX > Focus Profile`でProfileを作成します。
2. Fixture Typeの`Focus Profile`へ割り当てます。灯体ごとの差が必要な場合はDmx Fixture Componentの`Focus Profile Override`を使用します。
3. 使用ModeのChannel ElementsでFocusチャンネルを`Attribute = Focus`、`Instance = 1`、`Role = Value`または`Position`にします。
4. 16bitの場合はCoarseとFineを同じAttribute、Instance、Roleで定義します。
5. Focus Directionで機種のDMX方向を指定します。ランタイム内部では`0 = Near`、`1 = Far`へ統一されます。
6. Dmx Fixture Componentで`Reference Distance`を投影面までの距離に合わせるか、`Focus Target`を割り当てます。

Create a Focus Profile, assign it to the Fixture Type, and define the Focus Channel Elements. Coarse and Fine elements with the same Attribute, Instance, and Role provide 16-bit control. Set Reference Distance to the representative projection surface, or assign a Reference Target.

## Focus Control Mode

- `Manual`（既定値）: 受信したDMX Focus値をそのまま使います。`Focus Target`は鮮明さを評価する代表位置です。
- `Auto Target`: `Focus Target`までの距離から、合焦する描画用Focus値を自動計算します。
- `Auto Raycast`: Spot Lightの前方で最初に検出したColliderまでの距離から、合焦する描画用Focus値を自動計算します。`Auto Raycast Layers`で検出対象を限定できます。

Autoモードは受信DMXを変更しません。自動計算した値はUnityの描画だけに使用され、ターゲットを失った場合は受信DMX Focusへ戻ります。`Auto Focus Deadband`は小さな距離変化を無視して、Focusの揺れを防ぎます。Auto RaycastはColliderを必要とします。Auto Focusで使用する`Focus Distance Curve`はNearからFarへ単調増加するキャリブレーションにしてください。

`Manual` (the default) uses the received DMX Focus value directly. `Auto Target` calculates the rendering focus from the distance to Focus Target. `Auto Raycast` calculates it from the first Collider hit along the Spot Light direction. Auto modes never modify incoming DMX; they only affect Unity rendering and fall back to DMX Focus if no target is available.

## Focus Profile

- `Default Control Mode`: このProfileを使うFixtureの既定制御モードです。Fixture側の`Override Focus Control Mode`で上書きできます。
- `Near / Far Focus Distance`: Focus両端が表す焦点距離です。
- `Far Is Infinity`: Far端を無限遠として扱います。
- `Focus Distance Curve`: Focus位置から焦点距離への光学応答です。
- `Full Blur Diopter Difference`: どの程度の焦点距離差を最大Defocusとして扱うかを指定します。
- `Defocus Curve`: 焦点ずれから視覚効果量への応答です。
- `Maximum Cookie / Lens Blur`: 投影Cookieとレンズ面の最大ぼかし量です。
- `Maximum Edge Softness`: Focusによるビーム外周の最大Softnessです。照射角は変えません。
- `Near To Far / Far To Near Seconds`: Focusモーターの移動時間です。

Channel numbers and DMX ranges belong to the Fixture Type. The Focus Profile contains only optical and motion calibration, so the runtime has no model-specific branches.

## 描画方式

| 描画方式 | Focus表現 |
| --- | --- |
| Normal Light（URP / HDRP） | Iris後、Frost前にCookieをぼかし、Inner Spot Angleで外周を補助 |
| Pseudo Beam | ゴボサンプルのぼかしと外周Softness。FocusとFrostは合成 |
| VLB HD | Focus済みのLight CookieをVolumetric Cookieへ反映 |
| VLB SD | Lightの投影角を維持。Cookie非対応のため体積内ゴボの距離依存ぼかしは対象外 |
| Lens Surface | 既存のGobo Lens BlurへFocus由来のぼかしを加算 |

処理順は`Gobo / Prism -> Iris -> Focus -> Frost`です。FocusはZoom角、Iris径、ゴボ倍率を変更しません。

The processing order is `Gobo / Prism -> Iris -> Focus -> Frost`. Focus does not change Zoom angle, Iris diameter, or gobo magnification.

## MAC Ultra Performance検証設定

同梱の`MACUltraPerformance_FixtureType`には、Martin Basic 48ch仕様に合わせてCh.27をFocus Coarse、Ch.28をFocus Fineとして設定しています。DMX 0はInfinity、65535はNearで、内部の`0 = Near`、`1 = Far`へ反転して正規化します。

`MACUltraPerformance_FocusProfile`は、公式仕様で公開されている約4.5m〜Infinityを端点として使用します。DMX応答カーブとモーター移動時間は公式数値がないため、初期値はディオプター空間の線形応答と即時移動です。

| Ch.27–28 | 内部Focus | 初期Profileの焦点距離 |
| --- | --- | --- |
| 0 | 1 Far | Infinity |
| 32768 | 約0.5 | 約9m |
| 65535 | 0 Near | 4.5m |

実機仕様ではFocus範囲がZoom角により変化します。現在のProfileは代表投影距離を使う汎用近似で、実機測定値が得られた場合は`Focus Distance Curve`を調整してください。Focus TrackingのNear／Medium／FarモードはManual Focusとは別機能のため、このProfileには含めていません。

The bundled MAC Ultra Performance Fixture Type maps Basic-mode channels 27–28 to 16-bit Focus, with DMX infinity-to-near converted to the runtime's near-to-far convention. The profile uses the published approximate 4.5 m-to-infinity endpoints. Its response curve is an initial diopter-linear approximation because Martin does not publish a DMX-to-distance calibration curve.

Official reference: [Martin MAC Ultra Performance](https://www.martin.com/en-US/products/mac-ultra-performance)

## Unity上の制約

Unity標準のSpot Lightは1灯につき1枚のCookieを投影するため、複数距離の投影面へ異なるぼかしを同時適用できません。この実装ではReference DistanceまたはReference Targetを代表投影面として使用します。

A standard Unity Spot Light projects one cookie per light, so it cannot show a different blur on several projection surfaces at different distances simultaneously. Reference Distance or Reference Target represents the projection surface used for the approximation.

## Auto Focus

この実装はManual DMX Focusを対象とします。Zoom Tracking、距離帯選択、センサー方式などのAuto Focusは機種間の差が大きいため、通常Focusへ混在させません。将来実装する場合も明示的なTracking Modeとして追加します。

This implementation covers manual DMX Focus. Auto Focus variants such as Zoom tracking, distance bands, and sensor-based systems are intentionally kept separate and should be added as explicit tracking modes.

## 確認項目

- 8bit／16bitとDMX方向が正しいこと
- Reference DistanceとFocus焦点距離が一致すると最も鮮明になること
- 一致位置の両側でぼけること
- Focusのみでは照射角とゴボ倍率が変わらないこと
- Profile未設定、Sync無効化、Component無効化時に元のCookieとSpot設定へ戻ること
- Zoom、Iris、Frost、Prismとの組み合わせで順序が維持されること
