[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',
    [string] $ArtifactsPath = '',
    [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
    [string[]] $ApplicationArguments = @()
)

$ErrorActionPreference = 'Stop'
if (-not (Get-Command dotnet -CommandType Application -ErrorAction SilentlyContinue)) {
    throw 'The .NET 10 SDK is required to start Vex.'
}

$runArguments = @('run', '--project', (Join-Path $PSScriptRoot '../src/Vex/Vex.csproj'),
    '--framework', 'net10.0', '--configuration', $Configuration)
if (-not $ArtifactsPath) { $ArtifactsPath = Join-Path $PSScriptRoot '../../../temp_files/vex-start' }
$runArguments += "-p:ArtifactsPath=$([IO.Path]::GetFullPath($ArtifactsPath))"
if ($ApplicationArguments.Count -gt 0) { $runArguments += @('--') + $ApplicationArguments }

# Resolve global.json and relative document paths from the repository root.
Push-Location -LiteralPath $PSScriptRoot
try {
    & dotnet @runArguments
    $runExitCode = $LASTEXITCODE
}
finally { Pop-Location }
exit $runExitCode
