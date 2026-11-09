$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$repository = Split-Path -Parent $PSScriptRoot
$fixture = Join-Path $repository ('dist/panel-verify-test-' + [guid]::NewGuid().ToString('N'))
$source = Join-Path $fixture 'source'
$installed = Join-Path $fixture 'installed'
New-Item -ItemType Directory -Path $source,$installed | Out-Null
[IO.File]::WriteAllText((Join-Path $source 'plugin.rhp'), 'original')
[IO.Compression.ZipFile]::CreateFromDirectory($source, (Join-Path $fixture 'package.yak'))
Copy-Item -LiteralPath (Join-Path $source 'plugin.rhp') -Destination $installed
$verify = Join-Path $repository 'scripts/verify-yak-install.ps1'
if (!(Test-Path -LiteralPath $verify)) { throw 'Missing installed package verification script.' }
& $verify -PackagePath (Join-Path $fixture 'package.yak') -InstalledDirectory $installed
[IO.File]::WriteAllText((Join-Path $installed 'plugin.rhp'), 'modified')
$rejected = $false
try { & $verify -PackagePath (Join-Path $fixture 'package.yak') -InstalledDirectory $installed }
catch { $rejected = $true }
if (!$rejected) { throw 'Same-size installed file corruption was not rejected.' }
Remove-Item -LiteralPath (Join-Path $installed 'plugin.rhp')
$rejected = $false
try { & $verify -PackagePath (Join-Path $fixture 'package.yak') -InstalledDirectory $installed }
catch { $rejected = $true }
if (!$rejected) { throw 'A missing installed file was not rejected.' }
Write-Output 'PASS installed package hashes reject same-size corruption and missing files.'
