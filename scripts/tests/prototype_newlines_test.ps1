# 用独立夹具证明源码的 LF/CRLF 不影响单文件 HTML；不修改真实原型。
$ErrorActionPreference = 'Stop'
$builder = Join-Path (Split-Path -Parent $PSScriptRoot) 'build_standalone_prototype.ps1'
$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('prototype-newlines-' + [Guid]::NewGuid().ToString('N'))
$source = Join-Path $fixtureRoot 'scripts/prototype'
$design = Join-Path $fixtureRoot 'design'
try {
    New-Item -ItemType Directory -Path $source, $design -Force | Out-Null
    Copy-Item -LiteralPath $builder -Destination (Join-Path $fixtureRoot 'scripts/build_standalone_prototype.ps1')
    $template = '<!doctype html><html><head><link rel="stylesheet" href="sample.css"></head><body><img src="./logo.svg"><script src="sample.js"></script></body></html>'
    foreach ($name in 'index.template.html', 'property-panel.template.html') { [IO.File]::WriteAllText((Join-Path $source $name), $template) }
    [IO.File]::WriteAllText((Join-Path $source 'sample.css'), 'body { background-image: url("./logo.svg"); }' + [char]10)
    [IO.File]::WriteAllText((Join-Path $source 'sample.js'), 'const icon = "./logo.svg";' + [char]10)
    $svg = '<svg xmlns="http://www.w3.org/2000/svg">' + [char]10 + '<circle cx="8" cy="8" r="8"/>' + [char]10 + '</svg>' + [char]10
    [IO.File]::WriteAllText((Join-Path $source 'logo.svg'), $svg)
    $fixtureBuilder = Join-Path $fixtureRoot 'scripts/build_standalone_prototype.ps1'
    & $fixtureBuilder
    $first = @{}
    foreach ($file in Get-ChildItem -LiteralPath $design -Filter '*.html') { $first[$file.Name] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash }
    if ($first.Count -eq 0) { throw '夹具没有生成 HTML。' }
    foreach ($file in Get-ChildItem -LiteralPath $source -File) {
        [IO.File]::WriteAllText($file.FullName, [IO.File]::ReadAllText($file.FullName).ReplaceLineEndings(([char]13).ToString() + [char]10))
    }
    & $fixtureBuilder
    & $fixtureBuilder -Check
    foreach ($file in Get-ChildItem -LiteralPath $design -Filter '*.html') {
        if ($first[$file.Name] -ne (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash) { throw "LF 与 CRLF 生成结果不同：$($file.Name)" }
        $html = [IO.File]::ReadAllText($file.FullName)
        if ($html.Contains('src="./logo.svg"') -or -not $html.Contains('data:image/svg+xml;base64,')) { throw 'SVG 没有内嵌。' }
    }
    Write-Host 'PASS：LF/CRLF 的 CSS、JS、SVG 和 HTML 生成完全相同的单文件原型。'
} finally {
    $resolvedFixture = [IO.Path]::GetFullPath($fixtureRoot)
    $resolvedTemp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if (-not $resolvedFixture.StartsWith($resolvedTemp, [StringComparison]::OrdinalIgnoreCase)) { throw '拒绝清理临时目录之外的路径。' }
    if (Test-Path -LiteralPath $resolvedFixture) { Remove-Item -LiteralPath $resolvedFixture -Recurse -Force }
}
