# Builds and runs the app (Debug) with the project-local SDK.
. (Join-Path $PSScriptRoot 'env.ps1')
dotnet run --project (Join-Path $PSScriptRoot '..\src\TaskbarGroups') @args
exit $LASTEXITCODE
