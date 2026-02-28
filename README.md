# Art-Net DMX Lighting for Unity

Unity上でArt-Net/DMXを受信し、Fixture単位でライト・Pan/Tilt・レンズ表現まで制御するシステムです。  
ライブ受信とTimeline再生の両方に対応しています。

## ドキュメント

- クイックスタート: この `README.md`
- 開発スクリプト詳細版: [docs/DEVELOPED_SCRIPTS_GUIDE_JA.md](./docs/DEVELOPED_SCRIPTS_GUIDE_JA.md)

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
$projectPath = (Resolve-Path .).Path
Write-Host "取得完了: $projectPath"
```

`git clone` は、PowerShellを開いている現在のフォルダ配下に作成されます。  
現在位置の確認は `pwd`、任意の保存先に移動する場合は `cd <保存先パス>` を先に実行してください。

```powershell
pwd
```

エクスプローラーは自動では開きません。必要なら以下で開けます。

```powershell
explorer .
```

6. Unity Hub で `Add` を押し、`Art-Net-DMX-Lighting-for-Unity` フォルダを選択して開きます。

`winget` が使えない場合は、Git と Git LFS を通常インストーラーで入れた後に手順 4 以降を実行してください。

### 取得確認

以下を実行して、LFSファイル一覧が表示されれば取得設定は有効です。

```powershell
git lfs ls-files
```

`.unity` ファイルの先頭が `%YAML 1.1` なら実体です。  
`version https://git-lfs.github.com/spec/v1` の場合は、`git lfs pull` を再実行してください。

## このリポジトリでできること

- Art-Net DMX受信（Universe単位）
- Fixtureプロファイルに基づくDMX制御
- カラー（RGB）/Dimmer/Pan/Tiltの適用
- Built-in/URP向けとHDRP向けのLightDriver切替
- Timeline経由の再生（`ArtNetChannels` + `DmxTimelinePlayback`）
- DMX記録とAnimationClip書き出し（Recorder）
- Editor拡張によるPrefab置換・ライト複製・CSV書き出し

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

## Notion連携

このリポジトリのドキュメントは、Notion取り込みしやすいMarkdown形式にしています。  
以下の2ファイルをそのまま `Import > Markdown & CSV` で読み込めます。

- `README.md`
- `docs/DEVELOPED_SCRIPTS_GUIDE_JA.md`

## 補足

- 詳細な各スクリプトのparameter仕様は、詳細版ドキュメントを参照してください。
- `Assets/Editor` はEditor拡張、`Assets/ArtNet` はランタイム/再生系の本体です。

