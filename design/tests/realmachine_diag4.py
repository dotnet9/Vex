"""真机诊断 v4：剪贴板内容 + 光标闪烁连拍。"""
import sys, time, ctypes
import ctypes.wintypes as wt
sys.path.insert(0, r'E:\github\Apps\Vex\design\tests')
from realmachine import find_vex, print_shot

u = ctypes.windll.user32
out = r'E:\github\Apps\Vex\artifacts\realmachine'
WM_KEYDOWN, WM_KEYUP = 0x0100, 0x0101

wins = find_vex(); hwnd, title = wins[0]

def msg_key_mod(vk, mods=0, times=1):
    scan = u.MapVirtualKeyW(vk, 0)
    for _ in range(times):
        if mods: u.keybd_event(mods, 0, 0, 0)
        u.PostMessageW(hwnd, WM_KEYDOWN, vk, (scan << 16) | 1)
        time.sleep(0.04)
        u.PostMessageW(hwnd, WM_KEYUP, vk, (0xC0000000 | (scan << 16)) | 1)
        if mods: u.keybd_event(mods, 0, 2, 0)
        time.sleep(0.12)

# Ctrl+A 全选 + Ctrl+C 复制（焦点在第二块编辑器）
msg_key_mod(0x41, 0x02)  # Ctrl+A
msg_key_mod(0x43, 0x02)  # Ctrl+C
time.sleep(0.4)
k = ctypes.windll.kernel32
if u.OpenClipboard(hwnd):
    h = u.GetClipboardData(13)  # CF_UNICODETEXT
    if h:
        p = k.GlobalLock(h)
        text = ctypes.c_wchar_p(p).value if p else '<null>'
        k.GlobalUnlock(h)
        print('CLIPBOARD:', repr(text))
    else:
        print('CLIPBOARD: <empty>')
    u.CloseClipboard()

# 连拍三张看光标闪烁（间隔 300ms）
for i in range(3):
    print_shot(out + rf'\4{i}-caret-blink.png', hwnd)
    time.sleep(0.35)
print('done')
