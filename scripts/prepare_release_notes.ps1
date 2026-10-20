[CmdletBinding()]
param(
    [Parameter(Mandatory)] [ValidatePattern('^v?\d+(\.\d+)+$')] [string] $Version,
    [Parameter(Mandatory)] [string] $OutputPath,
    [string] $RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [switch] $FetchTag
)
$ErrorActionPreference = 'Stop'
$releaseVersion = $Version.TrimStart('v')
$tagName = "v$releaseVersion"
$root = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$outputFile = [IO.Path]::GetFullPath($OutputPath)
$curated = Join-Path $root ".github/release-notes/$tagName.md"
$body = ''
if (Test-Path -LiteralPath $curated -PathType Leaf) {
    $body = Get-Content -LiteralPath $curated -Raw -Encoding utf8
    Write-Host "使用发布说明：$curated"
}
else {
    $tagRef = "refs/tags/$tagName"
    if ($FetchTag) {
        # checkout 可能把本地标签引用指向提交；另取远端标签对象，不改写标签。
        $tagRef = "refs/vex-release-tags/$tagName"
        git -C $root fetch --quiet origin "refs/tags/${tagName}:$tagRef"
        if ($LASTEXITCODE -ne 0) { throw "无法读取远端标签：$tagName" }
    }
    git -C $root show-ref --verify --quiet $tagRef
    if ($LASTEXITCODE -eq 0) {
        $tagType = git -C $root cat-file -t $tagRef
        if ($LASTEXITCODE -ne 0) { throw "无法读取标签对象：$tagName" }
        if ($tagType -eq 'tag') {
            $body = (git -C $root for-each-ref $tagRef '--format=%(contents)') -join "`n"
            if ($LASTEXITCODE -ne 0) { throw "无法读取附注标签说明：$tagName" }
        }
    }
    if ([string]::IsNullOrWhiteSpace($body)) {
        $updateLog = Join-Path $root 'UpdateLog.md'
        if (Test-Path -LiteralPath $updateLog -PathType Leaf) {
            $lines = [Collections.Generic.List[string]]::new()
            $capture = $false
            foreach ($line in Get-Content -LiteralPath $updateLog -Encoding utf8) {
                if ($line -match ('^#{1,6}\s+v?' + [regex]::Escape($releaseVersion) + '(?:\s|[（(]|$)')) {
                    $capture = $true
                    $lines.Add($line)
                    continue
                }
                if ($capture -and $line -match '^#{1,6}\s') { break }
                if ($capture) { $lines.Add($line) }
            }
            $body = $lines -join "`n"
        }
    }
}
if ([string]::IsNullOrWhiteSpace($body)) { throw "缺少 $tagName 的完整发布说明。" }
New-Item -ItemType Directory -Path (Split-Path -Parent $outputFile) -Force | Out-Null
Set-Content -LiteralPath $outputFile -Value $body.Trim() -Encoding utf8
Write-Host "发布说明已生成：$outputFile"
exit 0
