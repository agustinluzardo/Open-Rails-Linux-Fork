#!/usr/bin/env python3
"""Extend the redistributable test route with split-directory editor assets."""

from pathlib import Path
import runpy
import struct
import sys

fixture = runpy.run_path(str(Path(__file__).resolve().parents[1] / "Source/Tools/TestRoute/make_test_route.py"))
root, route = fixture["root"], fixture["route"]
write, box, ace = fixture["write"], fixture["box_shape"], fixture["ace"]

# GLOBAL/SHAPES exists, but the two add-on shapes only exist in differently
# cased sibling trees. Texture fallback also crosses route TEXTURES siblings.
(root / "GLOBAL/SHAPES").mkdir(parents=True, exist_ok=True)
(route / "SHAPES").mkdir(parents=True, exist_ok=True)
(route / "TEXTURES").mkdir(parents=True, exist_ok=True)
for path in (root / "Global/Shapes/XTrackOnly.S", root / "global/shapes/YTrackOnly.s",
             route / "Shapes/RouteOnly.S"):
    write(path, box(3, 4, 3, "editorfixture.ace"))
(route / "Textures").mkdir(parents=True, exist_ok=True)
(route / "Textures/EditorFixture.ACE").write_bytes(ace(64, (60, 160, 220)))

# The native legacy WORLD/shape readers expect MSTS UTF-16 text. The base
# simulator fixture uses ASCII, which some editor readers cannot interpret.
text_extensions = {".s", ".eng", ".con", ".cvf", ".dat", ".trk", ".pat",
                   ".sms", ".env", ".act", ".srv", ".tdb"}
for path in root.rglob("*"):
    if path.is_file() and path.suffix.lower() in text_extensions:
        raw = path.read_bytes()
        text = raw.decode("utf-16" if raw.startswith((b"\xff\xfe", b"\xfe\xff")) else "utf-8")
        text = text.replace("\r\n", "\n")
        path.write_text(text.replace("\n", "\r\n"), encoding="utf-16")
# These route-database filenames are constructed directly by the legacy reader.
(route / "RIELTEST.TDB").rename(route / "RIELTEST.tdb")

# The simulator fixture's scanline table is zero-filled because its decoder
# ignores it. TSRE's ACE codec validates offsets, so write actual row addresses
# relative to the body after the 16-byte SIMISA header.
for path in root.rglob("*"):
    if not path.is_file() or path.suffix.lower() != ".ace":
        continue
    data = bytearray(path.read_bytes())
    flags, width, height = struct.unpack_from("<III", data, 20)
    channels = struct.unpack_from("<I", data, 36)[0]
    levels = []
    w, h = width, height
    while True:
        levels.append((w, h))
        if not flags & 1 or w == 1 or h == 1:
            break
        w, h = w // 2, h // 2
    table = 16 + 152 + channels * 16
    payload = table + sum(h for w, h in levels) * 4 - 16
    for w, h in levels:
        for row in range(h):
            struct.pack_into("<I", data, table, payload + row * w * channels)
            table += 4
        payload += w * h * channels
    path.write_bytes(data)

objects = []
for i in range(96):
    name = ("../../../GLOBAL/SHAPES/XTrackOnly.S",
            "../../../GLOBAL/SHAPES/YTrackOnly.S", "RouteOnly.S")[i % 3]
    x, z = (i % 12 - 6) * 7, (i // 12 - 4) * 7
    objects.append(f'''Static (
        UiD ( {1000 + i} ) FileName ( "{name}" )
        Position ( {x} 0 {z} ) QDirection ( 0 0 0 1 )
        StaticFlags ( 00000100 ) VDbId ( 4294967295 )
    )''')
world = route / "WORLD/w-000006+000012.w"
world.parent.mkdir(parents=True, exist_ok=True)
world.write_text("SIMISA@@@@@@@@@@JINX0w0t______\r\nTr_Worldfile (\r\n"
                 + "\r\n".join(objects) + "\r\n)\r\n", encoding="utf-16")
print("editor fixture includes 96 WORLD objects and split XTracks/YTracks assets")
