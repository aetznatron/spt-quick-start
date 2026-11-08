<#
.SYNOPSIS
    Creates a desktop shortcut for SPT-Start.exe (one click to server + game).

.PARAMETER InstallPath
    SPT install root, i.e. the folder that contains SPT_Runtime and EscapeFromTarkov.exe.

.PARAMETER Name
    Shortcut file name without extension. Default: "SPT Quick Start"

.EXAMPLE
    .\tools\install-shortcut.ps1 -InstallPath "D:\Games\SPT 4.1.6 essential"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$InstallPath,

    [string]$Name = 'SPT Quick Start'
)

$ErrorActionPreference = 'Stop'

$exe = Join-Path $InstallPath 'SPT-Start.exe'
if (-not (Test-Path $exe)) {
    throw "SPT-Start.exe not found in $InstallPath - build or copy it there first (tools\build.ps1 -InstallPath ...)."
}
if (-not (Test-Path (Join-Path $InstallPath 'SPT_Runtime'))) {
    Write-Warning "$InstallPath has no SPT_Runtime folder - is this really the SPT install root?"
}

$desktop = [Environment]::GetFolderPath('Desktop')
$path    = Join-Path $desktop ($Name + '.lnk')

$shell = New-Object -ComObject WScript.Shell
$lnk = $shell.CreateShortcut($path)
$lnk.TargetPath       = $exe
$lnk.WorkingDirectory = $InstallPath
$lnk.IconLocation     = $exe + ',0'
$lnk.Description      = 'Start the SPT server, then the client with the last used profile'
$lnk.WindowStyle      = 1
$lnk.Save()

Write-Host ("shortcut: " + $path)
