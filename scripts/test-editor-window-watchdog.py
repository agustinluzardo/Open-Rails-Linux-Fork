#!/usr/bin/env python3
"""Prove the window smoke check rejects the real editor with only Navi shown.

Mutates only the disposable generated TSRE source. Always restores and rebuilds
the production binary before packaging; no suppression hook is shipped.
"""

import argparse
import os
from pathlib import Path
import signal
import subprocess


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    parser.add_argument("build", type=Path)
    parser.add_argument("game_root", type=Path)
    args = parser.parse_args()
    source, build = args.source.resolve(), args.build.resolve()
    window = source / "src/routeEditor/RouteEditorWindow.cpp"
    original = window.read_text(encoding="utf-8")
    needle = "    QMainWindow::show();"
    if original.count(needle) != 1:
        raise SystemExit("Cannot install temporary Navi-only regression fixture")

    def rebuild():
        with (build / "riel-window-watchdog-build.log").open("w") as log:
            subprocess.run(["cmake", "--build", str(build), "--target", "TSRE5vc", "--parallel", "2"],
                           stdout=log, stderr=subprocess.STDOUT, check=True)

    try:
        window.write_text(original.replace(needle, "    return; // test-only: leave just Navi visible\n" + needle), encoding="utf-8")
        rebuild()
        env = os.environ.copy()
        env.update(RIEL_EDITOR_BUNDLE_DIR=str(source), RIEL_EDITOR_RENDER_DIAGNOSTICS="1")
        process = subprocess.Popen(["xvfb-run", "-a", str(build / "TSRE5vc"),
                                    "--game-root", str(args.game_root.resolve()), "--route", "RIELTEST",
                                    "--route-full-window-check"],
                                   stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                                   text=True, env=env, start_new_session=True)
        try:
            output, _ = process.communicate(timeout=35)
        except subprocess.TimeoutExpired:
            os.killpg(process.pid, signal.SIGKILL)
            process.communicate()
            raise SystemExit("Navi-only check did not exit through its watchdog")
        (build / "riel-window-watchdog-negative.log").write_text(output, encoding="utf-8")
        if (process.returncode != 1 or "RIEL_EDITOR_WINDOW navi-created" not in output
                or "RIEL_ROUTE_FULL_WINDOW_FAILED" not in output
                or "main= false navi= true" not in output
                or "RIEL_EDITOR_WINDOW main-shown" in output
                or "RIEL_ROUTE_FULL_WINDOW_OK" in output):
            raise SystemExit("Window watchdog accepted an unusable editor; see negative log")
    finally:
        window.write_text(original, encoding="utf-8")
        rebuild()
    print("RIEL_ROUTE_WINDOW_WATCHDOG_OK: Navi-only editor rejected; production binary restored")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
