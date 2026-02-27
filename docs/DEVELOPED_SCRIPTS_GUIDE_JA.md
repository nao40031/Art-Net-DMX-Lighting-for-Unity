# Developed Unity Scripts Guide (Japanese)

このドキュメントは、`Assets/Editor` と `Assets/ArtNet` 配下の開発スクリプトについて、スクリプトごとに以下を整理したものです。

- スクリプト概要
- 使い方
- 各 parameter の仕組み

Notion版への展開を想定し、Markdownの標準要素（見出し・箇条書き・番号リスト）中心で記載しています。

---

## 1. 全体アーキテクチャ

### ライブ入力（Art-Net受信）経路

`ArtNetReceiver`  
→ `DmxRigController`（Universeごとにバッファ化）  
→ `DmxFixtureComponent`（Fixture定義に従ってDMX解釈）  
→ `GenericLightDriver` / `HdrpLightDriver`（Light適用）

### タイムライン再生経路

`ArtNetChannels`（Ch1..Ch512をAnimationで駆動）  
→ `DmxTimelinePlayback`（byte[512]へコピーしてInject）  
→ `DmxRigController.InjectUniverse`  
→ `DmxFixtureComponent`（ライブ同等の適用）

---

## 2. Assets/Editor

## 2.1 `SpotLightReplicatorWindow.cs`

### 概要
テンプレートMovingLight内のSpot Lightオブジェクトを、`MovingLight_1..N` の各対象に一括複製するEditorWindowです。

### 使い方
1. メニュー `Tools/MovingLight/Replicate SpotLight...` を開く。
2. `Template MovingLight` を指定する。
3. `Prefix` と `Start/End Index` を設定する。
4. `Mount Path` と `Light Object Name` を設定して `Replicate`。

### parameters
- `Root (optional)` (`root`)  
  探索範囲。指定時はこの配下のみ探索、未指定時はシーン全体検索。
- `Template MovingLight` (`templateMovingLight`)  
  複製元となるMovingLightオブジェクト。
- `Prefix` (`movingLightPrefix`)  
  置換対象名の接頭辞（例: `MovingLight_`）。
- `Start Index` / `End Index` (`startIndex`, `endIndex`)  
  対象番号範囲。
- `Mount Path` (`mountPath`)  
  各MovingLight内でSpot Lightを配置する子階層パス。
- `Light Object Name` (`templateLightObjectName`)  
  テンプレート内で探すライト名。見つからなければSpotタイプLightをフォールバック探索。
- `Replace If Exists` (`replaceIfExists`)  
  同名ライトが既にある場合に置換するか。
- `Log Details` (`logDetails`)  
  対象ごとの詳細ログ出力有無。

---

## 2.2 `MovingLightPrefabReplacerWindow.cs`

### 概要
`MovingLight_1..N` の既存オブジェクトを、指定Prefabインスタンスへ置換するEditorWindowです。

### 使い方
1. メニュー `Tools/MovingLight/Replace With Prefab...` を開く。
2. `Prefab Asset` に置換先Prefabを設定。
3. 対象の `Prefix` + `Start/End` を設定。
4. `Replace` 実行。

### parameters
- `Root (optional)` (`root`)  
  対象探索の起点。
- `Prefab Asset` (`prefabAsset`)  
  置換先Prefab（Project内アセット）。
- `Prefix` (`prefix`)  
  対象名接頭辞。
- `Start Index` / `End Index` (`startIndex`, `endIndex`)  
  置換対象番号範囲。
- `Force Name To Target` (`forceNameToTarget`)  
  新規インスタンス名を元ターゲット名へ強制。
- `Copy Active/Layer/Tag/Static` (`copyCommonFlags`)  
  元オブジェクトの共通フラグを新規へコピー。
- `Log Details` (`logDetails`)  
  詳細ログ出力有無。

---

## 2.3 `GenericPrefabReplacerWindow.cs`

### 概要
汎用置換ツールです。対象収集方法（Prefix範囲/Root子孫フィルタ）とテンプレート種別（Prefab/Scene Object）を選び、必要に応じて子Transformローカル値を転送して置換します。

### 使い方
1. メニュー `Tools/Generic/Replace Targets With Template...` を開く。
2. `Target Mode` を選び対象を定義。
3. `Template Mode` を選び置換元（PrefabまたはScene Object）を定義。
4. 必要なら `Transfer Child Local Transforms` を有効化。
5. `Replace` 実行。

### parameters
- `Target Mode` (`targetMode`)  
  `PrefixNumberRange` か `ChildrenOfRoot` を選択。
- `Root (optional*)` (`root`)  
  範囲検索の起点。`ChildrenOfRoot` では必須。
- `Target Prefix`, `Start Index`, `End Index`  
  Prefix範囲モードの対象定義。
- `Only Direct Children`, `Name Contains`, `Tag Equals`, `Required Component (TypeName)`  
  Root子孫モードの絞り込み条件。
- `Template Mode` (`templateMode`)  
  `PrefabAsset` か `SceneObject`。
- `Prefab Asset` (`prefabAsset`) / `Template Scene GO` (`templateSceneGO`)  
  置換テンプレートの実体。
- `Force Name To Target`  
  新規名を元名へ合わせる。
- `Copy Active/Layer/Tag/Static`  
  共通フラグ継承。
- `Keep World Transform`  
  trueでワールド姿勢優先、falseでローカル姿勢優先。
- `Log Details`  
  詳細ログ。
- `Transfer Child Local Transforms` (`transferAllChildLocalTransforms`)  
  旧オブジェクト子階層のlocal TRSを新規へ復元。
- `Transform Match Mode` (`transformMatchMode`)  
  復元時の対応キー生成方式。  
  `PathOnly`: `A/B/C`  
  `NamePlusSiblingIndex`: `A[0]/B[1]/C[2]`
- `Warn If Missing` (`warnOnMissingTransform`)  
  新規側で対応Transformが無い場合に警告。

---

## 2.4 `MagicQCsvExporter.cs`

### 概要
選択HierarchyのTransform情報を、MagicQ向けキャリブレーションCSV（MagicQ列 + Unity列）へ出力します。

### 使い方
1. Hierarchyで対象ルートを選択。
2. メニュー `Tools/MagicQ/Export MagicQ Calibration CSV...` を実行。
3. オプションウィンドウで各設定を指定して保存。

### parameters（Exportウィンドウ）
- `Units Scale`  
  Unity座標スケール倍率。
- `Invert Z (MagicQ)`  
  MagicQ座標向けにZ反転。
- `Rotation Space (Unity)`  
  Unity回転の取得基準（World/Local）。
- `Rule Tolerance (deg)`  
  Unity→MagicQ回転変換ルール判定の許容角度。
- `Include Inactive`  
  非アクティブTransformも含めるか。
- `Sort`  
  出力順（名前順/HeadNo順/Hierarchy順）。
- `Name`, `Manufacturer`, `Model`, `Mode`  
  コンポーネントから値抽出できない場合のデフォルト文字列。

---

## 2.5 `remove_in_bulk_all_missing_error_script.cs`

### 概要
選択オブジェクト群から Missing Script を一括削除する簡易Editorメニューです。

### 使い方
1. Hierarchyで対象を複数選択。
2. メニュー `exception_handling/all missing error script removal in bulk` を実行。
3. Consoleに削除件数が出力される。

### parameters
Inspector parameterはありません。  
入力は `Selection.gameObjects`（現在選択）です。

---

## 3. Assets/ArtNet

## 3.1 `ArtNetOpCode.cs`

### 概要
Art-Net OpCodeの列挙体定義です。`OpDmx` などパケット種別判定に使います。

### 使い方
`ArtNetData.OpCode` との比較でパケット種別を判定します。

### parameters
なし（enum定義のみ）。

---

## 3.2 `ArtNetData.cs`

### 概要
Art-Net DMXパケットのデータ構造（受信byte列→構造体、構造体→送信用byte列）です。

### 使い方
- 受信時: `new ArtNetData(byte[])`
- 送信時: `new ArtNetData(...).ToBytes()`

### parameters（主要フィールド）
- `OpCode`
- `Sequence`
- `Physical`
- `Universe`
- `Channels` (`int[512]`)
- `ProtocolVersionHi/Lo`
- `LengthHi/Lo`

---

## 3.3 `ArtNetSender.cs`

### 概要
UDPでArt-Netパケット送信を行う送信コンポーネントです。

### 使い方
1. GameObjectへアタッチ。
2. `host` と `port` を設定。
3. `Send(...)` を呼びDMX送信。

### parameters
- `host`  
  送信先IP/ホスト名（既定 `127.0.0.1`）。
- `port`  
  送信先UDPポート（既定 `6454`）。

### `Send(...)` 引数
- `channels`  
  DMXデータ（通常512ch）。
- `opCode`  
  既定は `OpDmx`。
- `sequence`, `physical`, `universe`  
  Art-Netヘッダ情報。
- `protocolVersionHi/Lo`, `lengthHi/Lo`  
  プロトコルバージョン・データ長。

---

## 3.4 `ArtNetReceiver.cs`

### 概要
Art-Net UDP受信をバックグラウンドTaskで行い、メインスレッドで `OnDataReceived` を発火する受信コンポーネントです。無効パケットと受信ループエラーは集約ログ化しています。

### 使い方
1. GameObjectへアタッチ。
2. `host` と `port` を設定して有効化。
3. `OnDataReceived` を購読して利用。

### parameters
- `host`  
  バインドIP。`0.0.0.0` で全NIC待受。
- `port`  
  受信ポート（通常6454）。
- `logInvalidPackets`  
  無効パケット集約ログON/OFF。
- `invalidPacketLogIntervalSec`  
  無効パケット集約ログ間隔。
- `logReceiveLoopErrors`  
  ReceiveLoop例外集約ログON/OFF。
- `receiveErrorLogIntervalSec`  
  ReceiveLoop例外集約ログ間隔。

---

## 3.5 `ArtNetChannels.cs`

### 概要
`Ch1`〜`Ch512` を保持するDMXチャンネルコンテナです。AnimationClipから直接カーブを書き込む用途を想定しています。

### 使い方
Timeline/Animationで各 `ChN` をアニメートし、`DmxTimelinePlayback` から読み出します。

### parameters
- `Ch1` ... `Ch512`  
  各DMXチャンネル値（int）。

---

## 3.6 `ArtNetChannels.Copy.cs`

### 概要
`ArtNetChannels` の partial実装。`CopyTo(byte[] dst)` で `Ch1..Ch512` を `dst[0..511]` にコピーし、値変化があれば `true` を返します。

### 使い方
`DmxTimelinePlayback` 側で毎Tick呼び出し、変更時のみInjectに使います。

### parameters
- `dst` (`byte[512]`)  
  コピー先。512未満なら `false` を返して何もしません。

---

## 3.7 `DmxValueUtils.cs`

### 概要
DMX値の共通ユーティリティ（0..255クランプ、8bit/16bit読取、0..1正規化）です。

### 使い方
`DmxFixtureComponent` や `ArtNetChannels.Copy` から呼び出します。

### parameters
なし（静的メソッド群）。

---

## 3.8 `FixtureDefinition.cs`

### 概要
Fixtureプロファイル（機種/モード/機能→チャンネル割当）を定義するScriptableObjectです。

### 使い方
1. `Create > ArtNet > DMX > Fixture Definition` でアセット作成。
2. `modes` にモードごとの `channelCount` と `FunctionChannel` を設定。
3. `DmxFixtureComponent.fixture` に割り当て。

### parameters
- `manufacturer`, `model`, `revision`, `displayName`  
  識別表示用情報。
- `modes` (`List<FixtureModeDefinition>`)  
  モード定義一覧。

`FixtureModeDefinition`:
- `modeName`  
  モード名。
- `channelCount`  
  使用チャンネル数。
- `channels` (`List<FunctionChannel>`)  
  機能→相対DMXチャンネル。

`FunctionChannel`:
- `function` (`FixtureFunction`)  
  Dimmer/RGB/Pan/Tiltなど。
- `channel`  
  1始まりの相対チャンネル。

---

## 3.9 `ILightDriver.cs`

### 概要
ライト適用ドライバの共通インターフェースです。

### 使い方
`DmxFixtureComponent` / `DmxLightController` が `ILightDriver` 実装に対して `Initialize` と `Apply` を呼びます。

### parameters
なし（interface）。

---

## 3.10 `GenericLightDriver.cs`

### 概要
Built-in/URP向けのLightドライバです。`Light.intensity` と `Light.color` をDMXで更新します。

### 使い方
`DmxFixtureComponent` と同一GameObjectへアタッチ（または自動追加）して使用。

### parameters
- `maxIntensity`  
  `dimmer=1.0` 時の最大Intensity。
- `dimmerCurve`  
  0..1のディマーカーブ補正。

---

## 3.11 `HdrpLightDriver.cs`

### 概要
HDRP向けライトドライバです。`HDAdditionalLightData.SetIntensity` を優先使用し、失敗時は `Light.intensity` にフォールバックします。

### 使い方
HDRP構成で `HAS_HDRP` 有効時に利用。

### parameters
- `maxIntensity`  
  最大光量。
- `unit` (`Lumen/Candela/Lux`)  
  HDRP光量単位。
- `dimmerCurve`  
  ディマーカーブ補正。

---

## 3.12 `DmxLightController.cs`

### 概要
単一Lightへの簡易DMX適用コンポーネントです。`ArtNetReceiver` を購読し、指定チャンネルをRGB+Dimmerとしてドライバへ渡します。

### 使い方
1. `receiver` と `targetLight` を設定。
2. `dimmer/red/green/blue channel` を設定。
3. 必要なら `driverComponent` と `pipelineMode` を設定。

### parameters
- `receiver`  
  DMX受信元。
- `targetLight`  
  適用対象Light。
- `driverComponent`  
  明示利用ドライバ（`ILightDriver` 実装）。
- `pipelineMode`  
  Auto / ForceGeneric / ForceHDRP。
- `dimmerChannel`, `redChannel`, `greenChannel`, `blueChannel`  
  1..512のチャンネルマッピング。
- `onlyUniverse`  
  特定Universeのみ適用。`-1` で無効。

---

## 3.13 `DmxDebugMonitor.cs`

### 概要
受信DMX値をInspector表示し、必要に応じて周期ログ出力するデバッグ補助です。

### 使い方
1. `receiver` を設定（未設定時は同一GameObject探索）。
2. `monitorCh1..5` を設定。
3. 必要なら周期ログ設定をON。

### parameters
- `receiver`
- `monitorCh1..monitorCh5`  
  監視チャンネル（1..512）。
- `onlyUniverse`  
  Universeフィルタ（`-1` で無効）。
- `enablePeriodicLog`  
  定期ログON/OFF。
- `logIntervalSec`  
  出力間隔。
- `logOnlyWhenDataArrived`  
  受信なし時の無駄ログ抑制。
- `includeHeaderInLog`  
  Universe/Length等ヘッダをログへ含める。

---

## 3.14 `DmxFixtureComponent.cs`

### 概要
Fixture単位の中核コンポーネントです。`FixtureDefinition` に基づいてDMXを解釈し、Light、Pan/Tilt、Lens Shader、Pseudo Beam、監視ログを統合制御します。

### 使い方
1. Fixtureオブジェクトへアタッチ。
2. `fixture` と `mode`、`universe/startAddress` を設定。
3. `targetLight`（または `targetLights`）、`panTransform`、`tiltTransform` を設定。
4. `DmxRigController` でDiscoverし適用開始。

### parameters（Addressing / Profile）
- `universe`  
  受信Universe番号。
- `startAddress`  
  Fixture先頭DMXアドレス（1..512）。
- `fixture`  
  `FixtureDefinition` アセット。
- `mode`  
  `fixture.modes` のインデックス。

### parameters（Targets）
- `targetLight`  
  単一代表Light。
- `targetLights`  
  複数Light対応。
- `panTransform`, `tiltTransform`  
  Pan/Tilt回転の適用先。

### parameters（Lens Shader同期）
- `lensRenderer`, `extraLensRenderers`
- `syncLensToDmx`
- `syncLensColorToDmx`, `syncLensDimmerToDmx`
- `lensColorProperty`, `lensDimmerProperty`
- `lensDimmerScale`

仕組み: `MaterialPropertyBlock` を使ってRenderer単位に色/ディマーを上書きし、マテリアルインスタンス増殖を避けます。

### parameters（Pseudo Beam）
- `syncPseudoBeamToDmx`
- `beamRenderer`, `extraBeamRenderers`
- `syncBeamColorToDmx`, `syncBeamDimmerToDmx`
- `beamColorProperty`, `beamDimmerProperty`
- `beamDimmerScale`, `beamDimmerFloor`

仕組み: ボリュームライト代替としてビーム用RendererへDMX同期します。

### parameters（Light Response）
- `lightResponseMode` (`Led` / `Halogen`)
- `halogenSourceRiseTime`, `halogenSourceFallTime`
- `halogenBeamOnDelay`, `halogenBeamOffDelay`
- `halogenBeamRiseTime`, `halogenBeamFallTime`

仕組み: Halogen時はレンズ系とライト系の応答を分離し、遅延と立上/立下時定数を適用します。

### parameters（Pan/Tilt）
- `panRangeDeg`, `tiltRangeDeg`
- `panAxis`, `tiltAxis`
- `panInvert`, `tiltInvert`
- `panOffsetDeg`, `tiltOffsetDeg`
- `panTiltSmoothing`
- `enableContinuousPanTiltUpdate`
- `panTiltSpeedMinDegPerSec`, `panTiltSpeedMaxDegPerSec`

仕組み: DMX 16bit値を `-range/2 .. +range/2` へ変換し、`PanTiltSpeed` があれば角速度制限、なければSlerp補間で追従します。

### parameters（Driver）
- `pipelineMode`
- `driverOverride`
- `autoAddDriverIfMissing`
- `genericMaxIntensity`, `hdrpMaxIntensity`
- `overrideDriverMaxIntensity`

仕組み: `driverOverride` 優先、なければ同一GameObject上のDriver探索、なければ必要に応じ自動追加します。

### parameters（Reset / Auto Resolve）
- `resetTriggerThreshold`  
  Reset機能チャンネルが閾値以上で初期姿勢へ戻す。
- `autoResolveOnValidate`  
  Inspector変更時にマッピング再解決。

### parameters（Edit Mode Preview）
- `applyImmediateInEditMode`  
  EditモードでもPan/Tilt/光量を即時反映。

### parameters（Monitoring）
- `monitorEnabled`
- `monitorCh1..monitorCh5`
- `monitorIncludeResolvedFunctions`
- `monitorExtraRelativeChannels`
- `enablePeriodicLog`
- `logIntervalSec`
- `logOnlyWhenDataArrived`
- `includeHeaderInLog`

仕組み: 絶対ch5本 + 解決済み機能/相対chをInspectorに表示し、必要なら周期サマリログを出力します。

---

## 3.15 `DmxRigController.cs`

### 概要
シーン全体のFixture管理・Universe適用を行うハブです。受信/再生データをUniverseバッファへ集約し、該当Fixture群へ反映します。

### 使い方
1. シーンに1つ配置。
2. `receiver` を設定（未設定なら自動探索）。
3. `Discover & Initialize Fixtures` 実行（または `autoDiscoverFixturesOnEnable`）。
4. 必要なら自動アドレッシング設定を有効化。

### parameters
- `inputMode`  
  `LiveOnly` / `PlaybackOnly` / `LiveAndPlayback`。
- `receiver`
- `autoDiscoverFixturesOnEnable`
- `applyOnUpdate`
- `logRxRate`, `logRxIntervalSec`
- `logApplyHeadChannels`
- `autoAssignStartAddressOnEnable`
- `addressingRoot`
- `universeStart`, `startAddressStart`
- `usePrefabValueAsBase`
- `autoIncrementUniverse`
- `bakeWritesToScene`

### 自動アドレッシングの仕組み
Hierarchy順でFixtureを並べ、各Fixtureの `channelCount` 分だけ `startAddress` を加算。512超過時は `autoIncrementUniverse` に従ってUniverseを繰り上げます。

---

## 3.16 `ArtNetDataRecorder.cs`（クラス名: `ArtNetReceiverDmxRecorder`）

### 概要
`ArtNetReceiver.OnDataReceived` を録画し、Universeごとに `AnimationClip`（`Ch1..Ch512` カーブ）へ保存するレコーダです。

### 使い方
1. コンポーネントを配置し `receiver` を設定。
2. `Start Recording` / `Stop & Save`（ContextMenuまたはキー）で録画。
3. `Assets/<directoryPath>` に `.asset` が保存される。

### parameters（Record Control）
- `receiver`
- `recordOnPlay`
- `startKey`, `stopKey`

### parameters（Recording Mode）
- `useLowGcRecording`  
  低GCでサンプル蓄積し、停止時にカーブ化。
- `initialSampleCapacityPerChannel`

### parameters（Record Targets）
- `recordAllUniverses`
- `targetUniverses`

### parameters（Save）
- `directoryPath`
- `clipName`
- `appendUniverseSuffix`
- `channelsComponentTypeName`  
  既定 `ArtNet.Runtime.ArtNetChannels`。

### parameters（Curve）
- `channelCount`
- `verboseLog`
- `useStepKeys`  
  値変化を段差表現（直前キー + 変化キー）で記録。
- `stepEpsilon`
- `deadbandThreshold`

### parameters（Hybrid Recording）
- `hybridPanTiltLinear`
- `autoDetectPanTiltChannels`
- `applyDeadbandToLinearChannels`
- `fallbackLinearChannels`

仕組み: Pan/Tilt系chのみ線形、それ以外は段差記録にして、可動の滑らかさと色/ディマーの切替感を両立。

### parameters（Legacy/Clip Length）
- `setCurvesToConstant`  
  保存時に全キーConstant化（非推奨）。
- `normalizeClipEndAcrossUniverses`  
  複数Universe保存時のクリップ終端を最長に揃える。

---

## 3.17 `DmxTimelinePlayback.cs`

### 概要
`ArtNetChannels` ソース群を一定周期で読み取り、Universe単位で `DmxRigController` にInjectするTimeline再生ブリッジです。`ExecuteAlways` でEditモードPreviewにも対応します。

### 使い方
1. `DmxRigController` と同一または関連オブジェクトへ配置。
2. `sources` に `universe + ArtNetChannels` を登録。
3. `enablePlayback` をONにして再生。
4. 必要に応じ `overrideRigInputMode` でRigの入力モードを上書き。

### parameters
- `rig`
- `sources` (`List<UniverseSource>`)  
  要素: `universe`, `channels(ArtNetChannels)`
- `enablePlayback`
- `enableInEditMode`
- `updateTiming` (`Update` / `LateUpdate` / `FixedUpdate`)
- `sampleRate`  
  0で毎フレーム、正値でHz制御。
- `useLiveCompatiblePlayback`
- `forceUpdateTimingToUpdate`
- `applyDefaultSampleRateWhenZero`
- `defaultLiveSampleRate`
- `forceApplyOnEnable`
- `applyOnFirstFrame`
- `forceAnimatorAlwaysAnimate`
- `resetPanTiltOnExitingEditMode`
- `overrideRigInputMode`
- `inputModeWhileEnabled`
- `logMissingRig`

---

## 3.18 `DmxTimelinePlaybackEditor.cs`

### 概要
`DmxTimelinePlayback` 用CustomEditor。通常Inspectorに加え、Live運用とTimeline運用のプリセットボタンを提供します。

### 使い方
Inspectorで以下ボタンを使用。
- `Apply Live / Record`
- `Apply Timeline Playback`

### parameters
独自の永続parameterはありません。  
ボタン押下時に `DmxTimelinePlayback` と `DmxRigController` の関連設定を一括変更します。

---

## 4. GitHub公開向けの最小追記案

READMEへ最低限この3点を追加すると、利用者が迷いにくくなります。

1. 依存関係  
   `ArtNetReceiver` / `DmxRigController` / `DmxFixtureComponent` / `FixtureDefinition` の関係図
2. 最短セットアップ  
   「1灯だけ動かす」手順（3-5ステップ）
3. 運用モード  
   Live受信、Timeline再生、Recorder保存の切替方法
