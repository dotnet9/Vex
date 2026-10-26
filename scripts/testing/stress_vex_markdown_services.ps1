[CmdletBinding()]
param(
    [int]$Lines = 120000,
    [int]$HeadingInterval = 40,
    [int]$FenceInterval = 1000,
    [int]$TableInterval = 250,
    [string]$WorkRoot = "",
    [switch]$KeepWorkRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-RepoRoot {
    return (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
}

function Invoke-DotNet([string[]]$Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

if ($Lines -lt 1000) {
    throw "Lines must be at least 1000 for a meaningful stress run."
}

foreach ($interval in @($HeadingInterval, $FenceInterval, $TableInterval)) {
    if ($interval -le 0) {
        throw "Intervals must be positive."
    }
}

$repoRoot = Get-RepoRoot
$vexProjectPath = Join-Path $repoRoot "src\Vex\Vex.csproj"
if (-not (Test-Path -LiteralPath $vexProjectPath -PathType Leaf)) {
    throw "Vex project was not found at '$vexProjectPath'."
}

$createdTemporaryRoot = $false
if ([string]::IsNullOrWhiteSpace($WorkRoot)) {
    $WorkRoot = Join-Path ([System.IO.Path]::GetTempPath()) "VexMarkdownStress-$([Guid]::NewGuid().ToString('N'))"
    $createdTemporaryRoot = $true
}

try {
    New-Item -ItemType Directory -Force -Path $WorkRoot | Out-Null

    $escapedVexProjectPath = [System.Security.SecurityElement]::Escape($vexProjectPath)
    $stressProjectPath = Join-Path $WorkRoot "VexMarkdownStress.csproj"
    $projectContent = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="$escapedVexProjectPath" />
  </ItemGroup>
</Project>
"@
    Set-Content -LiteralPath $stressProjectPath -Value $projectContent -Encoding UTF8

    $programContent = @"
using System.Diagnostics;
using System.Text;
using Vex.Modules.Workspace.Services;

var lines = int.Parse(args[0]);
var headingInterval = int.Parse(args[1]);
var fenceInterval = int.Parse(args[2]);
var tableInterval = int.Parse(args[3]);

var markdown = BuildMarkdown(lines, headingInterval, fenceInterval, tableInterval);
Console.WriteLine($"markdown_chars={markdown.Length}");
Console.WriteLine($"requested_lines={lines}");

var outlineService = new MarkdownOutlineService();
var statisticsService = new MarkdownStatisticsService();

var outline = Measure("outline", () => outlineService.BuildOutline(markdown));
var statistics = Measure("statistics", () => statisticsService.Count(markdown));

Console.WriteLine($"outline_count={outline.Count}");
Console.WriteLine($"statistics_lines={statistics.Lines}");
Console.WriteLine($"statistics_words={statistics.Words}");
Console.WriteLine($"statistics_headings={statistics.Headings}");

if (outline.Count == 0)
{
    throw new InvalidOperationException("Outline stress run produced no headings.");
}

if (statistics.Lines < lines)
{
    throw new InvalidOperationException($"Statistics line count {statistics.Lines} is lower than requested {lines}.");
}

static T Measure<T>(string name, Func<T> action)
{
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();

    var beforeBytes = GC.GetTotalAllocatedBytes(true);
    var stopwatch = Stopwatch.StartNew();
    var result = action();
    stopwatch.Stop();
    var allocatedBytes = GC.GetTotalAllocatedBytes(true) - beforeBytes;
    Console.WriteLine($"{name}_ms={stopwatch.ElapsedMilliseconds}");
    Console.WriteLine($"{name}_allocated_bytes={allocatedBytes}");
    return result;
}

static string BuildMarkdown(int lines, int headingInterval, int fenceInterval, int tableInterval)
{
    var builder = new StringBuilder(lines * 96);
    var line = 1;
    while (line <= lines)
    {
        if (line % fenceInterval == 0 && line + 5 <= lines)
        {
            builder.AppendLine("```csharp");
            builder.AppendLine("# fenced heading should be ignored");
            builder.AppendLine("var sample = \"large markdown\";");
            builder.AppendLine("```");
            builder.AppendLine();
            line += 5;
            continue;
        }

        if (line % tableInterval == 0 && line + 4 <= lines)
        {
            builder.AppendLine("| Name | Value | Notes |");
            builder.AppendLine("| --- | --- | --- |");
            builder.AppendLine("| **bold** | `code` | [link](https://example.com) |");
            builder.AppendLine("| plain | ~~old~~ | long table cell text for wrapping |");
            line += 4;
            continue;
        }

        if (line % headingInterval == 0)
        {
            builder.AppendLine($"## Section {line}");
        }
        else if (line % 11 == 0)
        {
            builder.AppendLine("- [x] completed stress task item with enough content to exercise scanning.");
        }
        else
        {
            builder.AppendLine($"Paragraph {line} contains latin words, 中文字符, inline `code`, and enough text for counting.");
        }

        line++;
    }

    return builder.ToString();
}
"@
    Set-Content -LiteralPath (Join-Path $WorkRoot "Program.cs") -Value $programContent -Encoding UTF8

    Invoke-DotNet @(
        "run",
        "--project",
        $stressProjectPath,
        "--",
        $Lines.ToString(),
        $HeadingInterval.ToString(),
        $FenceInterval.ToString(),
        $TableInterval.ToString()
    )
}
finally {
    if ($createdTemporaryRoot -and -not $KeepWorkRoot -and (Test-Path -LiteralPath $WorkRoot)) {
        $resolvedWorkRoot = (Resolve-Path -LiteralPath $WorkRoot).Path
        $tempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
        if (-not $resolvedWorkRoot.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to remove stress work root outside the temp directory: '$resolvedWorkRoot'."
        }

        Remove-Item -LiteralPath $resolvedWorkRoot -Recurse -Force
    }
}
