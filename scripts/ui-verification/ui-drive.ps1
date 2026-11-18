param([string]$Action, [string]$Arg1, [string]$Arg2)
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class Win32 {
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint flags, int dx, int dy, uint data, UIntPtr extra);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
  public struct RECT { public int Left, Top, Right, Bottom; }
}
'@
$vex = Get-Process Vex -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $vex) { Write-Output "no window"; exit 1 }
[Win32]::SetForegroundWindow($vex.MainWindowHandle) | Out-Null
Start-Sleep -Milliseconds 600
$r = New-Object Win32+RECT
[Win32]::GetWindowRect($vex.MainWindowHandle, [ref]$r) | Out-Null
function Shot($path) {
  $w = $r.Right - $r.Left; $h = $r.Bottom - $r.Top
  $shot = New-Object System.Drawing.Bitmap $w, $h
  $g = [System.Drawing.Graphics]::FromImage($shot)
  $g.CopyFromScreen($r.Left, $r.Top, 0, 0, (New-Object System.Drawing.Size $w, $h))
  $shot.Save($path)
  Write-Output "shot $path"
}
function Click($x, $y) {
  [Win32]::SetCursorPos($r.Left + $x, $r.Top + $y) | Out-Null
  Start-Sleep -Milliseconds 200
  [Win32]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)  # down
  [Win32]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)  # up
}
switch ($Action) {
  "shot" { Shot $Arg1 }
  "click" { Click ([int]$Arg1) ([int]$Arg2); Write-Output "clicked $Arg1,$Arg2" }
  "type" { [System.Windows.Forms.SendKeys]::SendWait($Arg1); Write-Output "typed" }
  "enter" { [Win32]::keybd_event(0x0D, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 50; [Win32]::keybd_event(0x0D, 0, 2, [UIntPtr]::Zero); Write-Output "enter" }
  "key" { [Win32]::keybd_event([byte]$Arg1, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 50; [Win32]::keybd_event([byte]$Arg1, 0, 2, [UIntPtr]::Zero); Write-Output "key $Arg1" }
}
