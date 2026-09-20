$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
$installTarget = [IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\')
$expectedTarget = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs\LongerPumpControl')).TrimEnd('\')
if ($installTarget -ne $expectedTarget -or -not (Test-Path -LiteralPath (Join-Path $installTarget 'LongerPumpControl.exe'))) { throw 'Uninstaller must run from the installed application directory.' }
if (Get-Process -Name LongerPumpControl -ErrorAction SilentlyContinue) { [Windows.Forms.MessageBox]::Show('Please close Longer Pump Control first.') | Out-Null; exit 1 }
if ([Windows.Forms.MessageBox]::Show('Uninstall Longer Pump Control? Logs, connection settings and device drivers will be kept.', 'Uninstall', 'YesNo') -ne 'Yes') { exit }
foreach ($folder in @([Environment]::GetFolderPath('DesktopDirectory'), [Environment]::GetFolderPath('Programs'))) {
    $linkPath = Join-Path $folder 'Longer Pump Control.lnk'
    if (Test-Path -LiteralPath $linkPath) { Remove-Item -LiteralPath $linkPath }
}
Remove-Item -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\LongerPumpControl' -ErrorAction SilentlyContinue
# Both absolute paths were validated above; only this application's installed directory is removed.
Remove-Item -LiteralPath $installTarget -Recurse -Force
[Windows.Forms.MessageBox]::Show('Uninstalled. Logs and connection settings remain in %LOCALAPPDATA%\LongerPumpControl.') | Out-Null
