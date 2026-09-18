param([string]$OutputPath)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
if(-not $OutputPath){$OutputPath=Join-Path (Split-Path -Parent $root) '81_murums_Wii_Studio/murums Wii Mod Studio.exe'}
$OutputPath=[IO.Path]::GetFullPath($OutputPath)
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($OutputPath)) | Out-Null
& (Join-Path $PSScriptRoot 'build.ps1') -OutputPath (Join-Path $PSScriptRoot 'build/next/murums Wii Mod Studio.exe')
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
. (Join-Path $PSScriptRoot 'tools/SETUP_OPTIONS.ps1')
$catalogPath=Join-Path $PSScriptRoot 'build/setup-tools.tsv'
$catalogLines=@(Get-SetupToolCatalog | ForEach-Object { @($_.Id,$_.Name,$_.Recommendation,$_.Details,[string]$_.Default) -join [char]9 })
[IO.File]::WriteAllLines($catalogPath,$catalogLines,(New-Object Text.UTF8Encoding($false)))
$payload=Join-Path $PSScriptRoot 'build/setup-payload.zip'
$files=@{}
$files['LICENSE']=Join-Path $root 'LICENSE'
$files['THIRD_PARTY.md']=Join-Path $root 'THIRD_PARTY.md'
$files['murums Wii Mod Studio.exe']=Join-Path $PSScriptRoot 'build/next/murums Wii Mod Studio.exe'
$csc=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$chrome=Join-Path $PSScriptRoot 'build/SETUP_CHROME.dll'
& $csc /nologo /target:library /codepage:65001 /r:System.Drawing.dll /r:System.Windows.Forms.dll "/out:$chrome" (Join-Path $PSScriptRoot 'tools/SETUP_CHROME.cs')
if($LASTEXITCODE -ne 0){throw 'Setup UI compilation failed'}
$uninstaller=Join-Path $PSScriptRoot 'build/Uninstall.exe'
& $csc /nologo /target:winexe /define:STUDIO_UNINSTALLER /codepage:65001 /r:System.Windows.Forms.dll "/out:$uninstaller" "/win32icon:$PSScriptRoot/source/App/murums.ico" "/win32manifest:$PSScriptRoot/source/App/app.manifest" (Join-Path $PSScriptRoot 'tools/UNINSTALL_LAUNCHER.cs') (Join-Path $PSScriptRoot 'source/App/AssemblyInfo.cs')
if($LASTEXITCODE -ne 0){throw 'Uninstaller compilation failed'}
$files['Uninstall.exe']=$uninstaller
$files['internal/tools/SETUP_CHROME.dll']=$chrome
foreach($name in @('UNINSTALL.ps1','SETUP_GUI.ps1','SETUP_WORKER.ps1','SETUP_OPTIONS.ps1','INSTALL_TOOLCHAIN.ps1')){$files['internal/tools/'+$name]=Join-Path $PSScriptRoot ('tools/'+$name)}
$stream=[IO.File]::Open($payload,[IO.FileMode]::Create)
$zip=New-Object IO.Compression.ZipArchive($stream,[IO.Compression.ZipArchiveMode]::Create)
try{foreach($name in ($files.Keys | Sort-Object)){[void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$files[$name],$name,[IO.Compression.CompressionLevel]::Optimal)}}finally{$zip.Dispose();$stream.Dispose()}
$csc=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& $csc /nologo /target:winexe /define:SETUP_BUNDLE /codepage:65001 /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.IO.Compression.dll "/resource:$payload,studio.zip" "/resource:$catalogPath,tools.tsv" "/win32icon:$PSScriptRoot/source/App/murums.ico" "/win32manifest:$PSScriptRoot/source/App/app.manifest" "/out:$OutputPath" (Join-Path $PSScriptRoot 'tools/SETUP_BUNDLE.cs') (Join-Path $PSScriptRoot 'tools/SETUP_PAGE.cs') (Join-Path $PSScriptRoot 'source/App/AssemblyInfo.cs') (Join-Path $PSScriptRoot 'source/UI/FolderPickerDialog.cs') (Join-Path $PSScriptRoot 'tools/SETUP_CHROME.cs')
if($LASTEXITCODE -ne 0){throw 'Setup bundle compilation failed'}
Get-FileHash -LiteralPath $OutputPath -Algorithm SHA256 | Format-List
Get-Item -LiteralPath $OutputPath | Select-Object FullName,Length
