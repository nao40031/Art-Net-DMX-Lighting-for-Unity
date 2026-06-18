Prefab Override Reverter
========================

Unity Editor tool for selectively reverting prefab instance overrides under a chosen root object.

This tool is intended for cases where many prefab instances were edited in a scene and only specific child objects or components should be reverted to their prefab values.


Install
-------

Copy this folder into another Unity project's Assets folder:

Assets/PrefabOverrideReverter

After Unity reloads scripts, open the tool from:

Tools > Prefab Override Reverter

The script is inside an Editor folder, so it is not included in builds.


Basic Workflow
--------------

Use the window from top to bottom.

1. Assign Root Object
   - The scene object whose children should be searched.
   - Example: MovingLightGroup.

2. Assign Prefab Asset
   - The prefab asset from the Project window.
   - Only scene instances that correspond to this prefab are listed.

3. Choose a preset if useful
   - Moving Light Safe Preset selects common Transform / Light targets used by moving light prefabs.
   - For other projects, manually choose components in the next section.

4. Select prefab structure targets
   - In Prefab Structure Selection, choose the prefab child GameObjects / Components that are allowed to be reverted.
   - Example:
     - PanBody / Transform
     - TiltBody / Transform
     - Spot Light / Light

5. Select target prefab instances
   - In Target Prefab Instances, choose the scene prefab instances to include.
   - Use Select All, Select None, or individual checkboxes.
   - The Select button selects and pings the scene object in the Hierarchy.

6. Scan overrides
   - Click Scan Overrides.
   - The tool finds prefab-overridden serialized properties matching the selected prefab structure and selected instances.

7. Review preview
   - Preview shows the actual property overrides that will be reverted.
   - By default, Selected only is enabled so only revert targets are shown.
   - Use Search to filter by object, component, or property name.

8. Revert
   - Click Revert Selected.
   - Confirm the dialog to revert the selected property overrides.
   - Use Unity Undo if you need to roll back the operation.


Safety Notes
------------

- The tool does not run automatically.
- It only changes scene prefab instance overrides when Revert Selected is clicked.
- It does not modify the prefab asset itself.
- It does not affect builds because it is an Editor-only script.
- The safest workflow is:

Root Object
  -> Prefab Asset
  -> Prefab Structure Selection
  -> Target Prefab Instances
  -> Scan Overrides
  -> Preview
  -> Revert Selected


Fallback Rules
--------------

Fallback Rules are optional text filters used when Prefab Asset is empty or Use Prefab Selection is off.

For most use cases, prefer Prefab Structure Selection because it is safer than name-based filtering.


Moving Light Example
--------------------

For a moving light group:

1. Set Root Object to MovingLightGroup.
2. Set Prefab Asset to the MovingLight prefab.
3. Click Moving Light Safe Preset.
4. Confirm the selected components:
   - PanBody / Transform
   - TiltBody / Transform
   - TiltBody.001 / Transform
   - Spot Light / Light
5. Select the target moving light instances.
6. Click Scan Overrides.
7. Review Preview.
8. Click Revert Selected.


============================================================
Prefab Override Reverter 日本語ガイド
============================================================

指定した Root Object 配下にある Prefab Instance の Override を、選択した範囲だけ Revert する Unity Editor ツールです。

大量の Prefab Instance を Scene 上で編集したあと、特定の子オブジェクトや Component だけを Prefab の値に戻したい場合に使います。


インストール
------------

このフォルダを、別の Unity プロジェクトの Assets フォルダ内にコピーしてください。

Assets/PrefabOverrideReverter

Unity がスクリプトをリロードしたら、以下のメニューから開けます。

Tools > Prefab Override Reverter

スクリプトは Editor フォルダ内にあるため、Build には含まれません。


基本的な使い方
--------------

ウィンドウを上から下へ順番に操作します。

1. Root Object を指定
   - 探索対象にする Scene 上の親オブジェクトを指定します。
   - 例: MovingLightGroup

2. Prefab Asset を指定
   - Project ウィンドウ内の Prefab Asset を指定します。
   - この Prefab に対応する Scene 上の Instance だけが対象になります。

3. 必要なら Preset を使う
   - Moving Light Safe Preset は、Moving Light Prefab でよく使う Transform / Light 対象を選択します。
   - 他のプロジェクトでは、次の Prefab Structure Selection で手動選択してください。

4. Prefab 構造から Revert 対象を選ぶ
   - Prefab Structure Selection で、Revert 対象にしてよい GameObject / Component を選びます。
   - 例:
     - PanBody / Transform
     - TiltBody / Transform
     - Spot Light / Light

5. 対象にする Prefab Instance を選ぶ
   - Target Prefab Instances で、Scene 上のどの Prefab Instance を対象にするか選びます。
   - Select All, Select None, 個別チェックを使えます。
   - Select ボタンを押すと、Hierarchy 上の該当オブジェクトを選択して Ping します。

6. Override をスキャン
   - Scan Overrides を押します。
   - 選択した Prefab 構造と Instance に一致する Prefab Override を検出します。

7. Preview を確認
   - Preview には、実際に Revert される Property Override が表示されます。
   - 初期状態では Selected only が ON なので、Revert 対象だけが表示されます。
   - Search で Object 名、Component 名、Property 名を絞り込めます。

8. Revert 実行
   - Revert Selected を押します。
   - 確認ダイアログで Revert を押すと実行されます。
   - 必要なら Unity の Undo で戻せます。


安全上の注意
------------

- このツールは自動実行されません。
- Revert Selected を押した時だけ Scene 上の Prefab Instance Override を変更します。
- Prefab Asset 自体は変更しません。
- Editor 専用スクリプトなので Build には影響しません。
- 安全な操作フローは以下です。

Root Object
  -> Prefab Asset
  -> Prefab Structure Selection
  -> Target Prefab Instances
  -> Scan Overrides
  -> Preview
  -> Revert Selected


Fallback Rules について
-----------------------

Fallback Rules は、Prefab Asset が未指定、または Use Prefab Selection が OFF の時に使うテキストフィルタです。

通常は、名前ベースのフィルタより安全な Prefab Structure Selection を使うことを推奨します。


Moving Light の使用例
---------------------

Moving Light グループで使う場合:

1. Root Object に MovingLightGroup を指定します。
2. Prefab Asset に MovingLight Prefab を指定します。
3. Moving Light Safe Preset を押します。
4. 以下の Component が選ばれていることを確認します。
   - PanBody / Transform
   - TiltBody / Transform
   - TiltBody.001 / Transform
   - Spot Light / Light
5. 対象にする Moving Light Instance を選びます。
6. Scan Overrides を押します。
7. Preview を確認します。
8. Revert Selected を押します。
