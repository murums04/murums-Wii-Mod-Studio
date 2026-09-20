param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
$deps = Join-Path $PSScriptRoot 'dependencies'
$library = Join-Path $deps 'brawl-source/BrawlLib/bin/AnyCPU/Release/net472/BrawlLib.dll'
if (-not (Test-Path -LiteralPath $library)) { throw 'Build BrawlLib for AnyCPU using the included corresponding source before packaging.' }
foreach ($name in @('BrawlLib.dll','OpenTK.dll')) {
    Copy-Item -LiteralPath (Join-Path ([IO.Path]::GetDirectoryName($library)) $name) -Destination (Join-Path $OutputDirectory $name) -Force
}
Copy-Item -LiteralPath (Join-Path $deps 'assimp-6.0.5-x64/Release/assimp-vc143-mt.dll') -Destination (Join-Path $OutputDirectory 'assimp-vc143-mt.dll') -Force
Copy-Item -LiteralPath (Join-Path $deps 'blender-5.2.2-windows-x64.zip') -Destination $OutputDirectory -Force
$csc = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& $csc /nologo /unsafe /target:library /codepage:65001 /r:System.Core.dll /r:System.Drawing.dll ("/r:"+$library) ("/out:"+(Join-Path $OutputDirectory 'StudioModelCodec.dll')) (Join-Path $PSScriptRoot 'model/StudioModelCodec.cs')
if ($LASTEXITCODE -ne 0) { throw 'Internal model adapter compilation failed.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'model/notices') -Destination $OutputDirectory -Recurse -Force
$sourceFolder = Join-Path $OutputDirectory 'sources'
[IO.Directory]::CreateDirectory($sourceFolder) | Out-Null
foreach ($name in @('brawllib-studio-source.zip','blender-5.2.2.tar.xz')) { Copy-Item -LiteralPath (Join-Path $deps $name) -Destination $sourceFolder -Force }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'model/StudioModelCodec.cs') -Destination $sourceFolder -Force

