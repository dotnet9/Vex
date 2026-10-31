(function () {
  const aliases = {
    '新建文档':'NewDocument','打开文档…':'OpenFile','打开文件夹…':'OpenFolder','最近打开':'OpenRecent','清空最近记录':'ClearRecent',
    '保存全部文档':'SaveAll','关闭文档':'Close','删除文档…':'Delete','MCP 审计':'McpAudit',
    '编辑视图':'WBEdit','阅读视图':'WBRead','思维导图':'WBMindMap',
    '即时编辑':'WBInstant','源码编辑':'WBSource','导图显示设置':'WBMapSettings','Markdown 排版':'WBTypographyTitle',
    '文档导航':'WBToggleNavigation','刷新内容':'RefreshPreview','界面语言':'MenuLanguage','界面配色':'WBTheme',
    '保持窗口在最前':'AlwaysOnTop','全屏':'ToggleFullScreen','图片':'Image','行内代码':'InlineCode','打印预览':'Print','查看排版样式…':'WBTypographyTitle'
  };
  const extra = {
    '文档大纲':['文档大纲','文件大綱','Document outline','文書アウトライン'],
    '演示文档':['演示文档','示範文件','Sample documents','サンプル文書'],
    '导出':['导出','匯出','Export','書き出し'],
    '文档操作':['文档操作','文件操作','Document actions','文書の操作'],
    '编辑方式':['编辑方式','編輯方式','Editing mode','編集モード'],
    '侧栏与大纲':['侧栏与大纲','側欄與大綱','Sidebars and outline','サイドバーとアウトライン'],
    '行操作':['行操作','列操作','Rows','行の操作'],
    '列操作':['列操作','欄操作','Columns','列の操作'],
    '列对齐':['列对齐','欄對齊','Column alignment','列の配置'],
    '过程与行为':['过程与行为','流程與行為','Process and behavior','プロセスと振る舞い'],
    '计划与演进':['计划与演进','計畫與演進','Planning and evolution','計画と変遷'],
    '结构与关系':['结构与关系','結構與關係','Structure and relationships','構造と関係'],
    '数据与比较':['数据与比较','資料與比較','Data and comparison','データと比較'],
    '导出 HTML':['导出 HTML','匯出 HTML','Export HTML','HTML に書き出す'],
    '导出 PDF':['导出 PDF','匯出 PDF','Export PDF','PDF に書き出す'],
    '导出 PNG 长图':['导出 PNG 长图','匯出 PNG 長圖','Export PNG image','PNG 画像に書き出す'],
    '导出 Word（.docx）':['导出 Word（.docx）','匯出 Word（.docx）','Export Word (.docx)','Word (.docx) に書き出す'],
    '复制到微信公众号':['复制到微信公众号','複製到微信公眾號','Copy to WeChat','WeChat 向けにコピー'],
    '关于':['关于','關於','About','このアプリについて'],
    '维刻 Vex · 写下想法，看见脉络。':['维刻 Vex · 写下想法，看见脉络。','維刻 Vex · 寫下想法，看見脈絡。','Vex · Write your thoughts. See the connections.','Vex · 思いを書き、つながりを見つける。'],
    '编辑显示':['编辑显示','編輯顯示','Editor display','エディター表示'],
    '写作辅助':['写作辅助','寫作輔助','Writing tools','執筆補助'],
    '窗口':['窗口','視窗','Window','ウィンドウ'],
    '最大化 / 还原':['最大化 / 还原','最大化 / 還原','Maximize / restore','最大化 / 元に戻す'],
    '列表':['列表','清單','Lists','リスト'], '标题':['标题','標題','Headings','見出し'],
    'PNG 长图':['PNG 长图','PNG 長圖','PNG image','PNG 画像'],
    '链接':['链接','連結','Links','リンク'],
    '官网':['官网','官網','Website','公式サイト'],
    '开源仓库':['开源仓库','開源倉庫','Source code','ソースコード'],
    '重试':['重试','重試','Retry','再試行'],
    '更新已安装。':['更新已安装。','更新已安裝。','Update installed.','更新をインストールしました。'],
    '请前往发布页下载安装包。':['请前往发布页下载安装包。','請前往發佈頁下載安裝包。','Download the installer from the release page.','リリースページからインストーラーをダウンロードしてください。'],
    '检查失败，请重试。':['检查失败，请重试。','檢查失敗，請重試。','Check failed. Please retry.','確認に失敗しました。再試行してください。'],
    '下载失败，请重试。':['下载失败，请重试。','下載失敗，請重試。','Download failed. Please retry.','ダウンロードに失敗しました。再試行してください。'],
    '校验失败，请重新下载。':['校验失败，请重新下载。','驗證失敗，請重新下載。','Verification failed. Please download again.','検証に失敗しました。もう一度ダウンロードしてください。'],
    '安装失败，请重试。':['安装失败，请重试。','安裝失敗，請重試。','Installation failed. Please retry.','インストールに失敗しました。再試行してください。'],
    '插入(I)':['插入(I)','插入(I)','Insert(I)','挿入(I)'],
    '工具(T)':['工具(T)','工具(T)','Tools(T)','ツール(T)'],
    'Markdown 内容':['Markdown 内容','Markdown 內容','Markdown content','Markdown 本文'],
    'Markdown 思维导图':['Markdown 思维导图','Markdown 思維導圖','Markdown mind map','Markdown マインドマップ'],
    'Markdown 源码':['Markdown 源码','Markdown 原始碼','Markdown source','Markdown ソース'],
    '全部折叠':['全部折叠','全部摺疊','Collapse all','すべて折りたたむ'],
    '全部展开':['全部展开','全部展開','Expand all','すべて展開'],
    '关闭查找':['关闭查找','關閉尋找','Close find','検索を閉じる'],
    '关闭窗口':['关闭窗口','關閉視窗','Close window','ウィンドウを閉じる'],
    '关闭节点详情':['关闭节点详情','關閉節點詳細資訊','Close node details','ノード詳細を閉じる'],
    '关闭设置':['关闭设置','關閉設定','Close settings','設定を閉じる'],
    '删除确认':['删除确认','刪除確認','Delete confirmation','削除の確認'],
    '即时编辑区':['即时编辑区','即時編輯區','Live editing area','ライブ編集エリア'],
    '只匹配文件名，不搜索正文 · Ctrl+P':['只匹配文件名，不搜索正文 · Ctrl+P','只比對檔案名稱，不搜尋正文 · Ctrl+P','Matches file names only, not document text · Ctrl+P','ファイル名のみを対象、本文は検索しません · Ctrl+P'],
    '只筛选当前文档的大纲标题':['只筛选当前文档的大纲标题','只篩選目前文件的目錄標題','Filters only the outline headings of this document','この記事のアウトライン見出しのみ絞り込み'],
    '导图布局':['导图布局','導圖佈局','Mind map layout','マインドマップのレイアウト'],
    '导图画布，拖动平移，Ctrl 加滚轮缩放':['导图画布，拖动平移，Ctrl 加滚轮缩放','導圖畫布，拖曳平移，Ctrl + 滾輪縮放','Mind map canvas - drag to pan, Ctrl+scroll to zoom','マインドマップキャンバス - ドラッグで移動、Ctrl+スクロールで拡大縮小'],
    '展开文档大纲':['展开文档大纲','展開文件大綱','Expand the document outline','文書アウトラインを展開'],
    '收起文档大纲':['收起文档大纲','收起文件大綱','Collapse the document outline','文書アウトラインをたたむ'],
    '应用菜单':['应用菜单','應用程式功能表','Application menu','アプリケーションメニュー'],
    '按名称排序：默认':['按名称排序：默认','按名稱排序：預設','Sort by name: default','名前順：既定'],
    '按名称排序：升序':['按名称排序：升序','按名稱排序：昇序','Sort by name: ascending','名前順：昇順'],
    '按名称排序：降序':['按名称排序：降序','按名稱排序：降序','Sort by name: descending','名前順：降順'],
    '按文件名查找':['按文件名查找','按檔案名稱搜尋','Find by file name','ファイル名で検索'],
    '按文件夹分类':['按文件夹分类','按資料夾分類','Group by folder','フォルダ別に分類'],
    '收起文件导航':['收起文件导航','收起文件導覽','Collapse file navigation','ファイル一覧をたたむ'],
    '展开文件导航':['展开文件导航','展開文件導覽','Expand file navigation','ファイル一覧を展開'],
    '文件列表':['文件列表','文件清單','File list','ファイル一覧'],
    '文件列表工具':['文件列表工具','文件清單工具','File list toolbar','ファイル一覧ツール'],
    '替换为':['替换为','取代為','Replace with','置換後'],
    '替换内容':['替换内容','取代內容','Replacement text','置換内容'],
    '最小化':['最小化','最小化','Minimize','最小化'],
    '查找当前正文':['查找当前正文','尋找目前正文','Find in this document','この記事内を検索'],
    '正文预览':['正文预览','正文預覽','Rendered preview','本文のプレビュー'],
    '清除文件名筛选':['清除文件名筛选','清除檔案名稱篩選','Clear file name filter','ファイル名フィルタをクリア'],
    '清除标题筛选':['清除标题筛选','清除標題篩選','Clear title filter','標題フィルタをクリア'],
    '筛选当前文档标题':['筛选当前文档标题','篩選目前文件標題','Filter headings in this document','この記事の見出しを絞り込み'],
    '筛选当前标题':['筛选当前标题','篩選目前標題','Filter headings','見出しを絞り込み'],
    '节点全文':['节点全文','節點全文','Full node text','ノードの全文'],
    '调整文档大纲宽度':['调整文档大纲宽度','調整文件大綱寬度','Resize the document outline','アウトラインの幅を調整'],
    '调整文档导航宽度':['调整文档导航宽度','調整文件導覽寬度','Resize the file navigation','ファイル一覧の幅を調整'],
    '辅助面板':['辅助面板','輔助面板','Side panels','サイドパネル'],
    '章节内容':['章节内容','章節內容','Section content','セクションの内容'],
    '正文排版':['正文排版','正文排版','Text typography','本文の組版'],
    '退出演示':['退出演示','退出示範','Exit presentation','プレゼンを終了'],
    '查看此版本的发布与下载':['查看此版本的发布与下载','檢視此版本的發佈與下載','View this release and its downloads','このバージョンの公開とダウンロードを表示'],
    '自定义目录路径，例如 D:\\Notes':['自定义目录路径，例如 D:\\Notes','自訂目錄路徑，例如 D:\\Notes','Custom folder path, e.g. D:\\Notes','カスタムフォルダパス（例: D:\\Notes）']
  };
  const cultures = ['zh-CN','zh-Hant','en-US','ja-JP'];
  let culture = VexStorage.getItem('vex-prototype-language') || 'zh-CN';
  const normalize = text => text.replace(/[\s.…（）()·]/g, '');
  const byText = new Map(Object.entries(VEX_DATA.languages['zh-CN']).filter(([,value]) => typeof value === 'string').map(([key,value]) => [normalize(value),key]));
  function get(key) { return VEX_DATA.languages[culture][key] || VEX_DATA.languages['en-US'][key] || key; }
  function label(text) { if (culture === 'zh-CN') return text; const key = aliases[text] || byText.get(normalize(text)); if (key) return get(key);
    const bare = text.replace(/…$/, ""), hit = extra[text] || extra[bare];
    if (!hit) return text;
    const value = hit[cultures.indexOf(culture)];
    return value !== undefined && text.endsWith("…") && !/[….]$/.test(value) ? value + "…" : value || text; }
  function apply() {
    document.documentElement.lang = culture;
    document.querySelectorAll('[data-l10n]').forEach(el => el.textContent = get(el.dataset.l10n));
    document.querySelectorAll('[data-l10n-title]').forEach(el => { const value = get(el.dataset.l10nTitle); el.title = value; el.setAttribute('aria-label', value); });
    document.querySelectorAll('[data-l10n-name]').forEach(el => { const value = label(el.dataset.l10nName); el.title = value; el.setAttribute('aria-label', value); });
    document.querySelectorAll('[data-l10n-tip]').forEach(el => { el.title = label(el.dataset.l10nTip); });
    document.querySelectorAll('[data-l10n-label]').forEach(el => { el.setAttribute('aria-label', label(el.dataset.l10nLabel)); });
    document.querySelectorAll('[data-l10n-placeholder]').forEach(el => { el.placeholder = label(el.dataset.l10nPlaceholder); });
    document.querySelectorAll('[data-proto-label]').forEach(el => el.textContent = label(el.dataset.protoLabel));
    document.querySelectorAll('.tb-menuitem').forEach((el,index) => { const menu = window.VexProto?.menuData[index]; if (menu) { el.textContent = label(menu.label); el.dataset.menu = menu.label; } });
    document.querySelectorAll('.menu-item .menu-label').forEach(el => { const original = el.parentElement.dataset.menuLabel; if (original) el.textContent = label(original); });
    document.getElementById('about-version').textContent = VEX_DATA.metadata.version;
    window.dispatchEvent(new CustomEvent('vex-language-changed'));
  }
  window.VexL = { get, label, t: label, set(value) { if (!cultures.includes(value)) return; culture = value; VexStorage.setItem('vex-prototype-language', value); apply(); }, get culture() { return culture; } };
  document.addEventListener('DOMContentLoaded', () => {
    const roots = document.querySelectorAll('.titlebar,.work-toolbar,.edit-toolbar,.side-tabs,.pane-head,.about-rows,.about-hero,.mini-titlebar');
    roots.forEach(root => root.querySelectorAll('*').forEach(el => {
      if (el.children.length || el.hasAttribute('data-l10n') || el.closest('.about-rows')) return;
      const text = el.textContent.trim(); if (text && (aliases[text] || byText.has(normalize(text)) || extra[text])) el.dataset.protoLabel = text;
    }));
    apply();
  });
})();
