@echo off
echo Building LimitBar Phase 1...

REM Fallback to absolute path if dotnet is not in PATH (e.g. if terminal hasn't been restarted)
set DOTNET_CMD=dotnet
where dotnet >nul 2>&1
if %errorlevel% neq 0 (
    set DOTNET_CMD="C:\Program Files\dotnet\dotnet.exe"
)

%DOTNET_CMD% build limitbar-cli\limitbar-cli.csproj
if %errorlevel% neq 0 exit /b %errorlevel%

echo Running LimitBar CLI...
%DOTNET_CMD% run --project limitbar-cli\limitbar-cli.csproj
