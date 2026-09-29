#!/usr/bin/env python3
"""Apply Riel integration patches to a pinned TSRE5vc checkout.

The editor remains GPL-3.0-or-later software by Piotr Gadecki/GokuMK. This
script changes product integration/branding only; original copyright/license
headers stay intact.
"""

from __future__ import annotations

import argparse
from pathlib import Path


def replace_once(path: Path, old: str, new: str) -> None:
    text = path.read_text(encoding="utf-8")
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{path}: expected exactly one occurrence of {old!r}, found {count}")
    path.write_text(text.replace(old, new), encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    args = parser.parse_args()
    source = args.source.resolve()

    game = source / "src" / "tsre" / "Game.cpp"
    replace_once(game, 'QString Game::AppName = "TSRE5";', 'QString Game::AppName = "Riel";')
    replace_once(game, 'QString Game::AppVersion = "v" TSRE5_VERSION;', 'QString Game::AppVersion = TSRE5_VERSION;')
    replace_once(game, 'QString Game::root = "C:/tsdata/Train Simulator/";', 'QString Game::root = "";')
    replace_once(game, 'QString Game::route = "bbb1";', 'QString Game::route = "";')

    settings = source / "src" / "settings" / "SettingsProfile.cpp"
    replace_once(
        settings,
        'return QDir(base).filePath("TSRE");',
        'return QDir(base).filePath("Riel/RouteEditor");',
    )

    main = source / "src" / "main.cpp"
    replace_once(
        main,
        'const QCommandLineOption AppDataProfileOption("appdata-profile", "Use the TSRE profile stored in user application data.");',
        'const QCommandLineOption AppDataProfileOption("appdata-profile", "Use the Riel editor profile stored in user application data.");',
    )
    replace_once(
        main,
        "    QApplication app(argc, argv);\n    TranslationManager translationManager;",
        '    QApplication app(argc, argv);\n'
        '    QGuiApplication::setDesktopFileName("riel-route-editor");\n'
        '    app.setWindowIcon(QIcon(QDir::current().filePath("riel-route-editor.png")));\n'
        "    TranslationManager translationManager;",
    )

    splash = 'myImage->load(QString("appdata/")+Game::AppDataVersion+"/load.png");'
    for relative in (
        "src/routeEditor/LoadWindow.cpp",
        "src/routeEditor/AboutWindow.cpp",
        "src/conEditor/CELoadWindow.cpp",
    ):
        replace_once(source / relative, splash, 'myImage->load("riel-route-editor.png");')

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
