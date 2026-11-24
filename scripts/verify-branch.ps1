# 本地分支推送门禁（.githooks/pre-push 强制执行）：构建 + 三套 xunit，
# 与 CI 更新流程回归的编译/测试面同源。tag 推送另走 verify-release.ps1 全量门禁。
# 用法：pwsh scripts/verify-branch.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

function Step([string]$name, [scriptblock]$action) {
    Write-Host "==> $name" -ForegroundColor Cyan
    & $action
    if ($LASTEXITCODE -ne 0 -and $LASTEXITCODE -ne $null) { throw "步骤失败：$name (exit $LASTEXITCODE)" }
}

Step '构建解决方案' { dotnet build Vex.slnx -c Release -v q --nologo -m:1 }
Step 'xunit：Markdown' { dotnet test src/CodeWF.Avalonia.Markdown.Tests/CodeWF.Avalonia.Markdown.Tests.csproj --nologo }
Step 'xunit：Vex.Core' { dotnet test src/Vex.Core.Tests/Vex.Core.Tests.csproj --nologo }
Step 'xunit：MCP' { dotnet run --project tests/Vex.Mcp.Tests/Vex.Mcp.Tests.csproj -c Release }

Write-Host '分支推送门禁全部通过' -ForegroundColor Green
