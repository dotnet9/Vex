$ErrorActionPreference = 'Stop'
$temporaryRoot = if ($env:CODEWF_TEST_TMP) { $env:CODEWF_TEST_TMP } else { [IO.Path]::GetTempPath() }
$temporaryRoot = [IO.Path]::GetFullPath($temporaryRoot)
$fixture = Join-Path $temporaryRoot ('codewf-installers-test-' + [guid]::NewGuid().ToString('N'))
$verify = Join-Path $PSScriptRoot '../verify_release_installers.ps1'
function Assert-Rejected([scriptblock] $Action) {
    $rejected = $false
    try { & $Action } catch { $rejected = $true }
    if (-not $rejected) { throw '错误的发布产物未被拒绝。' }
}
function New-ControlledPackages([string] $Directory, [string] $Product = 'Vex', [string] $Version = '1.2.3') {
    New-Item -ItemType Directory -Path $Directory -Force | Out-Null
    foreach ($name in @(
        "$Product-v$Version-win-x64-setup.exe",
        "$Product-v$Version-linux-x64.deb",
        "$Product-v$Version-linux-arm64.deb",
        "$Product-$Version-osx-x64.dmg",
        "$Product-$Version-osx-arm64.dmg",
        "$Product-v$Version-win-x64.zip",
        "$Product-v$Version-linux-x64.tar.gz",
        "$Product-v$Version-linux-arm64.tar.gz",
        "$Product-$Version-osx-x64.zip",
        "$Product-$Version-osx-arm64.zip"
    )) {
        $file = Join-Path $Directory $name
        Set-Content -LiteralPath $file -Value ('受控安装包：' + $name) -Encoding utf8
        $hash = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash  $name" | Set-Content -LiteralPath ($file + '.sha256') -Encoding ascii
    }
}
try {
    $valid = Join-Path $fixture 'valid'
    New-ControlledPackages $valid
    & $verify -Version '1.2.3' -PackageDirectory $valid
    & $verify -Version 'v1.2.3' -PackageDirectory $valid

    $wrongVersion = Join-Path $fixture 'wrong-version'
    New-ControlledPackages $wrongVersion -Version '1.2.3.4'
    Assert-Rejected { & $verify -Version '1.2.3' -PackageDirectory $wrongVersion }
    $wrongProduct = Join-Path $fixture 'wrong-product'
    New-ControlledPackages $wrongProduct -Product 'AnotherApp'
    Assert-Rejected { & $verify -Version '1.2.3' -PackageDirectory $wrongProduct }

    $extra = Join-Path $valid 'Vex-portable.zip'
    Set-Content -LiteralPath $extra -Value '禁止的便携压缩包'
    Assert-Rejected { & $verify -Version '1.2.3' -PackageDirectory $valid }
    Remove-Item -LiteralPath $extra -Force
    $checksum = Get-ChildItem -LiteralPath $valid -Filter '*.sha256' | Select-Object -First 1
    Set-Content -LiteralPath $checksum.FullName -Value ('0' * 64 + '  ' + $checksum.BaseName)
    Assert-Rejected { & $verify -Version '1.2.3' -PackageDirectory $valid }
    Remove-Item -LiteralPath $checksum.FullName -Force
    Assert-Rejected { & $verify -Version '1.2.3' -PackageDirectory $valid }

    New-ControlledPackages $valid
    $installer = Join-Path $valid 'Vex-v1.2.3-win-x64-setup.exe'
    Remove-Item -LiteralPath $installer -Force
    Assert-Rejected { & $verify -Version '1.2.3' -PackageDirectory $valid }
    [IO.File]::WriteAllBytes($installer, [byte[]]::new(0))
    $emptyHash = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant()
    "$emptyHash  $([IO.Path]::GetFileName($installer))" | Set-Content -LiteralPath ($installer + '.sha256') -Encoding ascii
    Assert-Rejected { & $verify -Version '1.2.3' -PackageDirectory $valid }
    Write-Host 'PASS: 完整五平台安装包与绿色版压缩包通过，错误版本、错误产品、额外压缩包、篡改、缺失和空文件均被拒绝。'
}
finally {
    $resolved = [IO.Path]::GetFullPath($fixture)
    if ([IO.Path]::GetDirectoryName($resolved) -ne $temporaryRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) -or
        -not [IO.Path]::GetFileName($resolved).StartsWith('codewf-installers-test-')) { throw '临时目录范围错误。' }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}