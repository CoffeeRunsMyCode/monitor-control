param([string]$OutputDirectory = 'dist')
$ErrorActionPreference = 'Stop'
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compilerPath)) { throw 'The Windows .NET Framework C# compiler was not found.' }
$outputPath = Join-Path $PSScriptRoot $OutputDirectory
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
$sources = Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' | Select-Object -ExpandProperty FullName
$iconPath = Join-Path $PSScriptRoot 'assets\app.ico'
$iconArgs = @(); if (Test-Path -LiteralPath $iconPath) { $iconArgs = @("/win32icon:$iconPath") }
# The guides and their screenshots are built into the exe, so the Help window shows them offline (see Help.cs).
$docsPath = Join-Path $PSScriptRoot 'docs'
$resourceArgs = @('USER-GUIDE.md', 'FAQ.md', 'TROUBLESHOOTING.md') | ForEach-Object { "/resource:$(Join-Path $docsPath $_),$_" }
$resourceArgs += Get-ChildItem -LiteralPath (Join-Path $docsPath 'screenshots') -Filter '*.png' | ForEach-Object { "/resource:$($_.FullName),screenshots/$($_.Name)" }
& $compilerPath /nologo /target:winexe /platform:x64 /optimize+ /warnaserror+ /codepage:65001 "/out:$outputPath\MonitorControl.exe" "/win32manifest:$PSScriptRoot\app.manifest" @iconArgs @resourceArgs /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll $sources
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Output "Built $outputPath\MonitorControl.exe"
