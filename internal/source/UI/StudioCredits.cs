namespace murumsWiiModStudio
{
    internal static class StudioCredits
    {
        internal const string Text = @"# Credits and third-party notices

murums Wii Mod Studio
Copyright (c) 2026 murums04 — PolyForm Noncommercial 1.0.0
Developed by murums with AI assistance.
https://github.com/murums04/murums-Wii-Mod-Studio

Studio's own code in this distribution is offered under PolyForm Noncommercial 1.0.0. External programs, their dependencies,
game resources and referenced projects retain their own licenses.
No external editor binaries or game resources are embedded in Studio's installer.
The editor build references Microsoft .NET Framework system assemblies.

## Optional external tools

Wiimms SZS Tools — Wiimm and contributors
Archive, image, message, course and model command-line tools.
Installer: SZS 2.42a r8989 (Windows/Cygwin64).
License: GPL version 2; retain the package's additional component notices.
https://szs.wiimm.de/
https://github.com/Wiimm/wiimms-szs-tools/blob/master/project/gpl-2.0.txt
Source packages: https://download.wiimm.de/source/wiimms-szs-tools

Wiimms ISO Tools — Wiimm and contributors
Disc-image and WBFS workflows. Installer: WIT 3.05a r8638 (Windows/Cygwin64).
License: GPL version 2; retain the package's additional component notices.
https://wit.wiimm.de/
https://github.com/Wiimm/wiimms-iso-tools/blob/master/project/gpl-2.0.txt
Source packages: https://download.wiimm.de/source/wiimms-iso-tools

RiiStudio / rszst — riidefi, snailspeed3 and contributors
External model/resource editor and command-line converter.
The configured snailspeed3 repository has no top-level project-wide license
identified in this review. Dependencies have their own notices. Do not assume MIT
or permission to redistribute its binaries based on Studio's license.
https://github.com/snailspeed3/RiiStudio
https://github.com/snailspeed3/RiiStudio#credits

Switch Toolbox — KillzXGaming and contributors
External specialist resource editor.
License: GPL version 3; additional libraries have their own licenses.
https://github.com/KillzXGaming/Switch-Toolbox
https://github.com/KillzXGaming/Switch-Toolbox/blob/master/LICENSE

BrawlCrate / BrawlLib — soopercool101 and contributors; BrawlBox/BrawlTools lineage
External resource/audio editor and documented model-format reference.
License: LGPL version 3; see upstream notices for individual components.
The standalone EXE download is accompanied by the license from the same release tag.
https://github.com/soopercool101/BrawlCrate
https://github.com/soopercool101/BrawlCrate/blob/master/LICENSE

FFmpeg — the FFmpeg developers; Windows builds by Gyan Doshi
External audio/video converter. Installer uses gyan.dev release-essentials.
FFmpeg's general licensing depends on build configuration; the selected Gyan
builds are GPLv3. Keep the downloaded package's LICENSE and source/build notices.
https://ffmpeg.org/
https://ffmpeg.org/legal.html
https://www.gyan.dev/ffmpeg/builds/

Looping Audio Converter — libertyernie and contributors
External frontend for looped-audio conversion.
Frontend: MIT, Copyright (c) 2015-2023 libertyernie.
The complete package is not MIT-only. Its About page separately credits BrawlLib,
OpenTK, RunProcessAsTask, VGAudio, FFmpeg, metaflac, qaac, VGMPlay and vgmstream;
their licenses and any limitations remain applicable.
https://github.com/libertyernie/LoopingAudioConverter
https://github.com/libertyernie/LoopingAudioConverter/blob/master/LoopingAudioConverter/COPYING
https://github.com/libertyernie/LoopingAudioConverter/blob/master/LoopingAudioConverter/About.html

NintyFont — hadashisora / TheDzeraora and contributors
External Nintendo font editor. License: GPL version 3.
Upstream also credits Tempus, RoadrunnerWMC, gdkchan, kwsch and Citra, including
BRFNTify-Next/TPLLib and Ohana3DS-Rebirth work. See upstream Credits.
https://github.com/hadashisora/NintyFont
https://github.com/hadashisora/NintyFont/blob/master/license.txt

## Documented research and compatibility references

These entries acknowledge documented research. They are not a claim that these
projects are compiled into Studio or that their code is covered by Studio's license.

wuj5 — stblr and contributors
BRCTR structure reference. Upstream license: MIT.
https://github.com/stblr/wuj5
https://github.com/stblr/wuj5/blob/main/LICENSE

ogws — doldecomp contributors
NW4R pane geometry and layout-material research.
The repository includes CC0-1.0; this is not a license to redistribute Nintendo game assets.
https://github.com/doldecomp/ogws
https://github.com/doldecomp/ogws/blob/master/LICENSE

BRFNTify-Next — RoadrunnerWMC, Tempus and contributors
Font-format research. Upstream license: GPL version 3.
https://github.com/RoadrunnerWMC/BRFNTify-Next
https://github.com/RoadrunnerWMC/BRFNTify-Next/blob/master/license.txt

Retro Rewind / rr-pulsar — Retro Rewind Team and contributors
Background/model/globe compatibility research. The repository provides GPLv3
and separate Pulsar and mkw-sp notices; refer to all applicable upstream notices.
https://github.com/Retro-Rewind-Team/rr-pulsar
https://github.com/Retro-Rewind-Team/rr-pulsar/blob/main/LICENSE
https://github.com/Retro-Rewind-Team/rr-pulsar/blob/main/LICENSE_pulsar
https://github.com/Retro-Rewind-Team/rr-pulsar/blob/main/LICENSE_mkw-sp

BrawlCrate's MDL0 documentation/code was also consulted for format structure.
Earlier murums Brawl Studio and murums BRLAN Studio work forms part of Studio.

## Runtime and user-supplied resources

Microsoft Windows, Windows Forms, GDI+ and .NET Framework are platform dependencies,
not redistributed third-party editors. Their Microsoft terms remain applicable.
Nintendo game names and game assets belong to their respective owners; Studio is
not affiliated with or endorsed by Nintendo. Fonts, textures and other imported
resources remain subject to their original authors' terms.

## Scope and version limits

Checked against upstream project notices on 2026-09-18.
Except for the pinned Wiimms packages and BrawlCrate fallback, the downloader uses
upstream releases/latest. Refer to the actual downloaded package's notices for its
precise version and transitive dependencies. Archive extraction retains all files,
including supplied licenses; these credits do not replace those original notices.

No directly copied upstream block was established by the local provenance review.
That review was not an exhaustive source-similarity audit. In particular, RiiStudio's
overall licensing remains unresolved here; this document is not a blanket clearance
to redistribute third-party programs or relicense third-party code.
Earlier distributions were released under MIT. Their existing license grants remain valid.
";
    }
}
