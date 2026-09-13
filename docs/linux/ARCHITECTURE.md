# How Riel works

Riel runs Microsoft Train Simulator content as a native Linux program. It is a fork of
[Free Train Simulator][fts], which is itself a modernized fork of [Open Rails][or]: the simulation
is theirs, and what Riel adds is the platform layer described here.

[fts]: https://github.com/perpetualKid/FreeTrainSimulator
[or]: https://github.com/openrails/openrails

## Why this base

Open Rails and Free Train Simulator are the same engine at different ages.

Open Rails is the original: fifteen years of physics, signalling, timetables, cab views and
content compatibility, and the larger community. It targets .NET 6 with the `-windows` framework,
builds only for `win-x64`, and its user interface, launcher and diagnostics are Windows Forms
throughout.

Free Train Simulator is a fork by perpetualKid, a long-standing Open Rails contributor, that has
been modernizing the same code: .NET 10, a rewritten content model with a proper cache, an in-game
window system that draws its own interface rather than hosting Windows Forms, and a graphics layer
already separated from the game logic. The simulation - the part that took fifteen years - is the
same code, kept in sync with Open Rails.

Riel is based on Free Train Simulator because the modernization it has already done is exactly
what a Linux port needs. Starting from Open Rails would have meant doing that work first and then
porting. What Riel adds is the platform layer: everything below is the difference between the two.

Open Rails remains the reference for content behaviour. Where the two engines differ on how a
piece of MSTS content should behave, Open Rails is right by definition - it is what content authors
test against - and such differences belong upstream in Free Train Simulator rather than here.

## What had to be replaced

The engine is C# on MonoGame, so most of it is portable. Six things were not.

### Direct3D

MonoGame ships one assembly per backend. The Windows build uses `MonoGame.Framework.WindowsDX`,
which is Direct3D 11; this build uses `MonoGame.Framework.DesktopGL`, which is OpenGL over SDL.
The API is identical, so no rendering code changed - `Directory.Build.targets` swaps the package.
See [Graphics backends](#graphics-backends) below for why not Vulkan, yet.

### GDI+

Every piece of text the simulator shows - cab labels, the head-up display, the popup windows, the
track monitor - is drawn by rasterizing it with `System.Drawing` into a bitmap and uploading that
as a texture. .NET 7 removed the Unix implementation of `System.Drawing.Common`, so the type is
simply absent here.

`Source/FreeTrainSimulator.Common/Compatibility/` provides the slice of GDI+ the engine uses -
`Font`, `Graphics`, `Bitmap`, `Brush`, `Pen`, `GraphicsPath` and their supporting enums - backed by
[SkiaSharp][skia], in the same namespaces the engine already imports. The call sites compile
unchanged on both platforms, which keeps upstream merges cheap. The geometry types (`Color`,
`Point`, `Size`, `Rectangle`) are not redefined: those live in `System.Drawing.Primitives`, which
is part of the shared framework and works everywhere.

The one subtlety is pixel format. GDI+ hands out straight (non premultiplied) alpha in BGRA order
for `Format32bppArgb`, and the engine copies those bytes directly into a texture. Skia draws in
premultiplied alpha, so `LockBits` converts on the way out.

[skia]: https://github.com/mono/SkiaSharp

### Windows Forms

The engine used Windows Forms for four things, each replaced by something both platforms use:

| What | Was | Now |
| --- | --- | --- |
| Display layout and window placement | `Screen`, `Form` | `Common/Display/DisplayDevices` - SDL here, the Win32 monitor functions there |
| Fatal error and missing content dialogs | `MessageBox` | `Common/Display/MessageDialog` - SDL here, `MessageBoxW` there |
| Mouse cursors | `Cursors` | MonoGame's own `MouseCursor` |
| The launcher | the `Menu` project | `Source/Launcher.Cli`, see [The riel command](#the-riel-command) |

`RenderProcess` now drives the MonoGame `GameWindow` directly - `IsBorderless` and `Position`
instead of a form's border style and size. MonoGame reports window resizes but not moves, so the
window's placement is also read once per frame.

Antialiasing needs asking twice here. On Direct3D the adapter decides, and the engine asks it; on
OpenGL the back buffer is the window, so the sample count also has to exist as a GLX or EGL visual,
and where it does not - a virtual display, a remote session, an unusual driver - the window cannot
be created and the graphics device fails with an error naming neither antialiasing nor the window.
`Common/Display/GraphicsCapabilities` asks the window system directly, by trying to create a hidden
window, and the count is halved until both answers agree.

Under Wayland a client cannot read or set its own absolute position. The saved window position is
therefore advisory there and the compositor decides; everything else - the display list, sizes,
scaling - is reported correctly.

### Win32 APIs

`Common/Native/NativeMethods.Unix.cs` implements the entry points the engine calls, from `/proc`
and `libc`. The interesting ones:

- `GetPrivateProfileString` reads the `.ini` parameters that MSTS content and the train control
  system scripts use. `Common/Native/IniFile.cs` reproduces the Win32 behaviour, quirks included:
  case insensitive lookups, one layer of surrounding quotes stripped, a key without `=` yielding an
  empty value.
- `MapVirtualKey` and `GetKeyNameText` translate PC/AT scan codes. Open Rails stores key bindings
  as scan codes so they survive a keyboard layout change, and turns them into XNA key codes through
  these. `Common/Native/ScanCodeMap.Unix.cs` holds the tables Windows would consult, so the shipped
  default bindings, the saved user bindings and the in-game keyboard map all behave identically.
- The Windows registry is used in two places: to find the MSTS installation, and to import the
  content folders of an earlier Open Rails installation. Both are replaced - see
  [Finding content](#finding-content).

### The RailDriver desk

The upstream package wraps `piehid64.dll`. The desk is a plain USB HID device, so
`Common/Input/RailDriverHid.Unix.cs` talks to it through the kernel's hidraw driver instead, with
the same class name and members so `RailDriverDevice` compiles unchanged. It needs read access to
`/dev/hidraw*`, which the udev rule in `packaging/linux` grants to the `input` group.

### Where files go

The Windows build keeps settings, saves, logs and caches together in the roaming profile. Here they
follow the XDG base directory specification, because desktops and backup tools rely on that
separation:

| | |
| --- | --- |
| `~/.config/riel` | settings and profiles |
| `~/.local/share/riel` | saves |
| `~/.local/state/riel/Logs` | logs |
| `~/.cache/riel` | scanned content indexes, safe to delete |

## Loading MSTS content

This is the part that matters most, and the reason a custom route can look broken on Linux even
when the engine is working.

MSTS content was authored on Windows, and refers to its own files with whatever capitalisation the
author happened to type. A route writes `..\..\GLOBAL\SHAPES\track1.s` for a file stored as
`Global/Shapes/Track1.s`; two files of the same route disagree with each other; a texture named in
a `.s` file has a different case again. NTFS does not care. ext4 and btrfs do, and every one of
those references fails - which is what a route that loads with missing shapes, missing textures and
no sound, or that does not load at all, actually is.

`Common/Native/ContentIO.cs` resolves this. Every read of content goes through it, and it walks the
path one segment at a time, matching each segment case insensitively against a cached listing of
its parent directory. The listing is revalidated against the directory's timestamp, so a file added
while the game is running is still found, and loading a route costs one listing per directory
rather than one lookup per reference.

The fast path is a plain existence check, so a correctly cased path - and every path on a case
insensitive file system, Windows included - costs one `stat` and never builds an index. Whether the
file system is case sensitive is detected once by probing, so an NTFS or exFAT drive holding the
MSTS install is not misjudged from the operating system alone.

Coverage is at the two central readers, `SBR` for the binary formats and `STFReader` for the text
ones, plus the terrain, ace, timetable, signal script, texture and sound loaders, and every
existence check in the content projects.

### Finding content

`Orts.Formats/Msts/MstsInstallation.Unix.cs` replaces the registry lookup. It searches where an
installation actually turns up on Linux: the XDG data directory, the home directory, and the
`Program Files` folder of any Wine prefix it can find, Steam's Proton prefixes included. A folder
counts when it holds both `ROUTES` and `GLOBAL`. `RIEL_MSTS_PATH` overrides the search.

Separately, if Open Rails was previously run under Wine, its configured content folders are read
out of the prefix's `user.reg` - Wine stores that hive as plain text - so they carry over instead
of having to be added again.

## Graphics backends

The renderer is OpenGL. The reasoning, since Vulkan was the obvious thing to ask for:

**MonoGame has a Vulkan backend, but no usable runtime yet.** `MonoGame.Framework.Native` 3.8.5.1
exists on NuGet and its effect compiler accepts a `Vulkan` profile, but the package ships only the
managed assembly - the native library it needs is not published, so the backend cannot be used
without building MonoGame from source with the Vulkan SDK.

**The shaders would need modernizing first.** The effects still use DX9 era semantics - `POSITION`,
`COLOR0` - which shader model 6 rejects. Worth recording: `mgfxc`'s Vulkan profile compiles
natively on Linux through the DXC it bundles, with no Wine involved. Fixing the semantics is a
mechanical change, and it is what unblocks a fully native, Wine-free shader toolchain as well.

**OpenGL is not the bottleneck.** The engine's draw call patterns come from the XNA era; the
limits it hits are CPU-side content loading and single-threaded update work, not the graphics API.

**Vulkan is available today anyway, without a new renderer.** Mesa's zink driver runs OpenGL on top
of Vulkan. `RIEL_VULKAN=1 riel start` sets `MESA_LOADER_DRIVER_OVERRIDE=zink`, and on modern
AMD and Intel hardware the result is competitive.

The backend is a build-time switch - `-p:RielGraphicsBackend=DesktopGL` - so adding one later means
a new value and a shader profile, not a rewrite.

## Shaders

The effects are written for shader model 5 and compiled by MonoGame's effect compiler, which shells
out to Microsoft's HLSL compiler - a Windows-only DLL. The OpenGL backend needs the same effects at
shader model 3. Neither can be produced on Linux unaided, so the compiled `.mgfx` files are
committed under `Source/Shaders/prebuilt/` and the build copies them into place. The distribution
package then needs no shader toolchain at all.

`scripts/build-shaders.sh` regenerates them on Linux, and needs nothing but Wine and the .NET SDK.
MonoGame's own setup for this downloads two things into a Wine prefix - the Windows .NET SDK, and
Microsoft's `d3dcompiler_47.dll` extracted from a Firefox installer. Neither is needed here:

- `Source/Tools/FxcBridge` is a small self contained Windows program that speaks the protocol
  `mgfxc` uses, so the prefix needs no .NET SDK.
- The compiler comes from `Microsoft.Windows.SDK.CPP` on NuGet, which ships the D3D
  redistributable - the same DLL, from its actual source.

One wrinkle is worth recording. `mgfxc` runs the prefix with `WINEDLLOVERRIDES=d3dcompiler_47=n`,
native only, because its own setup expects Microsoft's redistributable. Wine recognises its own
builtin even when it is loaded from disk, so a copy under that name is rejected; the compiler is
therefore installed under a private file name, which the override does not match. That also means
Wine's own HLSL compiler can be used when the download is unavailable, though its shader model 3
backend is incomplete - as of vkd3d-shader 1.10 it compiles 3 of the 12 effects and reports "not
yet implemented feature" for the rest.

The `Shaders` workflow compiles them on Windows instead and commits the result. **GitHub Actions
has to be enabled on the fork for it to run** - Settings, Actions, General.

The effect compiler must match the framework: the tool manifest pins `dotnet-mgfxc` to the same
version as the `MonoGame.Framework` package. They were out of step upstream, and the older
compiler talks to the Wine bridge differently, which fails in a way that looks like a broken
prefix rather than a version mismatch.

## Keeping up with upstream

The fork is a real fork: the full history is there, and upstream is a remote.

```sh
git remote add upstream-fts https://github.com/perpetualKid/FreeTrainSimulator.git
git fetch upstream-fts
git merge upstream-fts/development
```

Three things keep that merge cheap:

**The platform switches live in one place.** `Source/Directory.Build.props` picks the target
framework and graphics backend from the host OS; `Source/Directory.Build.targets` swaps the MonoGame
package, drops the Windows-only packages, and decides which sources compile. Upstream project files
are almost untouched.

**Platform specific code is in its own files.** A file named `*.Windows.cs` is compiled only for the
Windows build and `*.Unix.cs` only for this one, so the two implementations of a partial class sit
side by side and neither is edited when the other changes.

**The compatibility layer keeps call sites identical.** Providing GDI+ under its own name means the
ten files that rasterize text are byte for byte upstream's.

What does conflict, and where to look when it does:

- `ActivityRunner/Processes/RenderProcess.cs` and `Viewer3D/Dispatcher/DispatcherWindow.cs`, whose
  window management was rewritten.
- `FreeTrainSimulator.Graphics/Window/WindowManager.cs`, for its DPI handling.
- The content projects, where `File.Exists` became `ContentIO.FileExists`. A merge that brings in
  new content reading code should route it through `ContentIO` too.

The Windows build is kept working - `-p:RielPlatform=windows` - so a merge can be verified against
both, and so fixes that belong upstream can be offered back.

## Layout

```
Source/
  Directory.Build.props        platform and backend selection
  Directory.Build.targets      package swaps, per-platform sources, shaders
  Riel.slnx                    the Linux solution
  FreeTrainSimulator.Common/
    Compatibility/             GDI+ over SkiaSharp
    Display/                   displays, dialogs, SDL
    Info/UserFolders.*.cs      XDG directories
    Input/RailDriverHid.*.cs   the desk over hidraw
    Native/ContentIO.cs        case insensitive content paths
    Native/IniFile.cs          GetPrivateProfileString
    Native/ScanCodeMap.*.cs    scan codes to keys
  Launcher.Cli/                the riel command
  Shaders/prebuilt/            compiled effects
  Tools/FxcBridge/             shader compilation under Wine
packaging/
  arch/PKGBUILD
  linux/                       wrappers, desktop entry, udev rule, icons
scripts/build-shaders.sh
```

## The riel command

`riel` manages content folders, lists routes, activities, paths and consists, and starts a run. It
works on the same content model the Windows menu does, so a profile configured with one works with
the other. Names are matched case insensitively on a unique prefix.

`riel start` is what the desktop entry runs: it opens the simulator on whatever the profile was
last left on, so a double click behaves the way the Windows menu's start button does.

`riel doctor` answers the first question a failed first run raises: it checks the simulator binary,
the compiled shaders, OpenAL, SDL, the display session, the configured content and the RailDriver,
and names the fix for each.

A graphical launcher would be the natural next step; the content model it would sit on is the same
one `riel` already uses.
