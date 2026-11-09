param(
    [Parameter(Mandatory=$true)][string]$PackagePath,
    [Parameter(Mandatory=$true)][string]$InstalledDirectory
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$root = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $InstalledDirectory).Path).TrimEnd([char[]]@('\','/')) + [IO.Path]::DirectorySeparatorChar
$archive = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $PackagePath).Path)
$hasher = [Security.Cryptography.SHA256]::Create()
$count = 0
$seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
try {
    foreach ($entry in $archive.Entries) {
        $relative = $entry.FullName
        if ($relative.EndsWith('/')) { continue }
        $parts = $relative.Split('/')
        if (!$relative -or $relative.Contains('\') -or [IO.Path]::IsPathRooted($relative) -or
            @($parts | Where-Object { !$_ -or $_ -eq '.' -or $_ -eq '..' -or $_.Contains(':') }).Count -gt 0 -or !$seen.Add($relative)) {
            throw 'The Yak archive contains an unsafe or duplicate file path.'
        }
        $target = [IO.Path]::GetFullPath((Join-Path $root ($relative.Replace('/', [IO.Path]::DirectorySeparatorChar))))
        if (!$target.StartsWith($root, [StringComparison]::OrdinalIgnoreCase) -or !(Test-Path -LiteralPath $target -PathType Leaf)) {
            throw ('An installed package file is missing: ' + $relative)
        }
        $item = Get-Item -LiteralPath $target
        if ($item.Length -ne $entry.Length) { throw ('An installed package file has the wrong size: ' + $relative) }
        $packaged = $entry.Open()
        try { $expected = [Convert]::ToBase64String($hasher.ComputeHash($packaged)) }
        finally { $packaged.Dispose() }
        $installed = [IO.File]::OpenRead($target)
        try { $actual = [Convert]::ToBase64String($hasher.ComputeHash($installed)) }
        finally { $installed.Dispose() }
        if ($actual -ne $expected) { throw ('An installed package file has the wrong hash: ' + $relative) }
        $count++
    }
    if ($count -eq 0) { throw 'The Yak archive contains no files.' }
} finally {
    $hasher.Dispose()
    $archive.Dispose()
}
Write-Output "Verified $count installed package files against the Yak archive."
