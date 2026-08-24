[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = [IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) '..'))
$findings = New-Object 'System.Collections.Generic.List[string]'
$textExtensions = @('.cs', '.xaml', '.csproj', '.props', '.targets', '.json', '.xml', '.config', '.txt', '.md', '.yml', '.yaml', '.ps1', '.iss', '.cmd', '.bat')

$files = @(Get-ChildItem -LiteralPath $repoRoot -Recurse -File | Where-Object {
    $relative = $_.FullName.Substring($repoRoot.Length).TrimStart('\').Replace('\', '/')
    $excluded = $relative -match '^(?:\.git|artifacts|work|output|state)/' -or
        $relative -match '/(?:bin|obj)/'
    (-not $excluded) -and ($textExtensions -contains $_.Extension.ToLowerInvariant())
})

$patterns = @(
    @{ Name = 'private key'; Pattern = '(?i)-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----' },
    @{ Name = 'OpenAI-style secret'; Pattern = '(?i)\b(?:sk|rk|pk)-[A-Za-z0-9_-]{20,}\b' },
    @{ Name = 'GitHub token'; Pattern = '(?i)\bgh[oprsu]_[A-Za-z0-9]{24,}\b' },
    @{ Name = 'AWS access key'; Pattern = '\bAKIA[0-9A-Z]{16}\b' },
    @{ Name = 'bearer credential'; Pattern = '(?i)\bBearer\s+[A-Za-z0-9._~-]{20,}' },
    @{ Name = 'literal credential assignment'; Pattern = '(?i)\b(?:api[_-]?key|password|access[_-]?token|client[_-]?secret)\s*[:=]\s*["''][^"'']{12,}["'']' }
)

$literalLocalValues = New-Object 'System.Collections.Generic.List[string]'
foreach ($value in @($env:USERPROFILE, $env:USERNAME)) {
    if ($value -and $value.Length -ge 3 -and -not $literalLocalValues.Contains($value)) {
        $literalLocalValues.Add($value)
    }
}

foreach ($file in $files) {
    $relative = $file.FullName.Substring($repoRoot.Length).TrimStart('\').Replace('\', '/')
    $content = [IO.File]::ReadAllText($file.FullName)
    $isDeclaredFictionalFixture = $relative -match '^tests/fixtures/' -and $content -match '(?i)fictional|fixture|allDataFictional'
    $isDeclaredFictionalTestHarness = $relative -eq 'tests/CodexSOS.Tests/Program.cs' -and
        $content -match 'all diagnostic data is fictional' -and
        $content -match 'FICTIONAL_BEARER_'
    foreach ($entry in $patterns) {
        if ([regex]::IsMatch($content, $entry.Pattern)) {
            if (-not $isDeclaredFictionalFixture -and -not $isDeclaredFictionalTestHarness) {
                $findings.Add("${relative}: $($entry.Name)")
            }
        }
    }
    foreach ($literal in $literalLocalValues) {
        $pattern = '(?<![A-Za-z0-9])' + [regex]::Escape($literal) + '(?![A-Za-z0-9])'
        if ([regex]::IsMatch($content, $pattern)) {
            $findings.Add("${relative}: current machine identity or path")
        }
    }
}

if ($findings.Count -gt 0) {
    $distinct = @($findings | Sort-Object -Unique)
    throw "Static secret scan failed:`n - $($distinct -join "`n - ")"
}

Write-Host "Static secret scan passed across $($files.Count) source and project files. Fictional security fixtures were intentionally excluded."
