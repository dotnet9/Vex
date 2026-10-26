[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$VexPath = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$supportedExtensions = @(".md", ".markdown", ".mdown", ".txt")
$menuKeyName = "OpenWithVex"
$menuText = -join @([char]0x7528, " Vex ", [char]0x6253, [char]0x5F00)

function Resolve-VexExecutable([string]$InputPath) {
    if ([string]::IsNullOrWhiteSpace($InputPath)) {
        $InputPath = Join-Path $PSScriptRoot "Vex.exe"
    }

    if (-not (Test-Path -LiteralPath $InputPath -PathType Leaf)) {
        throw "Vex.exe was not found. Run this script from the Vex publish folder, or pass -VexPath."
    }

    return (Resolve-Path -LiteralPath $InputPath).Path
}

function Set-RegistryDefaultValue([string]$Path, [string]$Value) {
    New-Item -Path $Path -Force | Out-Null
    $key = Get-Item -LiteralPath $Path
    $key.SetValue("", $Value, [Microsoft.Win32.RegistryValueKind]::String)
}

function Set-RegistryStringValue([string]$Path, [string]$Name, [string]$Value) {
    New-Item -Path $Path -Force | Out-Null
    New-ItemProperty -LiteralPath $Path -Name $Name -Value $Value -PropertyType String -Force | Out-Null
}

if ($env:OS -ne "Windows_NT") {
    throw "This script can only register Windows Explorer context menus on Windows."
}

$vexExecutable = Resolve-VexExecutable $VexPath
$command = '"{0}" "%1"' -f $vexExecutable
$progId = "Vex.Markdown"
$classesRoot = "HKCU:\Software\Classes"

if ($PSCmdlet.ShouldProcess($vexExecutable, "Register Vex Markdown opening candidate and icon")) {
    Set-RegistryDefaultValue "$classesRoot\$progId" "Markdown"
    Set-RegistryDefaultValue "$classesRoot\$progId\DefaultIcon" ('"{0}",0' -f $vexExecutable)
    Set-RegistryDefaultValue "$classesRoot\$progId\shell\open\command" $command
    Set-RegistryStringValue "$classesRoot\Applications\Vex.exe" "FriendlyAppName" "Vex"
    Set-RegistryDefaultValue "$classesRoot\Applications\Vex.exe\DefaultIcon" ('"{0}",0' -f $vexExecutable)
    Set-RegistryDefaultValue "$classesRoot\Applications\Vex.exe\shell\open\command" $command
    foreach ($extension in @(".md", ".markdown", ".mdown")) {
        Set-RegistryStringValue "$classesRoot\Applications\Vex.exe\SupportedTypes" $extension ""
        $candidatePath = "$classesRoot\$extension\OpenWithProgids"
        New-Item -Path $candidatePath -Force | Out-Null
        (Get-Item -LiteralPath $candidatePath).SetValue($progId, [byte[]]@(), [Microsoft.Win32.RegistryValueKind]::None)
    }
}

foreach ($extension in $supportedExtensions) {
    $verbPath = "HKCU:\Software\Classes\SystemFileAssociations\$extension\shell\$menuKeyName"
    $commandPath = "$verbPath\command"

    if ($PSCmdlet.ShouldProcess($extension, "Register Vex context menu")) {
        Set-RegistryDefaultValue $verbPath $menuText
        Set-RegistryStringValue $verbPath "MUIVerb" $menuText
        Set-RegistryStringValue $verbPath "Icon" $vexExecutable
        Set-RegistryStringValue $verbPath "MultiSelectModel" "Single"
        Set-RegistryDefaultValue $commandPath $command
    }
}

Write-Host "Vex context menu targets: $($supportedExtensions -join ', ')"
Write-Host "Command: $command"
