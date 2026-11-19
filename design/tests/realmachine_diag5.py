"""真机诊断 v5：TOPMOST+全屏截图对照 + 草稿内容。"""
import sys, time, glob, os, ctypes
import ctypes.wintypes as wt
sys.path.insert(0, r'E:\github\Apps\Vex\design\tests')
from realmachine import find_vex
from PIL import ImageGrab

u = ctypes.windll.user32
out = r'E:\github\Apps\Vex\artifacts\realmachine'

wins = find_vex(); hwnd, title = wins[0]
SWP = 0x0001 | 0x0040
u.SetWindowPos(hwnd, -1, 0, 0, 0, 0, SWP)
time.sleep(0.5)
r = wt.RECT(); u.GetWindowRect(hwnd, ctypes.byref(r))
img = ImageGrab.grab(bbox=(r.left, r.top, r.right, r.bottom))
img.save(out + r'\50-desktop-capture.png')
print('desktop shot saved')
u.SetWindowPos(hwnd, -2, 0, 0, 0, 0, SWP)

# 草稿内容（自动保存已触发）
for pat in [os.path.expandvars(r'%LOCALAPPDATA%/Vex/**/*draft*'),
            os.path.expandvars(r'%LOCALAPPDATA%/Vex/**/*untitled*'),
            os.path.expandvars(r'%LOCALAPPDATA%/Vex/**/*recovery*')]:
    for f in glob.glob(pat, recursive=True):
        print('draft file:', f)
