import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import tempfile
from urllib.parse import urlparse
from urllib.request import urlopen


def populate_cache(manifest_path, cache):
    manifest = json.loads(Path(manifest_path).read_text(encoding="utf-8-sig"))
    if manifest.get("schemaVersion") != 1 or not isinstance(manifest.get("artifacts"), dict):
        raise ValueError("Unsupported source-artifact catalogue")
    entries = []
    seen = set()
    for name, entry in manifest["artifacts"].items():
        filename = entry.get("cacheFile", "")
        parsed = urlparse(entry.get("url", ""))
        if (not filename or filename in {".", ".."} or any(character in filename for character in "/\\:")
                or filename.casefold() in seen or parsed.scheme != "https" or not parsed.hostname
                or parsed.username or parsed.password or entry.get("status") != "acquired"
                or not isinstance(entry.get("size"), int) or entry["size"] < 0
                or not re.fullmatch("[a-fA-F0-9]{64}", entry.get("sha256", ""))):
            raise ValueError("Unsafe or incomplete source-artifact entry: " + name)
        seen.add(filename.casefold())
        entries.append(entry)
    cache = Path(cache).resolve()
    cache.mkdir(parents=True, exist_ok=True)
    for entry in entries:
        target = cache / entry["cacheFile"]
        if target.exists():
            with target.open("rb") as source:
                digest = hashlib.file_digest(source, "sha256").hexdigest()
            if target.stat().st_size != entry["size"] or digest != entry["sha256"].lower():
                raise ValueError("Cached source evidence is corrupted; it was not overwritten: " + target.name)
            continue
        descriptor, temporary_name = tempfile.mkstemp(prefix=".source-evidence-", dir=cache)
        temporary = Path(temporary_name)
        try:
            digest = hashlib.sha256()
            size = 0
            with os.fdopen(descriptor, "wb") as output, urlopen(entry["url"], timeout=60) as response:
                while chunk := response.read(1024 * 1024):
                    size += len(chunk)
                    if size > entry["size"]:
                        raise ValueError("Downloaded source exceeds its pinned size")
                    digest.update(chunk)
                    output.write(chunk)
            if size != entry["size"] or digest.hexdigest() != entry["sha256"].lower():
                raise ValueError("Downloaded source does not match its pinned digest")
            os.link(temporary, target)
        finally:
            if temporary.parent == cache:
                temporary.unlink(missing_ok=True)
    return len(entries)


def main():
    parser = argparse.ArgumentParser(description="Fetch digest-pinned licence/source evidence at build time only")
    parser.add_argument("--manifest", required=True)
    parser.add_argument("--cache", required=True)
    options = parser.parse_args()
    print("Verified source evidence artifacts: " + str(populate_cache(options.manifest, options.cache)))


if __name__ == "__main__":
    main()
