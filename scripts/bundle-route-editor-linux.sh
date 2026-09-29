#!/usr/bin/env bash
# Bundle the Qt/OpenAL pieces required by the native Riel editor suite.
set -eo pipefail

if [[ $# -ne 2 ]]; then
  echo "usage: $0 <TSRE5vc-binary> <output-directory>" >&2
  exit 2
fi

binary="$(readlink -f "$1")"
out="$(readlink -m "$2")"
qtpaths_bin="$(command -v qtpaths6 || command -v qtpaths || true)"
if [[ -z "$qtpaths_bin" ]]; then
  echo "qtpaths was not found" >&2
  exit 1
fi

qt_lib="$("$qtpaths_bin" --query QT_INSTALL_LIBS)"
qt_plugins="$("$qtpaths_bin" --query QT_INSTALL_PLUGINS)"

rm -rf "$out"
mkdir -p "$out/bin" "$out/lib" "$out/plugins"
install -m 755 "$binary" "$out/bin/riel-route-editor-bin"

for category in platforms imageformats platformthemes xcbglintegrations wayland-decoration-client wayland-graphics-integration-client; do
  if [[ -d "$qt_plugins/$category" ]]; then
    cp -a "$qt_plugins/$category" "$out/plugins/"
  fi
done

copy_runtime_dep() {
  dep="$1"
  base="$(basename "$dep")"
  case "$base" in
    libQt6*.so*|libopenal.so*)
      cp -Lf "$dep" "$out/lib/$base"
      ;;
  esac
}

scan_one() {
  file="$1"
  while IFS= read -r dep; do
    if [[ -n "$dep" && -e "$dep" ]]; then
      copy_runtime_dep "$dep"
    fi
  done < <(LD_LIBRARY_PATH="$qt_lib:$LD_LIBRARY_PATH" ldd "$file" 2>/dev/null | awk '/=> \/.* \(0x/ {print $3} /^\/.* \(0x/ {print $1}')
}

scan_one "$binary"
while IFS= read -r plugin; do
  scan_one "$plugin"
done < <(find "$out/plugins" -type f -name '*.so' -print)
while IFS= read -r lib; do
  scan_one "$lib"
done < <(find "$out/lib" -type f -name '*.so*' -print)

cat > "$out/riel-route-editor" <<'EOF'
#!/bin/sh
set -e
here=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd -P)
if [ -n "$LD_LIBRARY_PATH" ]; then
    export LD_LIBRARY_PATH="$here/lib:$LD_LIBRARY_PATH"
else
    export LD_LIBRARY_PATH="$here/lib"
fi
export QT_PLUGIN_PATH="$here/plugins"
export QT_QPA_PLATFORM_PLUGIN_PATH="$here/plugins/platforms"
cd "$here"
exec "$here/bin/riel-route-editor-bin" "$@"
EOF
chmod 755 "$out/riel-route-editor"

if LD_LIBRARY_PATH="$out/lib" ldd "$out/bin/riel-route-editor-bin" | grep -q 'not found'; then
  LD_LIBRARY_PATH="$out/lib" ldd "$out/bin/riel-route-editor-bin" >&2
  exit 1
fi
