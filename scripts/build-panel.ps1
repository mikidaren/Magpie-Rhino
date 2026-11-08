param([Parameter(Mandatory=$true)][string]$YakPath)
$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
$packageDirectory = Join-Path $repository 'dist/panel'
$releaseDirectory = Join-Path $repository 'dist'
$buildDirectory = Join-Path $repository 'dist/compiled'
dotnet build (Join-Path $repository 'src/RhinoMagpie.Plugin') -c Release --nologo -o $buildDirectory
if ($LASTEXITCODE -ne 0) { throw 'Rhino plugin build failed.' }
New-Item -ItemType Directory -Path $packageDirectory -Force | Out-Null
foreach ($name in @('RhinoMagpie.Plugin.rhp', 'RhinoMagpie.Agent.dll', 'RhinoMagpie.Plugin.deps.json', 'RhinoMagpie.Plugin.runtimeconfig.json')) {
    Copy-Item -LiteralPath (Join-Path $buildDirectory $name) -Destination $packageDirectory -Force
}
foreach ($name in @('README.md', 'LICENSE')) {
    Copy-Item -LiteralPath (Join-Path $repository $name) -Destination $packageDirectory -Force
}
Copy-Item -LiteralPath (Join-Path $repository 'rhino-package/manifest.yml') -Destination $packageDirectory -Force
$resolvedYak = (Resolve-Path -LiteralPath $YakPath).Path
Push-Location $packageDirectory
try {
    & $resolvedYak build --platform win
    if ($LASTEXITCODE -ne 0) { throw 'Yak packaging failed.' }
} finally {
    Pop-Location
}
$package = Get-ChildItem -LiteralPath $packageDirectory -Filter '*.yak' | Select-Object -First 1
if (!$package) { throw 'Yak did not produce a package.' }
Copy-Item -LiteralPath $package.FullName -Destination $releaseDirectory -Force
$archive = Join-Path $releaseDirectory 'RhinoMagpie-panel-win-x64.zip'
if (Test-Path -LiteralPath $archive) { Remove-Item -LiteralPath $archive }
Compress-Archive -LiteralPath $package.FullName,(Join-Path $repository 'scripts/install-panel.ps1'),(Join-Path $repository 'README.md'),(Join-Path $repository 'plugin') -DestinationPath $archive
Get-ChildItem -LiteralPath $releaseDirectory -File | Where-Object { $_.Extension -in @('.yak', '.zip') } |
    Get-FileHash -Algorithm SHA256 | ForEach-Object {
        "$($_.Hash.ToLower())  $(Split-Path -Leaf $_.Path)"
    } | Set-Content -LiteralPath (Join-Path $releaseDirectory 'SHA256SUMS.txt')
