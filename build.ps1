$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$wpf = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\WPF'
$destination = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Force $destination | Out-Null
& (Join-Path $PSScriptRoot 'make-icon.ps1') -ImagePath (Join-Path $PSScriptRoot 'assets\app-icon.png') -Destination (Join-Path $destination 'VoltHUD.ico')
$references = @('PresentationFramework.dll','PresentationCore.dll','WindowsBase.dll','System.Xaml.dll') | ForEach-Object {
    $candidate = Join-Path $wpf $_
    if (!(Test-Path -LiteralPath $candidate)) { $candidate = Join-Path (Split-Path $wpf) $_ }
    '/reference:' + $candidate
}
$references += @('Windows.Devices.winmd','Windows.Foundation.winmd','Windows.Storage.winmd') | ForEach-Object { '/reference:' + (Join-Path $env:WINDIR ('System32\WinMetadata\' + $_)) }
$references += '/reference:' + (Join-Path $env:WINDIR 'Microsoft.NET\assembly\GAC_MSIL\System.Runtime\v4.0_4.0.0.0__b03f5f7f11d50a3a\System.Runtime.dll')
$options = @('/nologo','/target:winexe','/platform:anycpu','/optimize+','/deterministic+',('/out:' + (Join-Path $destination 'VoltHUD.exe')),('/win32manifest:' + (Join-Path $PSScriptRoot 'src\app.manifest')), '/reference:System.dll','/reference:System.Core.dll','/reference:System.Drawing.dll','/reference:System.Windows.Forms.dll')
# The inbox .NET Framework compiler predates /deterministic. Builds remain fully inspectable.
$options = $options | Where-Object { $_ -ne '/deterministic+' }
& $compiler @options @references ('/win32icon:' + (Join-Path $destination 'VoltHUD.ico')) ('/resource:' + (Join-Path $destination 'VoltHUD.ico') + ',VoltHUD.AppIcon.ico') ('/resource:' + (Join-Path $PSScriptRoot 'assets\device-icons.png') + ',VoltHUD.DeviceIcons.png') (Get-ChildItem (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object FullName)
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed' }
Get-FileHash (Join-Path $destination 'VoltHUD.exe') -Algorithm SHA256
