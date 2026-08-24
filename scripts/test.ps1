[CmdletBinding()]
param(
    [string]$DotnetPath,
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = [IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) '..'))
$solutionPath = Join-Path $repoRoot 'CodexSOS.slnx'
$testProject = Join-Path $repoRoot 'tests\CodexSOS.Tests\CodexSOS.Tests.csproj'

if (-not $DotnetPath) {
    if ($env:DOTNET_ROOT -and (Test-Path -LiteralPath (Join-Path $env:DOTNET_ROOT 'dotnet.exe'))) {
        $DotnetPath = Join-Path $env:DOTNET_ROOT 'dotnet.exe'
    }
    else {
        $command = Get-Command dotnet.exe -ErrorAction SilentlyContinue
        if ($command) {
            $DotnetPath = $command.Source
        }
    }
}

if (-not $DotnetPath -or -not (Test-Path -LiteralPath $DotnetPath -PathType Leaf)) {
    throw 'The .NET 10 SDK is required for tests. Install it or pass -DotnetPath.'
}
$DotnetPath = [IO.Path]::GetFullPath($DotnetPath)

& $DotnetPath restore $solutionPath
if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }

& $DotnetPath build $testProject --configuration $Configuration --no-restore --verbosity minimal
if ($LASTEXITCODE -ne 0) { throw 'The executable test harness did not build.' }

Push-Location $repoRoot
try {
    $testOutput = @(& $DotnetPath run --project $testProject --configuration $Configuration --no-build --no-restore 2>&1)
    $testExitCode = $LASTEXITCODE
}
finally {
    Pop-Location
}
foreach ($line in $testOutput) {
    Write-Host $line
}
if ($testExitCode -ne 0) { throw 'The executable test harness reported a failure.' }

$resultLine = @($testOutput | ForEach-Object { $_.ToString() } | Where-Object { $_ -match '^RESULT:' } | Select-Object -Last 1)
if ($resultLine.Count -ne 1 -or $resultLine[0] -notmatch '^RESULT: \d+/\d+ passed; 0 failed;') {
    throw 'The executable test harness did not report an all-green result marker.'
}

Write-Host ('All executable tests passed. ' + $resultLine[0] + ' The real Codex doctor was not invoked.')
