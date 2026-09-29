#!/usr/bin/env bash
# Bundle the Qt/OpenAL pieces required by the native Riel editor suite.
set -eo pipefail

if [[ $# -ne 2 ]]; then
  echo "usage: $0 <TSRE5vc-binary> <output-directory>" >&2
  exit 2
fi

binary="$(readlink -f "$1")"
out="$(readlink -m "$2")"
repo_root="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd -P)"
qtpaths_bin="$(command -v qtpaths6 || command -v qtpaths || true)"
if [[ -z "$qtpaths_bin" ]]; then
  echo "qtpaths was not found" >&2
  exit 1
fi

qt_prefix="$("$qtpaths_bin" --query QT_INSTALL_PREFIX)"
qt_lib="$("$qtpaths_bin" --query QT_INSTALL_LIBS)"
qt_plugins="$("$qtpaths_bin" --query QT_INSTALL_PLUGINS)"

rm -rf "$out"
mkdir -p "$out/bin" "$out/lib" "$out/plugins"
install -m 755 "$binary" "$out/bin/riel-route-editor-bin"

for category in platforms imageformats platformthemes xcbglintegrations wayland-shell-integration wayland-decoration-client wayland-graphics-integration-client; do
  if [[ -d "$qt_plugins/$category" ]]; then
    cp -a "$qt_plugins/$category" "$out/plugins/"
  fi
done

if [[ ! -f "$out/plugins/platforms/libqxcb.so" || ! -f "$out/plugins/xcbglintegrations/libqxcb-glx-integration.so" ]]; then
  echo "The portable editor requires Qt's xcb platform and GLX integration plugins" >&2
  exit 1
fi

copy_runtime_dep() {
  dep="$1"
  base="$(basename "$dep")"
  target="$out/lib/$base"

  # During transitive scans ldd may resolve a dependency from the bundle we are
  # currently building. Copying that file onto itself makes GNU cp exit 1, so
  # treat an already-bundled dependency as satisfied.
  if [[ "$(readlink -m "$dep")" == "$(readlink -m "$target")" ]]; then
    return
  fi

  # Anything resolved from the Qt installation is part of the runtime closure.
  # This includes Qt's own libraries and versioned third-party runtime pieces
  # such as ICU which may not exist at the same SONAME on the host distro.
  case "$dep" in
    "$qt_lib"/*)
      cp -Lf "$dep" "$target"
      return
      ;;
  esac

  # OpenAL is a direct TSRE5vc dependency. Qt's xcb platform plugin also
  # needs a small family of XCB/xkb helper libraries which are not guaranteed
  # to be installed on an otherwise valid Linux desktop. Bundle those userland
  # helpers, but deliberately leave libc, libGL/EGL and display-driver stacks
  # to the host distribution.
  case "$base" in
    libopenal.so*|libxcb-cursor.so*|libxcb-icccm.so*|libxcb-image.so*|libxcb-keysyms.so*|libxcb-render-util.so*|libxcb-util.so*|libxcb-xinerama.so*|libxcb-xkb.so*|libxkbcommon-x11.so*|libxkbcommon.so*|libX11-xcb.so*)
      cp -Lf "$dep" "$target"
      ;;
  esac
}

scan_one() {
  file="$1"
  while IFS= read -r dep; do
    if [[ -n "$dep" && -e "$dep" ]]; then
      copy_runtime_dep "$dep"
    fi
  done < <(LD_LIBRARY_PATH="$out/lib:$qt_lib:${LD_LIBRARY_PATH:-}" ldd "$file" 2>/dev/null | awk '/=> \/.* \(0x/ {print $3} /^\/.* \(0x/ {print $1}')
}

scan_one "$binary"
while IFS= read -r plugin; do
  scan_one "$plugin"
done < <(find "$out/plugins" -type f -name '*.so' -print)

# Resolve the transitive closure. A Qt library can pull in another library from
# the Qt distribution (for example libQt6Core -> ICU), whose own dependencies
# must then be inspected as well.
previous_count=-1
while :; do
  current_count="$(find "$out/lib" -type f -name '*.so*' | wc -l)"
  if [[ "$current_count" -eq "$previous_count" ]]; then
    break
  fi
  previous_count="$current_count"
  while IFS= read -r lib; do
    scan_one "$lib"
  done < <(find "$out/lib" -type f -name '*.so*' -print)
done

install -m 755 "$repo_root/packaging/linux/riel-route-editor" "$out/riel-route-editor"

verify_runtime() {
  file="$1"
  if LD_LIBRARY_PATH="$out/lib:${LD_LIBRARY_PATH:-}" ldd "$file" | grep -q 'not found'; then
    echo "Unresolved runtime dependency in $file" >&2
    LD_LIBRARY_PATH="$out/lib:${LD_LIBRARY_PATH:-}" ldd "$file" >&2
    return 1
  fi
}

verify_runtime "$out/bin/riel-route-editor-bin"
while IFS= read -r plugin; do
  verify_runtime "$plugin"
done < <(find "$out/plugins" -type f -name '*.so' -print)

# Catch accidental omission of Qt's documented xcb cursor dependency in the
# portable package while still allowing fully offscreen/headless CI tests.
if [[ -f "$out/plugins/platforms/libqxcb.so" ]]; then
  if ldd "$out/plugins/platforms/libqxcb.so" | grep -q 'libxcb-cursor.so.*not found'; then
    echo "Qt xcb platform dependency libxcb-cursor is unresolved" >&2
    exit 1
  fi
fi

# Preserve the licenses shipped with the Qt distribution when available.
if [[ -d "$qt_prefix/LICENSES" ]]; then
  mkdir -p "$out/licenses"
  cp -a "$qt_prefix/LICENSES" "$out/licenses/qt"
fi
