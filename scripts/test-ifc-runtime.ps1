param(
    [Parameter(Mandatory=$true)][string]$RuntimePath,
    [string]$ProjectPath,
    [string]$ReportPath
)
$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
$runtime = (Resolve-Path -LiteralPath $RuntimePath).Path
$python = Join-Path $runtime 'python.exe'
$worker = Join-Path (Split-Path -Parent $runtime) 'miki_ifc.py'
if (!$ProjectPath) { $ProjectPath = Join-Path $repository 'vendor/openbim/examples/two-storey.json' }
$project = (Resolve-Path -LiteralPath $ProjectPath).Path
if (!$ReportPath) { $ReportPath = Join-Path $runtime 'bundle-validation.json' }
if (Test-Path -LiteralPath $ReportPath) { throw 'Use a new smoke report path; existing reports are not overwritten.' }
& $python -I -S -B (Join-Path $PSScriptRoot 'ifc_runtime_builder.py') verify --runtime $runtime
if ($LASTEXITCODE -ne 0) { throw 'The private runtime manifest is incomplete or corrupted.' }
$fresh = Join-Path ([IO.Path]::GetTempPath()) ('Miki IFC Offline '+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fresh -Force | Out-Null
$baseline = Get-Content -LiteralPath $project -Raw | ConvertFrom-Json -AsHashtable
$window = $baseline.types | Where-Object id -eq 'WIN-A'
$upper = $baseline.levels | Where-Object id -eq 'L2'
if (!$window -or !$upper -or $baseline.id -ne 'TWO-STOREY') { throw 'The smoke test requires the canonical generic two-storey fixture.' }
$stages = [System.Collections.Generic.List[object]]::new()
foreach ($name in @('before','shared-type','after')) {
    if ($name -eq 'shared-type') { $window.width = 1.5; $baseline.revision = 1 }
    if ($name -eq 'after') { $upper.height = 3.5; $baseline.revision = 2 }
    $source = Join-Path $fresh ($name+'.json')
    $output = Join-Path $fresh ($name+'.ifc')
    $report = Join-Path $fresh ($name+'-probe.json')
    $baseline | ConvertTo-Json -Depth 40 | Set-Content -LiteralPath $source -Encoding utf8NoBOM
    $start = [Diagnostics.ProcessStartInfo]::new($python)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.WorkingDirectory = $fresh
    $start.Environment.Clear()
    foreach ($key in @('SystemRoot','WINDIR')) { $start.Environment[$key] = [Environment]::GetEnvironmentVariable($key) }
    foreach ($key in @('TEMP','TMP','USERPROFILE','HOME','APPDATA','LOCALAPPDATA')) { $start.Environment[$key] = $fresh }
    $start.Environment['PATH'] = Join-Path $fresh 'no executables'
    $start.Environment['PYTHONHOME'] = 'Z:/unavailable-system-python'
    $start.Environment['PYTHONPATH'] = 'Z:/forbidden-developer-packages'
    foreach ($argument in @('-I','-S','-B',(Join-Path $PSScriptRoot 'ifc_runtime_probe.py'),'--runtime',$runtime,'--worker',$worker,
            '--project',$source,'--output',$output,'--report',$report)) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($start)
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    if (!$process.WaitForExit(180000)) { $process.Kill($true); throw 'The isolated runtime smoke test timed out.' }
    if ($process.ExitCode -ne 0) { throw ('Offline runtime failed: '+$stderr.Result+' '+$stdout.Result) }
    $details = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json -AsHashtable
    if (!$details.verified -or !$details.interpreter.pipAbsent -or $details.networkEvents.Count -ne 0) { throw 'Offline isolation or IFC validation did not pass.' }
    $stages.Add(@{name=$name; verified=$true; revision=$details.ifc.revision; counts=$details.ifc.counts;
        python=$details.interpreter; versions=$details.versions; nativeDependencies=$details.nativeDependencies})
    Write-Output "PASS isolated offline IFC stage: $name"
}
$manifest = Get-Content -LiteralPath (Join-Path $runtime 'manifest.json') -Raw | ConvertFrom-Json
$summary = @{schemaVersion=1; verified=$true; platform='win-x64'; manifestSha256=(Get-FileHash -LiteralPath (Join-Path $runtime 'manifest.json')).Hash.ToLower();
    payloadFiles=$manifest.files.Count; payloadBytes=($manifest.files | Measure-Object size -Sum).Sum;
    pathRemoved=$true; pythonEnvironmentPoisoned=$true; freshUserProfile=$true; pipAbsent=$true;
    networkGuard='Python audit hooks forbid socket/network operations; local hostname lookup allowed';
    freshWindowsVm=$false; stages=$stages.ToArray()}
$summary | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath $ReportPath -Encoding utf8NoBOM
Write-Output 'PASS full private-runtime smoke test; this is isolated execution, not a fresh Windows VM.'
