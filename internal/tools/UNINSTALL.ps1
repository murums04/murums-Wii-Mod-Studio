param([Parameter(Mandatory=$true)][string]$Root,[switch]$VerifyOnly)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
$rootPath=[IO.Path]::GetFullPath($Root).TrimEnd('\')
$prefix=$rootPath+'\'
$manifest=Join-Path $rootPath 'install-manifest.txt'
if(-not (Test-Path -LiteralPath $manifest)){throw 'Installation manifest missing; no files were removed.'}
if((Get-Item -LiteralPath $rootPath).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Linked installation directory is not supported.'}
$targets=@()
foreach($relative in [IO.File]::ReadAllLines($manifest)){
 if([string]::IsNullOrWhiteSpace($relative)){continue}
 $target=[IO.Path]::GetFullPath((Join-Path $rootPath $relative))
 if([IO.Path]::IsPathRooted($relative) -or -not $target.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Invalid installation manifest path.'}
 $parent=[IO.Path]::GetDirectoryName($target)
 while($parent.Length -ge $rootPath.Length){if((Test-Path -LiteralPath $parent) -and ((Get-Item -LiteralPath $parent).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Linked installation contents are not supported.'};$parent=[IO.Path]::GetDirectoryName($parent)}
 $targets+=$target
}
if($VerifyOnly){Write-Output ('Validated '+$targets.Count+' removal paths. No changes made.');exit 0}
if([Windows.Forms.MessageBox]::Show('Remove murums Wii Mod Studio? Only packaged program files will be removed. Your projects, exported archives and separately downloaded tools will be kept. Close the editor first.','Uninstall murums Wii Mod Studio','YesNo','Question') -ne 'Yes'){exit 0}
try {
 $exe=Join-Path $rootPath 'murums Wii Mod Studio.exe'
 if(Test-Path -LiteralPath $exe){$lock=[IO.File]::Open($exe,'Open','ReadWrite','None');$lock.Dispose()}
 # The bootstrap that launched this script exits immediately.
 Start-Sleep -Milliseconds 800
 foreach($target in $targets){if(Test-Path -LiteralPath $target -PathType Leaf){Remove-Item -LiteralPath $target -Force}}
 $hash=[Security.Cryptography.SHA256]::Create();$id=([BitConverter]::ToString($hash.ComputeHash([Text.Encoding]::UTF8.GetBytes($rootPath.ToLowerInvariant())))).Replace('-','').Substring(0,16);$hash.Dispose()
 $key='HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\murumsWiiStudio-'+$id
 if(Test-Path $key){Remove-Item -LiteralPath $key}
 $shortcut=Join-Path ([Environment]::GetFolderPath('Programs')) ('murums Wii Mod Studio '+$id+'.lnk')
 if(Test-Path -LiteralPath $shortcut){Remove-Item -LiteralPath $shortcut}
 # The friendly shortcut may belong to another side-by-side installation.
 $friendlyShortcut=Join-Path ([Environment]::GetFolderPath('Programs')) 'murums Wii Mod Studio.lnk'
 if(Test-Path -LiteralPath $friendlyShortcut){
  $linkShell=New-Object -ComObject WScript.Shell
  $link=$null
  try{
   $link=$linkShell.CreateShortcut($friendlyShortcut)
   if([string]::Equals($link.TargetPath,$exe,[StringComparison]::OrdinalIgnoreCase)){Remove-Item -LiteralPath $friendlyShortcut}
  }finally{if($null -ne $link){[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($link)};[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($linkShell)}
 }
 Remove-Item -LiteralPath $manifest
 $directories=@($targets | ForEach-Object { $p=[IO.Path]::GetDirectoryName($_);while($p.Length -gt $rootPath.Length){$p;$p=[IO.Path]::GetDirectoryName($p)} } | Sort-Object -Unique | Sort-Object Length -Descending)
 foreach($dir in $directories){if((Test-Path -LiteralPath $dir) -and [IO.Directory]::GetFileSystemEntries($dir).Length -eq 0){[IO.Directory]::Delete($dir)}}
 if([IO.Directory]::GetFileSystemEntries($rootPath).Length -eq 0){[IO.Directory]::Delete($rootPath)}
 [void][Windows.Forms.MessageBox]::Show('Program removed. Any remaining files are your data or separately installed tools.','Uninstall complete')
} catch {[void][Windows.Forms.MessageBox]::Show(('Uninstall could not finish: '+$_.Exception.Message+' Close the editor and retry.'),'Uninstall','OK','Error');exit 1}
