# Timeline DMX → Art-Net 出力

`DmxTimelineArtNetOutput` は、`DmxTimelinePlayback` が `DmxRigController` に反映したDMX値を `ArtNetSender.Send()` に渡します。Live Inputや外部注入の値は転送しません。`Enable Output` は初期状態でONです。

1. シーンに `ArtNetSender` を配置し、`Host` と `Port`（通常6454）を設定します。
2. GameObjectに `DmxTimelineArtNetOutput` を追加し、`Sender` を指定します。
3. `Rig Selection` は、単一Rigなら `Assigned` のままで自動検出できます。複数Rigを順序付きで登録するか、`All Active` を選べます。
4. `Universe > Selection` で `Specified`（番号リスト、0～32767）または `All` を選びます。`All` は現在Timeline値を持つUniverseだけを対象にします。
5. 必要なら `Playable Director (Optional)` を指定します。指定するとDirectorが再生中のときだけ送信します。未指定の場合はRigのTimeline更新を送信します。
6. Play Modeで `Packets Sent` とChannel/Activity Monitorの `Sender Output` を確認します。送信しない場合は `Enable Output` をOFFにします。

送信レート上限は30Hz、値が変わらないときの維持送信は1秒間隔です。後者は0で無効にできます。複数Rigが同じUniverseを出す場合、指定リストの先頭が優先されます。「有効なRigすべて」ではInstance ID順です。シーンの追加読み込みでは自動再検索し、実行中にRigを生成した場合は `Rigを再検索` を押してください。

Edit Mode送信は `Send In Edit Mode` を明示的にONにした場合だけ動作します。物理照明に接続している場合は、`Host` とUniverseを確認してから有効化してください。`Send Current Frame Once` は、選択されたTimelineの現在値を強制的に再送します。

`Communication Mode` は初期状態で `Unicast` です。`Host = 127.0.0.1` は同じPC宛て、それ以外の `Host` は指定したIP宛てです。`Broadcast` を選ぶと `Broadcast Address` に送信します。初期値は `255.255.255.255` で、対象ネットワークのブロードキャストアドレス（例: `192.168.1.255`）も指定できます。`Local Interface Address` には送信元NICのIPv4アドレスを指定できます。`0.0.0.0` ならOSの選択に任せます。`Send To Loopback` はネットワークの送信に加えて同じPCの `127.0.0.1` にも送信します。ブロードキャストは実機にも届き得るため、宛先とUniverseを確認してください。`Sender Output` と `Packets Sent` はUnity側で送信処理が行われたことを示し、別アプリへの到達は保証しません。送信方式や宛先、NICをPlay Mode中に変更した場合は再接続します。

## English

`DmxTimelineArtNetOutput` forwards only buffers marked `TimelinePlayback` by `DmxRigController` to `ArtNetSender.Send()`. Enable Output is on by default. Assign the sender, choose an assigned rig list or all active rigs, and select specified Universe numbers or all active Timeline Universes. An optional PlayableDirector gates output to its Playing state. Verify `Packets Sent` plus the monitors' Sender Output source in Play Mode; turn Enable Output off when transmission is not needed. Edit Mode transmission requires a separate opt-in. Unchanged frames are resent every second by default, with a 30 Hz rate cap.

`Communication Mode` defaults to `Unicast`, which sends to `Host`. `Broadcast` sends to `Broadcast Address`, initially `255.255.255.255`; a directed broadcast address such as `192.168.1.255` can be used for the intended network. `Local Interface Address` selects the outgoing adapter by its IPv4 address, or lets the OS choose when set to `0.0.0.0`. `Send To Loopback` additionally sends the same packet to `127.0.0.1` for applications on the same computer. Broadcast may reach physical fixtures, so verify the destination and Universes. Sender Output and Packets Sent confirm Unity's send path, not reception by another application. Changes to communication mode, destination, or network adapter reconnect the sender in Play Mode.
