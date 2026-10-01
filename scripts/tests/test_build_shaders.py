"""Exercise shader build orchestration without downloading the compiler SDK."""

import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest


SCRIPT = Path(__file__).resolve().parents[1] / "build-shaders.sh"


class ShaderBuildChecks(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="riel-shader-check-")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        (self.root / "scripts").mkdir()
        (self.root / "Source").mkdir()
        shutil.copyfile(SCRIPT, self.root / "scripts/build-shaders.sh")
        self.bin = self.root / "bin"
        self.bin.mkdir()
        self.executable("uname", "echo MINGW64_NT\n")
        self.executable("dotnet", '''if [ "$1" = tool ] && [ "$2" = run ]; then
    printf 'compiled' > "$5"
fi
''')
        self.env = dict(os.environ, PATH=f"{self.bin}{os.pathsep}{os.environ['PATH']}")

    def executable(self, name, body):
        path = self.bin / name
        path.write_text("#!/bin/sh\n" + body, encoding="utf-8")
        path.chmod(0o755)

    def sources(self):
        directories = [self.root / "Source/ActivityRunner/Content/Shaders",
                       self.root / "Source/Riel.Graphics/Resources/Shaders"]
        for directory in directories:
            directory.mkdir(parents=True)
        return directories

    def run_build(self):
        return subprocess.run(["bash", str(self.root / "scripts/build-shaders.sh"), "--check"],
                              env=self.env, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                              text=True, timeout=15, check=False)

    def test_missing_source_directories_fail(self):
        result = self.run_build()
        self.assertNotEqual(result.returncode, 0, result.stdout)
        self.assertIn("shader source directory is missing", result.stdout)

    def test_missing_one_source_directory_fails(self):
        first, second = self.sources()
        (first / "Scenery.fx").write_text("shader", encoding="utf-8")
        second.rmdir()
        result = self.run_build()
        self.assertNotEqual(result.returncode, 0, result.stdout)
        self.assertIn("shader source directory is missing", result.stdout)

    def test_empty_source_directories_fail(self):
        self.sources()
        result = self.run_build()
        self.assertNotEqual(result.returncode, 0, result.stdout)
        self.assertIn("no shader sources were found", result.stdout)

    def test_compiler_failure_fails_check(self):
        first, _ = self.sources()
        (first / "Scenery.fx").write_text("shader", encoding="utf-8")
        self.executable("dotnet", 'if [ "$1" = tool ] && [ "$2" = run ]; then exit 1; fi\n')
        result = self.run_build()
        self.assertNotEqual(result.returncode, 0, result.stdout)
        self.assertIn("0 compiled, 1 failed", result.stdout)

    def test_successful_compilation_copies_output(self):
        first, second = self.sources()
        (first / "Scenery.fx").write_text("vs_5_0 ps_5_0", encoding="utf-8")
        (second / "Map.fx").write_text("vs_5_0 ps_5_0", encoding="utf-8")
        self.executable("dotnet", '''if [ "$1" = tool ] && [ "$2" = run ]; then
    if grep -q '5_0' "$4"; then exit 1; fi
    printf 'compiled' > "$5"
fi
''')
        result = self.run_build()
        self.assertEqual(result.returncode, 0, result.stdout)
        self.assertIn("2 compiled, 0 failed", result.stdout)
        for name in ("Scenery", "Map"):
            self.assertEqual((self.root / f"Source/Shaders/prebuilt/OpenGL/{name}.mgfx").read_bytes(),
                             b"compiled")


if __name__ == "__main__":
    unittest.main()
