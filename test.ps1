param([string]$BinDirectory = (Join-Path $PSScriptRoot 'bin'))
$ErrorActionPreference = 'Stop'
$projectDirectory = $PSScriptRoot
& (Join-Path $projectDirectory 'build.ps1') -OutputDirectory $BinDirectory
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$testExecutable = Join-Path $projectDirectory 'tests\CoreTests.exe'
$applicationPath = Join-Path $BinDirectory 'GapAudio.exe'
& $compiler /nologo /target:exe /platform:x64 /codepage:65001 "/out:$testExecutable" "/reference:$applicationPath" /reference:System.Core.dll /reference:System.Numerics.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll (Join-Path $projectDirectory 'tests\CoreTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
Copy-Item -LiteralPath $applicationPath -Destination (Join-Path $projectDirectory 'tests\GapAudio.exe') -Force
$fixtureDirectory = Join-Path $projectDirectory ('tests\runs\' + [Guid]::NewGuid().ToString('N'))
& $testExecutable $fixtureDirectory $BinDirectory
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
Write-Output "Fixtures: $fixtureDirectory"
