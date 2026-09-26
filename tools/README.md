# Unity AI CLI tools

Codex / Claude Code などのAIエージェントから、Unity EditorをCLIで検証するための共通スクリプトです。

## 前提

- Windows PowerShell 5.1 以降
- Unity.exe が PATH に入っていること
- または各コマンドで -UnityPath を指定
- コマンドはUnityプロジェクトのルートから実行

## コンパイルチェック

~~~powershell
powershell -ExecutionPolicy Bypass -File .\tools\unity-check.ps1
~~~

Unityをbatch modeで起動し、C#コンパイルを行います。
ログは Logs/AI/unity-check.log に保存し、コンパイルエラーだけをコンソールへ要約します。

## EditMode Test

~~~powershell
powershell -ExecutionPolicy Bypass -File .\tools\unity-test.ps1 -Mode EditMode
~~~

## PlayMode Test

~~~powershell
powershell -ExecutionPolicy Bypass -File .\tools\unity-test.ps1 -Mode PlayMode
~~~

特定テストだけ実行する場合:

~~~powershell
powershell -ExecutionPolicy Bypass -File .\tools\unity-test.ps1 -Mode EditMode -TestFilter "Namespace.ClassName"
~~~

## ログ要約

~~~powershell
powershell -ExecutionPolicy Bypass -File .\tools\unity-log.ps1 -Type Check
~~~

Type は Check / EditMode / PlayMode を指定できます。

## 別Unityバージョンを使う場合

~~~powershell
powershell -ExecutionPolicy Bypass -File .\tools\unity-check.ps1 -UnityPath "C:\Program Files\Unity\Hub\Editor\2022.3.xf1\Editor\Unity.exe"
~~~

スクリプト自体は特定Unityバージョンに固定していません。

## Unity Editorを開いている場合

同じプロジェクトがUnity Editorで開かれている可能性がある場合、Temp\UnityLockfile を検出して処理を停止します。
原則としてEditorを閉じてからCLI検証してください。

-AllowOpenEditor で回避できますが、同一プロジェクトを複数Editorプロセスから開くリスクがあるため通常は使用しません。

## AIエージェント向け運用

1. C#変更後に unity-check.ps1 を実行
2. 失敗した場合は表示されたエラーだけを先に確認
3. 必要箇所だけ修正
4. 再度 unity-check.ps1
5. 関連テストがある場合だけ unity-test.ps1
6. 巨大なUnityログ全体は、要約だけで原因が分からない場合に限り読む

Library/、Temp/、Logs/ の全走査は原則不要です。
