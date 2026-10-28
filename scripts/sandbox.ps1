# Publishes the app and opens it in Windows Sandbox: a throwaway VM with its own taskbar,
# discarded when closed. Requires the "Windows Sandbox" Windows feature (one-off, enabled by you).

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent

if (-not (Test-Path "$env:WINDIR\System32\WindowsSandbox.exe")) {
    throw 'Windows Sandbox is not enabled. Turn it on in "Turn Windows features on or off", then reboot.'
}

& (Join-Path $PSScriptRoot 'publish.ps1')
if ($LASTEXITCODE) { exit $LASTEXITCODE }

$publish = Resolve-Path (Join-Path $repoRoot 'artifacts\publish\win-x64')
$wsb     = Join-Path $repoRoot '.tools\sandbox.wsb'

# App folder mapped read-only; no network inside the sandbox.
@"
<Configuration>
  <Networking>Disable</Networking>
  <MappedFolders>
    <MappedFolder>
      <HostFolder>$publish</HostFolder>
      <SandboxFolder>C:\TaskbarGroups</SandboxFolder>
      <ReadOnly>true</ReadOnly>
    </MappedFolder>
  </MappedFolders>
  <LogonCommand>
    <Command>C:\TaskbarGroups\TaskbarGroups.exe</Command>
  </LogonCommand>
</Configuration>
"@ | Set-Content $wsb

Start-Process $wsb
