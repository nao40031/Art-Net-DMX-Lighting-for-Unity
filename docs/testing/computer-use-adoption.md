# Reuse Windows desktop Computer Use in another project

The reusable skill is [`skills/windows-desktop-computer-use/SKILL.md`](../../skills/windows-desktop-computer-use/SKILL.md). It covers API discovery, window selection, and visible input checks. Keep app menu paths and project data in that project's own runbook.

## Install once per Codex environment

Ask Codex to install the skill from:

`https://github.com/nao40031/Art-Net-DMX-Lighting-for-Unity/tree/release/skills/windows-desktop-computer-use`

Ask Codex to use `skill-installer` with its Git method (`--method git`) for this repository. Its default ZIP method can fail while extracting unrelated Japanese-named assets in the repository; Git's sparse checkout fetches the requested skill path. Start a new session after installation so the skill is discoverable. An environment that already has this skill can skip installation and update it from the same source when needed.

## Add a short project instruction

Add this to the other project's root `AGENTS.md` (adjust the runbook link if there is one):

```md
## Windows desktop GUI automation

- Before operating a Windows desktop app, use the `windows-desktop-computer-use` skill to discover the current Computer Use API and verify the selected window and input on screen. Follow any project-specific GUI runbook for app actions.
```

For a repository that needs its instructions to work before skill installation, also copy the skill folder into that repository and link its `SKILL.md` from `AGENTS.md`. Choose one maintained source for later updates to avoid divergent copies.

## First-use check

In the new session, confirm that the skill appears in the available skills. Ask Codex to inspect the current Computer Use tools, identify an already-open target window, and obtain a screenshot. Check the window title and rendered image before trying input. If the API is absent, report the documented surface and actual error; do not diagnose permissions from a browser-only app inventory.
