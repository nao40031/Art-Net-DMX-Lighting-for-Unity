# Art-Net DMX Lighting for Unity

## 導入と更新

Package Managerから更新する通常の導入では、次のGit URLを入力します。

```text
https://github.com/nao40031/Art-Net-DMX-Lighting-for-Unity.git?path=/Packages/jp.oshino.art-net-dmx-lighting-for-unity#upm/release
```

一度このURLで導入すれば、以後はPackage Managerの **Update** ボタンで `upm/release` に公開された最新版を取得できます。

特定バージョンに固定する場合は、代わりにタグ付きURLを使います。タグは自動で進まないため、更新時はURL内のタグを新しい版へ変更してください。

```text
https://github.com/nao40031/Art-Net-DMX-Lighting-for-Unity.git?path=/Packages/jp.oshino.art-net-dmx-lighting-for-unity#v0.1.8
```

すでにタグ付きURLを導入している場合は、一度だけ **Add package from git URL...** で上記の `#upm/release` URLへ切り替えてください。以後のリリースは **Update** から導入できます。

Art-Net/DMX を受信し、Fixture 定義に基づいて Unity のライト、Pan/Tilt、カラー、ゴボを制御するパッケージです。

## 固定版の導入

Unity の **Package Manager** で **Add package from git URL...** を選び、次を入力します。

```text
https://github.com/nao40031/Art-Net-DMX-Lighting-for-Unity.git?path=/Packages/jp.oshino.art-net-dmx-lighting-for-unity#v0.1.8
```

特定バージョンで固定して利用する場合に使います。更新時は、必要なバージョンのタグへURLを差し替えてください。

## まず試す

`Runtime/LightAsset/やまかライト/Prefab/Normal` 内のPrefabは、VLBなしで使用する標準版です。シーンに配置して `DmxFixtureComponent` のFixture・Universe・Start Addressを設定してください。

1. `Runtime/ArtNet/Prefabs/ArtNet.prefab` をHierarchyへドラッグ＆ドロップします。
2. 標準Prefabを配置し、`DmxFixtureComponent` のDMX設定を確認します。
3. `ArtNet Core` が灯体を自動検出して、ライブArt-Netを適用します。

`ArtNet.prefab` には、通常の受信を担当する `ArtNet Core`、DMXをAnimationClipへ記録する `ArtNet Recorder`、AnimationClipまたはTimelineを再生する `ArtNet Timeline Playback` が含まれます。初期状態は **Live / Record** プリセットです。Timeline再生を行うときは、`DmxTimelinePlayback` Inspectorの **Apply Timeline Playback** を実行してください。

## Timelineで複数Universeを再生する

`ArtNet Timeline Playback/Universes/Universe 0` は、Universe 0用の `ArtNetChannels` と `Animator` を持ちます。Universeを追加する場合は、`Runtime/ArtNet/Prefabs/ArtNet Universe.prefab` を `Universes` の子へ配置し、`ArtNetChannels` の **Universe** を重複しない番号へ設定してください。次に `DmxTimelinePlayback` Inspectorの **Auto Discover in Children** を押して、Sourcesへ登録します。Universe番号が重複している場合はSourcesを変更せず、Consoleとポップアップに該当Object名を表示します。

## VLB版（任意）

VLB版Prefabは `Samples~/VLB` に分離されています。VLBはこのパッケージの依存関係・同梱物ではないため、先にVolumetric Light Beam（VLB）を正規の配布元から導入してください。

VLBを導入した後、次の手順でPrefabをImportします。

1. Unityで **Window > Package Manager** を開き、**In Project** から **Art-Net DMX Lighting for Unity** を選択します。
2. 詳細欄の **Samples** で **VLB** の **Import** を押します。
3. `Assets/Samples/Art-Net DMX Lighting for Unity/0.1.8/VLB/` にコピーされた `MovingLight_withGobo(MAC Ultra)_VLB.prefab` をシーンへ配置します。

VLBを導入せずにImportするとMissing Scriptが表示される可能性があります。その場合はVLBを導入後、Samplesの **Reimport** を実行してください。VLBなしでも通常版PrefabとArt-Net/DMX機能は利用できます。

## 前提

- Unity 6000.0以降
- HDRP 17.0.4以降（パッケージ依存関係として自動導入されます）
- Timeline再生機能を利用する場合はUnity Timeline

## ライセンス

本パッケージ内のスクリプトおよびUnityアセットはMIT Licenseです。VLBおよびその他の外部アセットは、それぞれのライセンスに従ってください。
