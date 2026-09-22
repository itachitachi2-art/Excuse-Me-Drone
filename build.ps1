param(
    [string]$GameDir = "",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$out = Join-Path $here "ExcuseMeDrone.dll"
$sources = Get-ChildItem -Path (Join-Path $here "Scripts") -Filter "*.cs" | ForEach-Object { $_.FullName }

function Add-Candidate([System.Collections.Generic.List[string]]$list, [string]$path) {
    if ([string]::IsNullOrWhiteSpace($path)) { return }
    try { $full = [System.IO.Path]::GetFullPath($path) } catch { return }
    if (-not $list.Contains($full)) { $list.Add($full) }
}

function Find-7DTDGameDir([string]$explicitPath) {
    $candidates = New-Object 'System.Collections.Generic.List[string]'

    if (-not [string]::IsNullOrWhiteSpace($explicitPath)) {
        Add-Candidate $candidates $explicitPath
    }

    if (${env:ProgramFiles(x86)}) {
        Add-Candidate $candidates (Join-Path ${env:ProgramFiles(x86)} 'Steam\steamapps\common\7 Days To Die')
    }
    if ($env:ProgramFiles) {
        Add-Candidate $candidates (Join-Path $env:ProgramFiles 'Steam\steamapps\common\7 Days To Die')
    }

    $steamRoots = New-Object 'System.Collections.Generic.List[string]'
    foreach ($regPath in @(
        'HKCU:\Software\Valve\Steam',
        'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam',
        'HKLM:\SOFTWARE\Valve\Steam'
    )) {
        try {
            $item = Get-ItemProperty -Path $regPath -ErrorAction Stop
            if ($item.SteamPath) { Add-Candidate $steamRoots $item.SteamPath }
            if ($item.InstallPath) { Add-Candidate $steamRoots $item.InstallPath }
        } catch { }
    }

    foreach ($steamRoot in @($steamRoots)) {
        Add-Candidate $candidates (Join-Path $steamRoot 'steamapps\common\7 Days To Die')

        $vdf = Join-Path $steamRoot 'steamapps\libraryfolders.vdf'
        if (Test-Path $vdf) {
            foreach ($line in Get-Content $vdf -ErrorAction SilentlyContinue) {
                if ($line -match '"path"\s+"([^"]+)"') {
                    $library = $matches[1] -replace '\\\\', '\'
                    Add-Candidate $candidates (Join-Path $library 'steamapps\common\7 Days To Die')
                }
            }
        }
    }

    foreach ($candidate in @($candidates)) {
        $managed = Join-Path $candidate '7DaysToDie_Data\Managed'
        if ((Test-Path (Join-Path $managed 'Assembly-CSharp.dll')) -and
            (Test-Path (Join-Path $managed 'mscorlib.dll')) -and
            (Test-Path (Join-Path $managed 'netstandard.dll'))) {
            return $candidate
        }
    }

    return $null
}

$resolvedGameDir = Find-7DTDGameDir $GameDir
if (-not $resolvedGameDir) {
    throw @"
7 Days to Die のインストール先を自動検出できませんでした。
次の形式でゲームフォルダを指定してください。

  .\build.cmd "D:\SteamLibrary\steamapps\common\7 Days To Die"

または:
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -GameDir "D:\SteamLibrary\steamapps\common\7 Days To Die"
"@
}

$managed = Join-Path $resolvedGameDir '7DaysToDie_Data\Managed'
Write-Host "[ExcuseMeDrone] 7DTD: $resolvedGameDir"
Write-Host "[ExcuseMeDrone] Managed: $managed"

$required = @(
    'mscorlib.dll',
    'System.dll',
    'System.Core.dll',
    'netstandard.dll',
    'Assembly-CSharp.dll',
    'UnityEngine.dll',
    'UnityEngine.CoreModule.dll',
    'UnityEngine.InputLegacyModule.dll',
    'UnityEngine.PhysicsModule.dll'
)
foreach ($name in $required) {
    $p = Join-Path $managed $name
    if (-not (Test-Path $p)) { throw "Required 7DTD assembly not found: $p" }
}

# Use the exact framework assemblies shipped with the running game. 7DTD/Unity's
# mscorlib contains types (e.g. ReadOnlySpan<T>) that the Windows .NET Framework
# mscorlib does not, so /nostdlib+ is intentional here.
$referencePaths = @(
    (Join-Path $managed 'mscorlib.dll'),
    (Join-Path $managed 'System.dll'),
    (Join-Path $managed 'System.Core.dll'),
    (Join-Path $managed 'netstandard.dll'),
    (Join-Path $managed 'Assembly-CSharp.dll'),
    (Join-Path $managed 'UnityEngine.dll'),
    (Join-Path $managed 'UnityEngine.CoreModule.dll'),
    (Join-Path $managed 'UnityEngine.InputLegacyModule.dll'),
    (Join-Path $managed 'UnityEngine.PhysicsModule.dll')
)

# Include Unity's normal framework references when present. They are harmless if
# our current source does not directly use them, and avoid transitive metadata
# resolution surprises between minor Unity builds.
foreach ($optional in @(
    'System.Runtime.Serialization.dll',
    'System.Xml.dll',
    'System.Xml.Linq.dll'
)) {
    $p = Join-Path $managed $optional
    if (Test-Path $p) { $referencePaths += $p }
}

$gameHarmony = Join-Path $managed '0Harmony.dll'
$localHarmony = Join-Path (Join-Path $here 'References') '0Harmony.dll'
if (Test-Path $gameHarmony) {
    $referencePaths += $gameHarmony
} elseif (Test-Path $localHarmony) {
    $referencePaths += $localHarmony
} else {
    throw "0Harmony.dll was not found in the game Managed folder or local References folder."
}

$cscCandidates = @(
    (Join-Path $env:WINDIR 'Microsoft.NET\\Framework64\\v4.0.30319\\csc.exe'),
    (Join-Path $env:WINDIR 'Microsoft.NET\\Framework\\v4.0.30319\\csc.exe')
)
$csc = $cscCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $csc) {
    throw "Windows C# compiler (csc.exe) was not found."
}

if (Test-Path $out) { Remove-Item $out -Force }

Write-Host "[ExcuseMeDrone] Building with Windows compiler + 7DTD runtime references."
Write-Host "[ExcuseMeDrone] Compiler: $csc"

# Use a response file instead of passing quoted paths through PowerShell's native
# argument parser. This avoids paths containing spaces being split or quotes being
# preserved as literal filename characters on Windows PowerShell 5.1.
$rsp = Join-Path $here 'ExcuseMeDrone.csc.rsp'
$rspLines = New-Object 'System.Collections.Generic.List[string]'
$rspLines.Add('/nologo')
$rspLines.Add('/nostdlib+')
$rspLines.Add('/target:library')
$rspLines.Add('/optimize+')
$rspLines.Add('/debug-')
$rspLines.Add('/out:"' + $out + '"')

foreach ($ref in $referencePaths) {
    $rspLines.Add('/reference:"' + $ref + '"')
}
foreach ($src in $sources) {
    $rspLines.Add('"' + $src + '"')
}

# Framework csc accepts UTF-8 response files. Avoid UTF-16, which older csc builds
# may misread when invoked with @response-file syntax.
[System.IO.File]::WriteAllLines($rsp, $rspLines.ToArray(), (New-Object System.Text.UTF8Encoding($false)))

try {
    # /noconfig MUST be passed on the csc.exe command line. If it is placed
    # inside a response file, the .NET Framework compiler ignores it and
    # silently loads its own csc.rsp (System.dll/System.Core.dll, etc.),
    # which collides with the Unity/7DTD copies referenced below.
    & $csc '/noconfig' ('@' + $rsp)
    $compileExitCode = $LASTEXITCODE
}
finally {
    if (Test-Path $rsp) { Remove-Item $rsp -Force -ErrorAction SilentlyContinue }
}

if ($compileExitCode -ne 0 -or -not (Test-Path $out)) {
    throw "Compilation failed ($compileExitCode)."
}

Write-Host ""
Write-Host "[ExcuseMeDrone] SUCCESS"
Write-Host "[ExcuseMeDrone] Ready: $out"
Write-Host "[ExcuseMeDrone] Copy the ExcuseMeDrone folder to 7 Days To Die\Mods after confirming ExcuseMeDrone.dll exists."
