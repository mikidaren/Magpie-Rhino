param([string]$YakPath)
$ErrorActionPreference = 'Stop'
if (!$YakPath) {
    $install = Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\McNeel\Rhinoceros\9.0\Install' -ErrorAction Stop
    $rhinoPath = $install.ExePath
    if (!$rhinoPath) { $rhinoPath = Join-Path $install.Path 'Rhino.exe' }
    $YakPath = Join-Path (Split-Path -Parent $rhinoPath) 'Yak.exe'
}
if (!(Test-Path -LiteralPath $YakPath)) { throw 'Rhino 9 Yak.exe was not found. Pass -YakPath with its full path.' }
$package = Get-ChildItem -LiteralPath $PSScriptRoot -Filter 'rhino-magpie-*.yak' | Select-Object -First 1
if (!$package) { throw 'Extract the full RhinoMagpie-panel-win-x64.zip before running this installer.' }
& $YakPath install $package.FullName
if ($LASTEXITCODE -ne 0) { throw 'Rhino package installation failed.' }
$configDirectory = Join-Path $env:USERPROFILE '.config/rhino9-magpie'
New-Item -ItemType Directory -Path $configDirectory -Force | Out-Null
Write-Output 'RhinoMagpie installed. Restart Rhino 9 and run Magpie to open its panel.'
Write-Output 'Connect Rhino 9 · Magpie in the Magpie Agents page. Choose a model, custom group or strategy there.'
Write-Output 'RhinoMagpie 已安装。重启 Rhino 9 后输入 Magpie。请在 Magpie 的 Agent 页连接并选择模型或自定义组。'
