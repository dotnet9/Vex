#Requires -Version 7.0
<#
清理 Vex / CodeWF.Markdown 的可重建输出与不再使用的开发包。
支持 -WhatIf；保留源码、原型、文档、样例及 Vex 当前引用的包。
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$MarkdownRepoRoot = (Join-Path $PSScriptRoot '..\..\..\Libs\CodeWF.Markdown'),
    [string]$LocalPackageSource,
    [version[]]$SupersededDevelopmentVersions = @()
)

$ErrorActionPreference = 'Stop'
$vexRepoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$MarkdownRepoRoot = (Resolve-Path -LiteralPath $MarkdownRepoRoot).Path
if (-not (Test-Path -LiteralPath (Join-Path $MarkdownRepoRoot 'CodeWF.Markdown.slnx'))) {
    throw 'MarkdownRepoRoot must point to the CodeWF.Markdown repository.'
}
if (-not $LocalPackageSource) {
    $LocalPackageSource = Join-Path (Split-Path -Parent $MarkdownRepoRoot) 'nuget-local'
}
$LocalPackageSource = [IO.Path]::GetFullPath($LocalPackageSource)

$targets = @(
    @{ Root = $vexRepoRoot; Relative = '.plan' },
    @{ Root = $vexRepoRoot; Relative = 'artifacts' },
    @{ Root = $MarkdownRepoRoot; Relative = '.plan' },
    @{ Root = $MarkdownRepoRoot; Relative = 'artifacts' },
    @{ Root = $MarkdownRepoRoot; Relative = 'tests\CodeWF.Markdown.Tests\TestResults' }
)
foreach ($target in $targets) {
    $path = [IO.Path]::GetFullPath((Join-Path $target.Root $target.Relative))
    if (-not $path.StartsWith($target.Root.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw "Target is outside the repository: $path"
    }
    if (-not (Test-Path -LiteralPath $path)) { continue }
    $item = Get-Item -LiteralPath $path -Force
    if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Refusing linked directory: $path" }
    if ($target.Relative -eq '.plan' -and (Get-ChildItem -LiteralPath $path -Force)) {
        throw "Plan directory contains files; review them before cleaning: $path"
    }
    if ($PSCmdlet.ShouldProcess($path, 'Remove generated directory')) {
        Remove-Item -LiteralPath $path -Recurse -Force
    }
}

if (Test-Path -LiteralPath $LocalPackageSource) {
    [xml]$packageProps = Get-Content -LiteralPath (Join-Path $vexRepoRoot 'Directory.Packages.props') -Raw
    $packageIds = @('CodeWF.Markdown', 'CodeWF.Markdown.Lite', 'CodeWF.Markdown.Themes', 'CodeWF.Markdown.Lite.Themes')
    $references = @($packageProps.Project.ItemGroup.PackageVersion | Where-Object { $_.Include -in $packageIds })
    foreach ($reference in $references) {
        if ($reference.Version -match '^\d+(?:\.\d+){2,3}$') {
            $packageId = $reference.Include.ToLowerInvariant()
            $packageVersions = (Invoke-RestMethod "https://api.nuget.org/v3-flatcontainer/$packageId/index.json").versions
            if ($reference.Version -notin $packageVersions) { throw "Stable dependency is not available on nuget.org: $($reference.Include) $($reference.Version)" }
            $developmentVersions = @([version]$reference.Version) + @($SupersededDevelopmentVersions)
            if (@($developmentVersions | Where-Object { $_ -gt [version]$reference.Version }).Count) { throw 'Cannot remove development packages newer than the stable dependency.' }
            $versionPattern = ($developmentVersions | ForEach-Object { [regex]::Escape($_.ToString()) }) -join '|'
            $stablePattern = '^' + [regex]::Escape("$($reference.Include).") + '(?:' + $versionPattern + ')-dev\.\d{8}\.\d+\.(?:nupkg|snupkg)$'
            foreach ($package in Get-ChildItem -LiteralPath $LocalPackageSource -File) {
                if ($package.Name -notmatch $stablePattern) { continue }
                if ($package.DirectoryName -ne $LocalPackageSource) { throw "Unexpected package path: $($package.FullName)" }
                if ($PSCmdlet.ShouldProcess($package.FullName, 'Remove local development package superseded by NuGet release')) {
                    Remove-Item -LiteralPath $package.FullName -Force
                }
            }
            continue
        }
        if ($reference.Version -notmatch '^(?<batch>.+-dev\.\d{8})\.(?<iteration>\d+)$') { continue }
        $batch = $Matches.batch
        $iteration = [int]$Matches.iteration
        $currentPackage = Join-Path $LocalPackageSource "$($reference.Include).$($reference.Version).nupkg"
        if (-not (Test-Path -LiteralPath $currentPackage)) { throw "Current dependency package is missing: $currentPackage" }
        $pattern = '^' + [regex]::Escape("$($reference.Include).$batch") + '\.(?<iteration>\d+)\.(?:nupkg|snupkg)$'
        foreach ($package in Get-ChildItem -LiteralPath $LocalPackageSource -File) {
            if ($package.Name -notmatch $pattern -or [int]$Matches.iteration -ge $iteration) { continue }
            if ($package.DirectoryName -ne $LocalPackageSource) { throw "Unexpected package path: $($package.FullName)" }
            if ($PSCmdlet.ShouldProcess($package.FullName, 'Remove obsolete development package')) {
                Remove-Item -LiteralPath $package.FullName -Force
            }
        }
    }
}
