param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'bin'))
$ErrorActionPreference = 'Stop'
$projectDirectory = $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$outputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
if (!(Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4.x compiler is required.' }
if (!(Test-Path -LiteralPath (Join-Path $outputDirectory 'ffmpeg.exe')) -or !(Test-Path -LiteralPath (Join-Path $outputDirectory 'ffprobe.exe'))) {
    throw 'Offline FFmpeg tools missing. Extract the matching release package into bin, or rebuild with build-media.ps1 (see MEDIA-BUILD.md). No tools are downloaded by this script.'
}
$sourceFiles = Get-ChildItem -LiteralPath (Join-Path $projectDirectory 'src') -Filter '*.cs' | Select-Object -ExpandProperty FullName
$applicationPath = Join-Path $outputDirectory 'GapAudio.exe'
$iconPath = Join-Path $projectDirectory 'resources\app.ico'
$resourcePath = Join-Path $projectDirectory 'resources\AppAssets.resources'
Add-Type -AssemblyName System.Windows.Forms
$resourceReader = New-Object System.Resources.ResXResourceReader (Join-Path $projectDirectory 'resources\AppAssets.resx')
$resourceWriter = New-Object System.Resources.ResourceWriter $resourcePath
try { foreach ($entry in $resourceReader) { $resourceWriter.AddResource($entry.Key, $entry.Value) }; $resourceWriter.Generate() } finally { $resourceReader.Dispose(); $resourceWriter.Dispose() }
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /codepage:65001 "/out:$applicationPath" "/win32manifest:$(Join-Path $projectDirectory 'app.manifest')" "/win32icon:$iconPath" "/resource:$resourcePath,GapGone.AppAssets.resources" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Numerics.dll /reference:System.Web.Extensions.dll $sourceFiles
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Copy-Item -LiteralPath (Join-Path $projectDirectory 'GapAudio.exe.config') -Destination (Join-Path $outputDirectory 'GapAudio.exe.config') -Force
Copy-Item -LiteralPath (Join-Path $projectDirectory 'README.md') -Destination (Join-Path $outputDirectory 'README.md') -Force
foreach ($document in @('VALIDATION.md', 'THIRD-PARTY-NOTICES.md', 'MEDIA-BUILD.md', 'build-media.sh', 'build-media.ps1')) {
    if (Test-Path -LiteralPath (Join-Path $projectDirectory $document)) { Copy-Item -LiteralPath (Join-Path $projectDirectory $document) -Destination (Join-Path $outputDirectory $document) -Force }
}
$mediaDirectory = Join-Path $projectDirectory 'media'
if (Test-Path -LiteralPath $mediaDirectory) {
    foreach ($document in @('FFmpeg-BUILD.txt', 'FFmpeg-config.h', 'FFmpeg-LICENSE.txt')) {
        $mediaDocument = Join-Path $mediaDirectory $document
        if ((Test-Path -LiteralPath $mediaDocument) -and !(Test-Path -LiteralPath (Join-Path $outputDirectory $document))) { Copy-Item -LiteralPath $mediaDocument -Destination $outputDirectory -Force }
    }
    if (Test-Path -LiteralPath (Join-Path $mediaDirectory 'licenses')) { Copy-Item -LiteralPath (Join-Path $mediaDirectory 'licenses') -Destination $outputDirectory -Recurse -Force }
}
$licensePath = Join-Path $projectDirectory 'LICENSE'
if ([IO.Path]::GetFullPath($licensePath) -ne [IO.Path]::GetFullPath((Join-Path $outputDirectory 'LICENSE'))) { Copy-Item -LiteralPath $licensePath -Destination (Join-Path $outputDirectory 'LICENSE') -Force }
Write-Output "Built: $applicationPath"
