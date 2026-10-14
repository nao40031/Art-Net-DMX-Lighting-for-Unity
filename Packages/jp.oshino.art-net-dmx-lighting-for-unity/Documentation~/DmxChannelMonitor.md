# DMX Channel Monitor

## 開発順序

DMXモニターは段階的に開発します。第2段階は、MVPの実Art-Net入力とEditor UIを確認し、問題を修正してから開始してください。

1. MVP: Play Mode中の入力Channel Monitor
2. 実Art-Net入力、Receiver切り替え、Universe切り替え、Pause、Clear、Editor負荷、表示崩れを確認
3. 確認で見つかった問題を修正し、MVPを確定
4. 第2段階: Activity Monitor、履歴グラフ、送信元IP、Output Monitor、Edit Mode独立受信などを優先度順に実装

Do not begin phase two until the MVP has been checked with real Art-Net input and its Editor UI behavior has been accepted. Fix any MVP issues first, then implement the activity monitor, history, source IP, output monitoring, and edit-mode reception in priority order.

## 第2弾の残タスク

1. 完了: Activity Monitor: Universe範囲の非ゼロDMX値を一覧表示し、Live InputとTimeline Playbackを切り替える。
2. 完了: チャンネル履歴: 選択したチャンネルの直近10秒の値を短時間だけ追跡できるグラフを追加する。
3. 完了: 送信元情報: Art-Net送信元IP／ポートとReceiverごとの統計をChannel Monitorに表示する。
4. 完了: Output監視の拡張: All Rigs、Live Input、Timeline Playback、外部注入、ArtNetSender出力を区別する。
5. 完了: Edit Mode独立受信: Receiverの明示的なオプトイン設定で、Play Mode外でも安全に監視する。

Completed in phase two: Activity Monitor, channel history, receiver source statistics, output-source monitoring, and opt-in Edit Mode reception. Sender Output is available in both monitors; All Rigs is available in the Channel Monitor.

## MVPの範囲

- `Art-Net > Monitoring > DMX Channel Monitor` から開くEditorWindow
- `Live Input` は `ArtNetReceiver.OnDataReceived` を購読し、送信元IP／ポートと受信統計を表示
- `Timeline Playback` は選択した `DmxRigController`、またはAll Rigsの有効Universeバッファを表示。Timeline Playback、Live Input、外部注入のうち、最後にリグへ反映された値とその更新元を確認できる
- `Sender Output` は選択した `ArtNetSender` が実送信したDMXフレームを監視
- Edit ModeでのLive Input監視は、Receiverの `Receive In Edit Mode` を明示的に有効にした場合のみ動作
- 32列×16行で512チャンネルを表示
- すべてのReceiver、または指定Receiverを監視
- 生のUniverse番号を選択
- Pause、Clear DMX Buffers、受信状態とパケット情報
- 選択チャンネルの直近10秒の履歴グラフ
- Activity Monitorでも非ゼロのセルをクリックすると、選択したUniverse／チャンネルの直近10秒の履歴グラフを表示する。値が0になってセルが消えた後も追跡を続ける
- Clearはモニター内部だけを消去し、Fixtureやネットワーク出力を変更しない

The Editor-only monitors support non-mutating Live Input, Timeline Playback, and Sender Output sources. Live Input observes existing receivers, Timeline Playback copies effective rig buffers, and Sender Output observes packets already sent by ArtNetSender. Neither monitor modifies runtime routing, fixture state, or Art-Net output.

In the Activity Monitor, click a nonzero channel cell to graph that Universe and channel for the last 10 seconds. The graph continues to track the selected channel when its value falls to zero and the cell disappears.

## 現在の自動検証

- `unity-check` と `unity-test -Mode EditMode` を、変更後に実行してください
- EditModeテストは、受信値コピー、OpDmxフィルター、Source／Universe分離、Pause、Clear、Rig Outputバッファコピーを検証します

実Art-Net送信機を使った表示と操作の確認は別途必要です。これは自動テスト成功だけでは代替できません。

Real Art-Net input and visual interaction still require manual verification; automated tests do not replace that check.

## 検証プロジェクトのテスト設定

`ArtNetForUnity-URP-Validation` と `UPM_Test_HDRP` は、このパッケージを `file:` 参照する検証プロジェクトです。両方の `Packages/manifest.json` では、`testables` に `jp.oshino.art-net-dmx-lighting-for-unity` を常設してください。これにより、パッケージ同梱のEditModeテストを継続的に実行できます。これは検証プロジェクト専用の設定であり、配布先または本番プロジェクトには不要です。

Keep `jp.oshino.art-net-dmx-lighting-for-unity` in the `testables` list of the URP and HDRP validation projects. This makes the package's EditMode tests continuously available there. Do not add this validation-only setting to consumer or production projects.
