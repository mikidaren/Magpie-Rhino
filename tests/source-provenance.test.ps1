$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
$fixture = Join-Path $repository ('dist/source-provenance-test-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
& git -C $fixture init -q
& git -C $fixture config user.email 'source-test@example.invalid'
& git -C $fixture config user.name 'Source Test'
[IO.File]::WriteAllText((Join-Path $fixture 'source.txt'), 'first')
& git -C $fixture add source.txt
& git -C $fixture commit -q -m initial
$script = Join-Path $repository 'scripts/get-build-source.ps1'
if (!(Test-Path -LiteralPath $script)) { throw 'Missing build source identity script.' }
$clean = & $script -Repository $fixture | ConvertFrom-Json
if (!$clean.known -or !$clean.clean -or $clean.commit -notmatch '^[a-f0-9]{40}$' -or $clean.tree -notmatch '^[a-f0-9]{40}$') {
    throw 'A clean Git checkout did not yield its commit and tree.'
}
[IO.File]::WriteAllText((Join-Path $fixture 'source.txt'), 'modified')
$dirty = & $script -Repository $fixture | ConvertFrom-Json
if (!$dirty.known -or $dirty.clean -or $dirty.commit -ne $clean.commit) { throw 'Modified tracked source was not marked dirty.' }
& git -C $fixture checkout -- source.txt
[IO.File]::WriteAllText((Join-Path $fixture 'new.txt'), 'untracked')
$untracked = & $script -Repository $fixture | ConvertFrom-Json
if (!$untracked.known -or $untracked.clean) { throw 'Untracked source was not marked dirty.' }
Write-Output 'PASS source identity distinguishes clean, modified and untracked checkouts.'
