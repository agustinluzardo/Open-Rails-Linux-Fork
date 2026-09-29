#!/usr/bin/env python3
"""Reuní los archivos necesarios para identificar la señal de Quilmes a Ezpeleta.

Uso:
    python3 collect-quilmes-signal.py '/ruta/ROUTES/Ferrocarril Nacional General Roca V2'
    python3 collect-quilmes-signal.py '/ruta/ROUTES/Ferrocarril Nacional General Roca V2' --output quilmes.zip

Solo lee archivos de la ruta. Crea un ZIP fuera de ella para poder revisar el
objeto visible, su definición y las referencias de vía antes de preparar un parche.
No incluye SERVICES ni TRAFFIC: la corrección de Villa Domínico queda intacta.
"""

import argparse
import hashlib
import json
import zipfile
from pathlib import Path


def find_child(folder: Path, name: str, *, required: bool = True) -> Path | None:
    matches = [path for path in folder.iterdir() if path.name.casefold() == name.casefold()]
    if len(matches) > 1 or (required and len(matches) != 1):
        raise ValueError(f"Se esperaba un único {name} en {folder}; hay {len(matches)}")
    return matches[0] if matches else None


def collect(route: Path, output: Path) -> list[Path]:
    tdb = find_child(route, "FCGR.tdb")
    world = find_child(route, "WORLD")
    sigcfg = find_child(route, "sigcfg.dat")
    assert tdb and world and sigcfg
    files = [tdb, sigcfg]
    sigscr = find_child(route, "sigscr.dat", required=False)
    if sigscr:
        files.append(sigscr)

    # Quilmes (-9792, 10262) to Ezpeleta (-9790, 10261), plus adjacent tiles.
    for x in range(-9793, -9788):
        for z in range(10260, 10264):
            tile = find_child(world, f"w{x:+07d}{z:+07d}.w", required=False)
            if tile:
                files.append(tile)
    if not any(path.parent == world for path in files):
        raise ValueError("No se encontraron archivos .w de Quilmes/Ezpeleta en WORLD")

    if output == route or route in output.parents:
        raise ValueError("El ZIP debe guardarse fuera de la carpeta de la ruta")
    if output.exists():
        raise FileExistsError(f"Ya existe {output}; elegí otro nombre para no sobrescribirlo")

    manifest = {str(path.relative_to(route)): hashlib.sha256(path.read_bytes()).hexdigest() for path in files}
    with zipfile.ZipFile(output, "x", compression=zipfile.ZIP_DEFLATED) as archive:
        for path in files:
            archive.write(path, path.relative_to(route))
        archive.writestr("riel-manifest.json", json.dumps(manifest, indent=2) + "\n")
    return files


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("route", type=Path, help="Carpeta de la ruta que contiene FCGR.tdb")
    parser.add_argument("--output", type=Path, default=Path("quilmes-signal-files.zip"))
    args = parser.parse_args()
    route = args.route.expanduser().resolve(strict=True)
    output = args.output.expanduser().resolve()
    files = collect(route, output)
    print(f"ZIP creado: {output} ({len(files)} archivos de solo lectura)")
    print("Adjuntá ese ZIP para identificar la señal y preparar la reparación exacta.")


if __name__ == "__main__":
    main()
