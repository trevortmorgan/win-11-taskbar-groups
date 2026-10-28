# Builds the solution with the project-local SDK. Extra args pass through to `dotnet build`.
. (Join-Path $PSScriptRoot 'env.ps1')
dotnet build (Join-Path $PSScriptRoot '..\TaskbarGroups.slnx') @args
exit $LASTEXITCODE
