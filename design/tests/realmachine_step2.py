"""步骤2：点击编辑区空白 → 截图（看光标）→ 敲回车 → 截图（看拆块）。"""
import sys
sys.path.insert(0, r'E:\github\Apps\Vex\design\tests')
from realmachine import find_vex, foreground, rect, shot, click, key

wins = find_vex()
hwnd, title = wins[0]
foreground(hwnd)
r = rect(hwnd)
# 编辑区空白（工具带下方一大片空）——窗口内相对 (580, 200) 处
x = r.left + 580
y = r.top + 200
click(x, y)
print('clicked', x, y)
shot(r'E:\github\Apps\Vex\artifacts\realmachine\02-after-click.png', hwnd)
print('saved 02-after-click')
key(0x0D)  # VK_RETURN
print('pressed Enter')
time.sleep(0.5)
shot(r'E:\github\Apps\Vex\artifacts\realmachine\03-after-enter.png', hwnd)
print('saved 03-after-enter')
