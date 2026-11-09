param(
    [Parameter(Mandatory=$true)][string]$PythonPath,
    [string]$UvPath = 'uv',
    [string]$OutputDirectory,
    [string]$SourceCacheDirectory,
    [switch]$DownloadSourceEvidence,
    [switch]$AllowBuildDownloads
)
$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path -LiteralPath (Split-Path -Parent $PSScriptRoot)).Path
$python = (Resolve-Path -LiteralPath $PythonPath).Path
$uv = if ([IO.Path]::IsPathFullyQualified($UvPath)) { (Resolve-Path -LiteralPath $UvPath).Path } else { (Get-Command $UvPath -CommandType Application).Source }
$information = & $python -B -c "import sys,sysconfig,json;print(json.dumps({'version':sys.version.split()[0],'platform':sysconfig.get_platform(),'base':sys.base_prefix}))"
if ($LASTEXITCODE -ne 0) { throw 'The build interpreter could not be inspected.' }
$information = $information | ConvertFrom-Json
if ($information.version -ne '3.12.13' -or $information.platform -ne 'win-amd64') { throw 'Use CPython 3.12.13 Windows x64 to build the pinned offline runtime.' }
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repository 'dist/ifc-runtime/win-x64' }
$output = [IO.Path]::GetFullPath($OutputDirectory)
$dist = [IO.Path]::GetFullPath((Join-Path $repository 'dist'))
if (!$SourceCacheDirectory) { $SourceCacheDirectory = Join-Path $dist 'ifc-source-cache' }
if (!$output.StartsWith($dist + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Runtime staging must stay inside this repository dist directory.' }
if ((Test-Path -LiteralPath $output) -and (Get-ChildItem -LiteralPath $output -Force | Select-Object -First 1)) { throw 'Runtime staging is nonempty. Choose a new output directory; the builder will not recursively delete it.' }
$dependencies = Join-Path $dist ('ifc-runtime/dependencies-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $dependencies -Force | Out-Null
$requirements = Join-Path $repository 'runtime/requirements-win-x64.txt'
$dependencyArguments = @('pip','install','--python',$python,'--target',$dependencies,'--require-hashes','--no-deps','--only-binary',':all:','--no-compile','-r',$requirements)
if (!$AllowBuildDownloads) { $dependencyArguments += '--offline' }
& $uv @dependencyArguments
if ($LASTEXITCODE -ne 0) { throw 'Pinned IFC wheel installation failed.' }
& $python -B (Join-Path $PSScriptRoot 'ifc_runtime_builder.py') stage --python-base $information.base --site-packages $dependencies --output $output
if ($LASTEXITCODE -ne 0) { throw 'Private Python staging failed.' }
$collector = Join-Path $PSScriptRoot 'collect-ifc-licenses.py'
if (!(Test-Path -LiteralPath $collector)) { throw 'The licence/provenance collector is required for a complete runtime.' }
if ($DownloadSourceEvidence) {
    & $python -B (Join-Path $PSScriptRoot 'fetch-ifc-sources.py') --manifest (Join-Path $repository 'packaging/ifc-runtime/source-artifacts.json') --cache $SourceCacheDirectory
    if ($LASTEXITCODE -ne 0) { throw 'Pinned source/licence evidence download failed.' }
}
$licenseStage = Join-Path $dist ('ifc-runtime/license-stage-'+[guid]::NewGuid().ToString('N'))
& $python -B $collector --python-base $output --site-packages (Join-Path $output 'Lib/site-packages') --output $licenseStage --source-cache $SourceCacheDirectory
if ($LASTEXITCODE -ne 0) { throw 'Runtime licence collection failed.' }
Copy-Item -LiteralPath $licenseStage -Destination (Join-Path $output 'licenses') -Recurse
Copy-Item -LiteralPath $requirements -Destination (Join-Path $output 'requirements-win-x64.txt') -Force
$parent = Split-Path -Parent $output
Copy-Item -LiteralPath (Join-Path $repository 'vendor/openbim/python/miki_ifc.py') -Destination (Join-Path $parent 'miki_ifc.py') -Force
Copy-Item -LiteralPath (Join-Path $repository 'vendor/openbim/snapshot.json') -Destination (Join-Path $parent 'snapshot.json') -Force
& (Join-Path $output 'python.exe') -I -S -B (Join-Path $PSScriptRoot 'ifc_runtime_builder.py') manifest --runtime $output
if ($LASTEXITCODE -ne 0) { throw 'Runtime manifest creation failed.' }
& (Join-Path $PSScriptRoot 'test-ifc-runtime.ps1') -RuntimePath $output
Write-Output "Complete offline IFC runtime staged at $output"
