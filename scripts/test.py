#!/usr/bin/env python3
"""Build all projects and execute every console assertion suite (no game needed)."""
import argparse
import os
from pathlib import Path
import subprocess


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--configuration", choices=("Debug", "Release"), default="Release")
    parser.add_argument("--no-build", action="store_true")
    parser.add_argument("--benchmark", action="store_true", help="Also measure MaximumUI managed build/layout policies")
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    dotnet = os.environ.get("DOTNET", "dotnet")
    environment = os.environ.copy()
    if args.benchmark:
        environment["MAXIMUMUI_BENCHMARK"] = "1"

    def run(*command):
        subprocess.run(command, cwd=root, env=environment, check=True)

    if not args.no_build:
        run(dotnet, "build", "MaxIllusionPlugins.sln", "-c", args.configuration, "--nologo", "-m:1", "-nodeReuse:false")
    for suite in ("MaximumUI.Runner", "MultiTeamH.Regression", "AccessoryClothing.Regression", "PluginCore.Regression"):
        command = [dotnet, "run", "--project", f"tests/{suite}/{suite}.csproj", "-c", args.configuration, "--no-build"]
        if suite == "MaximumUI.Runner":
            command += ["--", f"bin/{args.configuration}/MaximumUI.Tests/MaximumUI.Tests.dll"]
        run(*command)


if __name__ == "__main__":
    main()
