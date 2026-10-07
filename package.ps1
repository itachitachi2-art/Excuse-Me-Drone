param([string]$GameDir = "")
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
& (Join-Path $here 'build.ps1') -GameDir $GameDir
$dll = Join-Path $here 'ExcuseMeDrone.dll'
if (-not (Test-Path $dll)) { throw 'Build did not produce ExcuseMeDrone.dll.' }
$stage = Join-Path ([System.IO.Path]::GetTempPath()) ('ExcuseMeDrone-release-' + [Guid]::NewGuid().ToString('N'))
$modFolder = Join-Path $stage 'ExcuseMeDrone'
$zip = Join-Path $here 'ExcuseMeDrone-1.0.4.zip'
try {
    New-Item -ItemType Directory -Path (Join-Path $modFolder 'Config') -Force | Out-Null
    Copy-Item $dll $modFolder
    Copy-Item (Join-Path $here 'ModInfo.xml') $modFolder
    Copy-Item (Join-Path $here 'Config\ExcuseMeDrone.cfg') (Join-Path $modFolder 'Config')
    Copy-Item (Join-Path $here 'INSTALL.txt') $modFolder
    Compress-Archive -Path $modFolder -DestinationPath $zip -Force
    Write-Host "[ExcuseMeDrone] CANDIDATE PACKAGE READY: $zip"
} finally {
    if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
}
