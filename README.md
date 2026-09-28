<h1 align="left"><img src="assets/murums_logo.png" width="64" height="64" align="middle" alt="murums Wii Mod Studio logo">&nbsp; murums Wii Mod Studio</h1>

Windows desktop editor for Wii modding and Mario Kart Wii custom packs.

**[Download for Windows](https://github.com/murums04/murums-Wii-Mod-Studio/releases)** · [Install](#install) · [Report a bug](https://github.com/murums04/murums-Wii-Mod-Studio/issues)

Current release: **2.1.0-beta6**. By murums, with AI assistance.

## Features

- **HUD & menus:** positions, sizes, colours, textures and fonts.
- **Archives & graphics:** resource editing, backgrounds, GIF import and BRLAN animations.
- **Characters:** model import, body assignment, poses, keyframes and replacement export.
- **Race effects & audio:** effect textures, colours and WAV loop previews.
- **Custom packs:** Retro Rewind (RR) pack creation, theme projects, snapshots and mod merging.
- **Text & help:** game messages, Unicode checks, symbol editing and integrated guides.

## Install

1. Download and run **murums.Wii.Mod.Studio.exe** from [Releases](https://github.com/murums04/murums-Wii-Mod-Studio/releases).
2. Choose an empty installation folder and optional tools; only Wiimms tools are preselected.
3. Select **Install**, then **Launch program**.

- **Requirements:** 64-bit Windows; .NET Framework 4.7.2 or newer for the bundled model components.
- **Resources:** game files not included; additional optional tools need internet and may have further requirements.
- **Updates:** startup notification or **Help > Check for updates**.

<details>
<summary>Updates & uninstall</summary>

- Close Studio, then choose **Install and restart**; projects, settings and optional tools preserved.
- Downloads checked against GitHub's SHA-256 digest; failed file replacement restores previous program files.
- Public Beta 1: download a newer installer once to enable the update workflow.
- Installer download also launches an existing installation; use `--setup` to reopen setup.
- Uninstall through **Windows Installed Apps** or `Uninstall.exe`.

</details>

## Known limitations

- **Previews:** approximate; keep backups and test edited copies in-game.
- **Character binding:** strongly posed unrigged models and some RR skeletons remain unreliable; Baby Daisy and King Boo unsupported by the tested automatic export path.
- **Still in progress:** closed finger grips and performance with multiple imported characters; incomplete in-game coverage of models, vehicles and animations.

<details>
<summary>Pack creation & character export</summary>

- **Custom Pack Maker:** select your RR installation and PAL, USA or Japan; copy supplied RR files with matching Title/Race/Common region names.
- **ISO/WBFS:** only supplements missing `Earth.szs`, `BackModel.szs` and `globe.arc`; complete required sources before pack creation.
- **Character Builder:** select a custom pack and installed RR variant → **Load model → Pose & movement → Export character**.
- **Formats:** GLB, glTF, BLEND, USDZ, DAE and OBJ; saved `.murcharacter` files via **Open project**.
- **Preview:** **Solid surface** for checking shape without textures; automatic binding still needs review.
- **Export:** copy the contents of `MUR_EDITED/Character replacement files` into the custom-pack root, then relaunch the enabled pack.

</details>

<details>
<summary>Build from source</summary>

Windows PowerShell and the .NET Framework C# compiler at `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe` required. Prepare the [model dependencies](internal/model/README.md) first; third-party binaries excluded from this repository.

From the repository root:

```powershell
.\internal\build.ps1
.\internal\build-setup.ps1 -OutputPath "$PWD\dist\murums.Wii.Mod.Studio.exe"
```

- Editor output: `internal/build`; installer output: `dist`.
- Game assets not required for building.

</details>

## Feedback & license

- **Bug reports:** version, reproduction steps and affected format; no commercial game archives, credentials or private files.
- **License:** [PolyForm Noncommercial 1.0.0](LICENSE); source-available, not OSI-approved open source. Commercial use not granted; earlier MIT grants remain valid.
- **Credits:** [third-party licenses and references](THIRD_PARTY.md) and **Help > About > Credits & licenses**; external tools retain their own licenses.
- Copyright (c) 2026 murums04.
