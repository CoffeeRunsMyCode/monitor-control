param([string]$OutputDirectory = 'dist')
$ErrorActionPreference = 'Stop'
$outputPath = Join-Path $PSScriptRoot $OutputDirectory
$executablePath = Join-Path $outputPath 'MonitorControl.exe'
if (-not (Test-Path -LiteralPath $executablePath)) { throw 'Build and verify the app before packaging.' }
$downloadPath = Join-Path $PSScriptRoot 'downloads'
New-Item -ItemType Directory -Path $downloadPath -Force | Out-Null
# The guides are built into the exe (see build.ps1), so the zip needs no docs folder.
$docsPath = Join-Path $outputPath 'docs'
if (Test-Path -LiteralPath $docsPath) { Remove-Item -LiteralPath $docsPath -Recurse -Force }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination (Join-Path $outputPath 'README.md') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LICENSE') -Destination (Join-Path $outputPath 'LICENSE') -Force
$zipPath = Join-Path $downloadPath 'MonitorControl-portable.zip'
Compress-Archive -LiteralPath $executablePath,(Join-Path $outputPath 'README.md'),(Join-Path $outputPath 'LICENSE') -DestinationPath $zipPath -Force
# The exe is not code-signed, so the release page lists this checksum: users compare it to prove their download is the original.
$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
Set-Content -LiteralPath "$zipPath.sha256" -Value "$hash  MonitorControl-portable.zip" -Encoding ascii
Write-Output "Packaged $zipPath"
Write-Output "SHA-256: $hash  (paste this into the release notes)"
