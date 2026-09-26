$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    dotnet build LimitBar.slnf -c Release -m:1
    if ($LASTEXITCODE) { throw 'Build failed.' }
    dotnet run --project LimitBar.Checks -c Release --no-build
    if ($LASTEXITCODE) { throw 'Provider checks failed.' }
    dotnet run --project LimitBar.Desktop -c Release --no-build -- --self-test
    if ($LASTEXITCODE) { throw 'Desktop checks failed.' }
} finally { Pop-Location }
