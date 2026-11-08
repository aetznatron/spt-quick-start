<#
.SYNOPSIS
    Compiles SPT-Start.exe from src\SPT-Start.cs and embeds icon\spt-quickstart.ico.

.DESCRIPTION
    Uses the C# compiler that ships with the .NET Framework
    (%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe) - no SDK required.

.PARAMETER OutputPath
    Where to write the exe. Default: <repo>\dist\SPT-Start.exe

.PARAMETER InstallPath
    Optional SPT install root (the folder containing SPT_Runtime). When given, the
    built exe is copied there as well.

.EXAMPLE
    .\tools\build.ps1

.EXAMPLE
    .\tools\build.ps1 -InstallPath "D:\Games\SPT 4.1.6 essential"
#>
[CmdletBinding()]
param(
    [string]$OutputPath,
    [string]$InstallPath
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$source   = Join-Path $repoRoot 'src\SPT-Start.cs'
$icon     = Join-Path $repoRoot 'icon\spt-quickstart.ico'

if (-not $OutputPath) { $OutputPath = Join-Path $repoRoot 'dist\SPT-Start.exe' }
$outDir = Split-Path -Parent $OutputPath
if ($outDir -and -not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }

$cscCandidates = @(
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'),
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe')
)
$csc = $cscCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $csc) { throw 'csc.exe not found - .NET Framework 4.x is required.' }

foreach ($required in @($source, $icon)) {
    if (-not (Test-Path $required)) { throw "missing input: $required" }
}

& $csc /nologo /target:exe /platform:anycpu /optimize+ /win32icon:$icon /out:$OutputPath $source
if ($LASTEXITCODE -ne 0) { throw "csc failed with exit code $LASTEXITCODE" }

Write-Host ("built   : " + (Resolve-Path $OutputPath))

if ($InstallPath) {
    $target = Join-Path $InstallPath 'SPT-Start.exe'
    Copy-Item -LiteralPath $OutputPath -Destination $target -Force
    Write-Host ("deployed: " + $target)
}
