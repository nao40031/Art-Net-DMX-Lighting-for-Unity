# Unity Editor Computer Use: GUI input and file-dialog runbook

> Status: working procedure based on manual observations reported on 2026-10-15.
> Scope: Windows, Unity Editor, Codex Computer Use (`@oai/sky`).
> This document is a troubleshooting runbook, not proof that every environment or dialog will behave the same way. The file-picker input and cancel flow was verified again on 2026-10-16 in Unity 6.0 (6000.0.48f1).

## Discover the API in the current session

Follow the reusable [`windows-desktop-computer-use` skill](https://github.com/nao40031/codex-windows-desktop-computer-use/tree/main/skills/windows-desktop-computer-use) for the current API discovery and window-selection rules. The examples below record the API that worked during this incident; check availability again in each session.

Check the tools exposed to the current Codex session before concluding that desktop control is unavailable. On 2026-10-15, `node_repl` with `@oai/sky` controlled Windows applications, while the available `cua_repl` instance exposed browser methods and returned an empty `apps` list. That browser result did not describe Sky's desktop access.

When `node_repl` is available, initialize Sky in its persistent JavaScript session:

```js
if (!globalThis.sky) {
  const { sky } = await import("@oai/sky");
  globalThis.sky = sky;
}
globalThis.apps = await sky.list_apps();
globalThis.windows = await sky.list_windows();
nodeRepl.write(JSON.stringify(windows.filter(w =>
  /Unity|Notepad|メモ帳/i.test(`${w.app} ${w.title}`)
), null, 2));
```

Choose **exactly one** returned Unity Editor window for the intended project. Use its returned `id` and `app` rather than constructing a handle. Then inspect it:

```js
const matches = windows.filter(w =>
  /Unity\.exe$/i.test(w.app) && w.title?.includes("<project name>")
);
if (matches.length !== 1) throw new Error(`Expected one Editor window; found ${matches.length}`);
globalThis.unityWindow = await sky.get_window({ id: matches[0].id, app: matches[0].app });
globalThis.unityState = await sky.get_window_state({
  window: unityWindow, include_screenshot: true, include_text: true
});
globalThis.unityWindow = unityState.window;
```

Inspect the rendered screenshot and the returned window title. A loading window such as `Compiling Scripts` is not yet the ready Editor; list windows again after it finishes because the window ID can change. Before each click or text entry, use a fresh observation and verify the result on screen.

If `node_repl` or the `@oai/sky` import is unavailable, inspect the tools and API documentation **actually exposed in that session**. Check the returned method list or a documented read-only inventory call; do not assume that methods described by another Computer Use surface exist. If Sky lists no Unity Editor window, check whether the Editor is running; Unity Hub alone is a different window. Record any import, connection, or permission error verbatim before attributing a cause.

### 2026-10-15 API discovery incident

An empty `cua.getState().apps` result was initially treated as a loss of local Windows access. The active `cua` object exposed browser methods, but `getApp`, `listApps`, and `listWindows` were absent. A subsequent tool inventory found `node_repl`; importing `@oai/sky` succeeded. `sky.list_apps()` found Notepad and Unity Hub, `sky.list_windows()` found the Unity Editor after it was opened from Hub, and `sky.get_window_state()` returned its screenshot. The earlier conclusion was caused by overlooking the available Sky tool, not by an observed Windows permission error. A shell process listing also failed to show apps that Sky found, so use Sky's window inventory to decide whether Sky can target a GUI window.

## Observed results

| Target | Observed result |
| --- | --- |
| Notepad plain-text field | `sky.type_text()` and keyboard operations worked |
| Unity Inspector editable name field | `sky.type_text()` worked; the original name was restored |
| Notepad **Save As** filename field | Text entry worked; saving was canceled |
| Unity GDTF file picker | Direct text entry, text entry after `Alt+N`, and `Ctrl+V` paste worked on the later retry |
| Unity GDTF file picker, 2026-10-16 | Direct click and `sky.type_text()` entered an existing GDTF archive's absolute path. The complete value was visible after enlarging the dialog and pressing Home; Cancel closed the dialog without importing. |
| Immediately after switching back from Notepad | `Alt+N` alone did not reliably show filename-field selection |
| Earlier Unity GDTF picker attempt | Text entry did not show up; the failure could not be reproduced on the later retry |

**Important:** The root cause of the earlier failure is **not confirmed**. Foreground-window state and keyboard focus are plausible explanations; process privileges were not confirmed in the observed environment.

## Screenshot capture

For screenshots of the Editor or its dialogs, follow the shared skill's general rule: maximize the target window when possible, verify the relevant content is visible, and enlarge dialogs that cannot be maximized.

## Standard procedure: choose a GDTF file

1. Make sure the correct Unity Editor window and project are visible. Avoid typing while another app or dialog is in front.
2. Open the project's GDTF import flow and wait until the Windows file picker is fully visible.
3. **Click directly into the filename field** (do not assume `Alt+N` has focused it). Capture a screenshot or inspect the window state to verify the active dialog and field selection.
4. Select any existing field text with `Ctrl+A` as appropriate, then use the supported `sky.type_text()` operation to enter the **absolute path** of an existing `.gdtf` file.
5. Recheck the screen. Only after the full path is visibly present should the agent click **Open** or press Enter.
6. Verify the Unity import result using the resulting UI and Unity Console. An API call reporting success is not equivalent to a visible UI change.

For an input-only check, enlarge the dialog or press Home if the field is horizontally scrolled, visually check the full path, and click **Cancel** without opening the file. Do not report an import result for that check.

Avoid hardcoding user-specific drive letters or paths in shared documentation. Resolve the file's existing absolute path within the active environment.

## Recovery ladder if typing appears to fail

1. **Inspect focus:** screenshot/window-state check; click the filename input again; retry typing.
2. **Use a shortcut:** try `Alt+N` if the dialog supports it, but check visually whether the filename field was actually focused.
3. **Paste:** if a permitted, already-tested method can place the desired path on the clipboard, click the filename field and use `Ctrl+V`. Confirm the pasted text is visible.
4. **Compare apps:** test entry in Notepad's normal field, Notepad's Save As dialog, and Unity Inspector. Record which contexts fail, without modifying project data.
5. **Reopen the dialog:** close and reopen it; ensure the intended Unity process is foreground, then retry the direct-click method.
6. **Avoid blocking work:** if GUI entry remains unreliable, use an **existing** Unity Editor API/import entry point for deterministic import tests, and reserve Computer Use for the final visual checks. Do not change import or correction logic solely to work around a picker problem.

Do not assume `sky.set_value()` can manipulate a native Windows dialog: an earlier attempt returned that the target was not an editable/settable element. Do not assume `Alt+N` alone is sufficient after an application switch.

## Verification checklist

- [ ] Correct Unity project/window is foreground and the expected dialog is visible
- [ ] Filename field has been explicitly clicked (or focus otherwise visibly verified)
- [ ] Full GDTF path is visibly present before clicking Open
- [ ] Selected file exists; if importing, the results are visible in the Editor
- [ ] For an input-only check, Cancel closes the dialog without importing
- [ ] If importing, Unity Console checked for logs, warnings, and errors
- [ ] UI output validated separately from any API/test return values
- [ ] Any temporary assets or test-only menu entries removed; original project data preserved

## Incident notes: how to report a recurrence

Record:
- Unity version, OS, Computer Use integration/version (if known)
- Which application/dialog was foreground; whether the field was clicked
- Exact operation attempted (`sky.type_text`, key press, paste, etc.) and return/error
- Whether characters were **visibly** inserted; screenshot before/after
- Comparison with Unity Inspector / Notepad Save As
- Any difference after switching applications, reopening the dialog, or restarting Unity

Redact personal paths, credentials, and unrelated screen content before publishing screenshots or logs.

## Separation of concerns

- **Computer Use:** visual checks of windows, editable fields, buttons, review dialogs, and screenshots.
- **Unity Editor API / EditMode tests:** reproducible GDTF import, structural validation, data diff checks, and test assertions.
- **Human/agent review:** approve changes to real fixture data only after the proposed differences and targets are explicitly verified.

For example, an import producing fixture types or passing EditMode tests does **not** by itself establish that a Review dialog's Apply/Approve buttons were exercised. Track those as separate verification items.

## Agent reminder

Before declaring a GUI step blocked, retry with a direct click into the filename input and verify the **rendered** result. Do not infer failure or success from the input API response alone. Preserve import/correction logic unless the development task separately calls for changing it.
