"""真机 GUI 自动化 v2：PrintWindow 截图（不怕窗口覆盖）+ 强制置前校验。"""
import ctypes
import ctypes.wintypes as wt
import time
import sys
from PIL import Image

u = ctypes.windll.user32
g = ctypes.windll.gdi32

def find_vex():
    result = []
    @ctypes.WINFUNCTYPE(ctypes.c_bool, wt.HWND, wt.LPARAM)
    def cb(hwnd, _):
        if u.IsWindowVisible(hwnd):
            buf = ctypes.create_unicode_buffer(256)
            u.GetWindowTextW(hwnd, buf, 256)
            if 'Vex' in buf.value and 'Vex' == buf.value.split(' - ')[-1]:
                result.append((hwnd, buf.value))
        return True
    u.EnumWindows(cb, 0)
    return result

def foreground(hwnd):
    fg = u.GetForegroundWindow()
    if fg == hwnd:
        return True
    # AttachThreadInput 绕前台锁
    fg_thread = u.GetWindowThreadProcessId(fg, None)
    my_thread = u.GetWindowThreadProcessId(hwnd, None)
    u.AttachThreadInput(fg_thread, my_thread, True)
    u.SetForegroundWindow(hwnd)
    u.AttachThreadInput(fg_thread, my_thread, False)
    time.sleep(0.5)
    return u.GetForegroundWindow() == hwnd

def print_shot(path, hwnd):
    """PrintWindow 抓窗口内容（即使被覆盖）。"""
    r = wt.RECT()
    u.GetWindowRect(hwnd, ctypes.byref(r))
    w, h = r.right - r.left, r.bottom - r.top
    hdc = u.GetWindowDC(hwnd)
    mem = g.CreateCompatibleDC(hdc)
    bmp = g.CreateCompatibleBitmap(hdc, w, h)
    g.SelectObject(mem, bmp)
    ok = u.PrintWindow(hwnd, mem, 2)  # PW_RENDERFULLCONTENT
    class BMPINFOHEADER(ctypes.Structure):
        _fields_ = [("biSize", ctypes.c_uint32), ("biWidth", ctypes.c_int32), ("biHeight", ctypes.c_int32),
                    ("biPlanes", ctypes.c_uint16), ("biBitCount", ctypes.c_uint16), ("biCompression", ctypes.c_uint32),
                    ("biSizeImage", ctypes.c_uint32), ("biXPelsPerMeter", ctypes.c_int32), ("biYPelsPerMeter", ctypes.c_int32),
                    ("biClrUsed", ctypes.c_uint32), ("biClrImportant", ctypes.c_uint32)]
    bi = BMPINFOHEADER(ctypes.sizeof(BMPINFOHEADER), w, -h, 1, 32, 0, 0, 0, 0, 0, 0)
    buf = (ctypes.c_ubyte * (w * h * 4))()
    g.GetDIBits(mem, bmp, 0, h, buf, ctypes.byref(bi), 0)
    img = Image.frombuffer('RGBA', (w, h), buf, 'raw', 'BGRA', 0, 1)
    img.convert('RGB').save(path)
    g.DeleteObject(bmp); g.DeleteDC(mem); u.ReleaseDC(hwnd, hdc)
    return ok

def click(x, y):
    u.SetCursorPos(int(x), int(y))
    time.sleep(0.08)
    u.mouse_event(2, 0, 0, 0, 0)
    u.mouse_event(4, 0, 0, 0, 0)
    time.sleep(0.2)

def key(vk, times=1):
    for _ in range(times):
        u.keybd_event(vk, 0, 0, 0)
        u.keybd_event(vk, 0, 2, 0)
        time.sleep(0.12)

if __name__ == '__main__':
    wins = find_vex()
    print('windows:', wins)
    if not wins:
        sys.exit(1)
    hwnd, title = wins[0]
    ok = foreground(hwnd)
    print('foreground ok:', ok)
    print_shot(r'E:\github\Apps\Vex\artifacts\realmachine\20-initial.png', hwnd)
    print('saved')
