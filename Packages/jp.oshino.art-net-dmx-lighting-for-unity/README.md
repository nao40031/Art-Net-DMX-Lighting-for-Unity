# Art-Net DMX Lighting for Unity

Art-Net/DMX を受信し、Fixture 定義に基づいて Unity のライト、Pan/Tilt、カラー、ゴボを制御するパッケージです。

## 導入

Unity の **Package Manager** で **Add package from git URL...** を選び、次を入力します。

```text
https://github.com/nao40031/Art-Net-DMX-Lighting-for-Unity.git?path=/Packages/jp.oshino.art-net-dmx-lighting-for-unity#v0.1.3
```

`v0.1.3` タグの公開後に利用できます。公開前は、対象ブランチまたはコミットSHAを指定してください。

## まず試す

`Runtime/LightAsset/やまかライト/Prefab/Normal` 内のPrefabは、VLBなしで使用する標準版です。シーンに配置して `DmxFixtureComponent` のFixture・Universe・Start Addressを設定してください。

1. `Runtime/ArtNet/Prefabs/ArtNet.prefab` をHierarchyへドラッグ＆ドロップします。
2. 標準Prefabを配置し、`DmxFixtureComponent` のDMX設定を確認します。
3. `ArtNet Core` が灯体を自動検出して、ライブArt-Netを適用します。

`ArtNet.prefab` には、通常の受信を担当する `ArtNet Core`、DMXをAnimationClipへ記録する `ArtNet Recorder`、AnimationClipまたはTimelineを再生する `ArtNet Timeline Playback` が含まれます。初期状態は **Live / Record** プリセットです。Timeline再生を行うときは、`DmxTimelinePlayback` Inspectorの **Apply Timeline Playback** を実行してください。

## Timelineで複数Universeを再生する

`ArtNet Timeline Playback/Universes/Universe 0` は、Universe 0用の `ArtNetChannels` と `Animator` を持ちます。Universeを追加する場合は、`Runtime/ArtNet/Prefabs/ArtNet Universe.prefab` を `Universes` の子へ配置し、`ArtNetChannels` の **Universe** を重複しない番号へ設定してください。`DmxTimelinePlayback` は `Universes` 配下を自動検出するため、Sourcesを手動で追加する必要はありません。

## VLB版（任意）

VLB版Prefabは `Samples~/VLB` に分離されています。Package Managerの **Samples** からImportする前に、Volumetric Light Beam（VLB）を正規の配布元から導入してください。

VLBはこのパッケージの依存関係・同梱物ではありません。VLBなしでも通常版PrefabとArt-Net/DMX機能は利用できます。

## 前提

- Unity 6000.0以降
- HDRP 17.0.4以降（パッケージ依存関係として自動導入されます）
- Timeline再生機能を利用する場合はUnity Timeline

## ライセンス

本パッケージ内のスクリプトおよびUnityアセットはMIT Licenseです。VLBおよびその他の外部アセットは、それぞれのライセンスに従ってください。
