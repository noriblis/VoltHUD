$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$destination = Join-Path $PSScriptRoot 'dist\BatteryTests.exe'
New-Item -ItemType Directory -Force (Split-Path $destination) | Out-Null
$references = @('Windows.Devices.winmd','Windows.Foundation.winmd','Windows.Storage.winmd') | ForEach-Object { '/reference:' + (Join-Path $env:WINDIR ('System32\WinMetadata\' + $_)) }
$references += '/reference:' + (Join-Path $env:WINDIR 'Microsoft.NET\assembly\GAC_MSIL\System.Runtime\v4.0_4.0.0.0__b03f5f7f11d50a3a\System.Runtime.dll')
& $compiler /nologo ('/out:' + $destination) @references (Get-ChildItem (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | Where-Object { $_.Name -notin @('App.cs','DeviceIcons.cs','ThemePalette.cs') } | ForEach-Object FullName) (Join-Path $PSScriptRoot 'tests\BatteryTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed' }
& $destination
if ($LASTEXITCODE -ne 0) { throw 'Battery tests failed' }
