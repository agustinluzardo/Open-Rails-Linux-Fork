# Contributing to Riel

Riel is the Linux platform layer over [Free Train Simulator][fts], which is a fork of
[Open Rails][or]. That split decides where a change belongs.

## Where does this change go?

**Here**, if it is about running on Linux: path resolution, the drawing and interop replacements,
the shader pipeline, the `riel` command, packaging, or anything under `docs/linux/`.

**Upstream**, if it is about the simulation: physics, signalling, timetables, content formats, the
in-game interface. Those fixes help every user of both projects, not only the ones on Linux, and
sending them upstream is also how this fork stays cheap to merge. Where Open Rails and Free Train
Simulator disagree about how a piece of MSTS content should behave, Open Rails is the reference.

If you are not sure, open an issue and say what you found; sorting that out is easy.

## Reporting a problem

Open an [issue](https://github.com/agustinluzardo/Open-Rails-Linux-Fork/issues) with:

- the output of `riel doctor`,
- the log from `~/.local/state/riel/Logs`,
- and, for a route that will not load, which route and where it came from.

A route that loads with pieces missing is worth reporting even if it mostly works: the log names
the file that could not be found, and that name is usually the whole bug.

## Working on the code

`docs/linux/ARCHITECTURE.md` explains how the port is put together and, in its last section, what
to keep in mind so upstream merges stay cheap. The short version:

- Platform-specific code goes in `*.Unix.cs` and `*.Windows.cs` files, not in `#if` blocks
  scattered through shared code.
- Build switches belong in `Source/Directory.Build.props` and `Source/Directory.Build.targets`,
  not in individual project files.
- Content reading goes through `ContentIO`, never `File.Open` directly.
- The Windows build must keep working: `dotnet build Source/FreeTrainSimulator.slnx -p:RielPlatform=windows`.
- Only the render thread may touch OpenGL, directly or through MonoGame. Run with
  `__GLVND_APP_ERROR_CHECKING=1 __GLVND_ABORT_ON_APP_ERROR=1` to make any call from another
  thread abort, the way NVIDIA's driver crashes on it.

No MSTS content at hand? `python3 Source/Tools/TestRoute/make_test_route.py <folder>` writes a
small route the simulator can load and drive; the script explains how to add and start it.

Before opening a pull request, run both test suites:

```sh
cd Source
dotnet test Test/Tests.Orts/Tests.Orts.csproj
dotnet test Test/Tests.FreeTrainSimulator/Tests.FreeTrainSimulator.csproj
```

Riel is GPL-3.0-or-later, as Open Rails and Free Train Simulator are; contributions are under the
same licence.

[fts]: https://github.com/perpetualKid/FreeTrainSimulator
[or]: https://github.com/openrails/openrails
