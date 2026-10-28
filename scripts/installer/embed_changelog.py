#!/usr/bin/env python3
"""Vex.Setup.iss 内嵌更新日志（VChangelogEmbedded）的解码校验与重生成。

背景见 Vex.Setup.iss 内注释与 docs/安装与发布.md v1.11.0 事故记录：CI 的 Inno 6.7.1
运行时所有 UTF-8 解码路径全数判死，唯有「#NNNN 转义 + 显式加号连接」的运行时拼接
形态幸存，故 docs/更新日志.md 必须以内嵌字面量函数随包携带。改完 md 后重生成：

    python scripts/installer/embed_changelog.py            # 校验：内嵌与 md 是否一致
    python scripts/installer/embed_changelog.py --regen    # 重生成内嵌字面量

编码规则（与既有字面量逐字符同构）：
- 源文本统一 LF（剥离 CR），且每个换行翻倍（条目间渲染为空行）；
- 单引号双写为 ''；ASCII 可见字符连续段出字面量；换行与其余非 ASCII 字符出独立 #N 转义；
- 所有 token 之间显式 + 连接：相邻字面量会被编译期常量折叠，折叠即损坏；
- 拆成多条中等长度赋值：单条语句的转义 token 一多即触发 6.7.1 长转义链缺陷。
"""

import argparse
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ISS_PATH = ROOT / 'scripts' / 'installer' / 'Vex.Setup.iss'
MD_PATH = ROOT / 'docs' / '更新日志.md'
FUNC_HEAD = 'function VChangelogEmbedded: String;'
MAX_STATEMENT = 1200


def read_md() -> str:
    return MD_PATH.read_text(encoding='utf-8').replace('\r', '')


def embedded_shape(md: str) -> str:
    """内嵌文本 = md 的每个换行翻倍：条目之间渲染为空行，与既有各版安装器一致。"""
    return md.replace('\n', '\n\n')


def read_embedded() -> str:
    iss = ISS_PATH.read_text(encoding='utf-8')
    match = re.search(re.escape(FUNC_HEAD) + r'\s*begin\n(.*?)\nend;', iss, re.S)
    if not match:
        sys.exit('Vex.Setup.iss 中未找到 VChangelogEmbedded 函数体。')
    return decode_body(match.group(1))


def decode_body(body: str) -> str:
    out = []
    i, n = 0, len(body)
    while i < n:
        ch = body[i]
        if ch in ' \n':
            i += 1
        elif body.startswith('Result:=Result+', i):
            i += len('Result:=Result+')
        elif body.startswith('Result:=', i):
            i += len('Result:=')
        elif ch == '+':
            i += 1
        elif ch == ';':
            i += 1
        elif ch == "'":
            end = i + 1
            buf = []
            while True:
                if end >= n:
                    sys.exit('字面量未闭合。')
                if body[end] == "'":
                    if end + 1 < n and body[end + 1] == "'":
                        buf.append("'")
                        end += 2
                        continue
                    break
                buf.append(body[end])
                end += 1
            out.append(''.join(buf))
            i = end + 1
        elif ch == '#':
            digits = re.match(r'#(\d+)', body[i:])
            if not digits:
                sys.exit(f'非法转义：{body[i:i + 8]!r}')
            out.append(chr(int(digits.group(1))))
            i += digits.end()
        else:
            sys.exit(f'无法解析的 token：{body[i:i + 8]!r}')
    return ''.join(out)


def encode(text: str) -> str:
    tokens = []
    run = []

    def flush():
        if run:
            tokens.append("'" + ''.join(run).replace("'", "''") + "'")
            run.clear()

    for ch in text:
        if ord(ch) < 128 and ch >= ' ':
            run.append(ch)
        else:
            flush()
            tokens.append(f'#{ord(ch)}')
    flush()

    statements = []
    current = ['Result:=']
    size = len('Result:=')
    for index, token in enumerate(tokens):
        piece = token if index == 0 else '+' + token
        if size + len(piece) > MAX_STATEMENT and len(current) > 1:
            statements.append(''.join(current) + ';')
            current = ['Result:=Result+']
            size = len('Result:=Result+')
            piece = token
        current.append(piece)
        size += len(piece)
    statements.append(''.join(current) + ';')
    return '\n\n'.join(statements)


def regen() -> None:
    iss = ISS_PATH.read_text(encoding='utf-8')
    body = re.search(re.escape(FUNC_HEAD) + r'\s*begin\n(.*?)\nend;', iss, re.S)
    if not body:
        sys.exit('Vex.Setup.iss 中未找到 VChangelogEmbedded 函数体。')
    renewed = iss[:body.start(1)] + encode(embedded_shape(read_md())) + iss[body.end(1):]
    ISS_PATH.write_text(renewed, encoding='utf-8', newline='')
    if read_embedded() != embedded_shape(read_md()):
        sys.exit('重生成后回读不一致，Vex.Setup.iss 未被写坏但内容漂移，请检查。')
    print(f'已重生成 {ISS_PATH.name} 的内嵌更新日志（{len(read_md())} 字符）。')


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--regen', action='store_true', help='按 docs/更新日志.md 重生成内嵌字面量')
    args = parser.parse_args()
    if args.regen:
        regen()
        return
    embedded, shaped = read_embedded(), embedded_shape(read_md())
    if embedded == shaped:
        print(f'一致：内嵌更新日志与 {MD_PATH.name} 逐字符相同（{len(shaped)} 字符）。')
        return
    for index, (a, b) in enumerate(zip(embedded, shaped)):
        if a != b:
            context = max(0, index - 40)
            sys.exit(f'第 {index} 字符起不一致：\n内嵌：{embedded[context:index + 40]!r}\nmd　：{shaped[context:index + 40]!r}')
    longer = '内嵌' if len(embedded) > len(shaped) else 'md'
    tail = (embedded if longer == '内嵌' else shaped)[min(len(embedded), len(shaped)):]
    sys.exit(f'{longer}多出 {len(tail)} 字符：{tail[:80]!r}')


if __name__ == '__main__':
    main()
