# Markdown 行内样式

在普通的 Markdown 之上，维刻 Vex 支持对**选中的文字**直接调整字号、前景色与背景色——以行内 `<span style>` 标签落盘，所见即所得，导出同样保留。

## 怎么用

- **选区浮动工具条**：在即时编辑里拖选文字，弹出快捷条——加粗、斜体、删除线、行内代码、标题、列表，以及字号、文字颜色、背景颜色与清除样式。
- **顶部工具条**：格式条右侧的 `A▾`（字号）、`A▾`（文字颜色）、`A▾`（背景颜色）三键，与浮动条共用同一套动作；颜色面板提供频谱取色、Semi 色板与 Hex 输入。
- 再点一次同款颜色即**解包**；同一处文字可以先设颜色再设字号，属性会合并进同一个标签。

## 写法

```markdown
<span style="color:#D52515">红色文字</span>
<span style="background-color:#FFF3BF">浅黄底色</span>
<span style="font-size:20px">大号文字</span>
<span style="color:#1971C2;font-size:22px">蓝色大字</span>
```

渲染效果：颜色、底色与字号可以自由组合，嵌在句中依旧随文排版。

<span style="color:#D52515">红色文字</span>、<span style="background-color:#FFF3BF">浅黄底色</span>、<span style="color:#1971C2;font-size:22px">蓝色大字</span>。

首字放大也是同一能力：

<span style="font-size:32px">想</span>法可以很大——把首字放大到几十像素，行高自动撑开，其余文字流畅接排。

## 支持的属性

| 属性 | 说明 | 取值 |
| --- | --- | --- |
| `color` | 前景色 | `#RGB`/`#RRGGBB`、`rgb()`、常用 CSS 命名色 |
| `background-color` | 背景色 | 同上 |
| `font-size` | 字号 | `Npx`（自动限制在 8–64px） |

白名单之外的属性会被丢弃，保证文档在其它编辑器里不会带入奇怪的结构。

## 可读性

作者色在渲染与导出时会做**对比度检查**：颜色与所在背景（span 自身底色或主题底色）的对比度不足（如暗色主题下配深色文字）时自动回退主题色，避免文字"变黑洞"。

## 兼容性

- **导出**：HTML、PDF、Word 与公众号复制均保留行内样式（PDF/Word 会映射为对应的文字色、底色与字号）。
- **纯 Markdown 阅读器**（GitHub、VS Code 预览等）会把 `<span>` 显示为原始标签——这是行内 HTML 的固有取舍，与 Typora 一致。
- **清除样式**：选区浮动条与格式条的清除键只剥 `<span>` 标签，不影响加粗等 Markdown 标记。

## 延伸阅读

- [Markdown 功能示例](Markdown功能示例.md)：表格、Callout、脚注等基础语法
- 官网指南：[Markdown 写作指南](https://vex.codewf.com/guide/markdown)
