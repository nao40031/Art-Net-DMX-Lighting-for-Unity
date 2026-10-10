# DMX Channel Monitor

## 開発順序

DMXモニターは段階的に開発します。第2段階は、MVPの実Art-Net入力とEditor UIを確認し、問題を修正してから開始してください。

1. MVP: Play Mode中の入力Channel Monitor
2. 実Art-Net入力、Receiver切り替え、Universe切り替え、Pause、Clear、Editor負荷、表示崩れを確認
3. 確認で見つかった問題を修正し、MVPを確定
4. 第2段階: Activity Monitor、履歴グラフ、送信元IP、Output Monitor、Edit Mode独立受信などを優先度順に実装

Do not begin phase two until the MVP has been checked with real Art-Net input and its Editor UI behavior has been accepted. Fix any MVP issues first, then implement the activity monitor, history, source IP, output monitoring, and edit-mode reception in priority order.

## 第2弾の残タスク

1. Activity Monitor: Universe範囲の非ゼロDMX値を一覧表示し、Live InputとRig Outputを切り替えられるようにする。
2. チャンネル履歴: 値の変化と受信時刻を短時間だけ追跡できるグラフまたはタイムラインを追加する。
3. 送信元情報: Art-Net送信元IP／ポートとReceiverごとの統計を表示する。
4. Output監視の拡張: 複数Rig、外部注入、出力送信機を明確に区別する。
5. Edit Mode独立受信: Play Mode外でも安全に監視できる受信ライフサイクルを検討する。

Phase two backlog: Activity Monitor with Live Input/Rig Output selection, a short channel-history view, sender IP/port and receiver statistics, expanded output-source monitoring, and safe Edit Mode reception.

## MVPの範囲

- `Art-Net > Monitoring > DMX Channel Monitor` から開くEditorWindow
- `Live Input` はPlay Mode中の `ArtNetReceiver.OnDataReceived` を購読
- `Rig Output` は選択した `DmxRigController` の有効Universeバッファを表示。Timeline Playback、Live Input、外部注入のうち、最後にリグへ反映された値とその更新元を確認できる
- 32列×16行で512チャンネルを表示
- すべてのReceiver、または指定Receiverを監視
- 生のUniverse番号を選択
- Pause、Clear DMX Buffers、受信状態とパケット情報
- Clearはモニター内部だけを消去し、Fixtureやネットワーク出力を変更しない

The Editor-only monitor supports two non-mutating sources. `Live Input` observes existing receivers. `Rig Output` copies the selected rig's effective Universe buffer, including Timeline Playback values. Neither source modifies runtime routing, fixture state, or Art-Net output.

## 現在の自動検証

- `unity-check` と `unity-test -Mode EditMode` を、変更後に実行してください
- EditModeテストは、受信値コピー、OpDmxフィルター、Source／Universe分離、Pause、Clear、Rig Outputバッファコピーを検証します

実Art-Net送信機を使った表示と操作の確認は別途必要です。これは自動テスト成功だけでは代替できません。

Real Art-Net input and visual interaction still require manual verification; automated tests do not replace that check.

## 検証プロジェクトのテスト設定

`ArtNetForUnity-URP-Validation` と `UPM_Test_HDRP` は、このパッケージを `file:` 参照する検証プロジェクトです。両方の `Packages/manifest.json` では、`testables` に `jp.oshino.art-net-dmx-lighting-for-unity` を常設してください。これにより、パッケージ同梱のEditModeテストを継続的に実行できます。これは検証プロジェクト専用の設定であり、配布先または本番プロジェクトには不要です。

Keep `jp.oshino.art-net-dmx-lighting-for-unity` in the `testables` list of the URP and HDRP validation projects. This makes the package's EditMode tests continuously available there. Do not add this validation-only setting to consumer or production projects.
