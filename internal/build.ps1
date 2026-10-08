param([string]$OutputPath)
. (Join-Path $PSScriptRoot 'build-guard.ps1')
Invoke-StudioBuild {
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutputPath) { $OutputPath = Join-Path $PSScriptRoot 'build/murums Wii Mod Studio.exe' }
$OutputPath = [IO.Path]::GetFullPath($OutputPath)
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($OutputPath)) | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'The .NET Framework C# compiler is missing.' }
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'source') -Filter '*.cs' -Recurse | ForEach-Object FullName)
$compilerArgs = @('/nologo','/codepage:65001', '/target:winexe', '/platform:anycpu', '/optimize+', '/main:murumsWiiModStudio.Program', '/reference:System.dll', '/reference:System.Core.dll','/reference:System.Web.Extensions.dll', '/reference:System.IO.Compression.dll', '/reference:System.IO.Compression.FileSystem.dll', '/reference:System.Drawing.dll', '/reference:System.Windows.Forms.dll', "/out:$OutputPath", "/win32icon:$PSScriptRoot/source/App/murums.ico", "/win32manifest:$PSScriptRoot/source/App/app.manifest")
$resourceNames = @(Get-Content -LiteralPath (Join-Path $PSScriptRoot 'resources.txt') | ForEach-Object { $_.Trim() } | Where-Object { $_ })
if (-not $resourceNames.Count -or @($resourceNames | Select-Object -Unique).Count -ne $resourceNames.Count) { throw 'Invalid resource manifest' }
foreach ($name in $resourceNames) {
    if ($name -cnotmatch '\A(?:[A-Za-z0-9][A-Za-z0-9_.-]*|Help/[A-Za-z0-9][A-Za-z0-9_.-]*\.png)\z') { throw 'Invalid resource filename' }
    $resource = Join-Path $PSScriptRoot ('source/Resources/' + $name)
    if (-not (Test-Path -LiteralPath $resource)) { throw ('Missing program resource: ' + $name) }
    $compilerArgs += ('/resource:' + $resource + ',Studio.' + $name)
}
& $compiler @compilerArgs @sources
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Write-Output "Built: $OutputPath"

& (Join-Path $PSScriptRoot 'build-model.ps1') -OutputDirectory (Join-Path ([IO.Path]::GetDirectoryName($OutputPath)) 'internal/model')
}
