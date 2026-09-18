<h1 align="left"><img src="assets/murums_logo.png" width="64" height="64" align="middle" alt="murums Wii Mod Studio logo">&nbsp; murums Wii Mod Studio</h1>

A Windows desktop editor for Wii archives, textures, menu layouts and animations.
Current release: **2.1.0-beta1**. Developed by murums with AI assistance.

## Features

- Race HUD Tool and Game HUD Tool: visual movement, resizing, colours and texture replacement.
- Archives and textures: open, inspect, edit, replace and export supported resources.
- Fonts and messages: Font Changer and game text editing.
- Backgrounds and animation: menu backgrounds, GIF import and BRLAN editing.
- Audio and projects: WAV loop preview, theme projects and supporting workflows.
- Integrated help, previews and optional external tools.

Some workflows require optional tools or original game resources. Previews approximate the game; runtime code and animations can affect the result. Keep backups and test edited copies in-game.

## Install

Download **murums Wii Mod Studio.exe** from [Releases](https://github.com/murums04/murums-Wii-Mod-Studio/releases).

1. Run it and choose an empty installation folder.
2. Select optional tools. Only Wiimms tools are preselected; uncheck all for Studio alone.
3. Install and select Launch program.

Later, the same download launches a detected installation. Pass --setup to reopen installation.
Uninstall through Windows Installed Apps or the installed Uninstall.exe.

Requires Windows and .NET Framework 4.x. Optional tools have their own requirements.
Game files and third-party programs are not included. Optional downloads require internet.

## Build

Use Windows PowerShell and the .NET Framework C# compiler at
%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe.

From the repository root:

    .\internal\build.ps1
    .\internal\build-setup.ps1 -OutputPath "$PWD\dist\murums Wii Mod Studio.exe"

The first command builds the editor under internal/build.
The second builds the downloadable installer/launcher under dist.
No game assets or optional downloads are needed to compile.

## Source layout

- internal/source: editor source, manifest and Studio icon.
- internal/tools: installer and optional tool download scripts.
- internal/build.ps1 and internal/build-setup.ps1: builds.
- THIRD_PARTY.md: external tools and documented research references.

Generated files, third-party binaries, game assets and private development artifacts are excluded.

## Contributing

Include reproduction steps, the version and the affected file format when reporting bugs.
Do not attach commercial game archives, credentials or private files.
Only share screenshots and assets you have permission to publish.

## License

Copyright (c) 2026 murums04.

This distribution is licensed under **PolyForm Noncommercial 1.0.0**.
Use, modification and redistribution are allowed only for purposes permitted by that license.
Commercial use, including selling Studio or modified versions, is not granted by this license.
See [LICENSE](LICENSE) for the complete terms.

This is **source-available software**, not OSI-approved open source.
Earlier MIT distributions retain their existing license grants.
Credits, external tool licenses and research references are listed in [THIRD_PARTY.md](THIRD_PARTY.md)
and in **Help > About > Credits & licenses**.
External programs retain their own licenses.