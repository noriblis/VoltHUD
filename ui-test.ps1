param([switch]$Interactive)
$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$references = @('PresentationCore.dll','PresentationFramework.dll','WindowsBase.dll') | ForEach-Object { '/reference:' + (Join-Path $framework ('WPF\' + $_)) }
$references += '/reference:' + (Join-Path $framework 'System.Xaml.dll')
$references += '/reference:System.Windows.Forms.dll'
$references += '/reference:' + (Join-Path $PSScriptRoot 'dist\VoltHUD.exe')
$testProgram = Join-Path $PSScriptRoot 'dist\UiTests.exe'
& (Join-Path $framework 'csc.exe') /nologo ('/out:' + $testProgram) @references (Join-Path $PSScriptRoot 'tests\UiTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'UI test compilation failed' }
$testArguments = @((Join-Path $PSScriptRoot 'dist\qa-state'))
if ($Interactive) { $testArguments += '--interactive' }
& $testProgram @testArguments
if ($LASTEXITCODE -ne 0) { throw 'UI tests failed' }
