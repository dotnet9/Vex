[CmdletBinding()]
param([switch] $Check)
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$sourceRoot = Join-Path $PSScriptRoot 'prototype'
$template = Join-Path $sourceRoot 'index.template.html'
$output = Join-Path $repositoryRoot 'design/index.html'
$designRoot = Join-Path $repositoryRoot 'design'

function Resolve-PrototypeAsset([string] $Reference, [string] $BaseDirectory) {
    if ($Reference -match '^(https?:|//)') { throw "原型运行资源不能依赖网络：$Reference" }
    $decoded = [Uri]::UnescapeDataString(($Reference -split '[?#]', 2)[0])
    $candidate = [IO.Path]::GetFullPath((Join-Path $BaseDirectory $decoded))
    if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
        $candidate = [IO.Path]::GetFullPath((Join-Path $designRoot $decoded))
    }
    if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) { throw "原型资源不存在：$Reference" }
    return $candidate
}
function Convert-PrototypeImage([string] $Reference, [string] $BaseDirectory) {
    $file = Resolve-PrototypeAsset $Reference $BaseDirectory
    $mime = switch ([IO.Path]::GetExtension($file).ToLowerInvariant()) {
        '.svg' { 'image/svg+xml' } '.png' { 'image/png' } '.jpg' { 'image/jpeg' }
        '.jpeg' { 'image/jpeg' } '.gif' { 'image/gif' } '.webp' { 'image/webp' }
        '.ico' { 'image/x-icon' } '.woff2' { 'font/woff2' } '.woff' { 'font/woff' }
        default { throw "未支持的原型资源类型：$file" }
    }
    # SVG 属于文本，先规范换行，避免 Git 在 Windows 检出 CRLF 后使生成物不稳定。
    $bytes = if ([IO.Path]::GetExtension($file).Equals('.svg', [StringComparison]::OrdinalIgnoreCase)) {
        [Text.Encoding]::UTF8.GetBytes([IO.File]::ReadAllText($file).ReplaceLineEndings("`n"))
    } else { [IO.File]::ReadAllBytes($file) }
    return 'data:' + $mime + ';base64,' + [Convert]::ToBase64String($bytes)
}
function Read-PrototypeCss([string] $Path) {
    $css = [IO.File]::ReadAllText($Path)
    $base = Split-Path -Parent $Path
    return [regex]::Replace($css, 'url\(\s*["'']?(?<path>[^)"'']+)["'']?\s*\)', [Text.RegularExpressions.MatchEvaluator] {
        param($match)
        $reference = $match.Groups['path'].Value.Trim()
        if ($reference.StartsWith('#') -or $reference.StartsWith('data:')) { return $match.Value }
        return 'url("' + (Convert-PrototypeImage $reference $base) + '")'
    })
}
$html = [IO.File]::ReadAllText($template)
$html = [regex]::Replace($html, '<link\b[^>]*>', [Text.RegularExpressions.MatchEvaluator] {
    param($match)
    if ($match.Value -notmatch '\brel\s*=\s*["'']stylesheet["'']') { return $match.Value }
    $reference = [regex]::Match($match.Value, '\bhref\s*=\s*["''](?<path>[^"'']+)["'']').Groups['path'].Value
    $file = Resolve-PrototypeAsset $reference $sourceRoot
    return "<style>`n" + (Read-PrototypeCss $file) + "`n</style>"
})
$html = [regex]::Replace($html, '<script\b[^>]*\bsrc\s*=\s*["''](?<path>[^"'']+)["''][^>]*>\s*</script\s*>', [Text.RegularExpressions.MatchEvaluator] {
    param($match)
    $file = Resolve-PrototypeAsset $match.Groups['path'].Value $sourceRoot
    $javascript = [IO.File]::ReadAllText($file) -replace '(?i)</script', '<\/script'
    return "<script>`n" + $javascript + "`n</script>"
})
# 静态 HTML 和脚本中引用的本地图片一并内嵌；动态模板和已有 data URI 保持原样。
$html = [regex]::Replace($html, '(?<quote>["''])(?<path>(?:\.\./|\./|res/|assets/)[^"''<>\s]+\.(?:png|jpe?g|webp|gif|ico|svg))\k<quote>', [Text.RegularExpressions.MatchEvaluator] {
    param($match)
    return $match.Groups['quote'].Value + (Convert-PrototypeImage $match.Groups['path'].Value $sourceRoot) + $match.Groups['quote'].Value
})
# 没有目录前缀的 HTML 图片和图标也必须内嵌。
$html = [regex]::Replace($html, '(?<prefix><(?:img|link)\b[^>]*?\b(?:src|href)\s*=\s*)(?<quote>["''])(?<path>[^"'']+\.(?:png|jpe?g|webp|gif|ico|svg))\k<quote>', [Text.RegularExpressions.MatchEvaluator] {
    param($match)
    return $match.Groups['prefix'].Value + $match.Groups['quote'].Value + (Convert-PrototypeImage $match.Groups['path'].Value $sourceRoot) + $match.Groups['quote'].Value
})
$html = $html.ReplaceLineEndings("`n")
if ($Check) {
    if (-not (Test-Path -LiteralPath $output) -or [IO.File]::ReadAllText($output).ReplaceLineEndings("`n") -cne $html) {
        throw '单文件原型未更新，请运行 scripts/build_standalone_prototype.ps1。'
    }
} else { [IO.File]::WriteAllText($output, $html, [Text.UTF8Encoding]::new($false)) }
Write-Host "单文件原型已校验：$output"
