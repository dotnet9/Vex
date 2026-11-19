"""真机验证 v2：TOPMOST 点击 + PrintWindow 截图。"""
import sys, time, ctypes
import ctypes.wintypes as wt
sys.path.insert(0, r'E:\github\Apps\Vex\design\tests')
from realmachine import find_vex, foreground, print_shot, click, key

u = ctypes.windll.user32
out = r'E:\github\Apps\Vex\artifacts\realmachine'

wins = find_vex(); hwnd, title = wins[0]
print('title:', title)
SWP = 0x0001 | 0x0040  # SWP_NOSIZE | SWP_SHOWWINDOW
u.SetWindowPos(hwnd, -1, 0, 0, 0, 0, SWP)  # HWND_TOPMOST
time.sleep(0.4)

rr = wt.RECT()
u.GetWindowRect(hwnd, ctypes.byref(rr))
print('rect:', rr.left, rr.top, rr.right, rr.bottom)

print_shot(out + r'\21-initial.png', hwnd)
click(rr.left + 580, rr.top + 220)
print('clicked')
time.sleep(0.5)
print_shot(out + r'\22-after-click.png', hwnd)

key(0x0D)  # Enter
time.sleep(0.6)
print_shot(out + r'\23-after-enter.png', hwnd)

key(0x41, 3)  # 'AAA'
time.sleep(0.5)
print_shot(out + r'\24-after-type.png', hwnd)
u.SetWindowPos(hwnd, -2, 0, 0, 0, 0, SWP)  # HWND_NOTOPMOST
print('done')
