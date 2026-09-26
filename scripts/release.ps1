param([string]$Compiler)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    [xml]$props = Get-Content Directory.Build.props
    $version = [string]$props.Project.PropertyGroup.Version
    if ($version -notmatch '^\d+\.\d+\.\d+(-[a-zA-Z0-9.-]+)?$') { throw 'Invalid release version.' }
    $binaryVersion = ($version -split '-')[0]
    $release = Join-Path $root "artifacts/releases/$version"
    if (Test-Path $release) { throw "Release output exists: $release. Use a new version or move the previous build aside." }
    if (!$Compiler) { $Compiler = & "$PSScriptRoot/get-inno.ps1" }
    & "$PSScriptRoot/verify.ps1"
    $publish = Join-Path $release 'app'
    dotnet publish LimitBar.Desktop -c Release -r win-x64 --self-contained true -p:DebugType=None -p:DebugSymbols=false -o $publish
    if ($LASTEXITCODE) { throw 'Publish failed.' }
    Copy-Item LICENSE,README.md,THIRD-PARTY-NOTICES.md,SECURITY.md,CONTRIBUTING.md $publish
    Copy-Item docs (Join-Path $publish 'docs') -Recurse
    $packages = (dotnet nuget locals global-packages --list) -replace '^global-packages:\s*', ''
    if ($LASTEXITCODE) { throw 'Cannot locate runtime license files.' }
    $runtime = Get-Content "$publish/LimitBar.Desktop.runtimeconfig.json" -Raw | ConvertFrom-Json
    foreach ($framework in $runtime.runtimeOptions.includedFrameworks) {
        $package = Join-Path $packages ($framework.name.ToLowerInvariant() + '.runtime.win-x64/' + $framework.version)
        $notices = Get-ChildItem $package -File | Where-Object { $_.Name -match '^(LICENSE|THIRD-PARTY-NOTICES)' }
        if (!$notices) { throw "Runtime license missing: $package" }
        $licenseDir = Join-Path $publish ('licenses/' + $framework.name)
        New-Item -ItemType Directory -Force $licenseDir | Out-Null
        $notices | Copy-Item -Destination $licenseDir
    }
    Copy-Item (Join-Path (Split-Path $Compiler -Parent) 'License.txt') (Join-Path $publish 'licenses/Inno-Setup-License.txt')
    $zip = Join-Path $release "LimitBar-$version-win-x64-portable.zip"
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory($publish, $zip)
    & $Compiler /Q "/DAppVersion=$version" "/DBinaryVersion=$binaryVersion" "/DSourceDir=$publish" "/DOutputDir=$release" "$root/packaging/LimitBar.iss"
    if ($LASTEXITCODE) { throw 'Installer compilation failed.' }
    $files = Get-ChildItem $release -File | Where-Object { $_.Extension -in '.exe', '.zip' }
    $files | Sort-Object Name | ForEach-Object { "{0}  {1}" -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name } |
        Set-Content (Join-Path $release 'SHA256SUMS.txt') -Encoding ascii
    Write-Output "Release files: $release"
} finally { Pop-Location }
