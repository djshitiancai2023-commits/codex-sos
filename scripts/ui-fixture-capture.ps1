[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Scenario,
    [Parameter(Mandatory = $true)][string]$OutputName,
    [string]$OutputDirectory = "artifacts/ui-acceptance/screenshots",
    [string]$ClickButtonName = "帮我看看",
    [string]$ClickAfterName = "",
    [ValidateSet('简体中文', '繁體中文', 'English')][string]$Language = '简体中文',
    [int]$WindowHeight = 0,
    [int]$WaitSeconds = 20
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = [IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) '..'))
$dotnetApp = Join-Path $repoRoot 'src/CodexSOS.App/bin/Release/net10.0-windows/win-x64/CodexSOS.exe'
$scenarioPath = [IO.Path]::GetFullPath((Join-Path $repoRoot $Scenario))
$outputPath = [IO.Path]::GetFullPath((Join-Path $repoRoot (Join-Path $OutputDirectory $OutputName)))

if (-not (Test-Path -LiteralPath $dotnetApp -PathType Leaf)) { throw "Build the app first: $dotnetApp" }
if (-not (Test-Path -LiteralPath $scenarioPath -PathType Leaf)) { throw "Fixture not found: $scenarioPath" }
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $outputPath) | Out-Null

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class CodexSosWindowNative {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);
}
'@

$process = Start-Process -FilePath $dotnetApp -ArgumentList @('--fixture', $scenarioPath) -PassThru
try {
    $window = $null
    $deadline = (Get-Date).AddSeconds(12)
    while ((Get-Date) -lt $deadline -and $null -eq $window) {
        Start-Sleep -Milliseconds 250
        $process.Refresh()
        if ($process.HasExited) { throw "Fixture app exited before the window appeared." }
        if ($process.MainWindowHandle -ne 0) {
            $window = [System.Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)
        }
    }
    if ($null -eq $window) { throw 'Fixture window did not appear.' }
    [CodexSosWindowNative]::ShowWindow([IntPtr]$process.MainWindowHandle, 5) | Out-Null
    if ($WindowHeight -gt 0) {
        $currentBounds = $window.Current.BoundingRectangle
        [CodexSosWindowNative]::SetWindowPos(
            [IntPtr]$process.MainWindowHandle,
            [IntPtr]::Zero,
            [int]$currentBounds.X,
            [int]$currentBounds.Y,
            [int]$currentBounds.Width,
            $WindowHeight,
            0x0004 -bor 0x0002) | Out-Null
        Start-Sleep -Milliseconds 350
    }
    [CodexSosWindowNative]::SetForegroundWindow([IntPtr]$process.MainWindowHandle) | Out-Null
    Start-Sleep -Milliseconds 350

    if ($Language -ne '简体中文') {
        $comboCondition = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
            'LanguageSelector')
        $combo = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $comboCondition)
        if ($null -eq $combo) { throw 'Language selector not found.' }
        $expand = $combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
        $expand.Expand()
        Start-Sleep -Milliseconds 250
        $itemCondition = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::NameProperty,
            $Language)
        $item = $combo.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $itemCondition)
        if ($null -eq $item) { throw "Language option not found: $Language" }
        $selection = $item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
        $selection.Select()
        Start-Sleep -Milliseconds 350
    }

    $buttonCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Button)
    $buttons = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, $buttonCondition)
    $target = $null
    foreach ($button in $buttons) {
        if ($button.Current.Name -eq $ClickButtonName) { $target = $button; break }
    }
    if ($null -eq $target) { throw "Button not found: $ClickButtonName" }
    $invoke = $target.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    $invoke.Invoke()

    Start-Sleep -Seconds $WaitSeconds
    if (-not [string]::IsNullOrWhiteSpace($ClickAfterName)) {
        $afterButtons = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, $buttonCondition)
        $afterTarget = $null
        foreach ($afterButton in $afterButtons) {
            if ($afterButton.Current.Name -eq $ClickAfterName) { $afterTarget = $afterButton; break }
        }
        if ($null -eq $afterTarget) { throw "Post-result button not found: $ClickAfterName" }
        $afterInvoke = $afterTarget.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
        $afterInvoke.Invoke()
        Start-Sleep -Seconds 2
    }
    $bounds = $window.Current.BoundingRectangle
    if ($bounds.Width -le 0 -or $bounds.Height -le 0) { throw 'Fixture window has no visible bounds.' }
    $bitmap = New-Object System.Drawing.Bitmap([int]$bounds.Width, [int]$bounds.Height)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $hdc = $graphics.GetHdc()
            try {
                $painted = [CodexSosWindowNative]::PrintWindow([IntPtr]$process.MainWindowHandle, $hdc, 2)
            }
            finally { $graphics.ReleaseHdc($hdc) }
            if (-not $painted) {
                $graphics.CopyFromScreen([int]$bounds.X, [int]$bounds.Y, 0, 0, $bitmap.Size)
            }
        }
        finally { $graphics.Dispose() }
        $bitmap.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally { $bitmap.Dispose() }

    $receipt = [ordered]@{
        fixture = (Split-Path -Leaf $scenarioPath)
        output = (Split-Path -Leaf $outputPath)
        title = $window.Current.Name
        bounds = [ordered]@{ width = [int]$bounds.Width; height = [int]$bounds.Height }
        allDataFictional = $true
        language = $Language
        capturedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    }
    $receiptPath = [IO.Path]::ChangeExtension($outputPath, '.receipt.json')
    $receipt | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $receiptPath -Encoding utf8
    Write-Host "Captured $outputPath"
}
finally {
    if ($null -ne $process -and -not $process.HasExited) {
        $process.CloseMainWindow() | Out-Null
        Start-Sleep -Milliseconds 500
        if (-not $process.HasExited) { $process.Kill() }
    }
}
