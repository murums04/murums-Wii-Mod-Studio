param([string]$OutputPath)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutputPath) { $OutputPath = Join-Path $PSScriptRoot 'build/murums Wii Mod Studio.exe' }
$OutputPath = [IO.Path]::GetFullPath($OutputPath)
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($OutputPath)) | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'The .NET Framework C# compiler is missing.' }
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'source') -Filter '*.cs' -Recurse | ForEach-Object FullName)
$compilerArgs = @('/nologo','/codepage:65001', '/target:winexe', '/platform:anycpu', '/optimize+', '/main:murumsWiiModStudio.Program', '/reference:System.dll', '/reference:System.Core.dll', '/reference:System.Drawing.dll', '/reference:System.Windows.Forms.dll', "/out:$OutputPath", "/win32icon:$PSScriptRoot/source/App/murums.ico", "/win32manifest:$PSScriptRoot/source/App/app.manifest")
& $compiler @compilerArgs @sources
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Write-Output "Built: $OutputPath"
