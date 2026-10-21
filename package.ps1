param([string]$BinDirectory = (Join-Path $PSScriptRoot 'bin'))
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1') -OutputDirectory $BinDirectory
$distributionDirectory = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Force -Path $distributionDirectory | Out-Null
$packagePath = Join-Path $distributionDirectory 'GapAudio-V1.0.0-Windows-x64.zip'
$releaseFiles = Get-ChildItem -LiteralPath $BinDirectory | Where-Object { $_.Name -notin @('GapGone.exe', 'GapGone.exe.config', 'temp') } | Select-Object -ExpandProperty FullName
Compress-Archive -Path $releaseFiles -DestinationPath $packagePath -Force
Write-Output "Package: $packagePath"
Get-FileHash -LiteralPath $packagePath -Algorithm SHA256
