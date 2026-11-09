param([Parameter(Mandatory=$true)][string]$YakPath, [string]$IfcRuntimePath, [switch]$LocalOnly, [switch]$NoRestore)
$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
$manifest = Join-Path $repository 'rhino-package/manifest.yml'
$version = ((Select-String -LiteralPath $manifest -Pattern '^version:\s*(.+)$').Matches.Groups[1].Value).Trim()
if (!$version) { throw 'The Rhino package manifest has no version.' }
$packageDirectory = Join-Path $repository ("dist/panel/$version/"+[guid]::NewGuid().ToString('N'))
$releaseDirectory = Join-Path $repository 'dist'
$buildDirectory = Join-Path $repository 'dist/compiled'
if (!$IfcRuntimePath) { $IfcRuntimePath = Join-Path $repository 'dist/ifc-runtime/win-x64' }
$runtime = (Resolve-Path -LiteralPath $IfcRuntimePath).Path
$runtimeManifest = Join-Path $runtime 'manifest.json'
$runtimeValidation = Join-Path $runtime 'bundle-validation.json'
if (!(Test-Path -LiteralPath $runtimeManifest) -or !(Test-Path -LiteralPath $runtimeValidation)) { throw 'Build and verify the complete offline IFC runtime before packaging.' }
$validated = Get-Content -LiteralPath $runtimeValidation -Raw | ConvertFrom-Json
if (!$validated.verified -or $validated.manifestSha256 -ne (Get-FileHash -LiteralPath $runtimeManifest).Hash.ToLower()) { throw 'The offline runtime proof does not match its manifest.' }
$licenseManifest = Join-Path $runtime 'licenses/components.json'
if (!(Test-Path -LiteralPath $licenseManifest)) { throw 'Runtime third-party licence material is missing.' }
$licenseReview = Get-Content -LiteralPath $licenseManifest -Raw | ConvertFrom-Json
$sourceBefore = & (Join-Path $PSScriptRoot 'get-build-source.ps1') -Repository $repository | ConvertFrom-Json
$publicReleaseAllowed = $licenseReview.publicReleaseGate.status -eq 'approved' -and $sourceBefore.known -and $sourceBefore.clean
if (!$publicReleaseAllowed -and !$LocalOnly) { throw 'Public binaries require an approved dependency review and a clean, identified source commit. Use -LocalOnly for owner-local validation.' }
& (Join-Path $runtime 'python.exe') -I -S -B (Join-Path $PSScriptRoot 'ifc_runtime_builder.py') verify --runtime $runtime
if ($LASTEXITCODE -ne 0) { throw 'Offline IFC runtime integrity failed.' }
$buildArguments = @('build', (Join-Path $repository 'src/RhinoMagpie.Plugin'), '-c', 'Release', '--nologo', '-o', $buildDirectory,
    '-t:Rebuild', '-p:DebugType=None', '-p:DebugSymbols=false')
if ($NoRestore) { $buildArguments += '--no-restore' }
& dotnet @buildArguments
if ($LASTEXITCODE -ne 0) { throw 'Rhino plugin build failed.' }
$managedBinaries = @('RhinoMagpie.Plugin.rhp', 'RhinoMagpie.Agent.dll', 'Miki.OpenBim.dll', 'Miki.OpenBim.Rhino.dll') |
    ForEach-Object { Join-Path $buildDirectory $_ }
& (Join-Path $PSScriptRoot 'check-managed-debug-paths.ps1') -Binaries $managedBinaries
New-Item -ItemType Directory -Path $packageDirectory -Force | Out-Null
foreach ($name in @('RhinoMagpie.Plugin.rhp', 'RhinoMagpie.Agent.dll', 'Miki.OpenBim.dll', 'Miki.OpenBim.Rhino.dll', 'RhinoMagpie.Plugin.deps.json', 'RhinoMagpie.Plugin.runtimeconfig.json')) {
    Copy-Item -LiteralPath (Join-Path $buildDirectory $name) -Destination $packageDirectory -Force
}
Copy-Item -LiteralPath (Join-Path $buildDirectory 'openbim') -Destination $packageDirectory -Recurse -Force
$runtimeDestination = Join-Path $packageDirectory 'openbim/runtime'
New-Item -ItemType Directory -Path $runtimeDestination -Force | Out-Null
$runtimeFiles = @((Get-Content -LiteralPath $runtimeManifest -Raw | ConvertFrom-Json).files.path) + @('manifest.json','bundle-validation.json')
foreach ($relative in $runtimeFiles) {
    $target = [IO.Path]::GetFullPath((Join-Path $runtimeDestination $relative))
    if (!$target.StartsWith([IO.Path]::GetFullPath($runtimeDestination)+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Runtime payload escaped package staging.' }
    New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $runtime $relative) -Destination $target -Force
}
& (Join-Path $runtimeDestination 'python.exe') -I -S -B (Join-Path $PSScriptRoot 'ifc_runtime_builder.py') verify --runtime $runtimeDestination
if ($LASTEXITCODE -ne 0) { throw 'Copied offline runtime failed its manifest check.' }
foreach ($name in @('README.md', 'LICENSE')) {
    Copy-Item -LiteralPath (Join-Path $repository $name) -Destination $packageDirectory -Force
}
Copy-Item -LiteralPath $manifest -Destination $packageDirectory -Force
$resolvedYak = (Resolve-Path -LiteralPath $YakPath).Path
Push-Location $packageDirectory
try {
    & $resolvedYak build --platform win
    if ($LASTEXITCODE -ne 0) { throw 'Yak packaging failed.' }
} finally {
    Pop-Location
}
$package = Get-ChildItem -LiteralPath $packageDirectory -Filter "rhino-magpie-$version-*.yak" | Select-Object -First 1
if (!$package) { throw 'Yak did not produce a package.' }
Copy-Item -LiteralPath $package.FullName -Destination $releaseDirectory -Force
Copy-Item -LiteralPath (Join-Path $repository 'docs/native-ai-menu.png') -Destination (Join-Path $releaseDirectory 'Magpie-Rhino-native-agents.png') -Force
$archive = Join-Path $releaseDirectory 'Magpie-Rhino-win-x64.zip'
if (Test-Path -LiteralPath $archive) { Remove-Item -LiteralPath $archive }
Compress-Archive -LiteralPath $package.FullName,(Join-Path $repository 'scripts/install-panel.ps1'),(Join-Path $repository 'scripts/verify-yak-install.ps1'),(Join-Path $repository 'README.md'),(Join-Path $repository 'Magpie-Rhino') -DestinationPath $archive -CompressionLevel NoCompression
$sourceAfter = & (Join-Path $PSScriptRoot 'get-build-source.ps1') -Repository $repository | ConvertFrom-Json
$publicReleaseAllowed = $publicReleaseAllowed -and $sourceAfter.known -and $sourceAfter.clean -and
    $sourceAfter.commit -eq $sourceBefore.commit -and $sourceAfter.tree -eq $sourceBefore.tree
@{version=$version; staging=$packageDirectory; yak=(Join-Path $releaseDirectory $package.Name); runtimeManifestSha256=$validated.manifestSha256;
    localOnly=[bool]$LocalOnly; publicReleaseAllowed=$publicReleaseAllowed; sourceBefore=$sourceBefore; sourceAfter=$sourceAfter} |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $releaseDirectory 'last-package.json') -Encoding utf8NoBOM
Get-FileHash -LiteralPath (Join-Path $releaseDirectory $package.Name),$archive,(Join-Path $releaseDirectory 'Magpie-Rhino-native-agents.png') -Algorithm SHA256 |
    ForEach-Object {
        "$($_.Hash.ToLower())  $(Split-Path -Leaf $_.Path)"
    } | Set-Content -LiteralPath (Join-Path $releaseDirectory 'SHA256SUMS.txt')
