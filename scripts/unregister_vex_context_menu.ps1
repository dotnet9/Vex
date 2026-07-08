[CmdletBinding(SupportsShouldProcess = $true)]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$supportedExtensions = @(".md", ".markdown", ".mdown", ".txt")
$menuKeyName = "OpenWithVex"

if ($env:OS -ne "Windows_NT") {
    throw "This script can only unregister Windows Explorer context menus on Windows."
}

foreach ($extension in $supportedExtensions) {
    $verbPath = "HKCU:\Software\Classes\SystemFileAssociations\$extension\shell\$menuKeyName"
    if (-not (Test-Path -LiteralPath $verbPath)) {
        continue
    }

    if ($PSCmdlet.ShouldProcess($extension, "Unregister Vex context menu")) {
        Remove-Item -LiteralPath $verbPath -Recurse -Force
    }
}

Write-Host "Unregistered Vex context menu for: $($supportedExtensions -join ', ')"
