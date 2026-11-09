param([Parameter(Mandatory=$true)][string]$Repository)
$ErrorActionPreference = 'Stop'
$result = [ordered]@{ known=$false; clean=$false; commit=$null; tree=$null }
try {
    $root = (Resolve-Path -LiteralPath $Repository).Path.TrimEnd([char[]]@('\','/'))
    if (Get-Command git -CommandType Application -ErrorAction SilentlyContinue) {
        $top = & git -C $root rev-parse --show-toplevel 2>$null
        if ($LASTEXITCODE -eq 0 -and [IO.Path]::GetFullPath($top).TrimEnd([char[]]@('\','/')).Equals($root, [StringComparison]::OrdinalIgnoreCase)) {
            $commit = & git -C $root rev-parse --verify HEAD 2>$null
            if ($LASTEXITCODE -ne 0) { throw 'Git commit unavailable.' }
            $tree = & git -C $root rev-parse --verify 'HEAD^{tree}' 2>$null
            if ($LASTEXITCODE -ne 0) { throw 'Git tree unavailable.' }
            $changes = @(& git -C $root status --porcelain=v1 --untracked-files=all 2>$null)
            if ($LASTEXITCODE -ne 0) { throw 'Git status unavailable.' }
            if ($commit -notmatch '^[a-f0-9]{40}$' -or $tree -notmatch '^[a-f0-9]{40}$') { throw 'Invalid Git source identity.' }
            $result.known = $true
            $result.clean = $changes.Count -eq 0
            $result.commit = $commit
            $result.tree = $tree
        }
    }
} catch {
    $result = [ordered]@{ known=$false; clean=$false; commit=$null; tree=$null }
}
$result | ConvertTo-Json -Compress
