# Installs t2med-fon.exe for the current Windows user and offers it as handler for tel: and callto: links.
[CmdletBinding()]
param(
    [string]$Phone,
    [string]$Username,
    [string]$CountryCode = '49',
    [string]$AreaCode,
    [switch]$Uninstall,
    [switch]$DryRun
)
$ErrorActionPreference = 'Stop'

$installDir = Join-Path $env:LOCALAPPDATA 't2med-fon'
$exe = Join-Path $installDir 't2med-fon.exe'
$configDir = Join-Path $env:APPDATA 't2med-fon'
$config = Join-Path $configDir 't2med-fon.ini'
# Same ProgId as the 1.x client, so an existing default-app choice for TEL keeps working.
$progId = 'T2medFon.Url'
$classKey = "HKCU:\Software\Classes\$progId"
$appKey = 'HKCU:\Software\T2medFon'
$registered = 'HKCU:\Software\RegisteredApplications'

function Assert-OwnRegistration {
    if (Test-Path $classKey) {
        $name = (Get-Item -Path $classKey).GetValue('')
        if ($name -and $name -ne 't2med-fon') { throw "Fremde Protokollregistrierung bleibt erhalten: $classKey" }
    }
}

if ($Uninstall) {
    Assert-OwnRegistration
    if ($DryRun) { Write-Host "Dry-run: würde $installDir, $config und die tel:-Registrierung entfernen."; exit 0 }
    foreach ($key in @($classKey, $appKey)) { if (Test-Path $key) { Remove-Item -Path $key -Recurse -Force } }
    Remove-ItemProperty -Path $registered -Name 't2med-fon' -ErrorAction SilentlyContinue
    foreach ($dir in @($installDir, $configDir)) { if (Test-Path -LiteralPath $dir) { Remove-Item -LiteralPath $dir -Recurse -Force } }
    Write-Host 't2med-fon wurde entfernt.'
    exit 0
}

$source = Join-Path $PSScriptRoot 't2med-fon.exe'
if (-not (Test-Path -LiteralPath $source)) { $source = Join-Path $PSScriptRoot 'dist\t2med-fon.exe' }
if (-not (Test-Path -LiteralPath $source)) { throw 't2med-fon.exe liegt nicht neben install.ps1. Zuerst build.ps1 ausführen.' }
if ($CountryCode -notmatch '^([1-9][0-9]{0,2})?$') { throw 'Landesvorwahl ohne +, z. B. 49.' }
# Must match the area_code check in src\Config.cs.
$areaCodePattern = '^(0[1-9][0-9]{1,4})?$'
if ($AreaCode -notmatch $areaCodePattern) { throw 'Ortsvorwahl mit führender 0, z. B. 0761.' }
Assert-OwnRegistration

$keepConfig = (Test-Path -LiteralPath $config) -and -not $Phone
if (-not $keepConfig) {
    if (-not $Phone) { $Phone = Read-Host 'Adresse des Telefons, z. B. http://192.168.1.50' }
    if ($Phone -notmatch '^https?://') { $Phone = "http://$Phone" }
    $Phone = $Phone.TrimEnd('/')
    if (-not $PSBoundParameters.ContainsKey('Username') -and -not $DryRun) { $Username = Read-Host 'Benutzer der Telefon-Weboberfläche (leer = ohne Anmeldung)' }
    if (-not $PSBoundParameters.ContainsKey('AreaCode') -and -not $DryRun) {
        $AreaCode = (Read-Host 'Ortsvorwahl für Nummern ohne Vorwahl, z. B. 0761 (leer = nicht ergänzen)').Trim()
        if ($AreaCode -notmatch $areaCodePattern) { throw 'Ortsvorwahl mit führender 0, z. B. 0761.' }
    }
    $lines = @('# t2med-fon: Telefon für tel:-Links', "phone = $Phone", "country_code = $CountryCode")
    if ($AreaCode) { $lines += "area_code = $AreaCode" }
    # Yealink phones present a device certificate without the IP address as name; it cannot be verified.
    if ($Phone -match '^https://') { $lines += @('# Selbst signiertes Gerätezertifikat des Telefons', 'tls_verify = false') }
    if ($Username) {
        $lines += "username = $Username"
        if (-not $DryRun) {
            $secure = Read-Host "Passwort für $Username" -AsSecureString
            $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
            try { $plain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr) } finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
            Add-Type -AssemblyName System.Security
            # Entropy must match Config.Entropy in src\Config.cs.
            $protected = [Security.Cryptography.ProtectedData]::Protect([Text.Encoding]::UTF8.GetBytes($plain), [Text.Encoding]::UTF8.GetBytes('t2med-fon'), 'CurrentUser')
            $lines += 'password_dpapi = ' + [Convert]::ToBase64String($protected)
        }
    }
}

$changeAreaCode = $keepConfig -and $PSBoundParameters.ContainsKey('AreaCode')
if ($DryRun) {
    $what = if ($changeAreaCode) { "in der vorhandenen Konfiguration $config die Ortsvorwahl setzen" } elseif ($keepConfig) { "die vorhandene Konfiguration $config behalten" } else { "die Konfiguration $config für $Phone schreiben" }
    Write-Host "Dry-run: würde $exe installieren, $what und t2med-fon für TEL/CALLTO in HKCU anbieten."
    exit 0
}

New-Item -ItemType Directory -Path $installDir -Force | Out-Null
Copy-Item -LiteralPath $source -Destination $exe -Force
if (-not $keepConfig) {
    New-Item -ItemType Directory -Path $configDir -Force | Out-Null
    [IO.File]::WriteAllText($config, ($lines -join "`r`n") + "`r`n", (New-Object Text.UTF8Encoding $false))
}
# -AreaCode alone updates an existing configuration instead of asking for everything again.
if ($changeAreaCode) {
    $lines = @([IO.File]::ReadAllLines($config) | Where-Object { $_ -notmatch '^\s*area_code\s*=' })
    if ($AreaCode) { $lines += "area_code = $AreaCode" }
    [IO.File]::WriteAllText($config, ($lines -join "`r`n") + "`r`n", (New-Object Text.UTF8Encoding $false))
}

New-Item -Path "$classKey\shell\open\command" -Force | Out-Null
Set-Item -Path $classKey -Value 't2med-fon'
New-ItemProperty -Path $classKey -Name 'URL Protocol' -Value '' -PropertyType String -Force | Out-Null
Set-Item -Path "$classKey\shell\open\command" -Value ('"' + $exe + '" "%1"')
New-Item -Path "$appKey\Capabilities\URLAssociations" -Force | Out-Null
New-ItemProperty -Path "$appKey\Capabilities" -Name ApplicationName -Value 't2med-fon' -PropertyType String -Force | Out-Null
New-ItemProperty -Path "$appKey\Capabilities" -Name ApplicationDescription -Value 'Wählt über das Telefon am Arbeitsplatz' -PropertyType String -Force | Out-Null
foreach ($scheme in @('tel', 'callto')) {
    New-ItemProperty -Path "$appKey\Capabilities\URLAssociations" -Name $scheme -Value $progId -PropertyType String -Force | Out-Null
}
New-Item -Path $registered -Force | Out-Null
New-ItemProperty -Path $registered -Name 't2med-fon' -Value 'Software\T2medFon\Capabilities' -PropertyType String -Force | Out-Null

# Leftovers of the 1.x client, which needed Python and a middleware server.
$oldShortcut = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\t2med-fon.lnk'
if (Test-Path -LiteralPath $oldShortcut) {
    if (([string](New-Object -ComObject WScript.Shell).CreateShortcut($oldShortcut).Arguments).Contains('t2med_fon_call.py')) { Remove-Item -LiteralPath $oldShortcut -Force }
}
foreach ($old in @((Join-Path $env:LOCALAPPDATA 'T2medFon\t2med_fon_call.py'), (Join-Path $env:APPDATA 'T2medFon\client.json'))) {
    if (Test-Path -LiteralPath $old) { Remove-Item -LiteralPath $old -Force }
}

Write-Host "Installiert: $exe"
if ($changeAreaCode) { Write-Host "Ortsvorwahl in $config gesetzt: $(if ($AreaCode) { $AreaCode } else { 'keine' })" }
elseif ($keepConfig) { Write-Host "Vorhandene Konfiguration behalten: $config (neu einrichten mit -Phone)" }
Write-Host 'Falls tel:-Links noch nicht über t2med-fon laufen: Einstellungen > Apps > Standard-Apps > "TEL" suchen > t2med-fon wählen.'
Write-Host "Testanruf: & '$exe' tel:<Nummer>"
