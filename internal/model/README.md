# Model component build

The release installer includes the runtime components and corresponding source archives described in `THIRD_PARTY.md`. Game resources are supplied by the user. The source repository does not contain third-party binaries.

`internal/build-model.ps1` expects these locally prepared dependencies under `internal/dependencies`:

- Modified BrawlLib v0.42h1 source at `brawl-source`, built for AnyCPU / Release / net472. Follow `STUDIO-BUILD.txt` in the bundled `internal/model/sources/brawllib-studio-source.zip`. The output directory must contain `BrawlLib.dll` and `OpenTK.dll`.
- Assimp 6.0.5 Windows x64 at `assimp-6.0.5-x64/Release/assimp-vc143-mt.dll`.
- Original Blender binary archive `blender-5.2.2-windows-x64.zip` and corresponding `blender-5.2.2.tar.xz` source archive.
- `brawllib-studio-source.zip` containing the corresponding modified BrawlLib source and its build instructions.

Run `internal/build.ps1` from the repository root after preparing those inputs. It compiles the Studio executable and adapter and copies required notices/source archives into its model-runtime folder. `internal/build-setup.ps1` packages that output into the installer.

The small `StudioModelCodec.cs` adapter is dynamically linked to BrawlLib. Blender is run as a separate process. Applicable component licenses and replacement permissions are in `THIRD_PARTY.md` and `notices`.
