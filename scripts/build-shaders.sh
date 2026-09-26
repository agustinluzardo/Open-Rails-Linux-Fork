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
#   d3dcompiler_47.dll - two large downloads its own setup script fetches, one of them out of a
#   Firefox installer.
#
#   Neither is needed here. Source/Tools/FxcBridge is a small self contained Windows program that
#   speaks the protocol mgfxc uses, so the prefix needs no .NET SDK; and the compiler itself comes
#   from Microsoft.Windows.SDK.CPP on NuGet, which ships the D3D redistributable. Wine is the only
#   thing that has to be installed.
#
#   Wine's own HLSL compiler is used as a fallback when the download is unavailable, but its
#   shader model 3 backend is incomplete - as of vkd3d-shader 1.10 it compiles 3 of the 12 effects
#   and reports "not yet implemented feature" for the rest.
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
    "$source_root/RunActivity/Content"
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

    # Create the prefix up front. Wine prints a banner the first time it initialises one, and
    # mgfxc reads the output of "winepath" to build its command line, so that banner would end up
    # inside the path it passes and the compile would fail with mangled arguments.
    if [ ! -d "$WINEPREFIX/drive_c" ]; then
        say "Creating the Wine prefix in $WINEPREFIX"
        wine wineboot --init >/dev/null 2>&1 || true
        wineserver -w
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

    # The compiler itself. It is installed under a private name because mgfxc sets
    # WINEDLLOVERRIDES=d3dcompiler_47=n - native only - and Wine recognises its own builtin even
    # when it is loaded from disk, so a copy under the original name would be rejected.
    if [ ! -f "$bridge/hlslcompiler.dll" ]; then
        local compiler
        compiler="$(find_hlsl_compiler)" || die "no HLSL compiler available"
        cp "$compiler" "$bridge/hlslcompiler.dll"
    fi
}

# Locates Microsoft's HLSL compiler, downloading it if need be, and falls back to Wine's.
find_hlsl_compiler() {
    if [ -n "${RIEL_D3DCOMPILER:-}" ] && [ -f "$RIEL_D3DCOMPILER" ]; then
        say "Using the HLSL compiler at $RIEL_D3DCOMPILER" >&2
        printf '%s' "$RIEL_D3DCOMPILER"
        return 0
    fi

    local cached="$repository_root/.build/d3dcompiler_47.dll"
    if [ -f "$cached" ]; then
        printf '%s' "$cached"
        return 0
    fi

    # Microsoft.Windows.SDK.CPP carries the D3D redistributable, which Microsoft licenses for
    # redistribution; this is the same DLL MonoGame's setup script extracts from a Firefox
    # installer, obtained from its actual source.
    if command -v curl >/dev/null 2>&1 && download_hlsl_compiler "$cached"; then
        printf '%s' "$cached"
        return 0
    fi

    local builtin
    builtin="$(ls /usr/lib*/wine/x86_64-windows/d3dcompiler_47.dll /usr/lib/*/wine/x86_64-windows/d3dcompiler_47.dll 2>/dev/null | head -1 || true)"
    if [ -n "$builtin" ]; then
        say "Falling back to Wine's own HLSL compiler ($builtin)." >&2
        say "Its shader model 3 support is incomplete, so several effects will fail to compile." >&2
        printf '%s' "$builtin"
        return 0
    fi

    return 1
}

download_hlsl_compiler() {
    local target="$1"
    local package=microsoft.windows.sdk.cpp
    local version="${RIEL_WINDOWS_SDK_VERSION:-10.0.28000.2705}"
    local archive="$repository_root/.build/$package.$version.nupkg"

    # Everything here goes to standard error: the caller reads this function's output.
    say "Fetching Microsoft's HLSL compiler from $package $version" >&2
    mkdir -p "$repository_root/.build"

    if [ ! -f "$archive" ] && ! curl -fsSL --retry 3 -o "$archive" \
        "https://api.nuget.org/v3-flatcontainer/$package/$version/$package.$version.nupkg"
    then
        rm -f "$archive"
        say "Download failed." >&2
        return 1
    fi

    python3 - "$archive" "$target" <<'PYTHON'
import sys, zipfile
archive, target = sys.argv[1], sys.argv[2]
with zipfile.ZipFile(archive) as package:
    with open(target, "wb") as output:
        output.write(package.read("c/Redist/D3D/x64/d3dcompiler_47.dll"))
PYTHON
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
