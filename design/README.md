# design 原型目录

桌面与三平台安装原型，是 Vex 界面与交互的唯一样式事实源：原生 Avalonia 实现由这里的令牌、配色、排版和动作表反向同步。

## 打开方式

```powershell
node design/scripts/preview.cjs    # http://127.0.0.1:4317/design/
node design/scripts/verify.cjs     # 静态检查：脚本可解析、引用文件存在、生成物与 docs 一致
```

直接双击 `index.html` 也可离线浏览，但需先 `node design/scripts/sync.cjs` 生成 `assets/prototype/project-info.js` 与 `assets/generated/documents-data.js`。

## 结构

| 位置 | 内容 |
| --- | --- |
| `*.html` | 页面：`Vex.html` 工作台、`index.html` 目录、`Appearance.html` 外观、`Usability-Review.html` 走查、`Vex-Installer-*.html` 三平台安装、`Third-Party-Licenses.html` 许可 |
| `assets/prototype/` | 跨页共享：令牌、字体、密度、配色与排版目录、桌面资源清单、生成器消费的 `palette-rules.js` |
| `assets/workbench/` | 仅 `Vex.html` 使用：编辑器、图表、导图、引导、对话框、工作区外壳等 |
| `assets/installer/` | 仅安装页使用：`installer.css` / `installer-windows.css` / `installer.js` |
| `assets/generated/` | 由脚本产出，禁止手写：`documents-data.js`、`palette-rules-data.js` |
| `assets/vendor/` `assets/fonts/` | 第三方字节原样保留，不参与批量格式化 |
| `scripts/` | 生成与服务：`sync.cjs`、`palette-rules.cjs`、`preview.cjs`、`verify.cjs` |
| `tests/` | Playwright 检查与截图：`browser-*.cjs`、`capture-parity.cjs`、`snapshot-styles.cjs` |

`assets/vendor/**` 与 `assets/fonts/**` 在 `design/.gitattributes` 中关闭了换行与空白处理；其余文本按仓库 `core.autocrlf` 走 CRLF，生成器读取源文档时会统一把 `\r\n`/`\r` 归一为 `\n`，避免提交内容随 checkout 抖动。

## 测试脚本

浏览器脚本需要 Playwright。本机没有全局安装时，借用依赖库里的副本：

```powershell
npm ci --prefix design/tests   # 仓内锁定 playwright 1.62.1，不借用其他仓库
$env:VEX_PREVIEW_URL = 'http://127.0.0.1:4317/design/'   # 可省，默认即此
$env:VEX_CAPTURE_DIR = 'E:/github/temp_files/vex-learning/prototype'  # 显式指定截图目录
node design/tests/browser-resource-review.cjs
node design/tests/browser-workspace-chrome.cjs
node design/tests/browser-design-principles.cjs
node design/tests/browser-mermaid.cjs
node design/tests/capture-parity.cjs artifacts/prototype-parity
node design/tests/snapshot-styles.cjs artifacts/styles-snapshot.json
```

`snapshot-styles.cjs` 按元素路径与计算样式建多重集，改动 CSS 或 HTML 前后各跑一次即可证明零视觉回归。完整验收口径见 [docs/开发与验证.md](../docs/开发与验证.md)，资源与令牌约定见 [docs/原型与资源.md](../docs/原型与资源.md)。
