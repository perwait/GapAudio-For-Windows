param(
    [string]$BinDirectory = (Join-Path $PSScriptRoot 'bin'),
    [string]$InnoCompiler
)
$ErrorActionPreference = 'Stop'
if (!$InnoCompiler) {
    $compilerCommand = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($compilerCommand) { $InnoCompiler = $compilerCommand.Source }
}
if (!$InnoCompiler) {
    $candidatePaths = @()
    foreach ($version in @('7', '6')) {
        foreach ($baseDirectory in @(${env:ProgramFiles(x86)}, $env:ProgramFiles, (Join-Path $env:LOCALAPPDATA 'Programs'))) {
            if ($baseDirectory) { $candidatePaths += Join-Path $baseDirectory "Inno Setup $version\ISCC.exe" }
        }
    }
    foreach ($registryRoot in @('HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall', 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall', 'HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall')) {
        foreach ($entry in (Get-ChildItem -LiteralPath $registryRoot -ErrorAction SilentlyContinue | Get-ItemProperty -ErrorAction SilentlyContinue)) {
            if ($entry.DisplayName -like 'Inno Setup*' -and $entry.InstallLocation) { $candidatePaths += Join-Path $entry.InstallLocation 'ISCC.exe' }
        }
    }
    $InnoCompiler = $candidatePaths | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
}
if (!$InnoCompiler -or !(Test-Path -LiteralPath $InnoCompiler -PathType Leaf)) { throw 'Inno Setup 7.1 or later is required. Install it from https://jrsoftware.org/isdl.php, or pass -InnoCompiler with the full path to ISCC.exe. This script does not download tools.' }
$InnoCompiler = (Resolve-Path -LiteralPath $InnoCompiler).Path
$versionOutput = & $InnoCompiler --version
if ($LASTEXITCODE -ne 0) { throw 'Unable to read the Inno Setup compiler version.' }
$compilerVersion = [version](($versionOutput | Select-Object -First 1).Trim())
if ($compilerVersion -lt [version]'7.1') { throw 'The modern dynamic Windows 11 wizard requires Inno Setup 7.1 or later.' }
$BinDirectory = [IO.Path]::GetFullPath($BinDirectory)
$assetDirectory = Join-Path $PSScriptRoot 'dist\installer-assets'
& (Join-Path $PSScriptRoot 'build.ps1') -OutputDirectory $BinDirectory
& (Join-Path $PSScriptRoot 'installer\generate-assets.ps1') -OutputDirectory $assetDirectory
& $InnoCompiler "--define=BinDirectory=$BinDirectory" "--define=WizardAssets=$assetDirectory" (Join-Path $PSScriptRoot 'installer\GapAudio.iss')
if ($LASTEXITCODE -ne 0) { throw "Inno Setup compilation failed (exit code $LASTEXITCODE)." }
$installerPath = Join-Path $PSScriptRoot 'dist\GapAudio-V1.0.0-Windows-x64-Setup.exe'
if (!(Test-Path -LiteralPath $installerPath)) { throw 'Inno Setup did not produce the expected installer.' }
Write-Output "Installer: $installerPath"
Get-FileHash -LiteralPath $installerPath -Algorithm SHA256
