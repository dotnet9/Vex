"""真机验证：```csharp+回车 → 代码块源码编辑。"""
import sys, time
import ctypes
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
    time.sleep(0.3)

def msg_char(ch):
    u.PostMessageW(hwnd, WM_CHAR, ord(ch), 0)
    time.sleep(0.1)

def msg_key(vk):
    scan = u.MapVirtualKeyW(vk, 0)
    u.PostMessageW(hwnd, WM_KEYDOWN, vk, (scan << 16) | 1)
    time.sleep(0.05)
    u.PostMessageW(hwnd, WM_KEYUP, vk, (0xC0000000 | (scan << 16)) | 1)
    time.sleep(0.15)

msg_click(580, 220)
for ch in '```csharp':
    msg_char(ch)
time.sleep(0.3)
print_shot(out + r'\60-fence-typed.png', hwnd)
msg_key(0x0D)  # 回车 → 围栏补全
time.sleep(0.6)
print_shot(out + r'\61-fence-enter.png', hwnd)
for ch in 'var x = 1;':
    msg_char(ch)
time.sleep(0.4)
print_shot(out + r'\62-fence-code.png', hwnd)
print('done')
