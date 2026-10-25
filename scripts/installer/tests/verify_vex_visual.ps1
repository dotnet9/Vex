[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Compiler,
    [Parameter(Mandatory)][string]$Output,
    [string]$ReferenceDirectory=''
)
$ErrorActionPreference='Stop'
if (-not $IsWindows) { throw '原生视觉验证仅支持 Windows。' }
$Output=[IO.Path]::GetFullPath($Output)
New-Item -ItemType Directory -Path $Output -Force | Out-Null
$fixtures=@{}
foreach($kind in @('new','maintenance','current','missing','unknown','multiple','migration')) {
    $folder=Join-Path $Output ('bin-'+$kind)
    New-Item -ItemType Directory -Path $folder -Force | Out-Null
    $arguments=@('/Qp',('/O'+$folder))
    if ($kind -ne 'new') { $arguments+='/DVexFixtureMaintenance' }
    if ($kind -eq 'current') { $arguments+='/DVexFixtureCurrentVersion' }
    if ($kind -eq 'unknown') { $arguments+='/DVexFixtureUnknownLocation' }
    if ($kind -eq 'missing') { $arguments+='/DVexFixtureMissingExecutable' }
    if ($kind -in @('multiple','migration')) { $arguments+='/DVexFixtureMultiple' }
    if ($kind -eq 'migration') { $arguments+='/DVexFixtureMigration' }
    & $Compiler @arguments (Join-Path $PSScriptRoot 'Vex.Fixture.iss')
    if ($LASTEXITCODE) { throw "夹具编译失败：$kind" }
    $fixtures[$kind]=Join-Path $folder 'Vex.Installer.Fixture.exe'
}
foreach($language in @('chinesesimplified','english')) {
    $lang=if($language -eq 'english'){'en'}else{'zh'}
    foreach($theme in @('light','dark')) {
        & (Join-Path $PSScriptRoot 'verify_vex_fixture.ps1') -Installer $fixtures.new -Language $language -Theme $theme -Output (Join-Path $Output "$lang-$theme")
        & (Join-Path $PSScriptRoot 'verify_vex_fixture.ps1') -Installer $fixtures.maintenance -Language $language -Theme $theme -Maintenance -Output (Join-Path $Output "$lang-$theme-maintenance")
    }
}
# 已安装版本与安装包一致时必须回到「修复安装」，两种语言各跑一遍浅色列。
foreach($language in @('chinesesimplified','english')) {
    $lang=if($language -eq 'english'){'en'}else{'zh'}
    & (Join-Path $PSScriptRoot 'verify_vex_fixture.ps1') -Installer $fixtures.current -Language $language -Theme light -Maintenance -CurrentVersion -Output (Join-Path $Output "$lang-light-maintenance-current")
}
& (Join-Path $PSScriptRoot 'verify_vex_fixture.ps1') -Installer $fixtures.unknown -Theme light -Maintenance -UnknownLocation -Output (Join-Path $Output 'unknown-location')
& (Join-Path $PSScriptRoot 'verify_vex_fixture.ps1') -Installer $fixtures.missing -Theme light -Maintenance -MissingExecutable -Output (Join-Path $Output 'missing-executable')
& (Join-Path $PSScriptRoot 'verify_vex_fixture.ps1') -Installer $fixtures.multiple -Language english -Theme dark -Maintenance -Multiple -Output (Join-Path $Output 'multiple-scopes')
& (Join-Path $PSScriptRoot 'verify_vex_fixture.ps1') -Installer $fixtures.new -Theme light -ReduceMotion -Output (Join-Path $Output 'reduced-motion')
foreach($language in @('chinesesimplified','english')) {
    & (Join-Path $PSScriptRoot 'verify_vex_fixture.ps1') -Installer $fixtures.migration -Language $language -Theme light -Maintenance -Multiple -Migration -Output (Join-Path $Output ('migration-'+$language))
}
if($ReferenceDirectory) {
    python (Join-Path $PSScriptRoot 'compare_vex_visual.py') --reference $ReferenceDirectory --native $Output
    if($LASTEXITCODE) { throw '原型截图对照未通过。' }
}
Write-Host "PASS: 原生中英文、浅深色与安装维护矩阵通过；证据：$Output"
