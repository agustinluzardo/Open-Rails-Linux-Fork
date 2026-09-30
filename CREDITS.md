# Riel Linux credits

Riel combines its own Linux integration with an engine and tools developed by other projects.
Product names and presentation changes preserve the authorship of that work.

## TSRE5 and TSRE5vc — Piotr Gadecki (GokuMK)

**Piotr Gadecki, known as GokuMK, is the creator of TSRE5 and TSRE5vc.** His tools are the
basis of Riel Route Editor, Riel Consist Editor, Riel Shape Viewer and Riel ACE Converter.
We thank Piotr and the TSRE contributors for these editors.

- [TSRE5vc: source and contributors](https://github.com/GokuMK/TSRE5vc).
- [TSRE5: the original project](https://github.com/GokuMK/TSRE5).
- [Website, manual and downloads](http://koniec.org/tsre5/).
- Upstream commit pinned by Riel:
  [`15a5d148a15ad305e14a9e602961210ad18ada74`](https://github.com/GokuMK/TSRE5vc/tree/15a5d148a15ad305e14a9e602961210ad18ada74).

TSRE5vc retains Piotr Gadecki's copyright and its GNU GPL 3.0-or-later license.
Riel preserves the original headers, applies its integration through
[`scripts/prepare-route-editor-source.py`](scripts/prepare-route-editor-source.py) and the
helpers in `scripts/route-editor/`, and attaches the exact source used for each release as
`riel-route-editor-source.tar.gz`. See [docs/route-editor.md](docs/route-editor.md).

## Free Train Simulator and Open Rails

Riel's engine comes from [Free Train Simulator](https://github.com/perpetualKid/FreeTrainSimulator),
maintained by **perpetualKid and its contributors**, which in turn descends from
[Open Rails](https://github.com/openrails/openrails), developed by its team and community.

We recognize their work on simulation, physics, MSTS formats, signaling, AI traffic, timetables,
rolling stock, graphics, sound and tools. Copyright notices in the source are retained.
The [historical README](docs/UPSTREAM-README.md) records that provenance; Riel's features
are documented in its own [README](README.md).

## Riel integration and maintenance

**Agustín Luzardo and the Riel contributors** maintain this repository, its Linux integration,
launcher, CLI, content adaptation, fixes, tests and packages.

Individual authors are recorded in Git history and file notices. See
[CONTRIBUTING.md](CONTRIBUTING.md).

## Libraries and tools

Riel also thanks the authors of these main components:

| Project | Use in Riel |
| --- | --- |
| [.NET](https://github.com/dotnet/runtime) | Runtime, libraries and build tools. |
| [MonoGame](https://github.com/MonoGame/MonoGame) | Game framework and DesktopGL rendering. |
| [SDL](https://github.com/libsdl-org/SDL) | Windows, events and the display session. |
| [OpenAL Soft](https://github.com/kcat/openal-soft) | Simulator and editor audio. |
| [Avalonia](https://github.com/AvaloniaUI/Avalonia) | Linux launcher interface. |
| [SkiaSharp / Skia](https://github.com/mono/SkiaSharp) | Drawing and text rasterization. |
| [Qt](https://www.qt.io/) | TSRE5vc interface and OpenGL widgets. |
| [Mesa / Zink](https://www.mesa3d.org/) | OpenGL drivers and experimental execution over Vulkan. |

Each dependency retains its own license. This acknowledgment does not replace its notices
or the licenses distributed with it.

## Content authors

MSTS/OR routes, trains, cabs, signals, sounds and textures belong to their respective authors.
Riel loads user-provided content; the program does not include a collection of third-party
routes and trains. The repository contains synthetic test content.

## License

Riel and its TSRE5vc integration are distributed under **GNU GPL 3.0 or later**. See
[LICENSE](LICENSE). Original notices and the licenses of dependencies and content remain
in effect.
