#Requires -Version 7.0
<#
清理本仓库可重建的项目输出。支持 -WhatIf，不修改源码、设计文件或共享 NuGet 包目录。
发布产物默认保留，需要清理时显式传入 -IncludePublishArtifacts。
#>
[CmdletBinding(SupportsShouldProcess)]
param([switch]$IncludePublishArtifacts)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$repositoryPrefix = $repositoryRoot.TrimEnd('\') + '\'
$targets = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)

# 只枚举受版本控制的工程，避免进入输出目录、链接目录或其他仓库。
$projects = & git -C $repositoryRoot ls-files -- '*.csproj'
if ($LASTEXITCODE -ne 0) { throw 'Could not enumerate tracked projects.' }
foreach ($project in $projects) {
    $projectDirectory = Split-Path -Parent (Join-Path $repositoryRoot $project)
    foreach ($output in @('bin', 'obj', 'TestResults')) {
        [void]$targets.Add([IO.Path]::GetFullPath((Join-Path $projectDirectory $output)))
    }
}
if ($IncludePublishArtifacts) {
    [void]$targets.Add([IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts')))
}

foreach ($path in $targets) {
    if (-not $path.StartsWith($repositoryPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Target is outside the repository: $path"
    }
    if (-not (Test-Path -LiteralPath $path)) { continue }
    $ancestor = Get-Item -LiteralPath $path -Force
    while ($ancestor.FullName -ne $repositoryRoot) {
        if ($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw "Refusing linked directory: $($ancestor.FullName)"
        }
        $ancestor = $ancestor.Parent
        if ($null -eq $ancestor) { throw "Could not verify target ancestry: $path" }
    }
    if ($PSCmdlet.ShouldProcess($path, 'Remove generated directory')) {
        Remove-Item -LiteralPath $path -Recurse -Force
    }
}
