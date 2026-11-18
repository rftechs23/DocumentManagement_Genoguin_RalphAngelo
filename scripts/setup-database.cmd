@echo off
setlocal

pushd "%~dp0.."
dotnet ef database update --project src/DocumentManagement.Infrastructure/DocumentManagement.Infrastructure.csproj --startup-project src/DocumentManagement.Api/DocumentManagement.Api.csproj
set "exitCode=%errorlevel%"
popd

exit /b %exitCode%
