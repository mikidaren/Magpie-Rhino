param([Parameter(Mandatory=$true)][string]$PythonPath)
$ErrorActionPreference = 'Stop'
$python = (Resolve-Path -LiteralPath $PythonPath).Path
& $python -c "import ifcopenshell; assert ifcopenshell.version == '0.9.0.post1', 'Expected ifcopenshell 0.9.0.post1'; import ifcopenshell.geom; print('IFC runtime ready')"
if ($LASTEXITCODE -ne 0) { throw 'Use a dedicated Python 3.12 environment with ifcopenshell==0.9.0.post1. This script does not install packages.' }
$directory = Join-Path $env:USERPROFILE '.config/rhino9-magpie'
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$path = Join-Path $directory 'openbim-runtime.json'
@{python=$python} | ConvertTo-Json | Set-Content -LiteralPath $path -Encoding utf8NoBOM
Write-Output 'Configured the optional IFC worker. Magpie routing settings were not changed.'
