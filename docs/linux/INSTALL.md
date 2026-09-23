# Installing and running Riel

## What you need

- A 64-bit Linux system with a working OpenGL 3.3 driver (Mesa, or NVIDIA's).
- The .NET 10 runtime.
- SDL 2, OpenAL Soft, fontconfig, zlib.
- For the launcher window: the X11 client libraries every desktop has. Under Wayland it runs
  through XWayland, which KDE, GNOME, Hyprland and Sway all provide.
- Microsoft Train Simulator content: routes, rolling stock and activities. Copying the MSTS folder
  from a Windows machine is enough; nothing needs installing.

Riel is native. Wine is not involved at runtime, and MSTS itself does not have to be installed.

### Dependencies by distribution

Installing the package pulls these in; they are listed for building by hand. The **build** column
is needed only to compile, the **run** column only to play.

| | build | run |
| --- | --- | --- |
| Arch, Manjaro, EndeavourOS | `dotnet-sdk-10.0 git` | `dotnet-runtime-10.0 sdl2 openal fontconfig zlib libx11 libxcursor libxext libxfixes libxi libxrandr libice libsm libglvnd` |
| Fedora | `dotnet-sdk-10.0 git` | `dotnet-runtime-10.0 SDL2 openal-soft fontconfig zlib libX11 libXcursor libXext libXfixes libXi libXrandr libICE libSM libglvnd-glx` |
| Debian, Ubuntu | `dotnet-sdk-10.0 git` | `dotnet-runtime-10.0 libsdl2-2.0-0 libopenal1 libfontconfig1 zlib1g libx11-6 libxcursor1 libxext6 libxfixes3 libxi6 libxrandr2 libice6 libsm6 libgl1` |

```sh
# Arch
sudo pacman -S --needed dotnet-sdk-10.0 dotnet-runtime-10.0 sdl2 openal fontconfig zlib git \
    libx11 libxcursor libxext libxfixes libxi libxrandr libice libsm libglvnd
```

Worth having, none required:

- `ttf-liberation` (Arch) or `liberation-fonts`: metric compatible replacements for the fonts
  MSTS content asks for by name.
- `xdg-desktop-portal` with your desktop's backend, or `gtk3`: the launcher's "Browse" button uses
  your desktop's own folder picker. Without either, paste the path instead.
- `mesa-utils` / `mesa-demos`: the `glxinfo` used further down to check the driver.

## Arch Linux

```sh
git clone https://github.com/agustinluzardo/Open-Rails-Linux-Fork.git
cd Open-Rails-Linux-Fork/packaging/arch
makepkg -si
```

That builds, runs the tests and installs `riel`. It takes a few minutes and several gigabytes of
scratch space in the build directory. The build needs `dotnet-sdk-10.0`; the installed package
needs only `dotnet-runtime-10.0`.

Before publishing to the AUR, take the `source=()` line off the working branch and point it at a
tag, and set `sha256sums` accordingly.

## Building it yourself

Any distribution, no packaging involved:

```sh
git clone https://github.com/agustinluzardo/Open-Rails-Linux-Fork.git
cd Open-Rails-Linux-Fork/Source
dotnet build Riel.slnx -c Release
```

The first build downloads the NuGet packages and takes a few minutes; later ones are quick. The
binaries land in `../Program/net10.0/`: `ActivityRunner` is the simulator and `riel` the command
that drives it.

```sh
cd ../Program/net10.0
./riel doctor
```

Run `./riel` from that directory - it finds the simulator beside itself. To use it from anywhere
without installing the package, link it onto your path:

```sh
mkdir -p ~/.local/bin
ln -sf "$PWD/riel" ~/.local/bin/riel
```

Optionally run the tests, which need no display and take a few seconds:

```sh
cd ../../Source
dotnet test Test/Tests.Orts/Tests.Orts.csproj
dotnet test Test/Tests.FreeTrainSimulator/Tests.FreeTrainSimulator.csproj
```

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

Open **Riel** from the applications menu - or run `riel gui` - and choose the folder that holds
`ROUTES` and `GLOBAL`, with "Browse" or by pasting its path. A folder one level off is fine: pick
`Microsoft Games` and the `Train Simulator` inside it is found. Then choose a route, an activity
- or, in the Explore tab, a path and a train - and press **Play**.

The launcher follows the system language; Spanish is complete. It is dark unless the switch at
the top says otherwise, and remembers the choice.

The same from a terminal:

```sh
riel content add "MSTS" /mnt/datos/games/MSTS
```

Quote a path with spaces or brackets as a whole - `"/.../Program Files (x86)/Microsoft Games/Train
Simulator"` - or the shell splits it, and fish reads the brackets as a command.

Any path works, on any disk. A train simulator install is tens of gigabytes, so keeping it on a
second drive - `/mnt/datos/games`, `/media/games`, wherever it is mounted - is the normal case,
not a special one. Riel only ever reads that folder, so a drive mounted read only is fine.

Riel also looks for an installation by itself, in `~/.local/share`, the home directory, every disk
mounted under `/mnt` or `/media` and a `games` folder inside each, and any Wine or Proton prefix.
A layout like `/mnt/datos/games/MSTS` is found without being told. Somewhere else entirely:

```sh
export RIEL_MSTS_PATH=/mnt/datos/otra-carpeta/TrainSim
```

Adding the folder as content, as above, is the normal way and does not need that variable.

Scanning takes a while the first time and is cached afterwards. Then:

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

More than one folder can be added - a second disk, a folder of downloaded routes kept apart from
the original install - and `riel routes` lists them all with the folder each came from:

```sh
riel content add "Rutas propias" /mnt/datos/games/rutas-custom
riel content                                 # what is configured
riel content refresh                         # after adding or changing routes on disk
```

## When something is wrong

"Check this computer" in the launcher, or

```sh
riel doctor
```

checks the simulator binary, the shaders, OpenAL, SDL, the display session and the configured
content, and names the fix for each. The RailDriver line is informational: the desk is optional
hardware, and "none" is the normal answer.

When a run fails, the launcher shows why: the cause in one sentence, what usually helps, the
details, and buttons to copy them or open the log. `riel play` prints the same in the terminal.
Logs are in `~/.local/state/riel/Logs`. Attach one to a bug report.

### The simulator crashes: exit code 139, "SIGSEGV"

A crash in native code - a graphics driver, the sound server, SDL - ends the process on the spot,
before anything reaches the log, so the log is no help. Riel keeps two other records of it:

- **A startup trail**, `~/.local/state/riel/Logs/Startup.log`: each step of starting up as it is
  reached - the window, the graphics device (with the card, `x11` or `wayland`, the screen mode and
  the antialiasing it got), the sound device, loading. The last step says where it died.
- **A crash report** in `~/.local/state/riel/Logs/Crashes`: .NET's own description of the crash,
  written as the process dies, with the stack of the thread that crashed - the library it died in
  and the code that called it.

The launcher reads both and shows them: what crashed and when, the library, and in "Copy details"
the whole trail and stack. That text is what a bug report needs. `riel play` and `riel explore`
print the same in the terminal.

The error window also offers to run again leaving a part out, for that run only - the saved
settings do not change:

- **Try without sound** opens no audio device at all.
- **Try with basic graphics** starts in a window instead of full screen, without antialiasing,
  dynamic shadows or hardware instancing.

The one that works points at the culprit. From a terminal, the same switches are environment
variables:

```sh
RIEL_NO_SOUND=1 riel start
RIEL_BASIC_GRAPHICS=1 riel start
```

If no crash report was written (it needs `createdump`, which the .NET runtime package includes),
systemd keeps its own record of every crash:

```sh
coredumpctl info ActivityRunner
```

### A route is missing from the list, or will not start

Some content cannot be read - a route whose `.trk` has an error, an activity whose service file is
missing, a track database the installed `tsection.dat` does not cover. Riel skips what it cannot
read and loads everything else, and says what it skipped: a "problems" button appears at the
bottom of the launcher, and `riel content refresh` prints the list. Each entry names the file and
the parser's reason, usually with the line.

A route that was listed but whose track data could not be read fails when started, and the
launcher shows what the scan recorded against it. The fix is almost always a missing file from
another content pack - many routes borrow objects, track pieces (XTracks) or rolling stock - which
the message names.

### A route loads with things missing

That used to be the characteristic Linux failure: MSTS content refers to its own files with
inconsistent capitalisation, which Linux file systems do not forgive. Riel resolves those
references case insensitively, so it should not happen. If it still does, the log names the file
it could not find - that is worth reporting, with the route.

### The MSTS installation is not found

Riel looks in the XDG data directory, the home directory, every disk mounted under `/mnt` or
`/media` and a `games` folder inside each, and any Wine or Proton prefix. If your content is
elsewhere, either add it as a content folder - which is the normal way - or:

```sh
export RIEL_MSTS_PATH=/mnt/datos/games/MSTS
```

A folder counts as an installation when it holds both `ROUTES` and `GLOBAL`. If yours has them one
level down, name that level: `/mnt/datos/games/MSTS`, not `/mnt/datos/games`.

### The content is on a disk that is not mounted yet

`riel doctor` reads the folder at the moment you run it, so a drive mounted by hand after login
looks like missing content. Mount it at boot instead - an `/etc/fstab` entry for the partition, or
the "mount at startup" checkbox in your desktop's disk utility.

If the drive is shared with Windows and formatted NTFS, mount it with `ntfs3` and make sure your
user can read it; the usual symptom of the wrong ownership is a route that lists but will not
load. Riel never writes to the content folder, so read only is enough.

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
| `~/.local/state/riel/Logs` | logs, the startup trail, and crash reports under `Crashes` |
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
