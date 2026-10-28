[CmdletBinding()]
param(
    # 只预览分类与新版本号，不写回 Directory.Build.props。
    [switch] $DryRun,
    # 递增起点 tag（默认取版本序最大的 tag）；提交范围 = <tag>..HEAD。
    [string] $FromTag,
    [string] $RepositoryRoot = ''
)
$ErrorActionPreference = 'Stop'
# git 的提交标题是 UTF-8；Windows PowerShell 按控制台代码页解码会导致中文前缀匹配全部落空。
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }
if (-not $RepositoryRoot) {
    if (-not $PSScriptRoot) { throw '无法定位脚本目录，请用 -RepositoryRoot 指定仓库根。' }
    $RepositoryRoot = Split-Path -Parent $PSScriptRoot
}

# 档位映射：标题前缀（中文括号体或英文冒号体）→ 四段版本号中要递增的段。
# 同一发布周期多类型并存时取最高档，较低各段按语义化版本惯例归零。
$majorTitles = @('破坏', 'breaking')
$minorTitles = @('功能', '需求', 'feat')
$patchTitles = @('修复', '优化', 'fix')
# 其余（构建/文档/测试/验证/原型/样式/架构/chore/docs…）一律视为 revision。

$propsPath = Join-Path $RepositoryRoot 'Directory.Build.props'
$props = Get-Content -LiteralPath $propsPath -Raw -Encoding utf8
if ($props -notmatch '<Version>(\d+(?:\.\d+){1,3})</Version>') {
    throw "Directory.Build.props 中未找到 <Version> 节点：$propsPath"
}
# 立即捕获版本锚点：后续标题分类的正则会覆盖自动变量 $Matches。
$currentText = $Matches[1]
# 基线取 tag 时刻的版本，而不是工作区当前版本：支持对任意历史 tag 模拟递增；
# 常规流程（tag=上个发布点、props 未动）两者一致。
$baselineText = $props
try { $tagProps = git -C $RepositoryRoot show "${FromTag}:Directory.Build.props" 2>$null; if ($LASTEXITCODE -eq 0 -and $tagProps) { $baselineText = $tagProps -join "`n" } } catch { }
if ($baselineText -notmatch '<Version>(\d+(?:\.\d+){1,3})</Version>') {
    throw "无法从 $FromTag 读取基线版本。"
}
$current = [version]($Matches[1])
$segments = @($current.Major, $current.Minor, $current.Build, $current.Revision)
for ($i = 0; $i -lt 4; $i++) { if ($segments[$i] -lt 0) { $segments[$i] = 0 } }

if (-not $FromTag) {
    $FromTag = git -C $RepositoryRoot tag --sort=-v:refname | Select-Object -First 1
    if (-not $FromTag) { throw '仓库中没有任何 tag，请用 -FromTag 指定起点。' }
}
if ($FromTag -notmatch '^v') { $FromTag = "v$FromTag" }

$subjects = @(git -C $RepositoryRoot log --format=%s "$FromTag..HEAD")
if ($LASTEXITCODE -ne 0) { throw "读取 $FromTag..HEAD 提交失败。" }

$tierRank = @{ major = 3; minor = 2; patch = 1; revision = 0 }
$tierName = @{ major = '主版本(破坏)'; minor = '次版本(功能)'; patch = '修补(修复/优化)'; revision = '修订(杂项)' }
$highest = $null
$rows = foreach ($subject in $subjects) {
    # 前缀 = 标题开头连续的中英文字符，兼容全角括号「功能(文件树)：」与英文冒号「feat:」两种风格。
    if ($subject -notmatch '^([A-Za-z\u4e00-\u9fff]+)') { $head = '' } else { $head = $Matches[1] }
    $tier = 'revision'
    if ($majorTitles -contains $head) { $tier = 'major' }
    elseif ($minorTitles -contains $head) { $tier = 'minor' }
    elseif ($patchTitles -contains $head) { $tier = 'patch' }
    if ($null -eq $highest -or $tierRank[$tier] -gt $tierRank[$highest]) { $highest = $tier }
    [pscustomobject]@{ Tier = $tier; Subject = $subject }
}

if (-not $subjects) {
    Write-Host "自 $FromTag 以来没有新提交，版本保持 $($current)。" -ForegroundColor Yellow
    return
}

for ($i = 0; $i -lt 4; $i++) { if ($null -eq $segments[$i]) { $segments[$i] = 0 } }
$index = @{ major = 0; minor = 1; patch = 2; revision = 3 }[$highest]
$segments[$index]++
# 语义化版本惯例：高段递增后，低段归零。
for ($i = $index + 1; $i -lt 4; $i++) { $segments[$i] = 0 }
$newVersion = ($segments -join '.')

$rows | Group-Object Tier | ForEach-Object { '{0}  ×{1}' -f $tierName[$_.Name], $_.Count } |
    ForEach-Object { Write-Host "  $_" }
Write-Host "范围: $FromTag..HEAD（$($subjects.Count) 个提交，取 $($tierName[$highest])）"
Write-Host "版本: $current -> $newVersion" -ForegroundColor Green
if ($DryRun) {
    Write-Host 'DryRun：未写回 Directory.Build.props。' -ForegroundColor Yellow
    return
}

 $updated = $props -replace "<Version>$([Regex]::Escape($currentText))</Version>", "<Version>$newVersion</Version>"
if ($updated -eq $props) { throw "版本替换未命中：<Version>$currentText</Version> 不在文件中？" }
Set-Content -LiteralPath $propsPath -Value $updated -Encoding utf8 -NoNewline
Write-Host "已写回 $propsPath" -ForegroundColor Green
