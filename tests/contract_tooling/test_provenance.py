import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]


class ProvenanceTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.checkout = Path(self.temporary.name)
        for relative in (
            'scripts/generate-api-client.py',
            'contracts/openapi.json',
            'contracts/source.json',
            'src/MuscleForge.Client/Generated/MuscleForgeApiClient.g.cs',
        ):
            target = self.checkout / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(ROOT / relative, target)

    def check(self):
        return subprocess.run(
            [sys.executable, str(self.checkout / 'scripts/generate-api-client.py'), '--check'],
            cwd=self.checkout, capture_output=True, text=True)

    def test_committed_contract_generates_without_another_checkout(self):
        result = self.check()
        self.assertEqual(0, result.returncode, result.stderr)

    def test_generated_drift_is_a_cli_failure(self):
        target = self.checkout / 'src/MuscleForge.Client/Generated/MuscleForgeApiClient.g.cs'
        target.write_text(target.read_text() + '// Deliberate drift\n')
        result = self.check()
        self.assertNotEqual(0, result.returncode)
        self.assertIn('Generated-client drift', result.stderr)

    def test_contract_hash_mismatch_is_a_cli_failure(self):
        target = self.checkout / 'contracts/openapi.json'
        target.write_bytes(target.read_bytes() + b' ')
        result = self.check()
        self.assertNotEqual(0, result.returncode)
        self.assertIn('Contract hash does not match', result.stderr)

    def test_generator_and_api_version_mismatches_are_cli_failures(self):
        target = self.checkout / 'contracts/source.json'
        original = json.loads(target.read_text())
        for field, value, message in (
            ('generatorVersion', 1, 'different generator version'),
            ('apiVersion', 'v2', 'metadata does not match'),
        ):
            with self.subTest(field=field):
                modified = dict(original, **{field: value})
                target.write_text(json.dumps(modified))
                result = self.check()
                self.assertNotEqual(0, result.returncode)
                self.assertIn(message, result.stderr)
