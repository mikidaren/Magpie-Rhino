param([switch]$Uninstall, [switch]$NoShortcut)
$ErrorActionPreference = 'Stop'
$installDirectory = Join-Path $env:LOCALAPPDATA 'Rhino9Magpie/Launcher'
if ($Uninstall) {
    $shortcutPath = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Rhino 9 + Magpie.lnk'
    if (Test-Path -LiteralPath $shortcutPath) { Remove-Item -LiteralPath $shortcutPath }
    Write-Output 'Shortcut removed. Disconnect Rhino 9 AI in Magpie before removing its plugin. Launcher files and session data have been kept.'
    exit 0
}
$sourceExecutable = Join-Path $PSScriptRoot 'RhinoMagpie.exe'
if (!(Test-Path -LiteralPath $sourceExecutable)) {
    throw 'Run install.ps1 from the extracted release ZIP beside RhinoMagpie.exe.'
}
New-Item -ItemType Directory -Path $installDirectory -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $env:USERPROFILE '.config/rhino9-magpie') -Force | Out-Null
Copy-Item -LiteralPath $sourceExecutable -Destination (Join-Path $installDirectory 'RhinoMagpie.exe') -Force
if ($NoShortcut) {
    Write-Output 'Launcher and detection directory installed.'
    exit 0
}
$shellObject = New-Object -ComObject WScript.Shell
$shortcutPath = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Rhino 9 + Magpie.lnk'
$shortcut = $shellObject.CreateShortcut($shortcutPath)
$shortcut.TargetPath = Join-Path $installDirectory 'RhinoMagpie.exe'
$shortcut.WorkingDirectory = $installDirectory
$shortcut.Description = 'Rhino 9 native AI panel through Magpie'
$shortcut.Save()
Write-Output 'Created desktop shortcut: Rhino 9 + Magpie. Select a model in Magpie, then launch Rhino using this shortcut.'
