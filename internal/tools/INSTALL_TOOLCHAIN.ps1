param(
    [string]$ToolId = "",
    [switch]$All
)

$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$ToolsRoot = $PSScriptRoot
$TempRoot = Join-Path $env:TEMP ("murums_wii_tools_" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force -Path $TempRoot | Out-Null

function Write-Step([string]$Text) {
    Write-Host ""
    Write-Host ("==> " + $Text) -ForegroundColor Cyan
}

function Download-File([string]$Url, [string]$Destination) {
    Write-Host ("Downloading: " + $Url)
    $last = $null
    for ($attempt = 1; $attempt -le 3; $attempt++) {
        try {
            Invoke-WebRequest -UseBasicParsing -Uri $Url -OutFile $Destination -TimeoutSec 120 -Headers @{"User-Agent"="murums-Wii-Mod-Studio/2.1"}
            return
        } catch {
            $last = $_
            Write-Host ("Download attempt " + $attempt + " failed: " + $_.Exception.Message) -ForegroundColor Yellow
            Start-Sleep -Seconds ([Math]::Min(6, $attempt * 2))
        }
    }
    throw ("Download failed after 3 attempts: " + $last.Exception.Message)
}

function Remove-ToolDirectory([string]$Directory) {
    $resolved = [IO.Path]::GetFullPath($Directory).TrimEnd('\')
    $toolsPrefix = [IO.Path]::GetFullPath($ToolsRoot).TrimEnd('\') + '\'
    $tempPrefix = [IO.Path]::GetFullPath($TempRoot).TrimEnd('\') + '\'
    $ownedTemp = $resolved.Equals([IO.Path]::GetFullPath($TempRoot).TrimEnd('\'), [StringComparison]::OrdinalIgnoreCase) -and ([IO.Path]::GetFileName($resolved) -match '^murums_wii_tools_[a-f0-9]{32}$')
    if (-not $ownedTemp -and -not $resolved.StartsWith($toolsPrefix, [StringComparison]::OrdinalIgnoreCase) -and -not $resolved.StartsWith($tempPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Refusing to remove a folder outside the tool workspace.' }
    if (Test-Path -LiteralPath $resolved) {
        if (((Get-Item -LiteralPath $resolved).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Refusing to remove a linked tool directory.' }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}

function Expand-Package([string]$Archive, [string]$Destination) {
    Remove-ToolDirectory $Destination
    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    $ext = [IO.Path]::GetExtension($Archive).ToLowerInvariant()
    if ($ext -eq ".zip") {
        Expand-Archive -Force -Path $Archive -DestinationPath $Destination
        Get-ChildItem -Path $Destination -File -Recurse -ErrorAction SilentlyContinue | Unblock-File -ErrorAction SilentlyContinue
        return
    }
    if ($ext -eq ".7z") {
        $tar = (Get-Command tar.exe -ErrorAction SilentlyContinue)
        if ($null -eq $tar) { throw "Windows tar.exe is required to extract .7z packages." }
        & $tar.Source -xf $Archive -C $Destination
        if ($LASTEXITCODE -ne 0) { throw "tar.exe failed with exit code $LASTEXITCODE." }
        Get-ChildItem -Path $Destination -File -Recurse -ErrorAction SilentlyContinue | Unblock-File -ErrorAction SilentlyContinue
        return
    }
    throw "Unsupported archive type: $ext"
}

function Get-GitHubAsset([string]$Repo, [string[]]$Patterns) {
    $release = Invoke-RestMethod -UseBasicParsing -TimeoutSec 60 -Uri ("https://api.github.com/repos/" + $Repo + "/releases/latest") -Headers @{"User-Agent"="murums-Wii-Mod-Studio/2.1"; "Accept"="application/vnd.github+json"}
    foreach ($pattern in $Patterns) {
        $asset = $release.assets | Where-Object { $_.name -match $pattern } | Select-Object -First 1
        if ($null -ne $asset) { $asset | Add-Member -NotePropertyName ReleaseTag -NotePropertyValue $release.tag_name -Force; return $asset }
    }
    return $null
}

function Install-GitHubArchive([string]$Repo, [string]$FolderName, [string[]]$Patterns, [string[]]$ExpectedExe) {
    Write-Step ("Installing " + $FolderName)
    $asset = Get-GitHubAsset $Repo $Patterns
    if ($null -eq $asset) { throw "No suitable Windows release asset found for $Repo." }
    $ext = [IO.Path]::GetExtension($asset.name)
    $tmp = Join-Path $TempRoot ($FolderName + $ext)
    Download-File $asset.browser_download_url $tmp
    $dest = Join-Path $ToolsRoot $FolderName
    Expand-Package $tmp $dest
    foreach ($exe in $ExpectedExe) {
        $hit = Get-ChildItem -Path $dest -Filter $exe -File -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($null -ne $hit) { Write-Host ("Ready: " + $hit.FullName) -ForegroundColor Green; return }
    }
    throw ("Package extracted, but expected executable was not found in " + $dest)
}

function Install-WiimmSZS {
    Write-Step "Installing Wiimms SZS Tools"
    $tmp = Join-Path $TempRoot "wiimm-szs.zip"
    Download-File "https://szs.wiimm.de/download/szs-v2.42a-r8989-cygwin64.zip" $tmp
    $dest = Join-Path $ToolsRoot "WiimmSZS"
    Expand-Package $tmp $dest
    $hit = Get-ChildItem -Path $dest -Filter "wszst.exe" -File -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -eq $hit) { throw "wszst.exe not found after extraction." }
    Write-Host ("Ready: " + $hit.FullName) -ForegroundColor Green
}

function Install-WiimmWIT {
    Write-Step "Installing Wiimms ISO Tools"
    $tmp = Join-Path $TempRoot "wiimm-wit.zip"
    Download-File "https://wit.wiimm.de/download/wit-v3.05a-r8638-cygwin64.zip" $tmp
    $dest = Join-Path $ToolsRoot "WiimmWIT"
    Expand-Package $tmp $dest
    $hit = Get-ChildItem -Path $dest -Filter "wit.exe" -File -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -eq $hit) { throw "wit.exe not found after extraction." }
    Write-Host ("Ready: " + $hit.FullName) -ForegroundColor Green
}

function Install-RiiStudio {
    Install-GitHubArchive "snailspeed3/RiiStudio" "RiiStudio" @(
        '(?i)(windows|win).*(x64|64).*[.](zip|7z)$',
        '(?i)(windows|win).*[.](zip|7z)$',
        '(?i)[.](zip|7z)$'
    ) @('RiiStudio.exe','rszst.exe')
    $dest = Join-Path $ToolsRoot "RiiStudio"
    $gui = Get-ChildItem -Path $dest -Filter "RiiStudio.exe" -File -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
    $cli = Get-ChildItem -Path $dest -Filter "rszst.exe" -File -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -eq $gui) { throw "RiiStudio.exe not found after extraction." }
    if ($null -eq $cli) { Write-Host "Note: rszst.exe is not included in this RiiStudio package." -ForegroundColor Yellow }
}

function Install-BrawlCrate {
    Write-Step "Installing BrawlCrate"
    # BrawlCrate release assets are portable Windows EXEs, not ZIP/7z archives.
    $asset = Get-GitHubAsset "soopercool101/BrawlCrate" @(
        '(?i)^BrawlCrate[.].*x86[.]exe$',
        '(?i)^BrawlCrate.*[.]exe$',
        '(?i)[.]exe$'
    )
    $downloadUrl = $null
    if ($null -ne $asset) {
        $downloadUrl = $asset.browser_download_url
        $licenseTag = $asset.ReleaseTag
    } else {
        # Fallback for GitHub API/rate-limit quirks. This is the current v0.42 Hotfix 1 portable x86 asset.
        $downloadUrl = "https://github.com/soopercool101/BrawlCrate/releases/download/v0.42h1/BrawlCrate.v0.42h1.x86.exe"
        $licenseTag = "v0.42h1"
        Write-Host "GitHub release metadata did not return the executable; using the known v0.42h1 asset." -ForegroundColor Yellow
    }
    $dest = Join-Path $ToolsRoot "BrawlCrate"
    Remove-ToolDirectory $dest
    New-Item -ItemType Directory -Force -Path $dest | Out-Null
    $target = Join-Path $dest "BrawlCrate.exe"
    Download-File ("https://raw.githubusercontent.com/soopercool101/BrawlCrate/" + [Uri]::EscapeDataString($licenseTag) + "/LICENSE") (Join-Path $dest "LICENSE")
    Download-File $downloadUrl $target
    Unblock-File -LiteralPath $target -ErrorAction SilentlyContinue
    if (-not (Test-Path $target)) { throw "BrawlCrate.exe was not downloaded." }
    $length = (Get-Item -LiteralPath $target).Length
    if ($length -lt 1048576) { throw "Downloaded BrawlCrate executable is unexpectedly small." }
    Write-Host ("Ready: " + $target) -ForegroundColor Green
}

function Install-SwitchToolbox {
    Install-GitHubArchive "KillzXGaming/Switch-Toolbox" "SwitchToolbox" @(
        '(?i)(toolbox|switch).*[.](zip|7z)$',
        '(?i)[.](zip|7z)$'
    ) @('Toolbox.exe','SwitchToolbox.exe','Switch Toolbox.exe')
}

function Install-FFmpeg {
    Write-Step "Installing FFmpeg"
    $tmp = Join-Path $TempRoot "ffmpeg.zip"
    Download-File "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip" $tmp
    $dest = Join-Path $ToolsRoot "FFmpeg"
    Expand-Package $tmp $dest
    $hit = Get-ChildItem -Path $dest -Filter "ffmpeg.exe" -File -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -eq $hit) { throw "ffmpeg.exe not found after extraction." }
    Write-Host ("Ready: " + $hit.FullName) -ForegroundColor Green
}

function Test-Exe([string]$Name) {
    if (Get-Command $Name -ErrorAction SilentlyContinue) { return $true }
    $localHit = Get-ChildItem -Path $ToolsRoot -Filter $Name -File -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -ne $localHit) { return $true }
    if ($env:ProgramFiles) {
        $known = @(
            (Join-Path $env:ProgramFiles ('Wiimm\SZS\' + $Name)),
            (Join-Path $env:ProgramFiles ('Wiimm\WIT\' + $Name)),
            (Join-Path $env:ProgramFiles ('RiiStudio\' + $Name)),
            (Join-Path $env:ProgramFiles ('BrawlCrate\' + $Name))
        )
        foreach ($candidate in $known) { if (Test-Path $candidate) { return $true } }
    }
    if (${env:ProgramFiles(x86)}) {
        $known = @(
            (Join-Path ${env:ProgramFiles(x86)} ('Wiimm\SZS\' + $Name)),
            (Join-Path ${env:ProgramFiles(x86)} ('Wiimm\WIT\' + $Name)),
            (Join-Path ${env:ProgramFiles(x86)} ('RiiStudio\' + $Name)),
            (Join-Path ${env:ProgramFiles(x86)} ('BrawlCrate\' + $Name))
        )
        foreach ($candidate in $known) { if (Test-Path $candidate) { return $true } }
    }
    return $false
}

function Test-ToolInstalled([string]$Id) {
    switch ($Id.ToLowerInvariant()) {
        'wszst' {
            $required = @('wszst.exe','wimgt.exe','wkmpt.exe','wkclt.exe','wbmgt.exe','wpatt.exe','wstrt.exe','wctct.exe','wlect.exe','wmdlt.exe')
            foreach ($name in $required) { if (-not (Test-Exe $name)) { return $false } }
            return $true
        }
        'wit' { return (Test-Exe 'wit.exe') }
        'riistudio' { return (Test-Exe 'RiiStudio.exe') }
        'rszst' { return (Test-Exe 'rszst.exe') }
        'switchtoolbox' { return ((Test-Exe 'Toolbox.exe') -or (Test-Exe 'SwitchToolbox.exe') -or (Test-Exe 'Switch Toolbox.exe')) }
        'brawlcrate' { return (Test-Exe 'BrawlCrate.exe') }
        'ffmpeg' { return (Test-Exe 'ffmpeg.exe') }
        'loopingaudioconverter' { return (Test-Exe 'LoopingAudioConverter.exe') }
        'nintyfont' { return (Test-Exe 'NintyFont.exe') }
    }
    return $false
}

function Install-One([string]$Id) {
    if (Test-ToolInstalled $Id) {
        Write-Host ("Already available: " + $Id) -ForegroundColor DarkGreen
        return
    }
    switch ($Id.ToLowerInvariant()) {
        { $_ -in @('wszst','wimgt','wkmpt','wkclt','wbmgt','wpatt','wstrt','wctct','wlect','wmdlt') } { Install-WiimmSZS; break }
        'wit' { Install-WiimmWIT; break }
        'riistudio' { Install-RiiStudio; break }
        'rszst' { Install-RiiStudio; break }
        'switchtoolbox' { Install-SwitchToolbox; break }
        'brawlcrate' { Install-BrawlCrate; break }
        'ffmpeg' { Install-FFmpeg; break }
        'loopingaudioconverter' { Install-GitHubArchive 'libertyernie/LoopingAudioConverter' 'LoopingAudioConverter' @('(?i)^LoopingAudioConverter.*[.]zip$') @('LoopingAudioConverter.exe'); break }
        'nintyfont' { Install-GitHubArchive 'hadashisora/NintyFont' 'NintyFont' @('(?i)^nintyfont-win.*[.]zip$') @('NintyFont.exe','nintyfont.exe'); break }
        default { throw "No automatic installer is configured for tool id '$Id'." }
    }
}

$requested = New-Object System.Collections.Generic.List[string]
if ($All -or [string]::IsNullOrWhiteSpace($ToolId)) {
    @('wszst','wit','RiiStudio','SwitchToolbox','BrawlCrate','ffmpeg','LoopingAudioConverter','NintyFont') | ForEach-Object { $requested.Add($_) }
} else {
    $ToolId.Split(',') | ForEach-Object {
        $id = $_.Trim()
        if ($id.Length -gt 0) { $requested.Add($id) }
    }
}

# Group aliases so one package is downloaded only once.
$normalized = New-Object System.Collections.Generic.List[string]
$seen = @{}
foreach ($id in $requested) {
    $key = $id.ToLowerInvariant()
    if ($key -in @('wimgt','wkmpt','wkclt','wbmgt','wpatt','wstrt','wctct','wlect','wmdlt')) { $key = 'wszst' }
    if ($key -eq 'rszst') { $key = 'riistudio' }
    if (-not $seen.ContainsKey($key)) { $seen[$key] = $true; $normalized.Add($key) }
}

$failures = 0
foreach ($id in $normalized) {
    try { Install-One $id }
    catch {
        $failures++
        Write-Host ("FAILED: " + $id + " — " + $_.Exception.Message) -ForegroundColor Red
    }
}

try { Remove-ToolDirectory $TempRoot } catch { }
Write-Host ""
if ($failures -eq 0) {
    Write-Host "Toolchain installation finished successfully." -ForegroundColor Green
    exit 0
}
Write-Host ("Toolchain installation finished with " + $failures + " failure(s).") -ForegroundColor Yellow
exit 2
