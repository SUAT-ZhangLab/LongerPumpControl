param([switch]$IncludeLocalThirdPartyFiles)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$compiler = Join-Path $env:SystemRoot 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4.8 developer/runtime tools are required to build on Windows x64.' }
New-Item -ItemType Directory -Force build,release,'release\LongerPumpControl-0.2.1' | Out-Null
& $compiler /nologo /target:winexe /win32manifest:src\app.manifest /out:build\LongerPumpControl.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Xml.Linq.dll /r:System.Management.dll src\Protocol.cs src\Control.cs src\Widgets.cs src\AdaptiveForm.cs src\PortSelection.cs src\SettingsDialog.cs src\ReviewDialog.cs src\PumpCard.cs src\DemoChecks.cs src\Drivers.cs src\App.cs
if ($LASTEXITCODE -ne 0) { throw 'App compilation failed' }
Copy-Item -LiteralPath src\app.config -Destination build\LongerPumpControl.exe.config -Force
& $compiler /nologo /out:build\ProtocolTests.exe src\Protocol.cs tests\ProtocolTests.cs
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed' }
& .\build\ProtocolTests.exe | Tee-Object -FilePath build\test-results.txt
if ($LASTEXITCODE -ne 0) { throw 'Protocol tests failed' }
& $compiler /nologo /out:build\GroupTests.exe src\Protocol.cs src\Control.cs tests\GroupTests.cs
if ($LASTEXITCODE -ne 0) { throw 'Group test compilation failed' }
& .\build\GroupTests.exe | Tee-Object -FilePath build\group-test-results.txt
if ($LASTEXITCODE -ne 0) { throw 'Group tests failed' }
& $compiler /nologo /out:build\DriverTests.exe /r:build\LongerPumpControl.exe tests\DriverTests.cs
if ($LASTEXITCODE -ne 0) { throw 'Driver test compilation failed' }
& .\build\DriverTests.exe $PSScriptRoot --optional-payload | Tee-Object -FilePath build\driver-test-results.txt
if ($LASTEXITCODE -ne 0) { throw 'Driver tests failed' }
& $compiler /nologo /out:build\LayoutPortTests.exe /r:build\LongerPumpControl.exe /r:System.Drawing.dll /r:System.Windows.Forms.dll tests\LayoutPortTests.cs
if ($LASTEXITCODE -ne 0) { throw 'Layout/port test compilation failed' }
& .\build\LayoutPortTests.exe | Tee-Object -FilePath build\layout-port-test-results.txt
if ($LASTEXITCODE -ne 0) { throw 'Layout/port tests failed' }

& $compiler /nologo /out:build\Probe.exe src\Protocol.cs tests\Probe.cs
if ($LASTEXITCODE -ne 0) { throw 'Probe compilation failed' }
Copy-Item -LiteralPath build\LongerPumpControl.exe -Destination release\LongerPumpControl-0.2.1\LongerPumpControl.exe -Force
Copy-Item -LiteralPath src\app.config -Destination release\LongerPumpControl-0.2.1\LongerPumpControl.exe.config -Force
foreach ($file in @('使用说明.txt','THIRD_PARTY.md','Install-Drivers.cmd','Install-Drivers.ps1','Uninstall.ps1')) { Copy-Item -LiteralPath $file -Destination release\LongerPumpControl-0.2.1 -Force }
if ($IncludeLocalThirdPartyFiles) { Copy-Item -LiteralPath drivers -Destination release\LongerPumpControl-0.2.1 -Recurse -Force } elseif (Test-Path 'release\LongerPumpControl-0.2.1\drivers') { throw 'Use a clean checkout for a public build; third-party files remain in release staging.' }
Copy-Item -LiteralPath docs -Destination release\LongerPumpControl-0.2.1 -Recurse -Force
if ($IncludeLocalThirdPartyFiles) { Copy-Item -LiteralPath LSP100_manual_cn.pdf -Destination release\LongerPumpControl-0.2.1 -Force } elseif (Test-Path 'release\LongerPumpControl-0.2.1\LSP100_manual_cn.pdf') { throw 'Use a clean checkout for a public build; vendor manual remains in release staging.' }
Compress-Archive -Path 'release\LongerPumpControl-0.2.1\*' -DestinationPath 'release\LongerPumpControl-0.2.1-win-x64-portable.zip' -Force
& $compiler /nologo /target:winexe /win32manifest:src\app.manifest /out:release\LongerPumpControl-0.2.1-Setup.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /resource:release\LongerPumpControl-0.2.1-win-x64-portable.zip,payload.zip src\Setup.cs
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed' }
Get-FileHash -Algorithm SHA256 release\LongerPumpControl-0.2.1-Setup.exe,release\LongerPumpControl-0.2.1-win-x64-portable.zip | Format-List | Out-File release\SHA256.txt -Encoding utf8
Write-Host 'Build complete: release\LongerPumpControl-0.2.1-Setup.exe'

