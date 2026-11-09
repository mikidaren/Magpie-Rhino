param([Parameter(Mandatory=$true)][string]$WarehousePath)
$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path -LiteralPath (Split-Path -Parent $PSScriptRoot)).Path
$warehouse = (Resolve-Path -LiteralPath $WarehousePath).Path
$destination = [IO.Path]::GetFullPath((Join-Path $repository 'vendor/openbim'))
if (!$destination.StartsWith($repository + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The vendor destination is outside the product repository.'
}
$sources = @('src/Miki.OpenBim', 'src/Miki.OpenBim.Rhino', 'python', 'examples')
$hashes = [System.Collections.Generic.List[object]]::new()
foreach ($source in $sources) {
    $sourceDirectory = Join-Path $warehouse $source
    if (!(Test-Path -LiteralPath $sourceDirectory)) { throw "Missing source directory: $source" }
    foreach ($file in Get-ChildItem -LiteralPath $sourceDirectory -File -Recurse) {
        $relative = [IO.Path]::GetRelativePath($warehouse, $file.FullName)
        if ($relative -match '[\\/](bin|obj|__pycache__)[\\/]') { continue }
        if ($file.Extension -notin @('.cs', '.csproj', '.py', '.json') -and $file.Name -ne 'requirements.txt') { continue }
        $target = [IO.Path]::GetFullPath((Join-Path $destination $relative))
        if (!$target.StartsWith($destination + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'A source path escaped the vendor snapshot.'
        }
        New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $target -Force
        $hashes.Add(@{path=$relative.Replace('\','/'); sha256=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLower()})
    }
}
$sourceCommit = 'local-unpublished'
if (Test-Path -LiteralPath (Join-Path $warehouse '.git')) {
    $candidate = git -C $warehouse rev-parse -q --verify HEAD
    if ($LASTEXITCODE -eq 0 -and $candidate -match '^[0-9a-f]{40}$') { $sourceCommit = $candidate }
}
Copy-Item -LiteralPath (Join-Path $warehouse 'LICENSE') -Destination (Join-Path $destination 'LICENSE') -Force
@{repository='https://github.com/mikidaren/Miki-GrasshopperWarehouse'; sourceCommit=$sourceCommit; contract=1; files=$hashes.ToArray()} |
    ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $destination 'snapshot.json') -Encoding utf8NoBOM
Write-Output "Pinned $($hashes.Count) source files; no runtime download is required."
