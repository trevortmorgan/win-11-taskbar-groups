# Points the current terminal at the project-local toolchain in .tools/.
# Usage (dot-source it):  . .\scripts\env.ps1
# Nothing outside this terminal session is changed.

$repoRoot = Split-Path $PSScriptRoot -Parent
$tools    = Join-Path $repoRoot '.tools'

$env:DOTNET_ROOT                 = Join-Path $tools 'dotnet'
$env:DOTNET_CLI_HOME             = Join-Path $tools 'home'
$env:NUGET_PACKAGES              = Join-Path $tools 'nuget\packages'
$env:NUGET_HTTP_CACHE_PATH       = Join-Path $tools 'nuget\http-cache'
$env:NUGET_PLUGINS_CACHE_PATH    = Join-Path $tools 'nuget\plugins-cache'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO               = '1'
# Don't leave MSBuild worker processes running after a build.
$env:MSBUILDDISABLENODEREUSE     = '1'

if (-not ($env:PATH -split ';' -contains $env:DOTNET_ROOT)) {
    $env:PATH = "$env:DOTNET_ROOT;$env:PATH"
}

if (-not (Test-Path (Join-Path $env:DOTNET_ROOT 'dotnet.exe'))) {
    Write-Warning 'Project SDK not found. Run .\scripts\bootstrap.ps1'
}
