# Riel editor suite

Riel ships a native Linux editor/tool suite based on **TSRE5vc**, by Piotr
Gadecki (GokuMK). Upstream source identifies the project as GNU GPL 3.0 or
later. Riel keeps the original copyright/license headers intact.

## Launcher integration

The **Tools** menu exposes:

- **Riel Route Editor** — opens the route currently selected in the launcher.
- **Riel Consist Editor** — uses the selected route's MSTS content root.
- **Riel Shape Viewer** — uses the selected route's MSTS content root.
- **Riel ACE Converter** — can open without selecting a route.

For the Route Editor Riel launches:

```text
riel-route-editor --game-root <content-root> --route <route-directory> --appdata-profile
```

Riel already knows the content root and source route directory from its normal content scan,
so the editor does not ask the user to locate MSTS content again.
The directory may have a different name from the `RouteID` inside its `.trk` file;
the editor needs the directory name.

For development, `RIEL_ROUTE_EDITOR=/path/to/executable` overrides the
packaged executable.

## Riel integration

The editor is branded as Riel at runtime, uses the Riel window icon/splash and
stores its app-data settings under `~/.local/share/Riel/RouteEditor/` on a
default Linux desktop. Original TSRE5vc credits and GPL notices remain intact.

The Route Editor has a distinct Riel icon (tracks with a pencil). Arch installs
its desktop entry and icon in the application menu. The portable package ships
the desktop entry under `app/route-editor/share/applications/` for optional
desktop integration; the windows also carry their icon when launched directly.

The editor uses OpenGL 3.3. On a Wayland session with XWayland available it
selects Qt's xcb/GLX platform to avoid a Qt/NVIDIA EGL context failure. You can
override this with `QT_QPA_PLATFORM=wayland` when testing native Wayland.
`riel route-editor --graphics-check` tests creation of the actual Qt OpenGL
widget without loading a route. The bundled `appdata/0.7` supplies its shaders.
New route templates and user geographic catalogues are stored in
`~/.local/share/Riel/RouteEditor/assets/` by default, so an installation under
`/opt` or `/usr/lib` can stay read-only. The first new route may need to fetch
the upstream template archive.

## Texture loading stability

Riel does not use TSRE5vc's original one-`QThread`-per-texture loading path. ACE,
DDS and ordinary image files are decoded by a bounded worker pool into private
`Texture` objects, then published on Qt's application thread. This keeps the
Route Editor, Consist Editor and Shape Viewer responsive without allowing a
background decoder to modify memory while OpenGL is using it. Explicit texture
reloads and map-tile image copies remain synchronous.

The pool uses at most four workers by default (and leaves one CPU thread free
when possible). `RIEL_EDITOR_TEXTURE_WORKERS=N` can override the limit for
diagnostics, and `RIEL_EDITOR_ASYNC_TEXTURES=0` forces fully synchronous
loading as a troubleshooting fallback.

The Qt message handler serializes writes to the shared log, including messages
from texture workers. Concurrent writes to the same QTextStream previously
caused a reproducible double-free during loading. DDS imports also validate
dimensions and pixel payload sizes before allocation or decoding; truncated
images are rejected with an error instead of reading past the file's pixels.
CI exercises concurrent logging and valid/truncated DDS files with AddressSanitizer
and UndefinedBehaviorSanitizer in a separate CPU-only test executable.

## Native Linux build

CI checks out a pinned TSRE5vc commit, applies
`scripts/prepare-route-editor-source.py`, builds with CMake/Ninja and Qt
6.10.1, then packages the resulting native ELF editor suite beside Riel.

Upstream project: https://github.com/GokuMK/TSRE5vc

`--route-full-window-check` opens the real Route Editor, checks that both the
Navi Window and main window are visible, waits for a presented OpenGL frame,
and checks three subsequent event-loop timer turns before exiting. CI runs it
under Xvfb with a process timeout. Logs include `RIEL_EDITOR_WINDOW` milestones
for window creation, main-window visibility, first frame and responsiveness.
The older `--route-session-check` remains available for compatibility.
CI also temporarily suppresses the main window in its disposable source
checkout and verifies that the check rejects an editor showing only Navi.
It restores the source and rebuilds the production binary before packaging.

Startup logs report `RIEL_STARTUP_BEGIN`/`RIEL_STARTUP_STAGE` for TRK, tsection,
TDB, RDB, REF, services, traffic, paths, activities, consists and OpenGL, plus
elapsed time to `main-shown` and `first-frame`. `RIEL_STARTUP_WORK` summarizes
WORLD loading, shape parsing, texture decoding and texture GPU upload at the
first frame and five seconds later. Work times are inclusive and decoder times
can overlap across workers; they should not be added to obtain wall time.
`RIEL_EDITOR_STARTUP_TIMINGS=0` disables these timing logs.

Case-only split MSTS directories such as `GLOBAL/SHAPES` and `Global/Shapes`
are read through directory indexes. The editor builds each view once, retains
up to 128 views, and refreshes a view when a directory's timestamp or inode
changes. Exact physical filenames take precedence; ambiguous fallback files
are reported rather than selected arbitrarily. No files are moved or renamed.
ShapeLib's shape identity and route/season texture context remain unchanged.

## Portable runtime and source

The Linux package bundles the editor's Qt/OpenAL runtime beside the native ELF binary. The portable `riel` wrapper also exposes `riel route-editor`, `riel consist-editor`, `riel shape-viewer`, and `riel ace-converter`. Every published Linux build attaches `riel-route-editor-source.tar.gz` with the exact patched TSRE5vc source used to build it.
