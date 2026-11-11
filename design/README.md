# design 原型目录

桌面与三平台安装原型，是 Vex 界面与交互的唯一样式事实源：原生 Avalonia 实现由这里的令牌、配色、排版和动作表反向同步。

## 打开方式

```powershell
node design/scripts/preview.cjs    # http://127.0.0.1:4317/design/
node design/scripts/verify.cjs     # 静态检查：脚本可解析、引用文件存在、生成物与 docs 一致
```

直接双击 `Vex.html` 也可离线浏览，但需先 `node design/scripts/sync.cjs` 生成 `assets/prototype/project-info.js` 与 `assets/generated/documents-data.js`。

## 维护纪律（改 docs 后必读）

- **改 `docs/更新日志.md`、`docs/鸣谢.md` 或版本号后，必须依次跑**
  `node design/scripts/sync.cjs`（重生成原型副本）→ `node design/scripts/verify.cjs`（门禁）；
  安装器内嵌另跑 `python scripts/installer/embed_changelog.py --regen` + 无参校验。
  三者数据源相同，漏跑任何一步都会在 CI 或安装器里出现"逐字不一致"。
- **条目标签白名单**（verify.cjs 逐行审计）：`✨[新增]`、`⚡[优化]`、`🐛[修复]`、
  `✅[验证]`、`😄[新增]`、`🔨[优化]`、`❌[删除]`。没有 `[功能]` 档——版本递增的
  "功能"分类只属于 `scripts/bump-version.ps1` 的提交标题口径，不进日志条目。
- **行尾陷阱**：本仓 docs 为 CRLF。用脚本往文件里插行时，锚点定位到 `
` 前插入会
  造出 `
` 混行尾（verify 只归一 `
`，孤立 `` 会残留成 diff）。插入后
  整文件归一一次行尾，或插入前后各跑一次 verify。

## 结构

| 位置 | 内容 |
| --- | --- |
| `*.html` | 页面：`Vex.html` 工作台、`Appearance.html` 外观、`Vex-Installer-*.html` 三平台安装、`Third-Party-Licenses.html` 许可 |
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
node design/tests/probe-desktop-review.cjs
node design/tests/probe-map-appearance.cjs
node design/tests/capture-parity.cjs artifacts/prototype-parity
node design/tests/snapshot-styles.cjs artifacts/styles-snapshot.json
```

`snapshot-styles.cjs` 按元素路径与计算样式建多重集，改动 CSS 或 HTML 前后各跑一次即可证明零视觉回归。完整验收口径见 [docs/开发与验证.md](../docs/开发与验证.md)，资源与令牌约定见 [docs/原型与资源.md](../docs/原型与资源.md)。
