param(
    [switch]$Smoke,
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.6.1f1\Editor\Unity.exe'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $projectRoot 'Artifacts'
New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null
if (-not (Test-Path -LiteralPath $UnityPath)) { throw "Unity editor not found at $UnityPath" }
$buildLog = Join-Path $artifactRoot 'build.log'
$buildArguments = @('-batchmode', '-nographics', '-quit', '-projectPath', "`"$projectRoot`"", '-executeMethod', 'VirtualHeritage.Editor.HeritageProject.BuildWindows', '-logFile', "`"$buildLog`"")
$buildProcess = Start-Process -FilePath $UnityPath -ArgumentList $buildArguments -WindowStyle Hidden -PassThru -Wait
if ($buildProcess.ExitCode -ne 0) { throw "Unity build failed ($($buildProcess.ExitCode)). See $buildLog" }
Write-Host 'Windows demo built successfully.'

if ($Smoke) {
    $playerPath = Join-Path $projectRoot 'Builds\Windows\VirtualHeritageKarnataka.exe'
    $playerLog = Join-Path $artifactRoot 'player.log'
    $smokeArguments = @('--heritage-smoke', '--artifacts', "`"$artifactRoot`"", '-screen-width', '1600', '-screen-height', '900', '-screen-fullscreen', '0', '-logFile', "`"$playerLog`"")
    $playerProcess = Start-Process -FilePath $playerPath -ArgumentList $smokeArguments -WindowStyle Hidden -PassThru -Wait
    $report = Get-Content -LiteralPath (Join-Path $artifactRoot 'smoke-results.json') -Raw | ConvertFrom-Json
    Write-Host "$($report.passed) checks passed; $($report.failures.Count) failures."
    if ($playerProcess.ExitCode -ne 0 -or $report.failures.Count -gt 0) { throw 'Smoke verification failed. Inspect Artifacts/smoke-results.json and player.log.' }
}
