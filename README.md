# Art-Net DMX Lighting for Unity

Unity上でArt-Net/DMXを受信し、Fixture単位でライト・Pan/Tilt・レンズ表現まで制御するシステムです。  
ライブ受信とTimeline再生の両方に対応しています。

## ドキュメント

- クイックスタート: この `README.md`
- 開発スクリプト詳細版: [docs/DEVELOPED_SCRIPTS_GUIDE_JA.md](./docs/DEVELOPED_SCRIPTS_GUIDE_JA.md)

## このリポジトリでできること

- Art-Net DMX受信（Universe単位）
- Fixtureプロファイルに基づくDMX制御
- カラー（RGB）/Dimmer/Pan/Tiltの適用
- Built-in/URP向けとHDRP向けのLightDriver切替
- Timeline経由の再生（`ArtNetChannels` + `DmxTimelinePlayback`）
- DMX記録とAnimationClip書き出し（Recorder）
- Editor拡張によるPrefab置換・ライト複製・CSV書き出し

## MagicQ Showデータ

MagicQ の show データをリポジトリ内に同梱しています。  
GitHub の `Download ZIP` / `git clone` のどちらでも取得できます。

- 保存先: `MagicQ/show`
- [ArtNetTest_LiveLightingTest6(Public).sbk](./MagicQ/show/ArtNetTest_LiveLightingTest6%28Public%29.sbk)
- [ArtNetTest_LiveLightingTest6(Public).shw](./MagicQ/show/ArtNetTest_LiveLightingTest6%28Public%29.shw)
- [ArtNetTest_LiveLightingTest6(Public).xhw](./MagicQ/show/ArtNetTest_LiveLightingTest6%28Public%29.xhw)

使用する場合は、必要に応じて `C:\Users\<ユーザー名>\Documents\MagicQ\show` 配下にコピーしてください。

## プロジェクトの取得方法（推奨）

このリポジトリには Git LFS 管理ファイル（`.unity` / `.fbx` など）が含まれます。  
`Download ZIP` では実体ではなくポインタファイルになる場合があるため、以下の手順で取得してください。

1. PowerShell を「通常権限」で開き、`winget` が使えるか確認します。

```powershell
winget --version
```

2. Git をインストールします。

```powershell
winget install --id Git.Git -e --source winget
```

3. Git LFS をインストールします。

```powershell
winget install --id GitHub.GitLFS -e --source winget
```

4. PowerShell を一度閉じて開き直し、インストール確認をします。

```powershell
git --version
git lfs version
```

5. 取得コマンドを実行します（`git lfs install` は最初の1回だけ）。

```powershell
git lfs install
git clone https://github.com/nao40031/Art-Net-DMX-Lighting-for-Unity.git
cd Art-Net-DMX-Lighting-for-Unity
git lfs pull
git lfs checkout
```

6. 取得したフォルダの場所は、次のコマンドを実行して確認します。

```powershell
Write-Host "取得完了フォルダ: $((Get-Location).Path)"
```

必要なら次を実行して、取得フォルダをエクスプローラーで開けます。

```powershell
explorer .
```

`git clone` は、PowerShellを開いている現在のフォルダ配下に作成されます。  
現在位置の確認は `pwd`、任意の保存先に移動する場合は `cd <保存先パス>` を先に実行してください。

```powershell
pwd
```

PowerShell のプロンプト（`PS C:\...\Art-Net-DMX-Lighting-for-Unity>`）に表示されるパスも、同じ取得先フォルダです。

7. Unity Hub で `Add` を押し、`Art-Net-DMX-Lighting-for-Unity` フォルダを選択して開きます。

`winget` が使えない場合は、Git と Git LFS を通常インストーラーで入れた後に手順 4 以降を実行してください。

### macOSで取得する場合

1. ターミナルを開き、`brew` が使えるか確認します。

```bash
brew --version
```

2. Git と Git LFS をインストールします。

```bash
brew install git git-lfs
```

3. インストール確認をします。

```bash
git --version
git lfs version
```

4. 取得コマンドを実行します（`git lfs install` は最初の1回だけ）。

```bash
git lfs install
git clone https://github.com/nao40031/Art-Net-DMX-Lighting-for-Unity.git
cd Art-Net-DMX-Lighting-for-Unity
git lfs pull
git lfs checkout
```

5. 取得したフォルダの場所は、次のコマンドを実行して確認します。

```bash
echo "取得完了フォルダ: $(pwd)"
```

必要なら次を実行して、Finderで取得フォルダを開けます。

```bash
open .
```

`git clone` は、ターミナルを開いている現在のフォルダ配下に作成されます。  
現在位置の確認は `pwd`、任意の保存先に移動する場合は `cd <保存先パス>` を先に実行してください。

`brew` が使えない場合は、Homebrew を導入した後に手順 1 以降を実行してください。


## クイックスタート（ライブ受信）

1. シーンに `ArtNetReceiver` を配置し、`host` と `port`（通常 `6454`）を設定します。
2. シーンに `DmxRigController` を配置し、`receiver` を割り当てます。
3. 制御対象オブジェクトに `DmxFixtureComponent` を追加します。
4. `DmxFixtureComponent` に `fixture`（`FixtureDefinition`）と `mode`、`universe`、`startAddress` を設定します。
5. `targetLight`/`targetLights`、必要に応じて `panTransform`/`tiltTransform` を設定します。
6. `DmxRigController` の `Discover & Initialize Fixtures` を実行します。
7. Art-Net送信側からDMXを送信して動作確認します。

## クイックスタート（Timeline再生）

1. `ArtNetChannels` を再生ソース用オブジェクトに追加し、`Ch1..Ch512` をAnimation/Timelineで駆動します。
2. `DmxTimelinePlayback` を配置し、`rig` に `DmxRigController` を割り当てます。
3. `sources` に `universe` + `ArtNetChannels` の組を追加します。
4. 必要に応じて `overrideRigInputMode` を有効化し、`PlaybackOnly` で再生します。

## レコーディング（DMXからClip化）

1. `ArtNetReceiverDmxRecorder`（`ArtNetDataRecorder.cs`）を配置し `receiver` を設定します。
2. `Start Recording` で録画開始、`Stop & Save` で停止保存します。
3. `Assets/<directoryPath>` に `ArtNetChannels` 向けAnimationClipが保存されます。

## 補足

- 詳細な各スクリプトのparameter仕様は、詳細版ドキュメントを参照してください。
- `Assets/Editor` はEditor拡張、`Assets/ArtNet` はランタイム/再生系の本体です。

## License

- This project (scripts and Unity assets in this repository) is provided under the **MIT License**.
- Unity-chan-related assets use **Unity-chan License 3.0 (UCL 3.0)**.

### Unity-chan License 3.0 documents
- `Assets/Avatar/Unity-chan/License/EN_Unity-Chan License Terms and Condition_UCL3.0.pdf`
- `Assets/Avatar/Unity-chan/License/JP_Unity-Chan License Terms and Condition_UCL3.0.pdf`
- `Assets/Avatar/Unity-chan/License/License Logo/` (logo usage/identity guidance)
