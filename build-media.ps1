param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'bin'))
$ErrorActionPreference = 'Stop'
$bashPath = Join-Path $PSScriptRoot 'tools/msys64/usr/bin/bash.exe'
if (!(Test-Path -LiteralPath $bashPath)) { throw 'MSYS2 build toolchain missing. See MEDIA-BUILD.md. This script does not download dependencies.' }
$buildScript = (Join-Path $PSScriptRoot 'build-media.sh').Replace('\', '/')
$mediaOutput = [IO.Path]::GetFullPath($OutputDirectory).Replace('\', '/')
& $bashPath $buildScript $mediaOutput
if ($LASTEXITCODE -ne 0) { throw 'Audio-only FFmpeg build failed.' }
