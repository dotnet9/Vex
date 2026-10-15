[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ExecutablePath,
    [Parameter(Mandatory)][string]$OutputPath,
    [string]$DocumentPath = ""
)

$ErrorActionPreference = "Stop"
$executable = (Resolve-Path -LiteralPath $ExecutablePath).Path
$stream = [IO.File]::OpenRead($executable)
$reader = [IO.BinaryReader]::new($stream)
try {
    $stream.Position = 0x3c
    $peOffset = $reader.ReadInt32()
    $stream.Position = $peOffset
    if ($reader.ReadUInt32() -ne 0x4550) { throw "Not a Windows PE executable: $executable" }
    $optionalHeader = $peOffset + 24
    $stream.Position = $optionalHeader
    $magic = $reader.ReadUInt16()
    $directoryOffset = switch ($magic) { 0x20b { 112 } 0x10b { 96 } default { throw "Unknown PE optional header." } }
    $stream.Position = $optionalHeader + $directoryOffset + 14 * 8
    $clrRva = $reader.ReadUInt32()
    $clrSize = $reader.ReadUInt32()
    if ($clrRva -ne 0 -or $clrSize -ne 0) { throw "The published executable still has a CLR directory." }
}
finally { $reader.Dispose(); $stream.Dispose() }

$startArguments = @{ FilePath = $executable; WorkingDirectory = [IO.Path]::GetDirectoryName($executable); PassThru = $true }
if ($DocumentPath) {
    $document = (Resolve-Path -LiteralPath $DocumentPath).Path
    $startArguments.ArgumentList = '"' + $document + '"'
}
$applicationProcess = Start-Process @startArguments
try { $null = $applicationProcess.WaitForInputIdle(15000) } catch { }
$deadline = [DateTime]::UtcNow.AddSeconds(30)
do {
    $applicationProcess.Refresh()
    if ($applicationProcess.HasExited) { throw "Published application exited with $($applicationProcess.ExitCode)." }
    if ($applicationProcess.MainWindowHandle -ne 0) { break }
    Start-Sleep -Milliseconds 250
} while ([DateTime]::UtcNow -lt $deadline)
Start-Sleep -Seconds 3
$applicationProcess.Refresh()
if ($applicationProcess.HasExited -or $applicationProcess.MainWindowHandle -eq 0 -or -not $applicationProcess.Responding) { throw "Published application did not keep a responding main window." }

$evidence = [ordered]@{
    application = [IO.Path]::GetFileNameWithoutExtension($executable)
    executable = $executable
    sha256 = (Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash.ToLowerInvariant()
    length = (Get-Item -LiteralPath $executable).Length
    clr_directory_rva = $clrRva
    clr_directory_size = $clrSize
    native = $true
    startup = [ordered]@{
        verified_at = [DateTimeOffset]::Now.ToString("o")
        process_id = $applicationProcess.Id
        responding = $applicationProcess.Responding
        main_window_handle = $applicationProcess.MainWindowHandle.ToInt64()
        main_window_title = $applicationProcess.MainWindowTitle
    }
}
$reportPath = [IO.Path]::GetFullPath($OutputPath)
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($reportPath)) | Out-Null
$evidence | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $reportPath -Encoding utf8
Write-Host "NativeAOT startup verified: $($evidence.application), PID $($applicationProcess.Id), $reportPath"
