"""真机验证：块首标记转换族 + 列表聚焦源码化（无叠影）。"""
import sys, time, ctypes
import ctypes.wintypes as wt
sys.path.insert(0, r'E:\github\Apps\Vex\design\tests')
from realmachine import find_vex, print_shot

u = ctypes.windll.user32
out = r'E:\github\Apps\Vex\artifacts\realmachine'
WM_KEYDOWN, WM_KEYUP = 0x0100, 0x0101
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
    u.PostMessageW(hwnd, 0x0102, ord(ch), 0)  # WM_CHAR
    time.sleep(0.1)

def msg_key(vk):
    scan = u.MapVirtualKeyW(vk, 0)
    u.PostMessageW(hwnd, WM_KEYDOWN, vk, (scan << 16) | 1)
    time.sleep(0.05)
    u.PostMessageW(hwnd, WM_KEYUP, vk, (0xC0000000 | (scan << 16)) | 1)
    time.sleep(0.12)

# 场景 A：空文档输入 "# 标题测试" → 应转标题（# 消失，失焦渲染大字号）
msg_click(580, 220)
msg_char('#'); msg_char(' '); msg_char('标'); msg_char('题'); msg_char('测'); msg_char('试')
time.sleep(0.5)
print_shot(out + r'\40-heading-editing.png', hwnd)
msg_key(0x1B)  # Escape 失焦
time.sleep(0.4)
print_shot(out + r'\41-heading-rendered.png', hwnd)

# 场景 B：输入 "- 列表一" 回车 "- 列表二" → 两条无序；点击第二项 → 无叠影
msg_key(0x0D)
msg_char('-'); msg_char(' '); msg_char('列'); msg_char('表'); msg_char('一')
msg_key(0x0D)
msg_char('-'); msg_char(' '); msg_char('列'); msg_char('表'); msg_char('二')
time.sleep(0.5)
msg_key(0x1B)
time.sleep(0.4)
print_shot(out + r'\42-lists-rendered.png', hwnd)
# 点击第二项聚焦（源码形态应无叠影）
msg_click(580, 330)
time.sleep(0.4)
print_shot(out + r'\43-list-focused.png', hwnd)
print('done')
