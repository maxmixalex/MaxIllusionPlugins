#!/usr/bin/env python3
"""Package only this repository's plugin assemblies and their project dependencies."""
import argparse
import json
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET
import zipfile


PLUGINS = (
    "MultiTeamH",
    "AccessoryBatchManager",
    "ClothingCompression",
    "ExpressionControl",
    "JuiceControl",
)
GALLERY = "MaximumUIGallery"
LIBRARIES = ("Common", "MaximumUI")
ALLOWED_PROJECTS = PLUGINS + (GALLERY,) + LIBRARIES
INSTALL_PREFIX = "BepInEx/plugins/MaxIllusionPlugins/"
SOURCE_URL = "https://github.com/maxmixalex/MaxIllusionPlugins"


def load_projects(root):
    projects = {}
    for name in ALLOWED_PROJECTS:
        path = root / "src" / name / (name + ".csproj")
        document = ET.parse(path).getroot()
        assembly = document.findtext("./PropertyGroup/AssemblyName")
        version = document.findtext("./PropertyGroup/Version")
        if not assembly or not version:
            raise ValueError(f"Missing AssemblyName or Version: {path}")
        dependencies = []
        for reference in document.findall("./ItemGroup/ProjectReference"):
            target = (path.parent / reference.attrib["Include"].replace("\\", "/")).resolve()
            dependency = target.stem
            expected = (root / "src" / dependency / (dependency + ".csproj")).resolve()
            if dependency not in ALLOWED_PROJECTS or target != expected:
                raise ValueError(f"Unlisted project dependency in {name}: {target}")
            dependencies.append(dependency)
        projects[name] = {"assembly": assembly + ".dll", "version": version,
                          "dependencies": dependencies}
    return projects


def dependency_closure(projects, names):
    resolved = set()

    def visit(name):
        if name in resolved:
            return
        resolved.add(name)
        for dependency in projects[name]["dependencies"]:
            visit(dependency)

    for name in names:
        visit(name)
    return sorted(resolved)


def installation_text(title, projects, included, revision):
    versions = "\n".join(f"- {name}: {projects[name]['version']}" for name in included)
    gallery_note = ("本包包含独立 Gallery，快捷键可在其配置中调整；默认值见对应版本的 README。\n"
                    if GALLERY in included else "独立 MaximumUI Gallery 不包含在功能插件合集内。\n")
    return f"""# {title}

## 安装

1. 退出游戏。确认已经安装 BepInEx 5 和对应插件 README 中的运行依赖。
2. 更新旧版本时，移除 `BepInEx/plugins` 其它位置中的同名旧插件 DLL、旧 `MaximumUI.dll` 和旧 `MaxIllusionPlugins.Common.dll`，避免重复加载。
3. 将本 ZIP 中的 `BepInEx` 文件夹解压到游戏根目录，与现有目录合并。每个单插件包已经包含它实际引用的本仓共享库。
4. 多个包来自同一次构建时，可以将其共享 DLL 覆盖到同一目录。现有 `.cfg`、预设、角色卡和存档不在包中。

{gallery_note}
ClothingCompression 的 XML 使用 `BepInEx/config/MaxMixAlex.ClothingCompression.xml`；已有旧文件请自行由 `ClothingCompression.xml` 重命名。

## Included versions

{versions}

Source commit: `{revision}`

Source and plugin requirements: {SOURCE_URL}/tree/{revision}/src

The DLLs target .NET Framework 3.5 / Unity 5.6. Headless CI validates compilation and managed assertions; it does not run Koikatsu. Game binaries, third-party runtime dependencies, tests and local user data are not distributed in this package.
"""


def build_packages(root, output, configuration="Release", revision=None):
    root = Path(root).resolve()
    output = Path(output).resolve()
    projects = load_projects(root)
    if revision is None:
        revision = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=root, text=True).strip()
    if not revision:
        raise ValueError("A source revision is required")

    # Validate every required assembly before creating any downloadable package.
    assembly_paths = {}
    for name in dependency_closure(projects, PLUGINS + (GALLERY,)):
        path = root / "bin" / configuration / name / projects[name]["assembly"]
        if not path.is_file() or path.stat().st_size == 0:
            raise FileNotFoundError(f"Build {name} before packaging: {path}")
        assembly_paths[name] = path

    output.mkdir(parents=True, exist_ok=True)
    specs = [(name, (name,), f"{name}-{projects[name]['version']}.zip") for name in PLUGINS + (GALLERY,)]
    specs.append(("MaxIllusionPlugins — all five functional plugins", PLUGINS, "MaxIllusionPlugins-all-plugins.zip"))
    manifest = {"source_commit": revision, "configuration": configuration, "packages": []}
    for title, roots, filename in specs:
        included = dependency_closure(projects, roots)
        with zipfile.ZipFile(output / filename, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
            for name in included:
                archive.write(assembly_paths[name], INSTALL_PREFIX + projects[name]["assembly"])
            archive.writestr("INSTALL.md", installation_text(title, projects, included, revision))
            archive.write(root / "LICENSE", "LICENSE")
        manifest["packages"].append({"file": filename, "projects": [
            {"name": name, "version": projects[name]["version"], "assembly": projects[name]["assembly"]}
            for name in included]})
        print(f"{filename}: {', '.join(projects[name]['assembly'] for name in included)}")

    (output / "packages.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    package_list = "\n".join(f"- `{item['file']}`" for item in manifest["packages"])
    (output / "README.md").write_text(f"""## 选择安装包

下载 GitHub Actions 的 `plugin-packages` 产物并解压，选择下面一个合集或按需选择单插件 ZIP，再把其内部的 `BepInEx` 文件夹解压到游戏根目录。

{package_list}

`MaxIllusionPlugins-all-plugins.zip` 包含五个功能插件及它们实际引用的 Common / MaximumUI 共享库；Gallery 单独提供。单插件包也带上所需共享库，不必另找依赖 DLL。游戏、BepInEx、KKAPI 等外部运行依赖需已经安装。

更新时请移除其它位置的同名旧 DLL，避免加载多份插件。配置和预设不会随包覆盖。ClothingCompression 旧 XML 请自行重命名为 `MaxMixAlex.ClothingCompression.xml`。

Source commit: `{revision}`

`packages.json` 列出每包中的本仓程序集和版本。CI 未运行真实游戏；详见各包的 `INSTALL.md` 与源码 README。
""", encoding="utf-8")
    return manifest


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--configuration", choices=("Debug", "Release"), default="Release")
    parser.add_argument("--output", type=Path, default=Path("artifacts/packages"))
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    build_packages(root, args.output, args.configuration)


if __name__ == "__main__":
    main()
