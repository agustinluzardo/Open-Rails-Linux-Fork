# Contributing to Riel Linux

Riel Linux is developed here as a Linux simulator. Its code has historical roots in
[Free Train Simulator][fts] and [Open Rails][or], with their copyright and GPL terms intact.
The native editor suite is based on [TSRE5vc](https://github.com/GokuMK/TSRE5vc),
created by Piotr Gadecki (GokuMK). Keep its copyright/license notices and the
[credits](CREDITS.md) when changing or packaging the editor integration.

## Where does this change go?

**Here**. Fixes to simulation, signalling, timetables, content formats, the interface and Linux
integration are all welcome in this repository. Include a reproducer or a comparison with
expected MSTS content behavior when a change affects a route or activity.

Changes can also be proposed to other projects independently. Riel Linux's issues, fixes and
releases do not depend on an upstream merge. Open Rails is useful for checking content behavior;
we test and integrate each fix in Riel Linux itself.

## Reporting a problem

Open an [issue](https://github.com/agustinluzardo/Riel-Linux/issues) with:

- the output of `riel doctor`,
- the log from `~/.local/state/riel/Logs`,
- and, for a route that will not load, which route and where it came from.

For visual problems, include the active renderer shown by F3, the camera, weather,
shadow settings and whether the whole screen, parts of the scenery or the dispatcher
map turn black. A short recording and the location/time in the activity help reproduce it.
For editor crashes, include `~/.local/state/riel/Logs/Riel Route Editor Log.txt` and
which editor/tool was open.

A route that loads with pieces missing is worth reporting even if it mostly works: the log names
the file that could not be found, and that name is usually the whole bug.

## Working on the code

`docs/linux/ARCHITECTURE.md` explains how the Linux build is put together. The short version:

- Platform-specific code goes in `*.Unix.cs` and `*.Windows.cs` files, not in `#if` blocks
  scattered through shared code.
- Build switches belong in `Source/Directory.Build.props` and `Source/Directory.Build.targets`,
  not in individual project files.
- Content reading goes through `ContentIO`, never `File.Open` directly.
- The Windows build must keep working: `dotnet build Source/Riel.Windows.slnx -p:RielPlatform=windows`.
- Only the render thread may touch OpenGL, directly or through MonoGame. Run with
  `__GLVND_APP_ERROR_CHECKING=1 __GLVND_ABORT_ON_APP_ERROR=1` to make any call from another
  thread abort, the way NVIDIA's driver crashes on it.

No MSTS content at hand? `python3 Source/Tools/TestRoute/make_test_route.py <folder>` writes a
small route the simulator can load and drive; the script explains how to add and start it.

Before opening a pull request, run both test suites:

```sh
cd Source
dotnet test Test/Tests.Orts/Tests.Orts.csproj
dotnet test Test/Tests.Riel/Tests.Riel.csproj
```

Riel Linux is GPL-3.0-or-later; contributions are under the same licence.

## Documentation and releases

Document user-visible features and limitations in [README.md](README.md); keep
installation, editor integration and diagnostics in their linked guides. Preserve
`docs/UPSTREAM-README.md` as historical attribution rather than describing Riel there.

For a stable version, update `Source/version.json`, `packaging/arch/PKGBUILD` and
`CHANGELOG.md`, and add its release notes to `.github/workflows/linux-portable.yml`.
Only a successful main workflow publishes the portable build. Existing stable tags
and assets remain fixed; later builds publish a separate `main-<commit>` prerelease.

[fts]: https://github.com/perpetualKid/FreeTrainSimulator
[or]: https://github.com/openrails/openrails
