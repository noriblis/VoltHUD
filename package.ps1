$ErrorActionPreference = 'Stop'
$release = Join-Path $PSScriptRoot 'release\0.1.1-preview'
$stage = Join-Path $release 'package'
$executable = Join-Path $PSScriptRoot 'dist\VoltHUD.exe'
if (!(Test-Path -LiteralPath $executable)) { throw 'Run build.ps1 first.' }
if ([Diagnostics.FileVersionInfo]::GetVersionInfo($executable).FileVersion -ne '0.1.1.0') { throw 'Unexpected executable version.' }
New-Item -ItemType Directory -Force $stage, (Join-Path $stage 'assets') | Out-Null
Copy-Item -LiteralPath $executable -Destination (Join-Path $stage 'VoltHUD.exe') -Force
foreach ($name in @('README.md','RELEASE_NOTES.md','SECURITY.md','LICENSE','LOCAL_REVIEW.md','MEMORY.md','CODE_SIGNING_POLICY.md')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $stage $name) -Force
}
foreach ($name in @('app-badge.png','preview.png','settings-preview.png','tray-hover-five-devices.svg','README.md')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('assets\' + $name)) -Destination (Join-Path $stage ('assets\' + $name)) -Force
}
$exeHash = (Get-FileHash -LiteralPath (Join-Path $stage 'VoltHUD.exe') -Algorithm SHA256).Hash.ToLowerInvariant()
($exeHash + '  VoltHUD.exe') | Set-Content -LiteralPath (Join-Path $stage 'SHA256SUMS.txt') -Encoding ascii
$zip = Join-Path $release 'VoltHUD-windows.zip'
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -Force
Copy-Item -LiteralPath $executable -Destination (Join-Path $release 'VoltHUD.exe') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'RELEASE_NOTES.md') -Destination (Join-Path $release 'RELEASE_NOTES.md') -Force
$checksums = foreach ($name in @('VoltHUD-windows.zip','VoltHUD.exe','RELEASE_NOTES.md')) {
    (Get-FileHash -LiteralPath (Join-Path $release $name) -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $name
}
$checksums | Set-Content -LiteralPath (Join-Path $release 'SHA256SUMS.txt') -Encoding ascii
Write-Output ('Prepared locally: ' + $zip)
