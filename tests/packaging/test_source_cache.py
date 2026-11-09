import importlib.util
import hashlib
import json
from pathlib import Path
import tempfile
import unittest


SCRIPT = Path(__file__).resolve().parents[2] / "scripts" / "fetch-ifc-sources.py"


class SourceCacheTests(unittest.TestCase):
    def setUp(self):
        self.assertTrue(SCRIPT.is_file(), "The pinned source-evidence cache fetcher is not implemented")
        spec = importlib.util.spec_from_file_location("fetch_ifc_sources", SCRIPT)
        self.fetcher = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(self.fetcher)
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.cache = self.root / "cache"
        self.cache.mkdir()
        self.payload = b"verified source notice"
        self.entry = {"cacheFile": "source.tar.gz", "url": "https://example.invalid/source.tar.gz",
                      "size": len(self.payload), "sha256": hashlib.sha256(self.payload).hexdigest(), "status": "acquired"}
        self.manifest = self.root / "sources.json"

    def write_manifest(self, entry):
        self.manifest.write_text(json.dumps({"schemaVersion": 1, "artifacts": {"sample": entry}}))

    def test_valid_existing_evidence_needs_no_network(self):
        (self.cache / "source.tar.gz").write_bytes(self.payload)
        self.write_manifest(self.entry)
        self.assertEqual(self.fetcher.populate_cache(self.manifest, self.cache), 1)

    def test_corrupt_existing_evidence_is_not_overwritten(self):
        target = self.cache / "source.tar.gz"
        target.write_bytes(b"keep corrupt evidence")
        self.write_manifest(self.entry)
        with self.assertRaises(ValueError):
            self.fetcher.populate_cache(self.manifest, self.cache)
        self.assertEqual(target.read_bytes(), b"keep corrupt evidence")

    def test_source_cache_path_cannot_escape(self):
        self.entry["cacheFile"] = "../outside.tar.gz"
        self.write_manifest(self.entry)
        with self.assertRaises(ValueError):
            self.fetcher.populate_cache(self.manifest, self.cache)


if __name__ == "__main__":
    unittest.main()
