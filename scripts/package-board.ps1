[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$Destination, [switch]$SkipTests)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$cache = Join-Path $root 'artifacts/node-cache'
$version = '24.16.0'
$expected = 'edaca9bd58ec8e92037dac4e877d52f6b8f430b81c18b57e264b4e2fb111cd56'
New-Item -ItemType Directory -Force -Path $cache | Out-Null
$zip = Join-Path $cache "node-v$version-win-x64.zip"
if (-not (Test-Path -LiteralPath $zip)) {
    Invoke-WebRequest "https://nodejs.org/dist/v$version/node-v$version-win-x64.zip" -OutFile $zip
}
if ((Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected) { throw 'Bundled Node SHA-256 mismatch.' }
$runtime = Join-Path $cache "node-v$version-win-x64"
if (-not (Test-Path -LiteralPath (Join-Path $runtime 'node.exe'))) { Expand-Archive -LiteralPath $zip -DestinationPath $cache -Force }
$node = Join-Path $runtime 'node.exe'
if (-not $SkipTests) {
    Push-Location (Join-Path $root 'dot-assistant')
    try { & $node --test 'tests/*.test.mjs'; if ($LASTEXITCODE -ne 0) { throw 'Task board tests failed.' } }
    finally { Pop-Location }
}
$target = Join-Path $Destination 'dot-assistant'
New-Item -ItemType Directory -Force -Path $target,(Join-Path $target 'runtime') | Out-Null
foreach ($file in @('package.json','state.mjs','observer.mjs','server.mjs','launch.mjs')) { Copy-Item -LiteralPath (Join-Path $root "dot-assistant/$file") -Destination $target }
foreach ($directory in @('public','vendor')) {
    $source = Join-Path $root "dot-assistant/$directory"
    foreach ($file in Get-ChildItem -LiteralPath $source -Recurse -File) {
        $relative = [IO.Path]::GetRelativePath($source, $file.FullName)
        $output = Join-Path (Join-Path $target $directory) $relative
        New-Item -ItemType Directory -Force -Path ([IO.Path]::GetDirectoryName($output)) | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $output -Force
    }
}
Copy-Item -LiteralPath $node -Destination (Join-Path $target 'runtime/node.exe')
Copy-Item -LiteralPath (Join-Path $runtime 'LICENSE') -Destination (Join-Path $target 'runtime/Node-LICENSE.txt')
Write-Host "Task board and verified Node $version packaged (no package-manager/runtime install required for users)."
