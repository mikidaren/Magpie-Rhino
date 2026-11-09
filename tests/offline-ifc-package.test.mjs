import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

const read = (relative) => readFileSync(new URL('../' + relative, import.meta.url), 'utf8');

test('the shipped IFC service resolves only its fixed private runtime', () => {
  const source = read('src/RhinoMagpie.Plugin/OpenBimTools.cs');
  assert.ok(source.includes('IfcRuntimeBundle.Resolve('));
  assert.ok(source.includes('IfcRuntimeBundle.CreateStartInfo('));
  assert.ok(!source.includes('openbim-runtime.json'));
  assert.ok(!source.includes('setup-openbim.ps1'));
});

test('the release builder requires an offline bundle and ships no manual setup step', () => {
  const source = read('scripts/build-panel.ps1');
  assert.ok(source.includes('IfcRuntimePath'));
  assert.ok(source.includes('bundle-validation.json'));
  assert.ok(!source.includes('setup-openbim.ps1'));
});

test('Windows packaging CI builds and checks the isolated runtime', () => {
  const source = read('.github/workflows/build.yml');
  assert.ok(source.includes('build-ifc-runtime.ps1'));
  assert.ok(source.includes('test-ifc-runtime.ps1'));
  assert.ok(source.indexOf('build-ifc-runtime.ps1') < source.indexOf('build-panel.ps1'));
});

test('all nine runtime dependencies have exact versions and wheel hashes', () => {
  const lines = read('runtime/requirements-win-x64.txt').trim().split(/\r?\n/);
  assert.equal(lines.length, 9);
  for (const line of lines) assert.match(line, /^[a-zA-Z0-9_-]+==[0-9.a-z]+ --hash=sha256:[a-f0-9]{64}$/);
});
