<img src="docs/linux/riel.png" width="96" align="left" alt="">

# Riel

**Drive Microsoft Train Simulator routes natively on Linux.**

*riel* is Spanish for the rail a train runs on.

<br clear="left">

MSTS content under Wine is a frustrating experience: custom routes fail to load, performance is
poor, and every problem has two possible causes. Riel is a train simulator built as a Linux
program — no Wine, no Direct3D, no Windows Forms — so the failures that are Wine's fault simply
do not happen, and the ones that remain have one place to look.

It reads what Microsoft Train Simulator wrote: routes, activities, paths, consists, rolling
stock, cab views, sounds and the rest, from the folders they already live in.

## Why it exists

Copy a hand-built route onto a Linux machine and, under Wine, it often does not load at all. The
cause is dull and specific: MSTS content refers to its own files with whatever capitalisation the
author typed, because NTFS never cared. ext4 does. A single `TEXTURES\Rail.ace` that the shape
file spells `Textures\RAIL.ACE` is enough to lose a route.

Riel resolves every content path case insensitively — one lookup layer, in front of the readers
the whole engine goes through — so a route copied off a Windows machine loads as it was authored.
That is the change the rest of this project was built around.

## What is here

- **Native throughout.** .NET 10 on `linux-x64`, MonoGame's OpenGL backend, SDL for the window
  and the displays, the distribution's OpenAL for sound.
- **Text without GDI+.** .NET has no `System.Drawing` on Unix, and the engine rasterises every
  label, cab dial and head-up display with it. A compatibility layer over SkiaSharp keeps that
  code unchanged rather than rewriting it.
- **No registry, no Win32.** Memory and process counters come from `/proc`, MSTS installations
  are found by searching the usual places and any Wine or Proton prefix, and the RailDriver desk
  is driven through the kernel's hidraw interface.
- **Files where they belong.** Settings, saves, logs and content indexes follow the XDG base
  directory specification instead of one folder holding all four.
- **One command.** `riel` manages content, lists what is in it, and starts a run.
- **Packaged for Arch**, with a PKGBUILD ready for the AUR.

## Getting started

```sh
cd packaging/arch && makepkg -si          # or build by hand: docs/linux/INSTALL.md

riel content add "MSTS" /mnt/datos/games/MSTS   # or wherever the ROUTES/GLOBAL folder lives
riel routes
riel play "Marias Pass" "Coal Train"
```

`riel doctor` checks whether this machine can run the simulator and names what is missing.
`riel help` lists the rest; [**INSTALL.md**](docs/linux/INSTALL.md) covers installation and what
to do when something does not work.

## Status

Riel builds and starts natively: it creates its OpenGL device, loads all twelve compiled effects,
brings up sound, runs its game loop and shuts down cleanly. The test suite passes on Linux — 547
tests in `Tests.FreeTrainSimulator`, 211 in `Tests.Orts`.

What has not been verified is a real route being driven, because that needs MSTS content and a
GPU, and the machine this was developed on had neither. Expect to find things when you first load
a route; the logs in `~/.local/state/riel/Logs` name the file that failed.

Known gaps:

- The launcher is a command line tool. A graphical one would sit on the same content model.
- The WPF Toolbox and TrackViewer are Windows only and are not part of this build.
- Multiplayer builds but is untested here.
- The renderer is OpenGL. [ARCHITECTURE.md](docs/linux/ARCHITECTURE.md) explains why, and what a
  Vulkan backend would actually take; `RIEL_VULKAN=1` runs the same renderer on Vulkan through
  Mesa's zink in the meantime.

## Where the code comes from

The simulation is not new work. Riel forks [Free Train Simulator][fts], a modernised .NET fork of
[Open Rails][or], and what this project adds is the platform layer: the path resolution above,
the drawing and interop replacements, the shader pipeline, the launcher and the packaging.
Physics, signalling, timetables and the content formats are theirs, and the credit for them is
theirs.

Fixes that are not Linux specific belong upstream in one of those two projects rather than here.
Where the two engines disagree about how a piece of MSTS content should behave, Open Rails is the
reference. [ARCHITECTURE.md](docs/linux/ARCHITECTURE.md) describes how to merge from upstream
without a fight; the upstream README is kept at [docs/UPSTREAM-README.md](docs/UPSTREAM-README.md).

[fts]: https://github.com/perpetualKid/FreeTrainSimulator
[or]: https://github.com/openrails/openrails

## Licence

GPL-3.0-or-later, as Open Rails and Free Train Simulator are. See [LICENSE](LICENSE).
