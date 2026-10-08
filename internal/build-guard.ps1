function Invoke-StudioBuild {
    param([Parameter(Mandatory=$true)][scriptblock]$Action)
    $root = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot)).ToUpperInvariant()
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { $key = [BitConverter]::ToString($algorithm.ComputeHash([Text.Encoding]::UTF8.GetBytes($root))).Replace('-','') }
    finally { $algorithm.Dispose() }
    $mutex = New-Object Threading.Mutex($false, ('Local\murums-studio-build-' + $key))
    $owned = $false
    try {
        try { $owned = $mutex.WaitOne(0) }
        catch [Threading.AbandonedMutexException] { $owned = $true }
        if (-not $owned) { throw 'Another Studio build is running for this project. Retry after it finishes.' }
        & $Action
    }
    finally {
        if ($owned) { $mutex.ReleaseMutex() }
        $mutex.Dispose()
    }
}
