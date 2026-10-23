"""Installation packages must include shared libraries, never reference/test DLLs."""
import contextlib
import importlib.util
import io
from pathlib import Path
import tempfile
import unittest
import zipfile


SCRIPT = Path(__file__).resolve().parents[1] / "package.py"
SPEC = importlib.util.spec_from_file_location("plugin_package", SCRIPT)
package = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(package)


class PackageTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.output = self.root / "artifacts"
        (self.root / "LICENSE").write_text("test license", encoding="utf-8")
        for name in package.ALLOWED_PROJECTS:
            project = self.root / "src" / name / (name + ".csproj")
            project.parent.mkdir(parents=True)
            references = []
            if name in package.PLUGINS:
                references.append("Common")
            if name in package.PLUGINS[1:] or name == package.GALLERY:
                references.append("MaximumUI")
            project.write_text('<Project><PropertyGroup><AssemblyName>' + name + '</AssemblyName>'
                               '<Version>1.2.3</Version></PropertyGroup><ItemGroup>' + ''.join(
                                   '<ProjectReference Include="..\\' + item + '\\' + item + '.csproj" />'
                                   for item in references) + '</ItemGroup></Project>', encoding="utf-8")
            output = self.root / "bin" / "Release" / name
            output.mkdir(parents=True)
            (output / (name + ".dll")).write_bytes(("compiled:" + name).encode())
            # Contaminated build outputs must not leak through wildcard packaging.
            for extra in ("UnityEngine.dll", "Assembly-CSharp.dll", "BepInEx.dll",
                          "MaximumUI.Tests.dll", "user.cfg", "presets.xml"):
                (output / extra).write_bytes(b"not distributable")
        old_summary = self.root / "bin" / "Release" / "KK"
        old_summary.mkdir()
        (old_summary / "StalePlugin.dll").write_bytes(b"stale output")

    def build(self):
        with contextlib.redirect_stdout(io.StringIO()):
            return package.build_packages(self.root, self.output, revision="test-revision")

    def test_single_plugin_includes_transitive_libraries_only(self):
        self.build()
        with zipfile.ZipFile(self.output / "JuiceControl-1.2.3.zip") as archive:
            self.assertEqual(set(archive.namelist()), {
                package.INSTALL_PREFIX + "JuiceControl.dll",
                package.INSTALL_PREFIX + "Common.dll",
                package.INSTALL_PREFIX + "MaximumUI.dll",
                "INSTALL.md", "LICENSE"})
            self.assertEqual(archive.read(package.INSTALL_PREFIX + "Common.dll"), b"compiled:Common")

    def test_combined_package_contains_five_plugins_and_no_gallery(self):
        result = self.build()
        self.assertEqual(len(result["packages"]), 7)
        with zipfile.ZipFile(self.output / "MaxIllusionPlugins-all-plugins.zip") as archive:
            dlls = {Path(name).name for name in archive.namelist() if name.endswith(".dll")}
            self.assertEqual(dlls, {name + ".dll" for name in package.PLUGINS + package.LIBRARIES})

    def test_gallery_is_independent_and_has_maximumui(self):
        self.build()
        with zipfile.ZipFile(self.output / "MaximumUIGallery-1.2.3.zip") as archive:
            dlls = {Path(name).name for name in archive.namelist() if name.endswith(".dll")}
            self.assertEqual(dlls, {"MaximumUIGallery.dll", "MaximumUI.dll"})

    def test_missing_required_library_fails_before_any_package_is_written(self):
        (self.root / "bin" / "Release" / "MaximumUI" / "MaximumUI.dll").unlink()
        with self.assertRaises(FileNotFoundError):
            self.build()
        self.assertFalse(self.output.exists())

    def test_unknown_project_dependency_is_not_silently_omitted(self):
        project = self.root / "src" / "JuiceControl" / "JuiceControl.csproj"
        project.write_text(project.read_text().replace('</ItemGroup>',
                           '<ProjectReference Include="..\\PrivateGame\\PrivateGame.csproj" /></ItemGroup>'))
        with self.assertRaises(ValueError):
            self.build()
        self.assertFalse(self.output.exists())


if __name__ == "__main__":
    unittest.main()
