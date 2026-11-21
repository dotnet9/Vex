# 本地发布门禁（v* tag 推送前由 .githooks/pre-push 强制执行）：
# 与 update-checks.yml / release.yml 的 CI 场景同源，失败即阻止推送。
# 用法：pwsh scripts/verify-release.ps1 [-SkipAot]
# 说明：真实 Inno 夹具仅在机器存在 ISCC.exe 时执行（CI 同款条件）。
param(
    [switch]$SkipAot
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$fail = 0

function Step([string]$name, [scriptblock]$action) {
    Write-Host "==> $name" -ForegroundColor Cyan
    & $action
    if ($LASTEXITCODE -ne 0 -and $LASTEXITCODE -ne $null) { throw "步骤失败：$name (exit $LASTEXITCODE)" }
}

Step '构建解决方案' { dotnet build Vex.slnx -c Release -v q --nologo -m:1 }

Step 'xunit：Markdown' { dotnet test src/CodeWF.Avalonia.Markdown.Tests/CodeWF.Avalonia.Markdown.Tests.csproj --nologo }
Step 'xunit：Vex.Core' { dotnet test src/Vex.Core.Tests/Vex.Core.Tests.csproj --nologo }
Step 'xunit：MCP' { dotnet run --project tests/Vex.Mcp.Tests/Vex.Mcp.Tests.csproj -c Release }

Step '候选库重建' { ./scripts/restore_library_candidate.ps1 -OutputRoot (Join-Path ([IO.Path]::GetTempPath()) 'vex-library-candidate') }
Step '更新状态与启动失败边界' { dotnet run --project scripts/update-verification -c Release }

Step '构建 UiVerification' { dotnet build scripts/ui-verification/UiVerification.csproj -c Release }
$uv = 'dotnet artifacts/bin/UiVerification/release/UiVerification.dll'
$out = Join-Path $root 'artifacts/verify-release'
foreach ($scenario in @(
        @{ mode = 'updates'; args = '' },
        @{ mode = 'media-layout'; args = '' },
        @{ mode = 'ui-density'; args = '' },
        @{ mode = 'ui-density-performance'; args = '--real-settings' },
        @{ mode = 'prototype-appearance'; args = '' },
        @{ mode = 'popup-layout'; args = '' },
        @{ mode = 'theme-performance'; args = '--real-settings' },
        @{ mode = 'drafts'; args = '' },
        @{ mode = 'documents'; args = '' },
        @{ mode = 'mermaid'; args = '' },
        @{ mode = 'blog-frontmatter'; args = '' },
        @{ mode = 'presentation'; args = '' })) {
    Step "UiVerification：$($scenario.mode)" { Invoke-Expression "$uv $($scenario.mode) $out/$($scenario.mode) $($scenario.args)" }
}

if (-not $SkipAot) {
    $version = '0.0.0'
    if ((Get-Content Directory.Build.props -Raw) -match '<Version>([^<]+)</Version>') { $version = $Matches[1] }
    Step 'AOT 发布 win-x64' { pwsh scripts/publish.ps1 -RuntimeIdentifier win-x64 -Version $version }
    $exe = Join-Path $root 'artifacts/publish/win-x64/Vex/Vex.exe'
    Step 'AOT 验证：ui' { & $exe --verify-ui (Join-Path $out 'aot-ui') }
    Step 'AOT 验证：layout' { & $exe --verify-layout (Join-Path $out 'aot-layout') }
    Step 'AOT 验证：runtime' { & $exe --verify-runtime (Join-Path $out 'aot-runtime') }
    if (Test-Path (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6/ISCC.exe')) {
        Step '真实 Inno 夹具' { pwsh scripts/installer/tests/verify_vex_fixture.ps1 -Cancel }
    } else {
        Write-Host '跳过真实 Inno 夹具（本机未安装 ISCC.exe）' -ForegroundColor Yellow
    }
}

Write-Host '本地发布门禁全部通过' -ForegroundColor Green
