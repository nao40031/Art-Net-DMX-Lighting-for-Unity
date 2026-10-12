# Timeline DMX → Art-Net 出力

`DmxTimelineArtNetOutput` は、`DmxTimelinePlayback` が `DmxRigController` に反映したDMX値を `ArtNetSender.Send()` に渡します。Live Inputや外部注入の値は転送しません。初期状態では送信OFFです。

1. シーンに `ArtNetSender` を配置し、`Host` と `Port`（通常6454）を設定します。
2. GameObjectに `DmxTimelineArtNetOutput` を追加し、`Sender` を指定します。
3. `Rig Selection` は、単一Rigなら `Assigned` のままで自動検出できます。複数Rigを順序付きで登録するか、`All Active` を選べます。
4. `Universe > Selection` で `Specified`（番号リスト、0～32767）または `All` を選びます。`All` は現在Timeline値を持つUniverseだけを対象にします。
5. 必要なら `Playable Director (Optional)` を指定します。指定するとDirectorが再生中のときだけ送信します。未指定の場合はRigのTimeline更新を送信します。
6. Play Modeで `Enable Output` をONにします。`Packets Sent` とChannel/Activity Monitorの `Sender Output` で確認します。

送信レート上限は30Hz、値が変わらないときの維持送信は1秒間隔です。後者は0で無効にできます。複数Rigが同じUniverseを出す場合、指定リストの先頭が優先されます。「有効なRigすべて」ではInstance ID順です。シーンの追加読み込みでは自動再検索し、実行中にRigを生成した場合は `Rigを再検索` を押してください。

Edit Mode送信は `Send In Edit Mode` を明示的にONにした場合だけ動作します。物理照明に接続している場合は、`Host` とUniverseを確認してから有効化してください。`Send Current Frame Once` は、選択されたTimelineの現在値を強制的に再送します。

## English

`DmxTimelineArtNetOutput` forwards only buffers marked `TimelinePlayback` by `DmxRigController` to `ArtNetSender.Send()`. Output is disabled by default. Assign the sender, choose an assigned rig list or all active rigs, and select specified Universe numbers or all active Timeline Universes. An optional PlayableDirector gates output to its Playing state. Enable output in Play Mode and verify `Packets Sent` plus the monitors' Sender Output source. Edit Mode transmission requires a separate opt-in. Unchanged frames are resent every second by default, with a 30 Hz rate cap.
