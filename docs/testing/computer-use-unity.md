# Unity Editor Computer Use: GUI input and file-dialog runbook

> Status: working procedure based on manual observations reported on 2026-10-15.  
> Scope: Windows, Unity Editor, Codex Computer Use (`@oai/sky`).  
> This document is a troubleshooting runbook, not proof that every environment or dialog will behave the same way.

## Observed results

| Target | Observed result |
| --- | --- |
| Notepad plain-text field | `sky.type_text()` and keyboard operations worked |
| Unity Inspector editable name field | `sky.type_text()` worked; the original name was restored |
| Notepad **Save As** filename field | Text entry worked; saving was canceled |
| Unity GDTF file picker | Direct text entry, text entry after `Alt+N`, and `Ctrl+V` paste worked on the later retry |
| Immediately after switching back from Notepad | `Alt+N` alone did not reliably show filename-field selection |
| Earlier Unity GDTF picker attempt | Text entry did not show up; the failure could not be reproduced on the later retry |

**Important:** The root cause of the earlier failure is **not confirmed**. Foreground-window state and keyboard focus are plausible explanations; process privileges were not confirmed in the observed environment.

## Standard procedure: choose a GDTF file

1. Make sure the correct Unity Editor window and project are visible. Avoid typing while another app or dialog is in front.
2. Open the project's GDTF import flow and wait until the Windows file picker is fully visible.
3. **Click directly into the filename field** (do not assume `Alt+N` has focused it). Capture a screenshot or inspect the window state to verify the active dialog and field selection.
4. Select any existing field text with `Ctrl+A` as appropriate, then use the supported `sky.type_text()` operation to enter the **absolute path** of an existing `.gdtf` file.
5. Recheck the screen. Only after the full path is visibly present should the agent click **Open** or press Enter.
6. Verify the Unity import result using the resulting UI and Unity Console. An API call reporting success is not equivalent to a visible UI change.

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
- [ ] Selected file exists and the import results are visible in the Editor
- [ ] Unity Console checked for logs, warnings, and errors
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
