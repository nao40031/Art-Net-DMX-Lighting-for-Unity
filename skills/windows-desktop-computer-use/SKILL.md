---
name: windows-desktop-computer-use
description: Discover and use the available Computer Use API for Windows desktop app tasks. Use when controlling a local app, verifying UI input, or diagnosing an apparently missing desktop connection.
---

# Windows desktop Computer Use

Discover the API exposed to the current session before deciding whether a Windows app can be controlled. Tool availability can differ between sessions.

## Discover the active surface

1. Inspect the available tools and their documented APIs. Check whether `node_repl` and the bundled `computer-use` skill are available.
2. When the bundled skill is available, read its `SKILL.md` and required guidance before using Windows UI actions. In a fresh `node_repl` session, try its documented `@oai/sky` import; confirm availability from the result, not from the tool name alone.
3. If Sky initializes, call `sky.list_apps()` and `sky.list_windows()`. Select exactly one returned window for the intended app and project, then call `sky.get_window_state()` with a screenshot to confirm its title and visible state. Use only window identifiers returned by Sky.
4. If Sky is unavailable, inspect the APIs actually documented for another Computer Use surface in this session. A browser-oriented surface returning an empty app list does not establish that Windows desktop control is unavailable. Record the import or connection error and which API calls succeeded before reporting a limitation.

The Sky calls above were confirmed in a Windows Codex session on 2026-10-15. Their presence in a different session must be checked again. For method signatures and input objects, use the bundled `computer-use` skill's API reference when present; do not invent calls.

## Act on observed state

- Read the project's `AGENTS.md` and any app-specific runbook before acting. Confirm the selected window from a fresh observation, then perform one state-derived action and observe again. A successful API response is not proof that the screen changed.
- For native file pickers, click the filename field directly, enter the existing file's absolute path with the supported input method, and visually verify the entire value. If the field is horizontally scrolled, expose the start and end before proceeding.
- If typing is not visible, recheck the foreground window and focus, click the field again, and retry after a fresh observation. Use a dialog shortcut such as `Alt+N` or paste only when supported and verify the visible result. Cancel an input-only check without opening the file.
- Follow the bundled Computer Use confirmation guidance for Windows UI actions. Obtain any authorization required by the task before submitting, importing, deleting, or sharing data.

Keep app menu paths, fixture names, and machine-specific absolute paths in the project runbook or the active task, not in this shared skill.
