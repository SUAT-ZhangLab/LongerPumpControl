param([switch]$FromApp, [string]$ResultPath, [switch]$VerifyOnly)
$ErrorActionPreference = 'Stop'
$installMessages = New-Object 'System.Collections.Generic.List[string]'
$installExitCode = 0
try {
$driverRoot = Join-Path $PSScriptRoot 'drivers'
if (-not [Environment]::Is64BitOperatingSystem) { throw 'This driver backup is for Windows x64 only.' }
foreach ($driverName in @('ftdibus', 'ftdiport')) {
    $infPath = Join-Path $driverRoot "$driverName\$driverName.inf"
    $catPath = Join-Path $driverRoot "$driverName\$driverName.cat"
    if (-not (Test-Path -LiteralPath $infPath)) { throw "Missing driver: $infPath" }
    $signature = Get-AuthenticodeSignature -LiteralPath $catPath
    if ($signature.Status -ne 'Valid') { throw "Driver signature cannot be verified: $catPath ($($signature.Status)). No driver has been installed." }
}
if ($VerifyOnly) {
    $installMessages.Add('Driver package signatures verified. No driver was installed.')
} else {
Write-Host 'FTDI 2.12.36.20 driver backup for the connected FTDI USB-RS485 adapters.'
Write-Host 'License is included in the original INF files. Continue only for genuine FTDI hardware.'
if (-not $FromApp -and (Read-Host 'Install these drivers? Type YES') -cne 'YES') { exit }
if ($FromApp) {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Administrator authorization is required. No driver was installed.' }
}
foreach ($driverName in @('ftdibus', 'ftdiport')) {
    $infPath = Join-Path $driverRoot "$driverName\$driverName.inf"
    $processArguments = @{FilePath="$env:SystemRoot\System32\pnputil.exe"; ArgumentList=@('/add-driver', "`"$infPath`"", '/install'); WindowStyle='Hidden'; PassThru=$true; Wait=$true}
    if (-not $FromApp) { $processArguments.Verb = 'RunAs' }
    $process = Start-Process @processArguments
    if ($process.ExitCode -ne 0 -and $process.ExitCode -ne 3010) { throw "Driver install failed: exit $($process.ExitCode)" }
    $installMessages.Add("$driverName : driver package processed successfully.")
    if ($process.ExitCode -eq 3010) { $installExitCode = 3010 }
}
Write-Host 'Driver installation complete. Reconnect the adapters and refresh COM ports in the app.'
$installMessages.Add('Driver installation completed. Reconnect adapters and detect COM ports again.')
if ($installExitCode -eq 3010) { $installMessages.Add('Windows requests a restart to complete installation.') }
}
} catch {
    $installExitCode = 1
    $installMessages.Add($_.Exception.Message)
} finally {
    $resultText = $installMessages -join [Environment]::NewLine
    if ($ResultPath) { [IO.File]::WriteAllText($ResultPath, $resultText, (New-Object Text.UTF8Encoding($true))) }
    Write-Output $resultText
}
exit $installExitCode
