[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$')]
    [string]$Version = '0.1.0',

    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release',

    [ValidateSet('win-x64')]
    [string]$Runtime = 'win-x64',

    [string]$DotnetPath,
    [string]$IsccPath,
    [switch]$SkipTests,
    [switch]$SkipInstaller
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = [IO.Path]::GetFullPath((Join-Path $scriptRoot '..'))
$solutionPath = Join-Path $repoRoot 'CodexSOS.slnx'
$appProject = Join-Path $repoRoot 'src\CodexSOS.App\CodexSOS.App.csproj'
$testProject = Join-Path $repoRoot 'tests\CodexSOS.Tests\CodexSOS.Tests.csproj'
$artifactsRoot = Join-Path $repoRoot 'artifacts'
$stagingRoot = Join-Path $artifactsRoot 'staging'
$portableName = "Codex-SOS-$Version-$Runtime"
$portableDir = Join-Path $artifactsRoot $portableName
$packagesDir = Join-Path $artifactsRoot 'packages'
$zipPath = Join-Path $packagesDir "Codex-SOS-$Version-$Runtime-portable.zip"
$manifestPath = Join-Path $artifactsRoot 'FILE-MANIFEST.txt'
$checksumsPath = Join-Path $artifactsRoot 'SHA256SUMS.txt'
$reportPath = Join-Path $artifactsRoot 'RELEASE-CHECK.md'

function Assert-LastExitCode {
    param([string]$Step)

    if ($LASTEXITCODE -ne 0) {
        throw "$Step failed with exit code $LASTEXITCODE."
    }
}

function Assert-SafeArtifactPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    $candidate = [IO.Path]::GetFullPath($Path)
    $root = [IO.Path]::GetFullPath($artifactsRoot).TrimEnd('\') + '\'
    if (-not $candidate.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Refusing to clean or move a path outside the project artifacts directory.'
    }
}

function Reset-ArtifactDirectory {
    param([Parameter(Mandatory = $true)][string]$Path)

    Assert-SafeArtifactPath -Path $Path
    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
    New-Item -ItemType Directory -Path $Path | Out-Null
}

function Resolve-DotnetExecutable {
    if ($DotnetPath) {
        $candidate = [IO.Path]::GetFullPath($DotnetPath)
        if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            throw 'The supplied dotnet executable does not exist.'
        }
        return $candidate
    }

    if ($env:DOTNET_ROOT) {
        $candidate = Join-Path $env:DOTNET_ROOT 'dotnet.exe'
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return $candidate
        }
    }

    $command = Get-Command dotnet.exe -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    throw 'The .NET 10 SDK is required to build. Install it or pass -DotnetPath. End users do not need the SDK.'
}

function Resolve-IsccExecutable {
    if ($IsccPath) {
        $candidate = [IO.Path]::GetFullPath($IsccPath)
        if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            throw 'The supplied Inno Setup compiler does not exist.'
        }
        return $candidate
    }

    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $registryKeys = @(
        'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1',
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1',
        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1'
    )
    foreach ($key in $registryKeys) {
        if (Test-Path -LiteralPath $key) {
            $installLocation = (Get-ItemProperty -LiteralPath $key -ErrorAction SilentlyContinue).InstallLocation
            if ($installLocation) {
                $candidate = Join-Path $installLocation 'ISCC.exe'
                if (Test-Path -LiteralPath $candidate -PathType Leaf) {
                    return $candidate
                }
            }
        }
    }

    $commonCandidates = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
    )
    foreach ($candidate in $commonCandidates) {
        if ($candidate -and (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            return $candidate
        }
    }

    return $null
}

function Test-IsTextPackageFile {
    param([Parameter(Mandatory = $true)][IO.FileInfo]$File)

    $textExtensions = @('.json', '.xml', '.config', '.txt', '.md', '.yml', '.yaml', '.ini', '.cmd', '.bat', '.ps1')
    return $textExtensions -contains $File.Extension.ToLowerInvariant()
}

function Test-PackagePrivacy {
    param([Parameter(Mandatory = $true)][string]$PackageDirectory)

    $findings = New-Object 'System.Collections.Generic.List[string]'
    $files = @(Get-ChildItem -LiteralPath $PackageDirectory -Recurse -File)
    $forbiddenNames = @('auth.json', 'auth.json.bak')
    $forbiddenSuffixes = @('.pdb', '.log', '.tmp', '.dmp')
    $sensitiveNamePatterns = @('^session.*\.json$', '^conversation.*\.(json|jsonl)$', '^doctor.*\.(json|log)$')

    foreach ($file in $files) {
        $nameLower = $file.Name.ToLowerInvariant()
        if ($forbiddenNames -contains $nameLower) {
            $findings.Add("forbidden file name: $($file.Name)")
        }
        if ($forbiddenSuffixes -contains $file.Extension.ToLowerInvariant()) {
            $findings.Add("temporary or debug file: $($file.Name)")
        }
        foreach ($pattern in $sensitiveNamePatterns) {
            if ($file.Name -match $pattern) {
                $findings.Add("possible private runtime material: $($file.Name)")
            }
        }
    }

    $literalMachineValues = New-Object 'System.Collections.Generic.List[string]'
    foreach ($value in @($env:USERPROFILE, $repoRoot, $artifactsRoot, [IO.Path]::GetTempPath())) {
        if ($value -and $value.Length -ge 4 -and -not $literalMachineValues.Contains($value)) {
            $literalMachineValues.Add($value)
        }
    }

    foreach ($file in $files) {
        $isText = Test-IsTextPackageFile -File $file
        $isProjectBinary = $file.Name -match '^CodexSOS(?:\..+)?\.(?:dll|exe)$'
        if (-not $isText -and -not $isProjectBinary) {
            continue
        }

        $bytes = [IO.File]::ReadAllBytes($file.FullName)
        $utf8View = [Text.Encoding]::UTF8.GetString($bytes)
        $utf16View = [Text.Encoding]::Unicode.GetString($bytes)
        foreach ($literal in $literalMachineValues) {
            $escaped = [regex]::Escape($literal)
            if ([regex]::IsMatch($utf8View, $escaped, [Text.RegularExpressions.RegexOptions]::IgnoreCase) -or
                [regex]::IsMatch($utf16View, $escaped, [Text.RegularExpressions.RegexOptions]::IgnoreCase)) {
                $findings.Add("local machine path embedded in $($file.Name)")
            }
        }

        if (-not $isText) {
            continue
        }

        $content = [IO.File]::ReadAllText($file.FullName)
        if ($env:USERNAME -and $env:USERNAME.Length -ge 3) {
            $userPattern = '(?<![A-Za-z0-9])' + [regex]::Escape($env:USERNAME) + '(?![A-Za-z0-9])'
            if ([regex]::IsMatch($content, $userPattern)) {
                $findings.Add("local account name embedded in $($file.Name)")
            }
        }

        $contentPatterns = @(
            '(?i)-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----',
            '(?i)\b(?:sk|rk|pk)-[A-Za-z0-9_-]{20,}\b',
            '(?i)\bgh[oprsu]_[A-Za-z0-9]{24,}\b',
            '(?i)\bAKIA[0-9A-Z]{16}\b',
            '(?i)\bBearer\s+[A-Za-z0-9._~-]{20,}',
            '(?i)\b(?:api[_-]?key|password|access[_-]?token|client[_-]?secret)\s*[:=]\s*["''][^"'']{12,}["'']',
            '(?i)[A-Z]:\\Users\\[^\\\r\n]+\\(?:Documents|Desktop|AppData)\\'
        )
        foreach ($pattern in $contentPatterns) {
            if ([regex]::IsMatch($content, $pattern)) {
                $findings.Add("secret or private path pattern in $($file.Name)")
            }
        }
    }

    if ($findings.Count -gt 0) {
        $distinct = @($findings | Sort-Object -Unique)
        throw "Package privacy scan failed:`n - $($distinct -join "`n - ")"
    }
}

function Write-PortableManifest {
    param(
        [Parameter(Mandatory = $true)][string]$PackageDirectory,
        [Parameter(Mandatory = $true)][string]$OutputPath
    )

    $base = [IO.Path]::GetFullPath($PackageDirectory).TrimEnd('\') + '\'
    $lines = New-Object 'System.Collections.Generic.List[string]'
    $lines.Add('# Relative path | bytes | SHA-256')
    foreach ($file in (Get-ChildItem -LiteralPath $PackageDirectory -Recurse -File | Sort-Object FullName)) {
        $relative = $file.FullName.Substring($base.Length).Replace('\', '/')
        $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        $lines.Add("$relative | $($file.Length) | $hash")
    }
    [IO.File]::WriteAllLines($OutputPath, $lines, (New-Object Text.UTF8Encoding($false)))
}

if (-not (Test-Path -LiteralPath $solutionPath -PathType Leaf)) {
    throw 'CodexSOS.slnx was not found.'
}

$dotnet = Resolve-DotnetExecutable
New-Item -ItemType Directory -Path $artifactsRoot -Force | Out-Null
Reset-ArtifactDirectory -Path $stagingRoot
Reset-ArtifactDirectory -Path $portableDir
Reset-ArtifactDirectory -Path $packagesDir

Write-Host 'Restoring pinned project dependencies...'
& $dotnet restore $solutionPath
Assert-LastExitCode -Step 'dotnet restore'

if (-not $SkipTests) {
    Write-Host 'Building the executable test harness...'
    & $dotnet build $testProject --configuration $Configuration --no-restore --verbosity minimal "-p:Version=$Version"
    Assert-LastExitCode -Step 'test harness build'

    Write-Host 'Running all executable tests (the real Codex doctor is never invoked by this build)...'
    Push-Location $repoRoot
    try {
        $testOutput = @(& $dotnet run --project $testProject --configuration $Configuration --no-build --no-restore 2>&1)
        $testExitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }
    foreach ($line in $testOutput) {
        Write-Host $line
    }
    if ($testExitCode -ne 0) {
        throw "Executable tests failed with exit code $testExitCode."
    }
    $resultLine = @($testOutput | ForEach-Object { $_.ToString() } | Where-Object { $_ -match '^RESULT:' } | Select-Object -Last 1)
    if ($resultLine.Count -ne 1 -or $resultLine[0] -notmatch '^RESULT: \d+/\d+ passed; 0 failed;') {
        throw 'Executable tests did not report an all-green result marker.'
    }
}

$stageApp = Join-Path $stagingRoot 'app'
New-Item -ItemType Directory -Path $stageApp | Out-Null
Write-Host 'Publishing a self-contained Windows x64 application...'
$pathMap = "$repoRoot=/_/CodexSOS"
& $dotnet publish $appProject --configuration $Configuration --runtime $Runtime --self-contained true --no-restore --output $stageApp `
    "-p:Version=$Version" '-p:PublishTrimmed=false' '-p:PublishSingleFile=false' '-p:PublishReadyToRun=false' `
    '-p:DebugType=None' '-p:DebugSymbols=false' "-p:PathMap=$pathMap"
Assert-LastExitCode -Step 'dotnet publish'

Get-ChildItem -LiteralPath $stageApp -Recurse -File |
    Where-Object { $_.Extension.ToLowerInvariant() -in @('.pdb', '.log', '.tmp', '.dmp') } |
    Remove-Item -Force
$portableReadme = Join-Path $repoRoot 'installer\portable-readme.zh-CN.txt'
if (Test-Path -LiteralPath $portableReadme -PathType Leaf) {
    Copy-Item -LiteralPath $portableReadme -Destination (Join-Path $stageApp '使用说明.txt')
}
foreach ($legalFileName in @('LICENSE', 'THIRD_PARTY_NOTICES.md')) {
    $legalFile = Join-Path $repoRoot $legalFileName
    if (-not (Test-Path -LiteralPath $legalFile -PathType Leaf)) {
        throw "Required distribution notice is missing: $legalFileName"
    }
    Copy-Item -LiteralPath $legalFile -Destination (Join-Path $stageApp $legalFileName)
}
$nugetPackages = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget\packages' }
$tesserNetLicense = Join-Path $nugetPackages 'tessernet\0.8.0\LICENSE'
if (-not (Test-Path -LiteralPath $tesserNetLicense -PathType Leaf)) {
    $tesserNetLicense = Get-ChildItem -LiteralPath (Join-Path $nugetPackages 'tessernet\0.8.0') -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '^LICEN[CS]E(?:\..+)?$' } |
        Select-Object -First 1 -ExpandProperty FullName
}
$thirdPartyLicenseDir = Join-Path $stageApp 'licenses'
New-Item -ItemType Directory -Path $thirdPartyLicenseDir | Out-Null
if ($tesserNetLicense -and (Test-Path -LiteralPath $tesserNetLicense -PathType Leaf)) {
    Copy-Item -LiteralPath $tesserNetLicense -Destination (Join-Path $thirdPartyLicenseDir 'TesserNet-LICENSE.txt')
}
else {
    $licenseNotice = @(
        'TesserNet 0.8.0',
        'Project: https://github.com/CptWesley/TesserNet',
        'Package: https://www.nuget.org/packages/TesserNet/0.8.0',
        'Declared license: Apache-2.0',
        '',
        'The restored NuGet package does not contain a standalone license text.',
        'The package metadata and project source identify Apache License 2.0.',
        'Apache License 2.0: https://www.apache.org/licenses/LICENSE-2.0',
        '',
        'Copyright 2022 Wesley Baartman',
        '',
        'Licensed under the Apache License, Version 2.0 (the "License");',
        'you may not use this file except in compliance with the License.',
        'You may obtain a copy of the License at',
        '',
        '    https://www.apache.org/licenses/LICENSE-2.0',
        '',
        'Unless required by applicable law or agreed to in writing, software',
        'distributed under the License is distributed on an "AS IS" BASIS,',
        'WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.',
        'See the License for the specific language governing permissions and',
        'limitations under the License.',
        '',
        'Before public distribution, verify this notice against the upstream source again.'
    )
    [IO.File]::WriteAllLines(
        (Join-Path $thirdPartyLicenseDir 'TesserNet-LICENSE-NOTICE.txt'),
        $licenseNotice,
        (New-Object Text.UTF8Encoding($false)))
}

if (-not (Test-Path -LiteralPath (Join-Path $stageApp 'CodexSOS.exe') -PathType Leaf)) {
    throw 'Publish did not produce CodexSOS.exe.'
}

Write-Host 'Scanning the publish output for machine paths, private runtime files, and secret patterns...'
Test-PackagePrivacy -PackageDirectory $stageApp

Assert-SafeArtifactPath -Path $stageApp
Assert-SafeArtifactPath -Path $portableDir
Remove-Item -LiteralPath $portableDir -Recurse -Force
Move-Item -LiteralPath $stageApp -Destination $portableDir
Remove-Item -LiteralPath $stagingRoot -Recurse -Force

Write-PortableManifest -PackageDirectory $portableDir -OutputPath $manifestPath

Write-Host 'Creating the portable ZIP...'
Compress-Archive -Path $portableDir -DestinationPath $zipPath -CompressionLevel Optimal -Force

$installerStatus = 'SKIPPED'
$installerNote = 'Inno Setup was not requested.'
$installerPath = $null
if (-not $SkipInstaller) {
    $iscc = Resolve-IsccExecutable
    if ($iscc) {
        $issScript = Join-Path $repoRoot 'installer\CodexSOS.iss'
        Write-Host 'Creating the local per-user installer...'
        $isccArguments = @(
            '/Qp',
            "/DMyAppVersion=$Version",
            "/DSourceDir=$portableDir",
            "/DOutputDir=$packagesDir",
            $issScript
        )
        & $iscc @isccArguments
        Assert-LastExitCode -Step 'Inno Setup compiler'
        $installerPath = Get-ChildItem -LiteralPath $packagesDir -Filter "Codex-SOS-Setup-$Version.exe" -File | Select-Object -First 1
        if (-not $installerPath) {
            throw 'Inno Setup completed but the expected installer was not found.'
        }
        $installerPath = $installerPath.FullName
        $installerStatus = 'PASS'
        $installerNote = 'Unsigned local installer created. No publishing or upload occurred.'
    }
    else {
        $installerNote = 'Inno Setup 6 is not installed. The portable app and ZIP were still created; nothing was downloaded silently.'
        Write-Warning $installerNote
    }
}

$checksumTargets = New-Object 'System.Collections.Generic.List[string]'
$checksumTargets.Add($zipPath)
if ($installerPath) {
    $checksumTargets.Add($installerPath)
}
$checksumLines = New-Object 'System.Collections.Generic.List[string]'
foreach ($target in $checksumTargets) {
    $hash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant()
    $checksumLines.Add("$hash  $([IO.Path]::GetFileName($target))")
}
[IO.File]::WriteAllLines($checksumsPath, $checksumLines, (New-Object Text.UTF8Encoding($false)))

$portableFiles = @(Get-ChildItem -LiteralPath $portableDir -Recurse -File)
$portableBytes = ($portableFiles | Measure-Object -Property Length -Sum).Sum
$zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
$testStatus = if ($SkipTests) { 'SKIPPED by explicit switch' } else { 'PASS' }
$reportLines = @(
    '# Codex SOS release preflight',
    '',
    "- Version: $Version",
    "- Runtime: $Runtime (self-contained; end users do not need .NET or an SDK)",
    "- Automated tests: $testStatus",
    '- Publish: PASS (WPF trimming and single-file bundling are intentionally disabled)',
    '- Package privacy scan: PASS',
    '- Real Codex doctor during build: NOT RUN',
    "- Installer: $installerStatus - $installerNote",
    '- Remote repository creation, upload, and public release: NOT PERFORMED',
    '',
    '## Portable package',
    '',
    "- Folder: $portableName",
    "- Files: $($portableFiles.Count)",
    "- Uncompressed bytes: $portableBytes",
    "- ZIP: $([IO.Path]::GetFileName($zipPath))",
    "- ZIP SHA-256: $zipHash",
    '',
    'See `FILE-MANIFEST.txt` for every packaged file and `SHA256SUMS.txt` for distributable hashes.'
)
[IO.File]::WriteAllLines($reportPath, $reportLines, (New-Object Text.UTF8Encoding($false)))

Write-Host ''
Write-Host 'Release package complete.'
Write-Host "Portable app: $portableDir"
Write-Host "Portable ZIP: $zipPath"
Write-Host "Preflight report: $reportPath"
if ($installerPath) {
    Write-Host "Installer: $installerPath"
}
