# release_to_github.ps1
# 完整的 GitHub 发布流程：构建 Native AOT、创建标签、推送并创建 GitHub Release
[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string] $Version = '',
    [switch] $SkipBuild,
    [switch] $Draft,
    [switch] $Prerelease,
    [string] $ReleaseNotes = ''
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path

function Get-ProjectVersion {
    if (-not [string]::IsNullOrWhiteSpace($Version)) {
        return $Version.Trim().TrimStart('v', 'V')
    }
    $propsPath = Join-Path $repositoryRoot 'Directory.Build.props'
    [xml] $properties = [IO.File]::ReadAllText($propsPath)
    return [string] $properties.Project.PropertyGroup.Version
}

function Test-GitClean {
    $status = git -C $repositoryRoot status --porcelain
    if ($LASTEXITCODE -ne 0) { throw "无法检查 Git 状态" }
    return [string]::IsNullOrWhiteSpace($status)
}

function Test-TagExists {
    param([Parameter(Mandatory)] [string] $Tag)
    git -C $repositoryRoot rev-parse "refs/tags/$Tag" 2>$null | Out-Null
    return $LASTEXITCODE -eq 0
}

function New-GitTag {
    param(
        [Parameter(Mandatory)] [string] $Tag,
        [Parameter(Mandatory)] [string] $Message
    )
    Write-Host "创建标签: $Tag"
    git -C $repositoryRoot tag -a $Tag -m $Message
    if ($LASTEXITCODE -ne 0) { throw "创建标签失败" }
}

function Push-GitTag {
    param([Parameter(Mandatory)] [string] $Tag)
    Write-Host "推送标签到 GitHub: $Tag"
    git -C $repositoryRoot push origin $Tag
    if ($LASTEXITCODE -ne 0) { throw "推送标签失败" }
}

function New-GitHubRelease {
    param(
        [Parameter(Mandatory)] [string] $Tag,
        [Parameter(Mandatory)] [string] $ReleaseVersion,
        [string] $Notes,
        [switch] $IsDraft,
        [switch] $IsPrerelease
    )

    $publishRoot = Join-Path $repositoryRoot 'publish'
    $packagesDir = Join-Path $publishRoot 'packages'
    $zipFile = Join-Path $packagesDir "Vex-$ReleaseVersion-win-x64.zip"

    if (-not (Test-Path -LiteralPath $zipFile -PathType Leaf)) {
        throw "找不到发布包: $zipFile"
    }

    Write-Host "创建 GitHub Release: $Tag"

    $ghArgs = @(
        'release', 'create', $Tag,
        $zipFile,
        '--title', "Vex $ReleaseVersion",
        '--repo', 'sunyonghuan/Vex'
    )

    if ($Notes) {
        $ghArgs += '--notes'
        $ghArgs += $Notes
    } else {
        $ghArgs += '--generate-notes'
    }

    if ($IsDraft) { $ghArgs += '--draft' }
    if ($IsPrerelease) { $ghArgs += '--prerelease' }

    & gh @ghArgs
    if ($LASTEXITCODE -ne 0) { throw "创建 GitHub Release 失败" }

    Write-Host "✅ GitHub Release 创建成功: https://github.com/sunyonghuan/Vex/releases/tag/$Tag"
}

# ============ 主流程 ============

$releaseVersion = Get-ProjectVersion
if ($releaseVersion -notmatch '^\d+\.\d+\.\d+(?:\.\d+)?$') {
    throw "版本号格式无效: $releaseVersion"
}

$tagName = "v$releaseVersion"
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "发布版本: $releaseVersion" -ForegroundColor Cyan
Write-Host "Git 标签: $tagName" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 检查 Git 状态
if (-not (Test-GitClean)) {
    Write-Host "⚠️  工作区有未提交的更改" -ForegroundColor Yellow
    $response = Read-Host "是否继续? (y/N)"
    if ($response -ne 'y' -and $response -ne 'Y') {
        Write-Host "已取消"
        exit 0
    }
}

# 检查标签是否已存在
if (Test-TagExists -Tag $tagName) {
    throw "标签 $tagName 已存在，请先删除或使用新版本号"
}

# 检查 GitHub CLI
if (-not (Get-Command gh -CommandType Application -ErrorAction SilentlyContinue)) {
    throw "需要安装 GitHub CLI (gh)。请访问: https://cli.github.com/"
}

# 执行构建
if (-not $SkipBuild) {
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "步骤 1/4: 构建 Native AOT 发布包" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green

    $publishScript = Join-Path $PSScriptRoot 'publish_aot.ps1'
    & pwsh -NoProfile -ExecutionPolicy Bypass -File $publishScript -Platform 'win-x64' -Version $releaseVersion
    if ($LASTEXITCODE -ne 0) { throw "构建失败" }

    Write-Host ""
    Write-Host "✅ 构建完成" -ForegroundColor Green
    Write-Host ""
} else {
    Write-Host "⏭️  跳过构建步骤" -ForegroundColor Yellow
    Write-Host ""
}

# 创建 Git 标签
Write-Host "========================================" -ForegroundColor Green
Write-Host "步骤 2/4: 创建 Git 标签" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green

$tagMessage = if ($ReleaseNotes) { $ReleaseNotes } else { "Release $releaseVersion" }
New-GitTag -Tag $tagName -Message $tagMessage
Write-Host ""
Write-Host "✅ 标签创建完成" -ForegroundColor Green
Write-Host ""

# 推送标签
Write-Host "========================================" -ForegroundColor Green
Write-Host "步骤 3/4: 推送标签到 GitHub" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green

Push-GitTag -Tag $tagName
Write-Host ""
Write-Host "✅ 标签推送完成" -ForegroundColor Green
Write-Host ""

# 创建 GitHub Release
Write-Host "========================================" -ForegroundColor Green
Write-Host "步骤 4/4: 创建 GitHub Release" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green

New-GitHubRelease -Tag $tagName -ReleaseVersion $releaseVersion -Notes $ReleaseNotes -IsDraft:$Draft -IsPrerelease:$Prerelease
Write-Host ""

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "🎉 发布完成！" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "版本: $releaseVersion"
Write-Host "标签: $tagName"
Write-Host "查看: https://github.com/sunyonghuan/Vex/releases/tag/$tagName"
Write-Host ""
