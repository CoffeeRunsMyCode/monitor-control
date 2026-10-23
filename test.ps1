param([string]$OutputDirectory = 'dist')
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1') -OutputDirectory $OutputDirectory
$reportPath = Join-Path $PSScriptRoot "$OutputDirectory\test-results.txt"
$process = Start-Process -FilePath (Join-Path $PSScriptRoot "$OutputDirectory\MonitorControl.exe") -ArgumentList @('--self-test', ('"' + $reportPath + '"')) -WindowStyle Hidden -Wait -PassThru
Get-Content -LiteralPath $reportPath
if ($process.ExitCode -ne 0) { throw 'Tests failed.' }
