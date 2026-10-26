"""生成可重复的本地 Markdown 性能样本；已有非同内容文件不覆盖。"""
import argparse
import hashlib
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("--output-root", required=True)
root = Path(parser.parse_args().output_root).resolve()
root.mkdir(parents=True, exist_ok=True)


def save(name, text):
    path = root / name
    data = text.replace("\r\n", "\n").encode("utf-8")
    if path.exists() and path.read_bytes() != data:
        raise SystemExit(f"拒绝覆盖已有不同内容文件：{path}")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(data)
    print(f"{len(data):>9} bytes  {hashlib.sha256(data).hexdigest()}  {path}")


intro = """\n本文件由 Vex 的测试样本生成脚本生成，用于检查渲染、编辑、文档切换、滚动与导图同步。
每份文档具有不同的主标题和结束标记；浏览、切换视图和选择文字不应修改文件。
"""
formulas = [
    r"E = mc^2, \quad \alpha + \beta \leq \gamma, \quad x \in \mathbb{R}",
    r"\int_0^\infty e^{-x^2}\,dx = \frac{\sqrt{\pi}}{2}",
    r"\frac{\partial u}{\partial t} + \nabla \cdot (\rho \mathbf{v}) = 0",
    r"\sum_{n=1}^{\infty}\frac{1}{n^2} = \frac{\pi^2}{6}",
    r"A=\begin{pmatrix}1&2&3\\4&5&6\\7&8&9\end{pmatrix}",
    r"f(x)=\begin{cases}x^2 & x\geq 0\\-x & x<0\end{cases}",
    r"\hat{\theta} = \operatorname{argmin}_{\theta} \sum_{i=1}^{N}(y_i-f_\theta(x_i))^2",
    r"\begin{aligned}a+b&=c\\a^2+b^2&=c^2\end{aligned}",
]


def section(i):
    table = "\n".join(f"| P{i:03}-{j:02} | {j * 1.25:.2f} | Pa / K | {'正常' if j % 3 else '**待复核**'} |" for j in range(12))
    code = "\n".join(f"    var sample{j:02} = source.Read({i}, {j}); // 第 {i} 组第 {j} 个采样点" for j in range(28))
    return f"""\n## 第 {i:03} 章：通道 {i} 的计算与验证

### 计算输入 {i}

这是第 {i} 章独立正文。检查 **加粗**、*斜体*、~~删除线~~、`inline_{i}`、[本地章节](#验收标记) 与 $pV=nRT$。
中英文混排 Text selection, Ελληνικά αβγ, 数学符号 ∑ ∫ ∂ ∇ ∈ ≠ ≤ ≥ ± × ÷ ∞，以及 emoji 👩‍💻 🚀。
段落按自然行宽换行，点击前后文字位置应保持稳定，拖选后复制不应丢失句子。

> 通道 {i} 的输入需要复核。
>
> 引用中的第二段包含 **关键量** 与 `sample_id`。

- [x] 输入单位检查
- [ ] 误差复核
  - 子任务：保留缩进
  - 子任务：检查回写

| 参数 | 数值 | 单位 | 状态 |
| :--- | ---: | :---: | --- |
{table}

### 算法与误差 {i}

```csharp
public static void CalculateChannel{i}(ISource source)
{{
{code}
    Console.WriteLine("channel-{i}: <ok> & complete");
}}
```

$$
{formulas[i % len(formulas)]}
$$

#### 章节结论 {i}

通道 {i} 校验完成。当前章节的独立标记为 MIX-{i:04}，视图切换后应能找到同一章节。
"""


overview = "# VEX-A 综合语法与交互样本\n" + intro
overview += """
## 文字与结构

普通文字、**粗体中的 *斜体***、`a < b && c > d`、[网页链接](https://example.com)、脚注[^note]。

第一行保留两个尾随空格。  
这是硬换行；下一行是
软换行，检查宽度变化。

1. 有序步骤一
2. 有序步骤二
   1. 嵌套步骤

### 本地图像

![本地测试示意图](Vex-测试资源/通道示意.svg)

```json
{"name": "维刻 Vex", "enabled": true, "values": [1, 2, 3], "text": "<tag> & quote"}
```

```python
def energy(mass: float) -> float:
    return mass * 299_792_458 ** 2
```

```mermaid
flowchart LR
  A[输入] --> B[计算]
  B --> C{校验}
  C -->|通过| D[输出]
  C -->|重试| A
```

[^note]: 脚注需要保留内容与定位关系。
"""
overview += "".join(section(i) for i in range(1, 9))
overview += "\n## 验收标记\n\nVEX-A-END：综合样本结束。\n"
save("Vex-01-综合语法与交互.md", overview)

mixed = "# VEX-B 大型混合技术文档\n" + intro + "".join(section(i) for i in range(1, 321))
save("Vex-02-大型混合技术文档.md", mixed + "\n## 验收标记\n\nVEX-B-END：大型混合样本结束。\n")

codes = "# VEX-C 长代码与横向滚动\n" + intro
for i in range(1, 65):
    lines = "\n".join(f'    Process({i}, {j}, "记录-{i:03}-{j:03}"); // 验证高亮与选区，第 {j} 行' for j in range(1, 121))
    codes += f"\n## 代码模块 {i:03}\n\n行内 `module_{i}` 与普通文字。\n\n```csharp\nvoid Module{i}()\n{{\n{lines}\n}}\n```\n"
    if i % 8 == 0:
        codes += '\n```text\n' + ('long_identifier_' * 180) + '\n```\n'
save("Vex-03-长代码与横向滚动.md", codes + "\n## 验收标记\n\nVEX-C-END：长代码样本结束。\n")

math = "# VEX-D 公式与数学符号密集文档\n" + intro
for i in range(1, 161):
    math += f"\n## 公式组 {i:03}\n\n变量 $x_{i}$，希腊字母 $\\alpha, \\beta, \\Gamma$，Unicode 数学符号 ∀ ∃ ∈ ∉ ⊂ ⊆ ∪ ∩ ∅ ℝ ℂ。\n"
    for j in range(4):
        math += "\n$$\n" + formulas[(i + j) % len(formulas)] + "\n$$\n"
    math += f"\n### 推导结论 {i:03}\n\n独立标记 MATH-{i:04}，检查公式尺寸、基线、字体回退与滚动稳定性。\n"
save("Vex-04-公式与数学符号密集.md", math + "\n## 验收标记\n\nVEX-D-END：数学样本结束。\n")

edges = "# VEX-E 边界与容错样本\n" + intro
for i in range(1, 61):
    edges += f"\n## 边界组 {i:03}\n\n### 同名标题\n\n重复正文。\n\n#### 同名标题\n\n重复正文。\n\n##### 五级标题\n\n###### 六级标题\n\n"
    edges += "| A | B | C |\n| --- | --- | --- |\n| 很长的单元格 " + ("内容与 `code`、$x^2$ " * 20) + "| 2 | 3 |\n"
    edges += '\n```unknown-language\n<raw>&value="中文"\n```\n\n---\n'
edges += "\n## 验收标记\n\nVEX-E-END：容错样本正常内容结束。\n\n下方故意包含不完整数学表达式与未闭合围栏，用于检查回退。\n\n$$\n\\frac{1}{\n$$\n\n```text\n# 这不是导图章节\n末尾代码围栏故意不闭合\n"
save("Vex-05-边界与容错.md", edges)

save("Vex-测试资源/通道示意.svg", '''<svg xmlns="http://www.w3.org/2000/svg" width="720" height="160" viewBox="0 0 720 160"><rect width="720" height="160" rx="16" fill="#f4f4ff"/><g fill="#6454dc"><rect x="30" y="50" width="160" height="60" rx="8"/><rect x="280" y="50" width="160" height="60" rx="8"/><rect x="530" y="50" width="160" height="60" rx="8"/></g><path d="M190 80H280M440 80H530" stroke="#6454dc" stroke-width="4"/><g fill="white" font-family="sans-serif" font-size="20" text-anchor="middle"><text x="110" y="87">Input</text><text x="360" y="87">Compute</text><text x="610" y="87">Verify</text></g></svg>''')
save("Vex-测试说明.md", """# Vex 性能与功能测试样本

01 综合语法：文字选择、点击与输入、表格、图片、多语言代码、Mermaid、公式与六级章节。
02 大型混合：320 章，含代码、表格、引用、列表与公式。
03 长代码：64 个长代码块，另含极长行，检查高亮、语言输入与横向滚动。
04 数学密集：640 个块公式和行内公式，检查首次显示、快速滚动与内存。
05 边界容错：重复标题、深层结构、宽表格、未知代码类型、无效公式与未闭合围栏。

建议与 RELAP 技术方案交替打开；循环切换八种布局；连续拖动竖直滚动条往返；在三视图中分别滚动并切换文件。
校验当前文件名、正文、章节树及渲染对应；检查无内容串写、无崩溃、无无意修改。
记录每轮的文档切换、布局切换、滚动耗时和内存；区分离屏测试与实际桌面结果。

样本由 Vex 仓库 scripts/generate_stress_documents.py 生成，UTF-8、LF，仅使用本地图片。
""")
