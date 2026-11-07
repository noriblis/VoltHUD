param(
    [Parameter(Mandatory=$true)][string]$Path,
    [Parameter(Mandatory=$true)][string]$ExpectedPublisher,
    [Parameter(Mandatory=$true)][string]$ExpectedVersion
)
$ErrorActionPreference = 'Stop'
$file = (Resolve-Path -LiteralPath $Path).Path
$signature = Get-AuthenticodeSignature -LiteralPath $file
if ($signature.Status -ne 'Valid' -or !$signature.SignerCertificate) { throw ('A valid Authenticode signature is required: ' + $signature.Status) }
$publisher = $signature.SignerCertificate.GetNameInfo([Security.Cryptography.X509Certificates.X509NameType]::SimpleName, $false)
if ($publisher -cne $ExpectedPublisher) { throw ('Unexpected signing publisher: ' + $publisher) }
if (!$signature.TimeStamperCertificate) { throw 'A timestamp is required.' }
$info = [Diagnostics.FileVersionInfo]::GetVersionInfo($file)
if ($info.ProductName -ne 'VoltHUD' -or $info.CompanyName -ne 'VoltHUD contributors' -or $info.FileVersion -ne $ExpectedVersion -or $info.ProductVersion -ne $ExpectedVersion) { throw 'Signed executable metadata does not match the approved build.' }
Write-Output ('Verified signed VoltHUD ' + $ExpectedVersion + ' from ' + $publisher)
