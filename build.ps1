# Builds dist\t2med-fon.exe with the C# compiler that ships with every Windows (.NET Framework 4.x).
[CmdletBinding()]
param([switch]$Test)
$ErrorActionPreference = 'Stop'

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path -LiteralPath $csc)) { throw 'csc.exe aus .NET Framework 4 wurde nicht gefunden.' }

$dist = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Path $dist -Force | Out-Null
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
$common = @('/nologo', '/codepage:65001', '/optimize+', '/warnaserror+', '/r:System.Security.dll', '/r:System.Windows.Forms.dll')
$exe = Join-Path $dist 't2med-fon.exe'

& $csc @common '/target:winexe' "/out:$exe" @sources
if ($LASTEXITCODE -ne 0) { throw 'Build fehlgeschlagen.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'install.ps1') -Destination $dist -Force
Write-Host "Gebaut: $exe"

if ($Test) {
    foreach ($script in @('build.ps1', 'install.ps1')) {
        $errors = $null
        [System.Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot $script), [ref]$null, [ref]$errors) | Out-Null
        if ($errors) { throw "Syntaxfehler in ${script}: $($errors[0])" }
    }
    $tests = Join-Path $dist 'tests.exe'
    & $csc @common '/target:exe' '/main:T2medFon.Tests' "/out:$tests" @sources (Join-Path $PSScriptRoot 'tests\Tests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Build der Tests fehlgeschlagen.' }
    & $tests $exe
    if ($LASTEXITCODE -ne 0) { throw 'Tests fehlgeschlagen.' }
    & (Join-Path $PSScriptRoot 'install.ps1') -DryRun -Phone 'http://192.168.1.50'
    if (-not $?) { throw 'Dry-run der Installation fehlgeschlagen.' }
}
