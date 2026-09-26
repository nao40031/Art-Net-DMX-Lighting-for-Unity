# CLAUDE.md

このUnityプロジェクトでClaude Codeが作業するときは、まず AGENTS.md のプロジェクトルールを読み、その内容を優先してください。

## Unity CLI validation

C#コードを変更した場合は、可能な限り次を実行してください。

~~~powershell
powershell -ExecutionPolicy Bypass -File .\tools\unity-check.ps1
~~~

- エラー時は、まずスクリプトが出力したコンパイルエラーだけを確認する。
- Logs/AI/ のログ全文は、要約だけで原因を特定できない場合に限って読む。
- Library/、Temp/、Logs/ を無目的に全走査しない。
- 関連するUnity Test Frameworkテストがある場合は unity-test.ps1 を実行する。
- 同一プロジェクトをUnity Editorで開いたままCLIから別Editorを起動しない。
- CLIを実行できない場合は、理由と未検証事項を最終報告に明記する。
