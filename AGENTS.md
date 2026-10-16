# Unity / Art-Net project rules

- Make the smallest change that satisfies the request; inspect first and treat investigation requests as read-only.
- Before editing, present the target files, implementation, impact, validation, and (when meaningful) a lower-cost alternative; wait for approval unless the user says to implement directly.
- Keep Unity Inspector, EditorWindow, and log text as Japanese paragraphs followed by one blank line and English paragraphs; do not alternate languages sentence by sentence.
- Preserve existing Unity conventions, APIs, and behavior. Avoid unrelated refactors and project-specific absolute paths or object names in reusable code.
- Search only the necessary scope: start with `Assets/` and custom packages under `Packages/`; exclude `Library/`, `Temp/`, `Logs/`, `obj/`, `Build/`, `Builds/`, and `UserSettings/`. Keep command, diff, and test output concise.
- Do not break `.meta`/GUIDs or Prefab, Scene, Timeline, Animator, and ScriptableObject references. Verify YAML/GUID references when changing serialized assets. Check Git/LFS before adding or changing large binaries.
- Preserve serialized field names, types, defaults, and attributes; use an explicit migration when a change is unavoidable.
- Respect MonoBehaviour lifecycle ordering and symmetric subscription cleanup. Do not add LINQ, allocations, reflection, scene-wide searches, or repeated component lookups to per-frame paths; cache and use event/delta updates where practical.
- Preserve Art-Net/DMX Universe and Address mapping, channel widths, coarse/fine order, 8/16-bit conversion, packet timing, and unreceived-data behavior. Never guess a 0-based/1-based boundary.
- Preserve Pan/Tilt axes, direction, range, clamp, normalization, initial pose, and parent-transform behavior; likewise preserve Invert, Offset, and Smoothing order, units, signs, defaults, and delta-time behavior.
- Use a single agent by default. Use one read-only subagent only when explicitly requested or a focused, independent investigation is genuinely necessary; give it a narrow question and paths, and do not repeat exploration.
- Validate only the changed scope. Report changed files, validation, use/setup notes when relevant, and remaining risks concisely.

## Unity GUI automation / Computer Use

- Before Windows desktop GUI automation, use the [reusable `windows-desktop-computer-use` skill](https://github.com/nao40031/codex-windows-desktop-computer-use/tree/main/skills/windows-desktop-computer-use) for API discovery and screen verification.
- When using Codex Computer Use to interact with Unity Editor, Windows file pickers, or GDTF import/review dialogs, **read and follow** [`docs/testing/computer-use-unity.md`](docs/testing/computer-use-unity.md) before the GUI operation.
- Before capturing a screenshot of Unity Editor, maximize its window and confirm the intended content is visible. For a modal dialog, maximize it if possible or enlarge it enough to show the relevant content; restore the prior window size afterward when needed.
- In a Windows file picker, explicitly click the filename field, enter or paste the file path, and **visually confirm** the complete value before pressing Open. Never treat a successful tool response as proof the UI changed.
- If text entry fails, use the runbook's focus and recovery checks before declaring the step blocked. Report any unresolved failure with UI evidence; do not bypass existing import/correction behavior or modify production logic just to work around automation.
- Apply this section only to GUI automation. Keep normal code edits and investigations on the project's existing minimal-change and validation rules.
