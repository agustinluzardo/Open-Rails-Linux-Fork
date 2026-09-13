# ![Logo](./Source/FTS_64.png) Open Rails for Linux

A train simulator for Microsoft Train Simulator content, built to run natively on Linux.

MSTS content under Wine is a frustrating experience: custom routes fail to load, performance is
poor, and every problem has two possible causes. This is the same simulator as a Linux program -
no Wine, no Direct3D, no Windows Forms - so the failures that are Wine's fault simply do not
happen.

It is a fork of [Free Train Simulator][fts], which is itself a modernized fork of
[Open Rails][or]. The simulation is theirs; what is added here is the platform layer.

[fts]: https://github.com/perpetualKid/FreeTrainSimulator
[or]: https://github.com/openrails/openrails

## What is different

- **Native throughout.** .NET 10 on `linux-x64`, MonoGame's OpenGL backend, SDL for the window,
  the distribution's OpenAL for sound.
- **Custom routes load.** MSTS content refers to its own files with inconsistent capitalisation,
  which NTFS forgives and ext4 does not. Every content path is resolved case insensitively, which
  is what makes a route copied from a Windows machine work unchanged. This is the single biggest
  reason a route looks broken on a native Linux build.
- **Text renders without GDI+.** .NET dropped `System.Drawing` on Unix, and the engine rasterizes
  every label with it; a compatibility layer over SkiaSharp keeps that code unchanged.
- **The RailDriver desk works**, through the kernel's hidraw driver rather than a Windows DLL.
- **Files go where they belong.** Settings, saves, logs and caches follow the XDG base directory
  specification.
- **Packaged for Arch**, with a PKGBUILD ready for the AUR.

## Getting started

```sh
cd packaging/arch && makepkg -si          # or see docs/linux/INSTALL.md to build by hand

fts content add "MSTS" ~/games/train-simulator
fts routes
fts play "Marias Pass" "Coal Train"
```

`fts doctor` checks whether this machine can run the simulator and says what is missing.

[**Installing and running**](docs/linux/INSTALL.md) covers the rest, including what to do when
something does not work.

## How it works

[**ARCHITECTURE.md**](docs/linux/ARCHITECTURE.md) explains why Free Train Simulator is the base,
what had to be replaced and how, why the renderer is OpenGL rather than Vulkan and what a Vulkan
backend would take, and how to merge from upstream without a fight.

## Status

The engine builds and runs natively, and the test suite passes on Linux. Known gaps:

- The prebuilt shaders in the repository are incomplete: 3 of 12 effects. Producing the rest needs
  Microsoft's HLSL compiler - a Windows machine, or the `Shaders` workflow once GitHub Actions is
  enabled on the fork. Until then the simulator will not render a scene. See
  [the shaders section](docs/linux/ARCHITECTURE.md#shaders).
- The launcher is a command line tool. A graphical one would sit on the same content model.
- The WPF Toolbox and TrackViewer are Windows only and are not part of this build.

## Upstream

Fixes that are not Linux specific belong upstream, in [Free Train Simulator][fts] or
[Open Rails][or], rather than here. Where the two engines disagree about how a piece of MSTS
content should behave, Open Rails is the reference.

The original project's README is kept at [docs/UPSTREAM-README.md](docs/UPSTREAM-README.md).

## Licence

GPL-3.0-or-later, as Open Rails and Free Train Simulator are. See [LICENSE](LICENSE).
