#!/usr/bin/env python3
"""Apply the small Riel integration patch to a pinned TSRE5vc checkout.

The route editor remains GPL-3.0-or-later software by Piotr Gadecki/GokuMK.
Keeping the patch here makes the exact corresponding source reproducible from
the upstream commit recorded by the Linux workflow.
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
    replace_once(
        source / "src" / "tsre" / "Game.cpp",
        'QString Game::AppName = "TSRE5";',
        'QString Game::AppName = "Riel Route Editor";',
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
