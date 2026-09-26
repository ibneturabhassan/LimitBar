# Install only the compiler tools into ignored build output; no global installation.
$ErrorActionPreference = 'Stop'
$tools = Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts/tools'
$compiler = Join-Path $tools 'inno/ISCC.exe'
if (Test-Path $compiler) { Write-Output $compiler; return }
New-Item -ItemType Directory -Force $tools | Out-Null
$download = Join-Path $tools 'innosetup-6.7.3.exe'
Invoke-WebRequest 'https://github.com/jrsoftware/issrc/releases/download/is-6_7_3/innosetup-6.7.3.exe' -OutFile $download
if ((Get-FileHash $download -Algorithm SHA256).Hash -ne '9C73C3BAE7ED48D44112A0F48E66742C00090BDB5BEF71D9D3C056C66E97B732') {
    throw 'Inno Setup download checksum mismatch.'
}
$destination = Join-Path $tools 'inno'
$process = Start-Process -FilePath $download -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-', '/CURRENTUSER', '/PORTABLE=1', "/DIR=`"$destination`"") -WindowStyle Hidden -Wait -PassThru
if ($process.ExitCode -ne 0 -or !(Test-Path $compiler)) { throw 'Could not prepare the installer compiler.' }
Write-Output $compiler
