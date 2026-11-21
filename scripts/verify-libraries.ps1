# 本地库包门禁（lib-v* tag 推送前由 .githooks/pre-push 强制执行）。
param()
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

Step '四库打包冒烟' {
    $out = Join-Path ([IO.Path]::GetTempPath()) 'vex-lib-pack-smoke'
    foreach ($project in @(
            'src/CodeWF.Avalonia.Markdown/CodeWF.Avalonia.Markdown.csproj',
            'src/CodeWF.Avalonia.Markdown.Lite/CodeWF.Avalonia.Markdown.Lite.csproj',
            'src/CodeWF.Avalonia.Markdown.Core/CodeWF.Avalonia.Markdown.Core.csproj',
            'src/CodeWF.Avalonia.MindView/CodeWF.Avalonia.MindView.csproj')) {
        dotnet pack $project -c Release -o $out
        if ($LASTEXITCODE -ne 0) { throw "pack failed: $project" }
    }
}
Write-Host '本地库包门禁全部通过' -ForegroundColor Green
