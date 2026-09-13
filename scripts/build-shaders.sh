#!/usr/bin/env bash
#
# Compiles the simulator's HLSL effects into the .mgfx files the engine loads.
#
# Shaders are build-time artifacts: the results are committed under
# Source/Shaders/prebuilt/<profile>/ so that neither a normal build nor the distribution package
# has to compile them. Run this only when a .fx file changes.
#
#   ./scripts/build-shaders.sh [--profile OpenGL|DirectX_11] [--output DIR] [--check]
#
# Why this is not simply "mgfxc *.fx":
#
#   MonoGame's effect compiler shells out to Microsoft's HLSL compiler, which only exists as a
#   Windows DLL. On Windows it is already there. On Linux mgfxc runs it under Wine and, out of
#   the box, expects a Wine prefix containing the Windows .NET SDK and Microsoft's
#   d3dcompiler_47.dll - two large downloads its own setup script fetches.
#
#   This script removes the first: Source/Tools/FxcBridge is a small self contained Windows
#   program that speaks the protocol mgfxc uses, so the prefix needs no .NET SDK at all.
#
#   The second download is still needed for full coverage. Wine ships its own HLSL compiler and
#   this script will use it when Microsoft's is absent, but its shader model 3 backend is
#   incomplete - as of vkd3d-shader 1.10 it compiles 3 of the 12 effects and reports
#   "not yet implemented feature" for the rest. Copy d3dcompiler_47.dll into the prefix, or run
#   this on Windows (which is what the release workflow does), for all of them.
#
# The OpenGL profile is limited to shader model 3, so the profile lines in each .fx are rewritten
# from 5_0 to 3_0 on the way in. The sources keep the 5_0 form the Windows build uses, so nothing
# diverges between the two.

set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source_root="$repository_root/Source"
profile="OpenGL"
output=""
check_only="no"

while [ $# -gt 0 ]; do
    case "$1" in
        --profile) profile="$2"; shift 2 ;;
        --output) output="$2"; shift 2 ;;
        --check) check_only="yes"; shift ;;
        -h|--help) sed -n '2,32p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'; exit 0 ;;
        *) echo "unknown option: $1" >&2; exit 2 ;;
    esac
done

[ -n "$output" ] || output="$source_root/Shaders/prebuilt/$profile"

shader_sources=(
    "$source_root/ActivityRunner/Content/Shaders"
    "$source_root/FreeTrainSimulator.Graphics/Resources/Shaders"
)

say() { printf '%s\n' "$*"; }
die() { printf 'error: %s\n' "$*" >&2; exit 1; }

# ---------------------------------------------------------------------------- Wine, when needed

prepare_wine() {
    command -v wine >/dev/null 2>&1 || die "wine is required to compile shaders on this platform (see the notes at the top of this script)"

    export WINEPREFIX="${MGFXC_WINE_PATH:-$HOME/.winemonogame}"
    export MGFXC_WINE_PATH="$WINEPREFIX"
    export WINEARCH=win64
    export WINEDEBUG="${WINEDEBUG:--all}"

    # mgfxc looks for wine64 specifically; recent Wine only installs "wine".
    if ! command -v wine64 >/dev/null 2>&1; then
        mkdir -p "$repository_root/.build/bin"
        ln -sf "$(command -v wine)" "$repository_root/.build/bin/wine64"
        ln -sf "$(command -v winepath)" "$repository_root/.build/bin/winepath64"
        export PATH="$repository_root/.build/bin:$PATH"
    fi

    local bridge="$WINEPREFIX/drive_c/fxcbridge"
    if [ ! -f "$bridge/dotnet.exe" ]; then
        say "Building the shader compiler bridge into $bridge"
        dotnet publish "$source_root/Tools/FxcBridge/FxcBridge.csproj" -c Release -o "$repository_root/.build/fxcbridge" >/dev/null
        mkdir -p "$bridge"
        cp -r "$repository_root/.build/fxcbridge/." "$bridge/"
        mv -f "$bridge/fxcbridge.exe" "$bridge/dotnet.exe"

        # mgfxc invokes the compiler as "dotnet", resolved through the prefix's PATH.
        cat > "$repository_root/.build/fxcbridge-path.reg" <<'REG'
REGEDIT4

[HKEY_LOCAL_MACHINE\System\CurrentControlSet\Control\Session Manager\Environment]
"PATH"="C:\\windows\\system32;C:\\windows;C:\\fxcbridge"
REG
        wine regedit "$repository_root/.build/fxcbridge-path.reg" >/dev/null 2>&1 || true
    fi

    # The compiler itself: Microsoft's if the user supplied one, Wine's otherwise. It is copied
    # in under a private name because mgfxc sets WINEDLLOVERRIDES=d3dcompiler_47=n, and Wine
    # recognises its own builtin even when loaded from disk, so the original name is rejected.
    if [ ! -f "$bridge/hlslcompiler.dll" ]; then
        local supplied="${FTS_D3DCOMPILER:-$WINEPREFIX/drive_c/windows/system32/d3dcompiler_47.dll}"
        local builtin
        builtin="$(ls /usr/lib*/wine/x86_64-windows/d3dcompiler_47.dll /usr/lib/*/wine/x86_64-windows/d3dcompiler_47.dll 2>/dev/null | head -1 || true)"

        if [ -f "$supplied" ]; then
            say "Using the HLSL compiler at $supplied"
            cp "$supplied" "$bridge/hlslcompiler.dll"
        elif [ -n "$builtin" ]; then
            say "Using Wine's own HLSL compiler ($builtin)."
            say "Its shader model 3 support is incomplete; set FTS_D3DCOMPILER to Microsoft's d3dcompiler_47.dll for full coverage."
            cp "$builtin" "$bridge/hlslcompiler.dll"
        else
            die "no HLSL compiler found; install wine's d3dcompiler or set FTS_D3DCOMPILER"
        fi
    fi
}

# ---------------------------------------------------------------------------------------- build

command -v dotnet >/dev/null 2>&1 || die "the .NET SDK is required"

pushd "$source_root" >/dev/null
dotnet tool restore >/dev/null
popd >/dev/null

case "$(uname -s)" in
    MINGW*|MSYS*|CYGWIN*) ;;    # Windows: mgfxc calls the compiler directly.
    *) prepare_wine ;;
esac

staging="$(mktemp -d)"
trap 'rm -rf "$staging"' EXIT
mkdir -p "$output"

compiled=0
failed=0
failures=()

for directory in "${shader_sources[@]}"; do
    [ -d "$directory" ] || continue
    for shader in "$directory"/*.fx; do
        [ -e "$shader" ] || continue
        name="$(basename "$shader" .fx)"
        prepared="$staging/$name.fx"

        if [ "$profile" = "OpenGL" ]; then
            # The OpenGL profile tops out at shader model 3.
            sed 's/vs_5_0/vs_3_0/g; s/ps_5_0/ps_3_0/g' "$shader" > "$prepared"
        else
            cp "$shader" "$prepared"
        fi

        printf '  %-32s' "$name"
        if (cd "$source_root" && dotnet tool run mgfxc "$prepared" "$staging/$name.mgfx" "/Profile:$profile") > "$staging/$name.log" 2>&1; then
            cp "$staging/$name.mgfx" "$output/$name.mgfx"
            printf 'ok\n'
            compiled=$((compiled + 1))
        else
            printf 'FAILED\n'
            sed 's/^/      /' "$staging/$name.log" | tail -8
            failures+=("$name")
            failed=$((failed + 1))
        fi
    done
done

say ""
say "$compiled compiled, $failed failed, into $output"

if [ "$failed" -gt 0 ]; then
    say "failed: ${failures[*]}"
    [ "$check_only" = "yes" ] && exit 1
    exit 1
fi
