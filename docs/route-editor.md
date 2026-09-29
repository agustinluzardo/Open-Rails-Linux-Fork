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

\`\`\`text
riel-route-editor --game-root <content-root> --route <route-id> --appdata-profile
\`\`\`

Riel already knows the content root and route id from its normal content scan,
so the editor does not ask the user to locate MSTS content again.

For development, \`RIEL_ROUTE_EDITOR=/path/to/executable\` overrides the
packaged executable.

## Riel integration

The editor is branded as Riel at runtime, uses the Riel window icon/splash and
stores its app-data settings under \`~/.local/share/Riel/RouteEditor/\` on a
default Linux desktop. Original TSRE5vc credits and GPL notices remain intact.

## Native Linux build

CI checks out a pinned TSRE5vc commit, applies
\`scripts/prepare-route-editor-source.py\`, builds with CMake/Ninja and Qt
6.10.1, then packages the resulting native ELF editor suite beside Riel.

Upstream project: https://github.com/GokuMK/TSRE5vc

## Portable runtime and source

The Linux package bundles the editor\'s Qt/OpenAL runtime beside the native ELF binary. The portable `riel` wrapper also exposes `riel route-editor`, `riel consist-editor`, `riel shape-viewer`, and `riel ace-converter`. Every published Linux build attaches `riel-route-editor-source.tar.gz` with the exact patched TSRE5vc source used to build it.
