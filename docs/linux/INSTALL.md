# Installing and running Riel

## What you need

- A 64-bit Linux system with a working OpenGL 3.3 driver (Mesa, or NVIDIA's).
- The .NET 10 runtime.
- SDL 2, OpenAL Soft, fontconfig.
- Microsoft Train Simulator content: routes, rolling stock and activities. Copying the MSTS folder
  from a Windows machine is enough; nothing needs installing.

Riel is native. Wine is not involved at runtime, and MSTS itself does not have to be installed.

## Arch Linux

```sh
git clone https://github.com/agustinluzardo/Open-Rails-Linux-Fork.git
cd Open-Rails-Linux-Fork/packaging/arch
makepkg -si
```

The build needs `dotnet-sdk-10.0`; the installed package needs only `dotnet-runtime-10.0`.

Before publishing to the AUR, take the `source=()` line off the working branch and point it at a
tag, and set `sha256sums` accordingly.

## Building it yourself

```sh
sudo pacman -S --needed dotnet-sdk-10.0 sdl2 openal fontconfig     # Arch
sudo apt install dotnet-sdk-10.0 libsdl2-2.0-0 libopenal1 fontconfig  # Debian, Ubuntu

git clone https://github.com/agustinluzardo/Open-Rails-Linux-Fork.git
cd Open-Rails-Linux-Fork/Source
dotnet build Riel.slnx -c Release
```

The binaries land in `Program/net10.0/`: `ActivityRunner` is the simulator and `riel` the command
that drives it. Run `./riel` from there; it finds the simulator beside itself.

### Shaders

All twelve compiled effects are committed, so a normal build needs no shader toolchain and
`riel doctor` should report twelve of them. Regenerating them - after changing a `.fx` file -
needs Wine and the .NET SDK:

```sh
sudo pacman -S --needed wine
./scripts/build-shaders.sh
```

The script fetches Microsoft's HLSL compiler from their own NuGet package and builds a small
bridge so the Wine prefix needs nothing else. See the shaders section of
[ARCHITECTURE.md](ARCHITECTURE.md#shaders) for why it works this way.

## First run

Point Riel at your content. The folder is the one holding `ROUTES` and `GLOBAL`:

```sh
riel content add "MSTS" ~/games/train-simulator
```

That scans the routes, which takes a while the first time and is cached afterwards. Then:

```sh
riel routes                                  # what is installed
riel activities "Marias Pass"                # what there is to drive
riel play "Marias Pass" "Coal Train"         # drive it
```

Names are matched case insensitively, and a unique prefix is enough: `riel play marias coal`
works.

To drive without an activity, pick a path and a consist:

```sh
riel paths "Marias Pass"
riel consists
riel explore "Marias Pass" "Shelby-Essex" "Freight" --time 08:30 --season autumn --weather rain
```

`riel resume` continues the last save, and `riel help` lists everything.

## When something is wrong

```sh
riel doctor
```

checks the simulator binary, the shaders, OpenAL, SDL, the display session, the configured content
and the RailDriver, and names the fix for each.

Logs are in `~/.local/state/riel/Logs`. Attach one to a bug report.

### A route loads with things missing

That used to be the characteristic Linux failure: MSTS content refers to its own files with
inconsistent capitalisation, which Linux file systems do not forgive. Riel resolves those
references case insensitively, so it should not happen. If it still does, the log names the file
it could not find - that is worth reporting, with the route.

### The MSTS installation is not found

Riel looks in the XDG data directory, the home directory, and any Wine or Proton prefix. If your
content is elsewhere, either add it as a content folder - which is the normal way - or:

```sh
export RIEL_MSTS_PATH=/mnt/games/MSTS
```

### The RailDriver desk is not detected

It needs read access to its hidraw device. The package installs a udev rule granting that to the
`input` group:

```sh
sudo usermod -aG input "$USER"
```

Then log out and back in. `riel doctor` says which of the two is missing.

Building from source rather than installing the package? Install the rule by hand:

```sh
sudo cp packaging/linux/70-raildriver.rules /usr/lib/udev/rules.d/
sudo udevadm control --reload-rules && sudo udevadm trigger
```

### Running the renderer on Vulkan

```sh
RIEL_VULKAN=1 riel start
```

This runs the same OpenGL renderer on top of Vulkan through Mesa's zink driver. Worth trying on
recent AMD and Intel hardware. It is not a Vulkan renderer - see
[ARCHITECTURE.md](ARCHITECTURE.md#graphics-backends) for what that would take.

### Antialiasing looks turned off

It may be. The setting is honoured only when the display can actually provide it: on OpenGL the
sample count has to exist as a visual, and virtual displays, remote sessions and some drivers
have none. Riel halves the count until it finds one it can use, and turns antialiasing off rather
than failing to start.

### Poor performance

- Check that you are not on software rendering: `glxinfo | grep "OpenGL renderer"` should name
  your card, not `llvmpipe`.
- The viewing distance and the shadow map count cost the most.
- The first run of a route is slow because it is being scanned; later runs read the cache in
  `~/.cache/riel`.

## Where things are kept

| | |
| --- | --- |
| `~/.config/riel` | settings and profiles |
| `~/.local/share/riel` | saves |
| `~/.local/state/riel/Logs` | logs |
| `~/.cache/riel` | scanned content, safe to delete |

These follow the XDG base directory specification and move with the corresponding `XDG_*_HOME`
variables.

## Uninstalling

```sh
sudo pacman -R riel
rm -rf ~/.config/riel ~/.local/share/riel \
       ~/.local/state/riel ~/.cache/riel
```

Your MSTS content is untouched - Riel only ever reads it.
