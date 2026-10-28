# Runs the tests with the project-local SDK. Extra args pass through to `dotnet test`.
. (Join-Path $PSScriptRoot 'env.ps1')
dotnet test (Join-Path $PSScriptRoot '..\TaskbarGroups.slnx') @args
exit $LASTEXITCODE
