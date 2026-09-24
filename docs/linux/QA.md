# Linux UI and shutdown checks

Build from the repository root with .NET 10:

```sh
cd Source
dotnet build Riel.slnx -c Release
dotnet test Test/Tests.Orts/Tests.Orts.csproj -c Release
dotnet test Test/Tests.FreeTrainSimulator/Tests.FreeTrainSimulator.csproj -c Release
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

The default F1 Help, F4 Track Monitor, Escape Pause Menu and Alt+F4 Quit bindings match
Open Rails. Some bindings inherited from Free Train Simulator differ: Driving is
Ctrl+F5 here rather than F5, and Train Operations is F9 here rather than Ctrl+Alt+F9.
