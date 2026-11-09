import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import test from 'node:test';

const snapshotDirectory = fileURLToPath(new URL('../vendor/openbim/', import.meta.url));
const snapshot = JSON.parse(readFileSync(path.join(snapshotDirectory, 'snapshot.json'), 'utf8'));

test('OpenBIM snapshot has portable bounded source paths', () => {
  assert.equal(snapshot.repository, 'https://github.com/mikidaren/Miki-GrasshopperWarehouse');
  assert.equal(snapshot.contract, 1);
  assert.ok(snapshot.files.length >= 11);
  for (const file of snapshot.files) {
    assert.ok(!file.path.includes('\\'), 'Snapshot paths must use forward slashes');
    assert.ok(!path.isAbsolute(file.path) && !file.path.split('/').includes('..'));
  }
});

test('every vendored OpenBIM source matches its pinned SHA256', () => {
  for (const file of snapshot.files) {
    const actual = createHash('sha256').update(readFileSync(path.join(snapshotDirectory, file.path))).digest('hex');
    assert.equal(actual, file.sha256, file.path);
  }
});

test('the IFC worker and offline runtime lock use the same fixed dependency', () => {
  const requirements = readFileSync(path.join(snapshotDirectory, 'python/requirements.txt'), 'utf8').trim();
  assert.equal(requirements, 'ifcopenshell==0.9.0.post1');
  const runtimeLock = readFileSync(new URL('../runtime/requirements-win-x64.txt', import.meta.url), 'utf8');
  assert.match(runtimeLock, /^ifcopenshell==0\.9\.0\.post1 --hash=sha256:[a-f0-9]{64}/);
});
