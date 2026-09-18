param([string]$VerifyLayoutOutput)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'SETUP_OPTIONS.ps1')
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()
if(Test-Path (Join-Path $PSScriptRoot 'SETUP_CHROME.dll')){Add-Type -Path (Join-Path $PSScriptRoot 'SETUP_CHROME.dll')}else{Add-Type -Path (Join-Path $PSScriptRoot 'SETUP_CHROME.cs') -ReferencedAssemblies System.Windows.Forms,System.Drawing}

$Internal = Split-Path -Parent $PSScriptRoot
$Root = Split-Path -Parent $Internal
$Worker = Join-Path $PSScriptRoot 'SETUP_WORKER.ps1'
$ExePath = Join-Path $Root 'murums Wii Mod Studio.exe'
$StatusFile = Join-Path $env:TEMP ('murums_wii_setup_' + [Guid]::NewGuid().ToString('N') + '.status')
$LogFile = Join-Path $env:TEMP ('murums_wii_setup_' + [Guid]::NewGuid().ToString('N') + '.log')
$script:proc = $null
$script:lastLog = ''
$script:installing = $false

function Read-SharedText([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    for ($attempt = 0; $attempt -lt 5; $attempt++) {
        $fs = $null; $sr = $null
        try {
            $fs = New-Object IO.FileStream($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
            $sr = New-Object IO.StreamReader($fs, [Text.Encoding]::UTF8, $true)
            return $sr.ReadToEnd()
        } catch [IO.IOException] {
            Start-Sleep -Milliseconds 10
        } finally {
            if ($sr -ne $null) { try { $sr.Dispose() } catch { } }
            elseif ($fs -ne $null) { try { $fs.Dispose() } catch { } }
        }
    }
    return $null
}

$form = New-Object System.Windows.Forms.Form
$form.Text = 'murums Wii Mod Studio - Setup'
$form.StartPosition = 'CenterScreen'
$form.Size = New-Object System.Drawing.Size(920,820)
$form.MinimumSize = New-Object System.Drawing.Size(860,800)
$form.BackColor = [Drawing.Color]::FromArgb(29,30,37)
$form.ForeColor = [Drawing.Color]::White
$form.Font = New-Object Drawing.Font('Segoe UI',10)
$form.MaximizeBox = $false

$iconPath = Join-Path $Internal 'source\App\murums.ico'
$iconObj = $null
if (Test-Path $iconPath) {
    try {
        $iconObj = New-Object Drawing.Icon($iconPath)
        $form.Icon = $iconObj
    } catch { }
}

$header = New-Object Windows.Forms.Panel
$header.Dock='Top'; $header.Height=120; $header.BackColor=[Drawing.Color]::FromArgb(27,29,36)
if(-not $iconObj -and (Test-Path $ExePath)){$iconObj=[Drawing.Icon]::ExtractAssociatedIcon($ExePath);$form.Icon=$iconObj}
$form.Controls.Add($header)

$logo = New-Object Windows.Forms.PictureBox
$logo.Left=22; $logo.Top=16; $logo.Width=64; $logo.Height=64; $logo.SizeMode='Zoom'; $logo.BackColor=[Drawing.Color]::Transparent
$logoPath = Join-Path $Internal 'assets\murums_logo.png'
if (Test-Path $logoPath) {
    try {
        $bytes = [IO.File]::ReadAllBytes($logoPath)
        $ms = New-Object IO.MemoryStream(,$bytes)
        $tmpImage = [Drawing.Image]::FromStream($ms)
        $logo.Image = New-Object Drawing.Bitmap($tmpImage)
        $tmpImage.Dispose(); $ms.Dispose()
    } catch { }
}
if(-not $logo.Image -and $iconObj){$logo.Image=$iconObj.ToBitmap()}
$header.Controls.Add($logo)

$header.Controls.Remove($logo)
$headerLayout = New-Object Windows.Forms.TableLayoutPanel
$headerLayout.Dock='Fill'; $headerLayout.ColumnCount=3; $headerLayout.RowCount=1
$headerLayout.Padding=New-Object Windows.Forms.Padding(20,14,20,10)
[void]$headerLayout.ColumnStyles.Add((New-Object Windows.Forms.ColumnStyle('Absolute',76)))
[void]$headerLayout.ColumnStyles.Add((New-Object Windows.Forms.ColumnStyle('Percent',100)))
[void]$headerLayout.ColumnStyles.Add((New-Object Windows.Forms.ColumnStyle('Absolute',155)))
$logo.Dock='Fill'; $logo.Margin=New-Object Windows.Forms.Padding(0,8,14,8)
$headerLayout.Controls.Add($logo,0,0)
$textLayout=New-Object Windows.Forms.TableLayoutPanel
$textLayout.Dock='Fill'; $textLayout.ColumnCount=1; $textLayout.RowCount=3
foreach($height in @(22,34,24)){[void]$textLayout.RowStyles.Add((New-Object Windows.Forms.RowStyle('Absolute',$height)))}
$brand=New-Object Windows.Forms.Label
$brand.Text='murums Wii Mod Studio';$brand.Dock='Fill';$brand.Font=New-Object Drawing.Font('Segoe UI',10,[Drawing.FontStyle]::Bold)
$title=New-Object Windows.Forms.Label
$title.Text='Install your modding studio';$title.Dock='Fill';$title.AutoEllipsis=$true;$title.Font=New-Object Drawing.Font('Segoe UI',17,[Drawing.FontStyle]::Bold)
$sub=New-Object Windows.Forms.Label
$sub.Text='Set up the editor and optional modding tools';$sub.Dock='Fill';$sub.AutoEllipsis=$true;$sub.ForeColor=[Drawing.Color]::FromArgb(185,187,198)
$textLayout.Controls.Add($brand,0,0);$textLayout.Controls.Add($title,0,1);$textLayout.Controls.Add($sub,0,2)
$headerLayout.Controls.Add($textLayout,1,0)
$version=New-Object Windows.Forms.Label
$version.Dock='Fill';$version.TextAlign='MiddleRight';$version.Font=New-Object Drawing.Font('Segoe UI',10,[Drawing.FontStyle]::Bold)
$versionPath=Join-Path $Internal 'source\App\StudioVersion.cs'
$version.Text='Setup'
if(Test-Path $ExePath){$version.Text='v'+[Diagnostics.FileVersionInfo]::GetVersionInfo($ExePath).ProductVersion}
if(Test-Path -LiteralPath $versionPath){$versionMatch=[regex]::Match([IO.File]::ReadAllText($versionPath),'Current\s*=\s*"([^"]+)"');if($versionMatch.Success){$version.Text='v'+$versionMatch.Groups[1].Value}}
$headerLayout.Controls.Add($version,2,0)
$header.Controls.Add($headerLayout)
$accent=New-Object murumsWiiModStudio.Setup.AccentStrip
$header.Controls.Add($accent);$headerLayout.BringToFront()

$panel = New-Object Windows.Forms.Panel
$panel.Left=0; $panel.Top=120
$panel.Width=$form.ClientSize.Width; $panel.Height=$form.ClientSize.Height-120
$panel.Anchor='Top,Bottom,Left,Right'
$panel.Padding=New-Object Windows.Forms.Padding(22,16,22,16)
$form.Controls.Add($panel); $header.BringToFront()

$info = New-Object Windows.Forms.Label
$info.Text="Already included: HUD editing, textures, Font Changer and WAV loop preview.`r`nChoose extra tools only for the tasks below. Uncheck all to install just murums Wii Mod Studio."
$info.Left=22; $info.Top=14; $info.Width=748; $info.Height=48; $info.ForeColor=[Drawing.Color]::FromArgb(220,220,225)
$panel.Controls.Add($info)

$toolCatalog = @(Get-SetupToolCatalog)
$tools = New-Object Windows.Forms.DataGridView
$tools.Left=22; $tools.Top=68; $tools.Width=720; $tools.Height=324
$tools.AllowUserToAddRows=$false; $tools.AllowUserToDeleteRows=$false; $tools.AllowUserToResizeRows=$false
$tools.RowHeadersVisible=$false; $tools.MultiSelect=$false; $tools.SelectionMode='FullRowSelect'
$tools.BackgroundColor=[Drawing.Color]::FromArgb(22,23,28); $tools.BorderStyle='FixedSingle'
$tools.EnableHeadersVisualStyles=$false; $tools.ColumnHeadersHeight=32; $tools.ColumnHeadersHeightSizeMode='DisableResizing'
$tools.ColumnHeadersDefaultCellStyle.BackColor=[Drawing.Color]::FromArgb(42,40,54)
$tools.ColumnHeadersDefaultCellStyle.ForeColor=[Drawing.Color]::White
$tools.DefaultCellStyle.BackColor=[Drawing.Color]::FromArgb(25,26,33)
$tools.DefaultCellStyle.ForeColor=[Drawing.Color]::White
$tools.DefaultCellStyle.SelectionBackColor=[Drawing.Color]::FromArgb(65,48,95)
$tools.DefaultCellStyle.SelectionForeColor=[Drawing.Color]::White
$tools.AlternatingRowsDefaultCellStyle.BackColor=[Drawing.Color]::FromArgb(32,33,41)
$tools.DefaultCellStyle.Padding=New-Object Windows.Forms.Padding(6,3,6,3)
$tools.DefaultCellStyle.WrapMode='True'; $tools.DefaultCellStyle.Alignment='MiddleLeft'
$tools.GridColor=[Drawing.Color]::FromArgb(55,57,69)
$tools.RowTemplate.Height=36; $tools.AccessibleName='Choose extra tools'
$check=New-Object Windows.Forms.DataGridViewCheckBoxColumn
$check.Name='Install';$check.HeaderText='';$check.Width=34
[void]$tools.Columns.Add($check)
foreach($spec in @(@('Tool',210),@('When to install',145),@('What it adds',0))){
 $column=New-Object Windows.Forms.DataGridViewTextBoxColumn
 $column.Name=$spec[0];$column.HeaderText=$spec[0];$column.ReadOnly=$true;$column.SortMode='NotSortable'
 if($spec[1] -eq 0){$column.AutoSizeMode='Fill'}else{$column.Width=$spec[1]}
 [void]$tools.Columns.Add($column)
}
foreach($entry in $toolCatalog){[void]$tools.Rows.Add([object[]]@($entry.Default,$entry.Name,$entry.Recommendation,$entry.Details))}
$tools.add_CurrentCellDirtyStateChanged({if($tools.IsCurrentCellDirty){[void]$tools.CommitEdit([Windows.Forms.DataGridViewDataErrorContexts]::Commit)}})
$panel.Controls.Add($tools)

$status = New-Object Windows.Forms.Label
$status.Text='Ready to install.'; $status.Left=22; $status.Top=402; $status.Width=748; $status.Height=28
$status.ForeColor=[Drawing.Color]::FromArgb(194,194,205)
$panel.Controls.Add($status)

$progress = New-Object Windows.Forms.ProgressBar
$progress.Left=22; $progress.Top=432; $progress.Width=748; $progress.Height=18; $progress.Style='Continuous'; $progress.Value=0
$panel.Controls.Add($progress)

$log = New-Object Windows.Forms.TextBox
$log.Left=22; $log.Top=462; $log.Width=748; $log.Height=330; $log.Multiline=$true; $log.ScrollBars='None'; $log.ReadOnly=$true
$log.BackColor=[Drawing.Color]::FromArgb(22,23,28); $log.ForeColor=[Drawing.Color]::FromArgb(215,215,220); $log.BorderStyle='FixedSingle'
$log.Font=New-Object Drawing.Font('Consolas',9)
$log.Anchor='Top,Bottom,Left,Right'
$panel.Controls.Add($log)

$install = New-Object Windows.Forms.Button
$install.Text='Install'; $install.Left=476; $install.Top=494; $install.Width=140; $install.Height=40
$install.Anchor='Bottom,Right'; $install.FlatStyle='Flat'; $install.BackColor=[Drawing.Color]::FromArgb(139,92,246); $install.ForeColor=[Drawing.Color]::White
$panel.Controls.Add($install)

$start = New-Object Windows.Forms.Button
$start.Text='Launch program'; $start.Left=324; $start.Top=494; $start.Width=140; $start.Height=40
$start.Anchor='Bottom,Right'; $start.FlatStyle='Flat'; $start.BackColor=[Drawing.Color]::FromArgb(139,92,246); $start.ForeColor=[Drawing.Color]::White; $start.Visible=(Test-Path $ExePath)
$panel.Controls.Add($start)
if (Test-Path -LiteralPath $ExePath) { $install.Text='Reinstall' }

$close = New-Object Windows.Forms.Button
$close.Text='Close'; $close.Left=628; $close.Top=494; $close.Width=142; $close.Height=40
$close.Anchor='Bottom,Right'; $close.FlatStyle='Flat'; $close.BackColor=[Drawing.Color]::FromArgb(45,47,57); $close.ForeColor=[Drawing.Color]::White
$panel.Controls.Add($close)
$layoutContent={
 $width=[Math]::Max(100,$panel.ClientSize.Width-44)
 foreach($control in @($info,$tools,$status,$progress,$log)){$control.Width=$width}
 $close.Left=$panel.ClientSize.Width-22-$close.Width
 $install.Left=$close.Left-12-$install.Width
 $start.Left=$install.Left-12-$start.Width
 foreach($button in @($start,$install,$close)){$button.Top=$panel.ClientSize.Height-16-$button.Height}
 $log.Height=[Math]::Max(80,$install.Top-16-$log.Top)
}
$panel.add_Resize($layoutContent)
& $layoutContent

$timer = New-Object Windows.Forms.Timer
$timer.Interval = 300
$timer.add_Tick({
    try {
        $statusText = Read-SharedText $StatusFile
        if (-not [string]::IsNullOrWhiteSpace($statusText)) {
            $lines = $statusText -split '\r?\n'
            if ($lines.Length -ge 2) {
                $pct = 0
                if ([int]::TryParse($lines[0], [ref]$pct)) {
                    if ($pct -ge 0 -and $pct -le 100) { $progress.Value = $pct }
                    elseif ($pct -lt 0) { $progress.Value = 0 }
                }
                if (-not [string]::IsNullOrWhiteSpace($lines[1])) { $status.Text = $lines[1] }
            }
        }
        $txt = Read-SharedText $LogFile
        if ($null -ne $txt -and $txt -ne $script:lastLog) {
            $script:lastLog = $txt
            $log.ScrollBars='Vertical'
            $log.Text = $txt
            $log.SelectionStart = $log.Text.Length
            $log.ScrollToCaret()
        }
        if ($script:proc -ne $null -and $script:proc.HasExited) {
            $timer.Stop()
            $code = $script:proc.ExitCode
            $script:proc.Dispose(); $script:proc = $null; $script:installing = $false
            $install.Enabled = $true; $tools.Enabled = $true
            if ((Test-Path $ExePath)) { $start.Visible=$true }
            if ($code -eq 0 -and (Test-Path $ExePath)) {
                $status.Text='Installation completed.'
                $progress.Value=100
                $install.Text='Reinstall'
            } elseif (Test-Path $ExePath) {
                $status.Text='Update failed. The previous program is still available; see the log.'
                $progress.Value=0
                [Windows.Forms.MessageBox]::Show($form,'The update failed. Your previous program was preserved. Close the editor before retrying; see the log for details.','murums Wii Mod Studio Setup',[Windows.Forms.MessageBoxButtons]::OK,[Windows.Forms.MessageBoxIcon]::Warning) | Out-Null
            } else {
                $status.Text='Setup failed. See the log for details.'
                [Windows.Forms.MessageBox]::Show($form,'Setup failed. Copy the log text from this window if you need help.','murums Wii Mod Studio Setup',[Windows.Forms.MessageBoxButtons]::OK,[Windows.Forms.MessageBoxIcon]::Error) | Out-Null
            }
        }
    } catch { }
})

$install.add_Click({
    if ($script:proc -ne $null) { return }
    if (-not (Test-Path $Worker)) {
        [Windows.Forms.MessageBox]::Show($form,'The internal setup worker is missing. Please fully extract the ZIP.','murums Wii Mod Studio Setup',[Windows.Forms.MessageBoxButtons]::OK,[Windows.Forms.MessageBoxIcon]::Error) | Out-Null
        return
    }
    Remove-Item -LiteralPath $StatusFile -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $LogFile -Force -ErrorAction SilentlyContinue
    $log.Clear(); $script:lastLog=''; $progress.Value=0
    $install.Enabled=$false; $tools.Enabled=$false; $start.Visible=$false
    $status.Text='Starting setup...'

    $ps = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
    if (-not (Test-Path $ps)) { $ps='powershell.exe' }
    $selectedTools = (@(for($i=0;$i -lt $tools.Rows.Count;$i++){if($tools.Rows[$i].Cells[0].Value){$toolCatalog[$i].Id}}) -join ',')
    $installTools = if ($selectedTools.Length -gt 0) { 1 } else { 0 }
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $ps
    $psi.Arguments = '-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "' + $Worker + '" -Root "' + $Root + '" -StatusFile "' + $StatusFile + '" -LogFile "' + $LogFile + '" -InstallTools ' + $installTools
    if($installTools -ne 0){$psi.Arguments += ' -SelectedTools "' + $selectedTools + '"'}
    $psi.WorkingDirectory = $Root
    $psi.UseShellExecute = $false; $psi.CreateNoWindow = $true; $psi.WindowStyle = [System.Diagnostics.ProcessWindowStyle]::Hidden
    $script:proc = New-Object System.Diagnostics.Process; $script:proc.StartInfo = $psi
    [void]$script:proc.Start(); $script:installing=$true; $timer.Start()
})

$start.add_Click({
    try { if (Test-Path $ExePath) { Start-Process -FilePath $ExePath -WorkingDirectory $Root; $form.Close() } }
    catch { [Windows.Forms.MessageBox]::Show($form,$_.Exception.Message,'murums Wii Mod Studio Setup',[Windows.Forms.MessageBoxButtons]::OK,[Windows.Forms.MessageBoxIcon]::Error) | Out-Null }
})
$close.add_Click({ $form.Close() })
$form.add_FormClosing({
    if ($script:proc -ne $null -and -not $script:proc.HasExited) {
        $r=[Windows.Forms.MessageBox]::Show($form,'Installation is still running. Close setup anyway?','murums Wii Mod Studio Setup',[Windows.Forms.MessageBoxButtons]::YesNo,[Windows.Forms.MessageBoxIcon]::Question)
        if ($r -ne [Windows.Forms.DialogResult]::Yes) { $_.Cancel=$true }
        else { try { $script:proc.Kill() } catch { } }
    }
})
$form.add_FormClosed({
    $timer.Stop(); $timer.Dispose(); $accent.Dispose()
    Remove-Item -LiteralPath $StatusFile -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $LogFile -Force -ErrorAction SilentlyContinue
    if ($logo.Image -ne $null) { $logo.Image.Dispose() }
    if ($iconObj -ne $null) { $iconObj.Dispose() }
})

if($VerifyLayoutOutput){
 [void][IO.Directory]::CreateDirectory($VerifyLayoutOutput)
 $form.ShowInTaskbar=$false;$form.StartPosition='Manual';$form.Location=New-Object Drawing.Point(-30000,-30000)
 $form.Show();[Windows.Forms.Application]::DoEvents()
 if($accent.Height -ne 4 -or -not $accent.Visible){throw 'Missing animated accent strip'}
 foreach($size in @((New-Object Drawing.Size(920,820)),(New-Object Drawing.Size(860,800)))){
  $form.Size=$size;[Windows.Forms.Application]::DoEvents()
  foreach($button in @($start,$install,$close,$tools,$status,$progress,$log)){if(-not $panel.ClientRectangle.Contains($button.Bounds)){throw 'Clipped setup button'}}
  for($frame=0;$frame -lt 2;$frame++){
   $until=[DateTime]::UtcNow.AddMilliseconds(700)
   while([DateTime]::UtcNow -lt $until){[Windows.Forms.Application]::DoEvents();Start-Sleep -Milliseconds 20}
   $bitmap=New-Object Drawing.Bitmap($form.Width,$form.Height)
   $form.DrawToBitmap($bitmap,(New-Object Drawing.Rectangle(0,0,$form.Width,$form.Height)))
   $bitmap.Save((Join-Path $VerifyLayoutOutput ($size.Width.ToString()+'-'+$frame+'.png')));$bitmap.Dispose()
  }
 }
 $form.Close();$form.Dispose()
 Write-Output 'PASS setup layout at default/minimum sizes; animation frames captured; no installer worker started.'
}else{[void]$form.ShowDialog();$form.Dispose()}

