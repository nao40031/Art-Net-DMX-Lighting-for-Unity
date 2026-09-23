# Art-Net DMX Lighting for Unity

[![Unity CI](https://github.com/nao40031/Art-Net-DMX-Lighting-for-Unity/actions/workflows/unity-ci.yml/badge.svg?branch=release)](https://github.com/nao40031/Art-Net-DMX-Lighting-for-Unity/actions/workflows/unity-ci.yml)
[![Release](https://img.shields.io/github/v/tag/nao40031/Art-Net-DMX-Lighting-for-Unity?label=release)](https://github.com/nao40031/Art-Net-DMX-Lighting-for-Unity/tags)
[![License: MIT](https://img.shields.io/badge/license-MIT-green)](https://github.com/nao40031/Art-Net-DMX-Lighting-for-Unity/blob/release/Packages/jp.oshino.art-net-dmx-lighting-for-unity/LICENSE.md)

[English](README.md) | [日本語](README_ja.md)

A Unity system that receives Art-Net/DMX and controls lights, Pan/Tilt, and lens effects per fixture. It supports both live input and Timeline playback.

## Sample scenes

[![Sample scene 1](docs/sample-scene/scene1-thumb-new.jpg)](https://x.com/Oshino_Tech/status/2030463515891220541?s=20)  
https://x.com/Oshino_Tech/status/2030463515891220541?s=20

[![Sample scene 2](docs/sample-scene/scene2-thumb-new.jpg)](https://x.com/Oshino_Tech/status/2025485134578028589?s=20)  
https://x.com/Oshino_Tech/status/2025485134578028589?s=20

## Documentation

- Quick start: this README
- Detailed script guide: [Japanese guide](./docs/DEVELOPED_SCRIPTS_GUIDE_JA.md) *(currently available in Japanese)*
- Project documentation: [Art-Net DMX Lighting for Unity by Oshino](https://sleepy-smoke-ee3.notion.site/Art-Net-DMX-Lighting-for-Unity-by-Oshino-313d1c2c96f580be8e67eef37628ef5f?source=copy_link)

## Features

- Art-Net DMX reception by Universe
- Fixture-profile-based DMX control
- Color (RGB), Dimmer, and Pan/Tilt application
- LightDriver switching for Built-in/URP and HDRP
- Timeline playback with ArtNetChannels and DmxTimelinePlayback
- DMX recording and AnimationClip export
- Editor tools for prefab replacement, light duplication, and CSV export

## MagicQ show data

MagicQ show data is included and can be obtained through GitHub's Download ZIP or git clone.

- Location: `MagicQ/show`
- [ArtNetTest_LiveLightingTest6(Public).sbk](./MagicQ/show/ArtNetTest_LiveLightingTest6%28Public%29.sbk)
- [ArtNetTest_LiveLightingTest6(Public).shw](./MagicQ/show/ArtNetTest_LiveLightingTest6%28Public%29.shw)
- [ArtNetTest_LiveLightingTest6(Public).xhw](./MagicQ/show/ArtNetTest_LiveLightingTest6%28Public%29.xhw)

Copy the files you need to `C:\Users\<username>\Documents\MagicQ\show`.

## Getting the project (recommended)

This repository includes Git LFS-managed files such as `.unity` and `.fbx`. A Git clone is recommended because a GitHub ZIP download may contain LFS pointer files instead of their actual contents.

### Windows

1. Open PowerShell with standard user permissions, then verify `winget`.

```powershell
winget --version
```

2. Install Git and Git LFS.

```powershell
winget install --id Git.Git -e --source winget
winget install --id GitHub.GitLFS -e --source winget
```

3. Restart PowerShell and verify the installation.

```powershell
git --version
git lfs version
```

4. Clone the project. Run `git lfs install` only once per machine.

```powershell
git lfs install
git clone https://github.com/nao40031/Art-Net-DMX-Lighting-for-Unity.git
cd Art-Net-DMX-Lighting-for-Unity
git lfs pull
git lfs checkout
```

5. Confirm the project path, or open it in Explorer.

```powershell
Write-Host "Project folder: $((Get-Location).Path)"
explorer .
```

`git clone` creates the project under the folder currently open in PowerShell. Check it with `pwd`, or run `cd <destination path>` first. The PowerShell prompt path, such as `PS C:\...\Art-Net-DMX-Lighting-for-Unity>`, is the project location.

6. In Unity Hub, select `Add`, choose the `Art-Net-DMX-Lighting-for-Unity` folder, and open it.

If `winget` is unavailable, install Git and Git LFS with their standard installers, then continue from step 3.

### macOS

1. Open Terminal and verify `brew`.

```bash
brew --version
```

2. Install Git and Git LFS, then verify the installation.

```bash
brew install git git-lfs
git --version
git lfs version
```

3. Clone the project. Run `git lfs install` only once per machine.

```bash
git lfs install
git clone https://github.com/nao40031/Art-Net-DMX-Lighting-for-Unity.git
cd Art-Net-DMX-Lighting-for-Unity
git lfs pull
git lfs checkout
```

4. Confirm the project path, or open it in Finder.

```bash
echo "Project folder: $(pwd)"
open .
```

`git clone` creates the project under the folder currently open in Terminal. Check it with `pwd`, or run `cd <destination path>` first.

If `brew` is unavailable, install Homebrew first and then continue from step 1.

## Quick start: live Art-Net input

1. Add `ArtNetReceiver` to the scene and set `host` and `port` (usually `6454`).
2. Add `DmxRigController` and assign the `receiver`.
3. Add `DmxFixtureComponent` to each controlled object.
4. Set `fixture` (`FixtureDefinition`), `mode`, `universe`, and `startAddress`.
5. Assign `targetLight`/`targetLights`; set `panTransform`/`tiltTransform` if required.
6. Run `Discover & Initialize Fixtures` on `DmxRigController`.
7. Send DMX from an Art-Net sender and verify the result.

## Quick start: Timeline playback

1. Add `ArtNetChannels` to the playback source and drive `Ch1..Ch512` using Animation or Timeline.
2. Add `DmxTimelinePlayback` and assign `DmxRigController` to `rig`.
3. Add pairs of `universe` and `ArtNetChannels` to `sources`.
4. If needed, enable `overrideRigInputMode` and use `PlaybackOnly`.

## Recording: create clips from DMX

1. Add `ArtNetReceiverDmxRecorder` (`ArtNetDataRecorder.cs`) and assign `receiver`.
2. Select `Start Recording`, then select `Stop & Save`.
3. An `ArtNetChannels` AnimationClip is saved to `Assets/<directoryPath>`.

## Notes

- Refer to the detailed guide for parameter specifications for each script.
- `Assets/Editor` contains Editor tools; `Assets/ArtNet` contains the runtime and playback implementation.

## License

- This project (scripts and Unity assets in this repository) is provided under the **MIT License**.
- Unity-chan-related assets use **Unity-chan License 3.0 (UCL 3.0)**.

### Unity-chan License 3.0 documents

- `Assets/Avatar/Unity-chan/License/EN_Unity-Chan License Terms and Condition_UCL3.0.pdf`
- `Assets/Avatar/Unity-chan/License/JP_Unity-Chan License Terms and Condition_UCL3.0.pdf`
- `Assets/Avatar/Unity-chan/License/License Logo/` (logo usage/identity guidance)
