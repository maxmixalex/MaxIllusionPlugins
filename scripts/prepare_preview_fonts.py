#!/usr/bin/env python3
"""Install the pinned open-source Selawik fonts for Linux Gallery previews only.

Microsoft's fonts repository links to Winstrap as a Selawik distribution:
https://github.com/microsoft/fonts
The fonts retain their accompanying SIL Open Font License and are not included
in any plugin installation ZIP.
"""
import argparse
from pathlib import Path
import shutil
import subprocess
import urllib.request


REVISION = "342cf99031344f48917e14a5c3a728ec5ede8d8f"
BASE_URL = f"https://raw.githubusercontent.com/winjs/winstrap/{REVISION}/src/fonts/"
FONTS = ("selawk.ttf", "selawkl.ttf", "selawksl.ttf", "selawksb.ttf", "selawkb.ttf")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, help="Use an existing checkout's src/fonts directory instead of downloading.")
    parser.add_argument("--directory", type=Path,
                        default=Path.home() / ".local/share/fonts/maximumui-preview")
    args = parser.parse_args()
    if shutil.which("fc-cache") is None:
        parser.error("Fontconfig is required (install the fontconfig package).")
    args.directory.mkdir(parents=True, exist_ok=True)
    for name in (*FONTS, "LICENSE.txt"):
        if args.source is not None:
            data = (args.source / name).read_bytes()
        else:
            with urllib.request.urlopen(BASE_URL + name, timeout=30) as response:
                data = response.read(4 * 1024 * 1024)
        if name.endswith(".ttf") and data[:4] != b"\x00\x01\x00\x00":
            raise ValueError(f"Invalid TrueType data from {BASE_URL + name}")
        target = args.directory / name
        temporary = target.with_suffix(target.suffix + ".tmp")
        temporary.write_bytes(data)
        temporary.replace(target)
    (args.directory / "SOURCE.txt").write_text(BASE_URL + "\n", encoding="utf-8")
    subprocess.run(["fc-cache", "-f", str(args.directory)], check=True)
    print("Preview font: Microsoft Selawik, Winstrap revision " + REVISION)


if __name__ == "__main__":
    main()
