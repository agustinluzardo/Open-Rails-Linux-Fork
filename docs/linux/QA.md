# Linux UI and shutdown checks

Build from the repository root with .NET 10:

```sh
cd Source
dotnet build Riel.slnx -c Release
dotnet test Test/Tests.Orts/Tests.Orts.csproj -c Release
dotnet test Test/Tests.Riel/Tests.Riel.csproj -c Release
```

For a content-free smoke test, create the small fixture and give the launcher an isolated
profile. The simulator runs with SDL's offscreen OpenGL driver and a silent OpenAL device;
`RuntimeSmoke` opens eight in-game windows, writes screenshots, opens a modal Quit window,
dispatches `GameQuit` and checks that `Game.Run()` returns. Use a timeout so regressions
cannot leave a worker process alive indefinitely.

```sh
python3 Tools/TestRoute/make_test_route.py /tmp/riel-test-content
export XDG_CONFIG_HOME=/tmp/riel-test-profile/config
export XDG_DATA_HOME=/tmp/riel-test-profile/data
export XDG_STATE_HOME=/tmp/riel-test-profile/state
export XDG_CACHE_HOME=/tmp/riel-test-profile/cache
export SDL_VIDEODRIVER=offscreen
export ALSOFT_DRIVERS=null
export __GLVND_APP_ERROR_CHECKING=1
export __GLVND_ABORT_ON_APP_ERROR=1
dotnet ../Program/net10.0/riel.dll content add Test '/tmp/riel-test-content/Train Simulator'
dotnet build Tools/RuntimeSmoke/RuntimeSmoke.csproj -c Release
timeout 90 dotnet ../Program/net10.0/Riel.RuntimeSmoke.dll /tmp/riel-smoke
timeout 90 dotnet ../Program/net10.0/Riel.RuntimeSmoke.dll /tmp/riel-smoke-loading loading
```

Inspect the PNGs for cut or overlapping text. SDL offscreen can render a black region
outside its actual drawable area; test physical window/display behavior on Arch/KDE.
With a real activity on Arch, open Pause/Quit, Help, Activity, Track Monitor, Driving,
Train Operations, Next Station and Compass at normal and high DPI, then verify their
click areas, tab scrolling and focus. Press Alt+F4 while a modal window is open and
confirm window and sound close and `pgrep -af ActivityRunner` no longer lists the game.
Also test the Quit button and quitting during loading. In launcher Settings, test
load/save/reopen, resetting bindings, Ctrl/Shift/Alt combinations, Escape cancelling
a capture, and video settings (especially fullscreen with an existing profile).

The launcher exposes optional tracing under Settings → Advanced → Train and signal traces.
AI stops also trace placement attempts; AI progress samples moving trains. Enter train numbers
such as `15,267` to record route changes and the first renderer selection, return to track height,
model load, and frame
for those trains, with wall-clock timestamps; `*` traces every AI train. The signal checkbox
includes `[SignalBlock]`, `[SignalNode]`, `[SignalNodeRoute]`, `[SignalRequest]` and `[SignalDiag]`.
These high-volume signal messages are off by default. The road crossing checkbox records
discovered crossing groups and changes to `HasTrain`. Settings apply to the next run.
For terminal-only runs, the same diagnostics are available with `RIEL_TRACE_AI_STOPS=1`,
`RIEL_TRACE_AI_PROGRESS=1`, `RIEL_TRACE_AI_TRAINS=15,267`, `RIEL_TRACE_SIGNALS=1`, and
`RIEL_TRACE_ROAD_CROSSINGS=1` as environment variables. These overrides keep logging even
when the corresponding GUI checkbox is off.

The default bindings in `ProfileKeyboardSettingsModel` are F1 Help, F4 Track Monitor,
F5 Driving, F9 Car Operations, Ctrl+Alt+F9 Train Operations, Ctrl+9 Dispatcher,
Escape Pause Menu and Alt+F4 Quit. Existing customized profiles can differ; verify
the bindings in launcher Settings rather than inferring them from an older profile.

## Native editor checks

The portable workflow builds the pinned TSRE5vc source with the Riel patches and
retains the credits of creator Piotr Gadecki (GokuMK). It checks Route Editor main-window
visibility, a presented OpenGL frame, responsive event-loop turns, real shapes and
textures uploaded to the GPU. Its negative watchdog check hides the main window and
verifies that Navi alone does not pass, then restores and rebuilds the production binary.

Consist selection and ACE preview have separate checks. The content-index benchmark
checks repeated lookup work, and the loader-safety executable exercises concurrent
logging and valid/truncated DDS imports under AddressSanitizer/UndefinedBehaviorSanitizer.
See [the editor guide](../route-editor.md) for flags and log markers.

## Visual flicker reports

`RenderFrameCameraTests` checks that opaque, blended and distant-mountain passes retain
the camera used to prepare their frame after a subsequent update changes the view by
one 2,048-metre tile and changes the projection. It also checks that the next frame gets
the new camera and that the loading screen keeps its orthographic projection.

For a graphics check, load visible terrain and scenery, switch between cab and exterior
cameras, rotate the view, change zoom and open/close the dispatcher map. Repeat with
dynamic shadows disabled and enabled, distant mountains enabled and 4× antialiasing.
Check the scene outside overlays as well as the full window; an interface element can
remain visible during a scene-rendering failure.

Record whether black appears over the entire game window, only terrain/objects, or the
dispatcher map. Include the F3 renderer, route/activity, camera, simulation time and
location, weather, shadow configuration and a short recording when possible.
Compare a run with dynamic shadows disabled and one with the same camera/settings;
do not treat a changed setting alone as proof of a cause.

Synthetic-route smoke tests exercise the render pipeline but cannot establish that
a content-specific or hardware-specific flicker is fixed on a user's route/GPU.
Use a reproducible recording/location for that final check.
