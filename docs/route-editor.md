# Riel Route Editor

Riel integrates a native Linux route editor based on **TSRE5vc**, by Piotr
Gadecki (GokuMK). TSRE5vc source files identify the project as licensed under
GNU GPL 3.0 or later.

## Launcher contract

The Riel launcher starts the editor selected under **Tools → Riel Route Editor**
with:

```text
riel-route-editor --game-root <content-root> --route <route-id> --appdata-profile
```

The launcher already knows the content root and route id from its normal route
scan, so the editor opens the same route without asking the user to locate MSTS
content again.

For development builds, `RIEL_ROUTE_EDITOR=/path/to/executable` overrides the
packaged executable.

## Native Linux build

The Linux CI checks out a pinned TSRE5vc commit, applies
`scripts/prepare-route-editor-source.py`, compiles it with CMake/Ninja against
Qt 6 and OpenAL, and packages the resulting ELF executable in
`app/route-editor/riel-route-editor` together with its matching `appdata`.

The exact upstream commit is deliberately pinned in
`.github/workflows/linux-portable.yml` so the corresponding source for every
Riel build is unambiguous and reproducible.

Upstream project: https://github.com/GokuMK/TSRE5vc
