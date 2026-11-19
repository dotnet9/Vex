"""真机验证 v3：PostMessage 直达窗口（绕过前台/RDP/覆盖）。"""
import sys, time, ctypes
import ctypes.wintypes as wt
sys.path.insert(0, r'E:\github\Apps\Vex\design\tests')
from realmachine import find_vex, print_shot

u = ctypes.windll.user32
out = r'E:\github\Apps\Vex\artifacts\realmachine'
WM_KEYDOWN, WM_KEYUP, WM_CHAR = 0x0100, 0x0101, 0x0102
WM_MOUSEMOVE, WM_LBUTTONDOWN, WM_LBUTTONUP = 0x0200, 0x0201, 0x0202

wins = find_vex(); hwnd, title = wins[0]
print('title:', title)

def msg_click(cx, cy):
    lp = (cy << 16) | cx
    u.PostMessageW(hwnd, WM_MOUSEMOVE, 0, lp)
    time.sleep(0.05)
    u.PostMessageW(hwnd, WM_LBUTTONDOWN, 1, lp)
    time.sleep(0.06)
    u.PostMessageW(hwnd, WM_LBUTTONUP, 0, lp)
    time.sleep(0.25)

def msg_key(vk, times=1):
    scan = u.MapVirtualKeyW(vk, 0)
    for _ in range(times):
        u.PostMessageW(hwnd, WM_KEYDOWN, vk, (scan << 16) | 1)
        time.sleep(0.05)
        u.PostMessageW(hwnd, WM_KEYUP, vk, (0xC0000000 | (scan << 16)) | 1)
        time.sleep(0.12)

# 恢复草稿横幅先关掉？直接点编辑区。客户区坐标：窗口客户区左上起。
msg_click(580, 220)
print('clicked via message')
time.sleep(0.5)
print_shot(out + r'\30-after-click.png', hwnd)

msg_key(0x0D)  # Enter
print('enter via message')
time.sleep(0.6)
print_shot(out + r'\31-after-enter.png', hwnd)

msg_key(0x41, 3)  # AAA
time.sleep(0.5)
print_shot(out + r'\32-after-type.png', hwnd)
print('done')
