# Publishes a self-contained Release build to artifacts\publish\<rid>.
param([ValidateSet('win-x64', 'win-arm64')] [string] $Runtime = 'win-x64')

. (Join-Path $PSScriptRoot 'env.ps1')
$out = Join-Path $PSScriptRoot "..\artifacts\publish\$Runtime"
dotnet publish (Join-Path $PSScriptRoot '..\src\TaskbarGroups') -c Release -r $Runtime -p:Platform=$($Runtime -replace 'win-', '' -replace 'arm64', 'ARM64') -o $out
exit $LASTEXITCODE
