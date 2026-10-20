$ErrorActionPreference = 'Stop'
$temporaryRoot = [IO.Path]::GetFullPath($(if ($env:CODEWF_TEST_TMP) { $env:CODEWF_TEST_TMP } else { [IO.Path]::GetTempPath() }))
$fixture = Join-Path $temporaryRoot ('vex-release-notes-test-' + [guid]::NewGuid().ToString('N'))
$prepare = Join-Path $PSScriptRoot '../prepare_release_notes.ps1'
function Invoke-CheckedGit {
    & git @args
    if ($LASTEXITCODE -ne 0) { throw "Git 夹具操作失败：$args" }
}
function Assert-Contains([string] $Text, [string] $Expected) {
    if (-not $Text.Contains($Expected)) { throw "缺少预期内容：$Expected" }
}
function Assert-Rejected([scriptblock] $Action) {
    $rejected = $false
    try { & $Action } catch { $rejected = $true }
    if (-not $rejected) { throw '缺少完整发布说明时未被拒绝。' }
}
try {
    $remote = Join-Path $fixture 'remote.git'
    $checkout = Join-Path $fixture 'checkout'
    $output = Join-Path $fixture 'body.md'
    Invoke-CheckedGit init --bare --quiet $remote
    Invoke-CheckedGit init --quiet $checkout
    Invoke-CheckedGit -C $checkout config user.name 'Release test'
    Invoke-CheckedGit -C $checkout config user.email 'release-test@example.invalid'
    Invoke-CheckedGit -C $checkout config core.autocrlf false
    Set-Content -LiteralPath (Join-Path $checkout 'UpdateLog.md') -Encoding utf8 -Value "# 更新日志`n`n## 1.2.30`n错误版本内容`n`n## 1.2.3（2026-10-20）`n正确版本内容`n`n## 1.2.2`n旧版内容"
    Invoke-CheckedGit -C $checkout add UpdateLog.md
    Invoke-CheckedGit -C $checkout commit --quiet -m '无关提交标题'
    $tagBody = "Vex 1.2.3`n`n完整中文附注说明`n`n- 第一项`n- 第二项"
    $tagFile = Join-Path $fixture 'tag.md'
    Set-Content -LiteralPath $tagFile -Value $tagBody -Encoding utf8
    Invoke-CheckedGit -C $checkout tag -a v1.2.3 -F $tagFile
    Invoke-CheckedGit -C $checkout remote add origin $remote
    Invoke-CheckedGit -C $checkout push --quiet origin refs/tags/v1.2.3
    $remoteBefore = (Invoke-CheckedGit -C $checkout ls-remote --tags origin) -join "`n"

    & $prepare -RepositoryRoot $checkout -Version 1.2.3 -OutputPath $output
    if ((Get-Content -LiteralPath $output -Raw).Trim() -ne $tagBody) { throw '附注标签的中文或真实换行丢失。' }

    # 只在临时夹具中模拟 CI 将本地标签扁平化；远端标签始终不动。
    Invoke-CheckedGit -C $checkout tag -d v1.2.3 | Out-Null
    Invoke-CheckedGit -C $checkout tag v1.2.3
    & $prepare -RepositoryRoot $checkout -Version 1.2.3 -OutputPath $output
    $fallback = Get-Content -LiteralPath $output -Raw
    Assert-Contains $fallback '正确版本内容'
    foreach ($unexpected in @('无关提交标题', '错误版本内容', '旧版内容')) {
        if ($fallback.Contains($unexpected)) { throw "更新日志兜底混入无关内容：$unexpected" }
    }

    & $prepare -RepositoryRoot $checkout -Version v1.2.3 -OutputPath $output -FetchTag
    if ((Get-Content -LiteralPath $output -Raw).Trim() -ne $tagBody) { throw '远端附注说明未恢复。' }
    if ((Invoke-CheckedGit -C $checkout cat-file -t refs/tags/v1.2.3) -ne 'commit') { throw '不应改写本地标签。' }
    if (((Invoke-CheckedGit -C $checkout ls-remote --tags origin) -join "`n") -ne $remoteBefore) { throw '远端标签发生变化。' }

    $curated = Join-Path $checkout '.github/release-notes/v1.2.3.md'
    New-Item -ItemType Directory -Path (Split-Path -Parent $curated) -Force | Out-Null
    Set-Content -LiteralPath $curated -Value '# 精选完整说明' -Encoding utf8
    & $prepare -RepositoryRoot $checkout -Version 1.2.3 -OutputPath $output -FetchTag
    Assert-Contains (Get-Content -LiteralPath $output -Raw) '# 精选完整说明'
    Set-Content -LiteralPath $curated -Value ''
    Assert-Rejected { & $prepare -RepositoryRoot $checkout -Version 1.2.3 -OutputPath $output }
    Assert-Rejected { & $prepare -RepositoryRoot $checkout -Version 1.2.4 -OutputPath $output }
    Write-Host 'PASS: 附注标签、轻量标签兜底、远端对象恢复、精选优先、空说明及缺失说明六组发布场景通过；本地与远端标签均未改写。'
}
finally {
    $resolved = [IO.Path]::GetFullPath($fixture)
    if ([IO.Path]::GetDirectoryName($resolved) -ne $temporaryRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) -or
        -not [IO.Path]::GetFileName($resolved).StartsWith('vex-release-notes-test-')) { throw '临时目录范围错误。' }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
# 预期的 show-ref 未命中不能成为整个测试进程的失败退出码。
exit 0
