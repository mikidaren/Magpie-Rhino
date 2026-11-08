$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path $PSScriptRoot
$fixtureRoot = Join-Path $repositoryRoot ('dist/install-test-' + [guid]::NewGuid())
$packageDirectory = Join-Path $fixtureRoot 'package'
New-Item -ItemType Directory -Path $packageDirectory -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'scripts/install.ps1') -Destination $packageDirectory
Set-Content -LiteralPath (Join-Path $packageDirectory 'RhinoMagpie.exe') -Value 'fixture, never executed'
$originalProfile = $env:USERPROFILE
$originalLocalAppData = $env:LOCALAPPDATA
try {
    $env:USERPROFILE = Join-Path $fixtureRoot 'fresh profile'
    $env:LOCALAPPDATA = Join-Path $fixtureRoot 'local app data'
    & (Join-Path $packageDirectory 'install.ps1') -NoShortcut
    $marker = Join-Path $env:USERPROFILE '.config/rhino9-magpie'
    if (!(Test-Path -LiteralPath $marker -PathType Container)) { throw 'Fresh installation lacks the Magpie detection directory.' }
    if (Test-Path -LiteralPath (Join-Path $marker 'settings.json')) { throw 'Installer must not invent gateway settings.' }
    if (!(Test-Path -LiteralPath (Join-Path $env:LOCALAPPDATA 'Rhino9Magpie/Launcher/RhinoMagpie.exe'))) { throw 'Launcher was not installed.' }
    Write-Output 'PASS clean-profile installer creates the detection directory without credentials.'
}
finally {
    $env:USERPROFILE = $originalProfile
    $env:LOCALAPPDATA = $originalLocalAppData
}
