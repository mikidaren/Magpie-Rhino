param([string]$YakPath)
$ErrorActionPreference = 'Stop'
if (!$YakPath) {
    $install = Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\McNeel\Rhinoceros\9.0\Install' -ErrorAction Stop
    $rhinoPath = $install.ExePath
    if (!$rhinoPath) { $rhinoPath = Join-Path $install.Path 'Rhino.exe' }
    $YakPath = Join-Path (Split-Path -Parent $rhinoPath) 'Yak.exe'
}
if (!(Test-Path -LiteralPath $YakPath)) { throw 'Rhino 9 Yak.exe was not found. Pass -YakPath with its full path.' }
$pluginManifest = Join-Path $PSScriptRoot 'Magpie-Rhino/package.json'
if (!(Test-Path -LiteralPath $pluginManifest)) { throw 'Extract the full Magpie-Rhino-win-x64.zip before running this installer.' }
$expectedVersion = (Get-Content -LiteralPath $pluginManifest -Raw | ConvertFrom-Json).version
if (!$expectedVersion) { throw 'The companion plugin version is missing.' }
$package = Get-ChildItem -LiteralPath $PSScriptRoot -Filter "rhino-magpie-$expectedVersion-*.yak" | Select-Object -First 1
if (!$package) { throw 'Extract the full Magpie-Rhino-win-x64.zip before running this installer.' }
& $YakPath install $package.FullName
if ($LASTEXITCODE -ne 0) { throw 'Rhino package installation failed.' }
$configDirectory = Join-Path $env:USERPROFILE '.config/rhino9-magpie'
New-Item -ItemType Directory -Path $configDirectory -Force | Out-Null
Write-Output 'Magpie-Rhino installed. Restart Rhino 9, open its AI panel and choose Magpie. No command required.'
Write-Output 'Connect Magpie-Rhino in the Magpie Agents page. Choose a model, custom group or strategy there.'
Write-Output 'Magpie-Rhino 已安装。重启 Rhino 9，打开 AI 面板选择 Magpie，无需输入命令。'
