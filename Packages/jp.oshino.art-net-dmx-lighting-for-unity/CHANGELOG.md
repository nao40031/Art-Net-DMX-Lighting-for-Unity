# Changelog

## 0.1.7

- Moves the normal Git installation URL from the public-project `release` branch to the dedicated `upm/release` branch.

## 0.1.6

- Places the **Auto Discover in Children** button directly below the Timeline Playback `Sources` list.

## 0.1.5

- Documents the `release`-branch Git URL for Package Manager updates and the tagged URL for version-pinned installs.

## 0.1.4

- Replaces automatic Timeline source discovery with the explicit **Auto Discover in Children** Inspector action.
- Validates all candidate Universe numbers before replacing Sources and reports duplicate object names in an Editor dialog.

## 0.1.3

- Adds auto-discovery of Timeline Playback universe sources and an `ArtNet Universe` prefab for multi-universe playback.
- Organizes `ArtNet.prefab` so each universe owns its own `ArtNetChannels` and `Animator` components.

## 0.1.2

- Adds `Runtime/ArtNet/Prefabs/ArtNet.prefab` for drag-and-drop setup of live Art-Net input, recording, and Timeline playback.

## 0.1.1

- Removes development and verification assets under `LightAsset/Archive` from the distributed UPM package.

## 0.1.0

- Initial UPM package release.
- Includes Art-Net/DMX runtime, fixture definitions, editor tools, and VLB-free lighting prefabs.
- Adds an optional VLB prefab as a Package Manager sample.
