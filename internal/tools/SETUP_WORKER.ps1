param(
    [Parameter(Mandatory=$true)][string]$Root,
    [Parameter(Mandatory=$true)][string]$StatusFile,
    [Parameter(Mandatory=$true)][string]$LogFile,
    [int]$InstallTools = 1,
    [string]$SelectedTools = "wszst,wit"
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'SETUP_OPTIONS.ps1')

# The setup GUI polls these files while this worker writes them.
# Always open them with FileShare.ReadWrite so a 300 ms UI poll can never abort setup.
function Write-SharedText([string]$Path, [string]$Text, [bool]$Append) {
    $mode = if ($Append) { [IO.FileMode]::Append } else { [IO.FileMode]::Create }
    $encoding = New-Object Text.UTF8Encoding($true)
    for ($attempt = 0; $attempt -lt 20; $attempt++) {
        $fs = $null; $sw = $null
        try {
            $fs = New-Object IO.FileStream($Path, $mode, [IO.FileAccess]::Write, [IO.FileShare]::ReadWrite)
            $sw = New-Object IO.StreamWriter($fs, $encoding)
            $sw.Write($Text)
            $sw.Flush()
            return $true
        } catch [IO.IOException] {
            Start-Sleep -Milliseconds 20
        } finally {
            if ($sw -ne $null) { try { $sw.Dispose() } catch { } }
            elseif ($fs -ne $null) { try { $fs.Dispose() } catch { } }
        }
    }
    return $false
}

function Write-Log([string]$Text) {
    $stamp = Get-Date -Format 'HH:mm:ss'
    [void](Write-SharedText $LogFile ("[$stamp] " + $Text + [Environment]::NewLine) $true)
}
function Append-RawLog([string]$Text) {
    if ([string]::IsNullOrWhiteSpace($Text)) { return }
    [void](Write-SharedText $LogFile ($Text.TrimEnd() + [Environment]::NewLine) $true)
}
function Write-StatusFile([int]$Percent, [string]$Text) {
    # Status display is best-effort only. A transient file access race must never fail installation.
    [void](Write-SharedText $StatusFile (([string]$Percent) + [Environment]::NewLine + $Text + [Environment]::NewLine) $false)
}
function Set-Status([int]$Percent, [string]$Text) { Write-StatusFile $Percent $Text; Write-Log $Text }

function Find-Csc {
    $candidates = @(
        (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'),
        (Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe')
    )
    foreach ($p in $candidates) { if (Test-Path $p) { return $p } }
    $cmd = Get-Command csc.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    return $null
}

function Run-Hidden([string]$FileName, [string]$Arguments, [string]$WorkingDirectory, [int]$TimeoutSeconds = 120) {
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $FileName; $psi.Arguments = $Arguments; $psi.WorkingDirectory = $WorkingDirectory
    $psi.UseShellExecute = $false; $psi.CreateNoWindow = $true; $psi.WindowStyle = [System.Diagnostics.ProcessWindowStyle]::Hidden
    $psi.RedirectStandardOutput = $true; $psi.RedirectStandardError = $true
    $p = New-Object System.Diagnostics.Process; $p.StartInfo = $psi; [void]$p.Start()
    $stdoutTask = $p.StandardOutput.ReadToEndAsync(); $stderrTask = $p.StandardError.ReadToEndAsync()
    if (-not $p.WaitForExit($TimeoutSeconds * 1000)) {
        try { $p.Kill() } catch { }
        return @(124, '', ('Timed out after ' + $TimeoutSeconds + ' seconds.'))
    }
    $stdout = $stdoutTask.Result; $stderr = $stderrTask.Result
    return @($p.ExitCode, $stdout, $stderr)
}

function Start-ToolInstallerProcess([string]$PowerShell, [string]$Script, [string]$ToolId, [string]$Label) {
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $PowerShell
    $psi.Arguments = '-NoProfile -ExecutionPolicy Bypass -File "' + $Script + '" -ToolId "' + $ToolId + '"'
    $psi.WorkingDirectory = $Root
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $psi.WindowStyle = [System.Diagnostics.ProcessWindowStyle]::Hidden
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true

    $p = New-Object System.Diagnostics.Process
    $p.StartInfo = $psi
    [void]$p.Start()

    return [PSCustomObject]@{
        Label = $Label
        ToolId = $ToolId
        Process = $p
        StdoutTask = $p.StandardOutput.ReadToEndAsync()
        StderrTask = $p.StandardError.ReadToEndAsync()
    }
}

function Install-ToolchainParallel([string]$PowerShell, [string]$Script, [object[]]$Jobs) {
    if ($null -eq $Jobs -or $Jobs.Count -eq 0) { return 0 }

    $running = New-Object System.Collections.Generic.List[object]
    foreach ($job in $Jobs) {
        $item = Start-ToolInstallerProcess $PowerShell $Script ([string]$job[0]) ([string]$job[1])
        [void]$running.Add($item)
    }

    $sw = [Diagnostics.Stopwatch]::StartNew()
    $timeoutSeconds = 420
    while ($true) {
        $done = 0
        foreach ($item in $running) {
            if ($item.Process.HasExited) { $done++ }
        }

        $pct = 58 + [int][Math]::Floor(36.0 * $done / $running.Count)
        $elapsed = [int]$sw.Elapsed.TotalSeconds
        Write-StatusFile $pct ("Installing optional modding tools in parallel ... {0}/{1} finished - {2}s" -f $done, $running.Count, $elapsed)

        if ($done -ge $running.Count) { break }
        if ($sw.Elapsed.TotalSeconds -ge $timeoutSeconds) {
            Write-Log 'Toolchain installation reached the time limit. Remaining tools will be skipped.'
            foreach ($item in $running) {
                if (-not $item.Process.HasExited) {
                    try { $item.Process.Kill() } catch { }
                }
            }
            break
        }
        Start-Sleep -Milliseconds 400
    }

    $failures = 0
    foreach ($item in $running) {
        try {
            if (-not $item.Process.HasExited) {
                try { $item.Process.Kill() } catch { }
                $failures++
                Write-Log ($item.Label + ' timed out.')
                continue
            }

            $stdout = $item.StdoutTask.Result
            $stderr = $item.StderrTask.Result
            if (-not [string]::IsNullOrWhiteSpace($stdout)) {
                Append-RawLog $stdout
            }
            if (-not [string]::IsNullOrWhiteSpace($stderr)) {
                Append-RawLog $stderr
            }

            if ($item.Process.ExitCode -eq 0) {
                Write-Log ($item.Label + ' ready.')
            } else {
                $failures++
                Write-Log ($item.Label + ' could not be installed (ExitCode ' + $item.Process.ExitCode + ').')
            }
        } finally {
            try { $item.Process.Dispose() } catch { }
        }
    }
    return $failures
}

function Build-ResponseFile([string]$Path, [string]$Output) {
    $sourceRoot = Join-Path $Root 'internal\source'
    $sources = @(Get-ChildItem -LiteralPath $sourceRoot -Filter '*.cs' -Recurse | Sort-Object FullName)
    if ($sources.Count -eq 0) { throw 'No source files found. Please fully extract the package.' }
    $lines = @(
        '/nologo','/codepage:65001','/target:winexe','/optimize+','/platform:anycpu','/main:murumsWiiModStudio.Program',
        ('/out:"' + $Output + '"'),
        '/win32icon:"internal\source\App\murums.ico"',
        '/win32manifest:"internal\source\App\app.manifest"',
        '/reference:System.dll','/reference:System.Core.dll','/reference:System.Web.Extensions.dll','/reference:System.Drawing.dll','/reference:System.Windows.Forms.dll'
    )
    $lines += @($sources | ForEach-Object { '"' + $_.FullName + '"' })
    [IO.File]::WriteAllLines($Path, $lines, (New-Object Text.UTF8Encoding($true)))
}

try {
    if (Test-Path $LogFile) { Remove-Item -LiteralPath $LogFile -Force }
    $jobs = @(Get-SetupToolJobs $(if($InstallTools -ne 0){$SelectedTools}else{''}))
    Set-Status 2 'Preparing setup...'
    $exePath = Join-Path $Root 'murums Wii Mod Studio.exe'
    if(Test-Path (Join-Path $Root 'internal/source/App/Program.cs')) {
    $csc = Find-Csc
    if (-not $csc) { throw '.NET Framework 4.x C# compiler (csc.exe) was not found.' }
    Set-Status 8 ('Compiler found: ' + $csc)

    $exePath = Join-Path $Root 'murums Wii Mod Studio.exe'
    $stagedExe = Join-Path $Root ('murums-setup-' + [Guid]::NewGuid().ToString('N') + '.exe')
    $rsp = Join-Path $env:TEMP ('murums_wii_' + [Guid]::NewGuid().ToString('N') + '.rsp')
    try {
        Build-ResponseFile $rsp $stagedExe
        Set-Status 15 'Building program...'
        $result = Run-Hidden $csc ('@"' + $rsp + '"') $Root 180
        $exitCode = [int]$result[0]
        $combined = (([string]$result[1]) + [Environment]::NewLine + ([string]$result[2])).Trim()
        if ($combined) { Append-RawLog $combined }
        if ($exitCode -ne 0 -or -not (Test-Path $stagedExe)) { throw ('Build failed (ExitCode ' + $exitCode + ').') }
        if (Test-Path -LiteralPath $exePath) {
            $backupDir = Join-Path $Root 'internal\backups'
            [IO.Directory]::CreateDirectory($backupDir) | Out-Null
            $backup = Join-Path $backupDir ('before-setup-' + [Guid]::NewGuid().ToString('N') + '.exe.bak')
            [IO.File]::Replace($stagedExe, $exePath, $backup)
        } else { [IO.File]::Move($stagedExe, $exePath) }
    } finally {
        Remove-Item -LiteralPath $rsp -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $stagedExe -Force -ErrorAction SilentlyContinue
    }
    Set-Status 55 'Program built successfully.'

    } else {
        if(-not (Test-Path -LiteralPath $exePath)){throw 'Packaged program is missing. Run the setup EXE again.'}
        [void][Reflection.AssemblyName]::GetAssemblyName($exePath)
        Set-Status 55 'Packaged program is ready. No compilation needed.'
    }

    $toolFailures = 0
    if ($jobs.Count -gt 0) {
        $toolInstaller = Join-Path $Root 'internal\tools\INSTALL_TOOLCHAIN.ps1'
        if (Test-Path $toolInstaller) {
            $ps = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'; if (-not (Test-Path $ps)) { $ps='powershell.exe' }
            Set-Status 58 'Checking and installing optional modding tools...'
            $toolFailures = Install-ToolchainParallel $ps $toolInstaller $jobs
            $toolReady = $jobs.Count - $toolFailures
            if ($toolFailures -eq 0) {
                Set-Status 96 'Selected modding tools are ready.'
            } else {
                Set-Status 96 (('Editor is ready. {0}/{1} optional tools are available; details are in the log.' -f $toolReady, $jobs.Count))
            }
        } else {
            $toolFailures++
            Set-Status 96 'Program is ready; toolchain installer is missing.'
        }
    } else { Set-Status 96 'Toolchain installation was skipped.' }

    if ($toolFailures -gt 0) {
        Set-Status 100 'Setup complete. The editor is ready; optional tools can be retried later from Tools > Toolchain.'
    } else {
        Set-Status 100 'Setup complete. murums Wii Mod Studio is ready to launch.'
    }
    # Optional backend download failures must never make a successful editor build
    # look like a failed installation. The Toolchain window can retry them later.
    exit 0
}
catch {
    Write-Log ('ERROR: ' + $_.Exception.Message)
    Write-StatusFile -1 ('Setup failed: ' + $_.Exception.Message)
    exit 1
}
