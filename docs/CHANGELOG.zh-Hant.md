# 更新日誌

## 本地化摘要 - 2026-05-25

此文件提供繁體中文摘要；完整逐項記錄仍以 `CHANGELOG.zh-CN.md` 與 `CHANGELOG.en-US.md` 維護。

### 最近重點

- 直接開啟單一 Markdown/txt 檔案後，左側檔案清單會同步載入同資料夾中的 Markdown 文件。
- 屬性、字數統計與刪除確認改為 UrsaWindow 對話框；長名稱與路徑可選取複製，刪除確認保留警告色按鈕。
- 新增 Word `.docx` 匯出，保留基礎 Markdown 結構與 Word 樣式；PDF/PNG/Word 匯出共用 `CodeWF.Markdown` 圖片載入與柵格化能力，支援相對本地圖、`data:image`、HTTP(S)、SVG、GIF 與 WebP，PDF 和 Word 會嵌入圖片，離線分享後仍可查看。
- PDF/PNG/Word 匯出實作改用 `CodeWF.Markdown` 12.0.3.12 的 `MarkdownDocumentExporter`，Vex 端統一呼叫 `ExportKind` 入口，只負責選擇儲存路徑並傳入目前排版主題，不再維護本地 Word/OpenXML、PDF 切片與 PNG 渲染器。
- 複製到公眾號、知乎與稀土掘金改用 `CodeWF.Markdown` 12.0.3.12 的 `MarkdownHtmlClipboardExtensions.TrySetMarkdownHtmlAsync`，Vex 只傳目前 Markdown、排版主題與發布目標；平台 profile、inline HTML、圖片嵌入、CF_HTML 與尾註文案由公共庫處理。
- 改善 PDF 匯出中文頁眉頁腳字型選擇，減少中文亂碼；HTML、PDF、PNG 與社群複製會更一致地使用目前排版樣式。
- 改善複製到公眾號、知乎與稀土掘金：剪貼簿富 HTML 由 `CodeWF.Markdown` 公共能力產生，Windows `HTML Format` 使用 UTF-8 CF_HTML 位元組資料，並把目前排版主題、緊湊版面與掘金尾註樣式寫入 inline style。
- 說明選單已將「主題色」與「排版」提升為二級選單，並補齊行號、狀態列、視窗置頂等選單勾選與持久化狀態。
- 尋找與取代輸入框限制 200 字元且不換行，避免貼上過長文字導致版面錯亂。
- 改善本地圖片匯出：HTML、列印、複製、PNG、PDF 與 Word 匯出會嘗試 URL 解碼後的圖片路徑，檔名含空格時更不容易缺圖。
- 改善 PNG/PDF 任務清單匯出：`- [ ]` 與 `- [x]` 會保留為 `[ ]`/`[x]` 標記，不再退化成一般項目符號。
- 改善大文件效能：大綱掃描改用 `ReadOnlySpan<char>` 逐行解析，減少每行字串配置；文件列表摘要也加入有界掃描保護。
- 改善暗色模式：一般文字與右鍵選單接入 Vex 動態主題資源，減少暗色主題下的淺色預設回退。
- 改善 MSIX 打包：`PrepareOnly` 準備布局不再被已存在的 `.msix` 產物阻塞。
- 改善 PDF 匯出：加入頁眉、頁腳與更穩定的分頁斷點，並保留圖像型 PDF 的相容性。
- 改善列印預覽：HTML 預覽加入畫面工具列，可手動重新列印或關閉，正式列印時會隱藏工具列。
- 補齊更多 i18n 錯誤詳情：重新命名、說明文件缺失、匯出渲染與文件路徑缺失等邊界提示已具備四套文案。
- 改善暗色模式：關於視窗連結與 Shell 浮層遮罩改用主題資源，暗色主題下對比更穩定。
- 改善大文件體驗：預覽滾動比例直接使用編輯器維護的行數，減少光標移動時的全文掃描。
- 整理發布產物：新增壓縮包打包腳本，可產生 zip、SHA256 與 release manifest。
