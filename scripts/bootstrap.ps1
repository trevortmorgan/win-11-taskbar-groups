# Installs the .NET SDK pinned in global.json into .tools\dotnet.
# No admin rights, no registry entries, no global PATH changes. Delete .tools\ to undo.

$ErrorActionPreference = 'Stop'
$repoRoot  = Split-Path $PSScriptRoot -Parent
$tools     = Join-Path $repoRoot '.tools'
$installer = Join-Path $tools 'dotnet-install.ps1'

New-Item -ItemType Directory -Force $tools | Out-Null
Invoke-WebRequest 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer

# Refuse to run the installer unless it's validly signed by Microsoft.
$sig = Get-AuthenticodeSignature $installer
if ($sig.Status -ne 'Valid' -or $sig.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') {
    throw "dotnet-install.ps1 signature check failed ($($sig.Status))."
}

& $installer -JSonFile (Join-Path $repoRoot 'global.json') -InstallDir (Join-Path $tools 'dotnet') -NoPath

. (Join-Path $PSScriptRoot 'env.ps1')
dotnet --version
