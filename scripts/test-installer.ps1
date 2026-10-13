[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$Version)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+-(?:preview\.[1-9]\d*|ci\.\d+)$') {
    throw 'Installer smoke test accepts only isolated preview/CI versions, never a stable installation.'
}
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$installer = Join-Path $root "artifacts/packages/Codex-SOS-Setup-$Version.exe"
$manifest = Join-Path $root 'artifacts/FILE-MANIFEST.txt'
if (-not (Test-Path -LiteralPath $installer) -or -not (Test-Path -LiteralPath $manifest)) { throw 'Candidate or manifest is missing.' }
$previewKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{BAE3C630-1755-496C-91D3-C2BFAE9D99BB}_is1'
if (Test-Path -LiteralPath $previewKey) { throw 'A preview is already installed; refusing to alter it for testing.' }
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('CodexSOS-Installer-Fixture-' + [Guid]::NewGuid().ToString('N'))
$target = [IO.Path]::GetFullPath((Join-Path $testRoot 'app'))
if (-not $target.StartsWith([IO.Path]::GetFullPath($testRoot) + [IO.Path]::DirectorySeparatorChar)) { throw 'Unsafe test path.' }
New-Item -ItemType Directory -Path $testRoot | Out-Null
function Invoke-CandidateProcess([string]$File, [string[]]$Arguments) {
    $start = [Diagnostics.ProcessStartInfo]::new($File)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($start)
    if (-not $process.WaitForExit(120000)) { throw 'Candidate process exceeded the wait limit; no unrelated process was stopped.' }
    if ($process.ExitCode -ne 0) { throw "Candidate process failed with exit code $($process.ExitCode)." }
}
Invoke-CandidateProcess $installer @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/NOICONS','/TASKS=',"/DIR=$target","/LOG=$testRoot/install.log")
$count = 0
try {
    foreach ($line in Get-Content -LiteralPath $manifest) {
        if ($line.StartsWith('#') -or [string]::IsNullOrWhiteSpace($line)) { continue }
        $parts = $line -split ' \| '
        if ($parts.Count -ne 3) { throw 'Invalid manifest line.' }
        $file = [IO.Path]::GetFullPath((Join-Path $target $parts[0]))
        if (-not $file.StartsWith($target + [IO.Path]::DirectorySeparatorChar)) { throw 'Manifest escaped the isolated target.' }
        if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw "Missing installed file: $($parts[0])" }
        if ((Get-Item -LiteralPath $file).Length -ne [long]$parts[1] -or (Get-FileHash -LiteralPath $file).Hash.ToLowerInvariant() -ne $parts[2]) { throw "Installed file mismatch: $($parts[0])" }
        $count++
    }
    if ($count -lt 200) { throw 'Manifest unexpectedly small.' }
}
finally {
    $uninstaller = Join-Path $target 'unins000.exe'
    if (Test-Path -LiteralPath $uninstaller) {
        Invoke-CandidateProcess $uninstaller @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',"/LOG=$testRoot/uninstall.log")
    }
}
if (Test-Path -LiteralPath (Join-Path $target 'CodexSOS.exe')) { throw 'Application remains after uninstall.' }
if (Test-Path -LiteralPath $previewKey) { throw 'Preview uninstall registration remains.' }
Write-Host "PASS: isolated per-user install, $count file hashes, and uninstall. No real-account diagnostic was run."
