/*__VEX_RESOURCES__*/
(function () {
  const aliases = {
    '新建文档':'NewDocument','打开文档…':'OpenFile','打开文件夹…':'OpenFolder','最近打开':'OpenRecent','清空最近记录':'ClearRecent',
    '保存全部文档':'SaveAll','关闭文档':'Close','删除文档…':'Delete','文档属性':'Properties','字数统计':'WordCount','MCP 审计':'McpAudit',
    '插入(I)':'WBInsert','工具(T)':'WBTools','编辑视图':'WBEdit','阅读视图':'WBRead','思维导图':'WBMindMap','编辑方式':'WBEdit',
    '即时渲染':'WBInstant','源码':'WBSource','导图显示设置':'WBMapSettings','Markdown 排版':'WBTypographyTitle',
    '文档大纲':'Outline','文档导航':'WBToggleNavigation','刷新内容':'RefreshPreview','界面语言':'MenuLanguage','界面配色':'WBTheme',
    '保持窗口在最前':'AlwaysOnTop','全屏':'ToggleFullScreen','图片':'Image','行内代码':'InlineCode','打印预览':'Print','查看排版样式…':'WBTypographyTitle'
  };
  const extra = {
    '维刻 Vex · 写下想法，看见脉络。':['维刻 Vex · 写下想法，看见脉络。','維刻 Vex · 寫下想法，看見脈絡。','Vex · Write your ideas. See the connections.','Vex · 思いを書き、つながりを見つける。'],
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
    '安装失败，请重试。':['安装失败，请重试。','安裝失敗，請重試。','Installation failed. Please retry.','インストールに失敗しました。再試行してください。']
  };
  const cultures = ['zh-CN','zh-Hant','en-US','ja-JP'];
  let culture = localStorage.getItem('vex-prototype-language') || 'zh-CN';
  const normalize = text => text.replace(/[\s.…（）()·]/g, '');
  const byText = new Map(Object.entries(VEX_DATA.languages['zh-CN']).filter(([,value]) => typeof value === 'string').map(([key,value]) => [normalize(value),key]));
  function get(key) { return VEX_DATA.languages[culture][key] || VEX_DATA.languages['en-US'][key] || key; }
  function label(text) { if (culture === 'zh-CN') return text; const key = aliases[text] || byText.get(normalize(text)); return key ? get(key) : extra[text]?.[cultures.indexOf(culture)] || text; }
  function apply() {
    document.documentElement.lang = culture;
    document.querySelectorAll('[data-l10n]').forEach(el => el.textContent = get(el.dataset.l10n));
    document.querySelectorAll('[data-l10n-title]').forEach(el => { const value = get(el.dataset.l10nTitle); el.title = value; el.setAttribute('aria-label', value); });
    document.querySelectorAll('[data-proto-label]').forEach(el => el.textContent = label(el.dataset.protoLabel));
    document.querySelectorAll('.tb-menuitem').forEach((el,index) => { const menu = window.VexProto?.menuData[index]; if (menu) { el.textContent = label(menu.label); el.dataset.menu = menu.label; } });
    document.querySelectorAll('.menu-item .menu-label').forEach(el => { const original = el.parentElement.dataset.menuLabel; if (original) el.textContent = label(original); });
    document.getElementById('about-version').textContent = VEX_DATA.metadata.version;
    window.dispatchEvent(new CustomEvent('vex-language-changed'));
  }
  window.VexL = { get, label, set(value) { if (!cultures.includes(value)) return; culture = value; localStorage.setItem('vex-prototype-language', value); apply(); }, get culture() { return culture; } };
  document.addEventListener('DOMContentLoaded', () => {
    const roots = document.querySelectorAll('.titlebar,.work-toolbar,.edit-toolbar,.side-tabs,.pane-head,.about-rows,.about-hero,.mini-titlebar');
    roots.forEach(root => root.querySelectorAll('*').forEach(el => {
      if (el.children.length || el.hasAttribute('data-l10n') || el.closest('.about-rows')) return;
      const text = el.textContent.trim(); if (text && (aliases[text] || byText.has(normalize(text)) || extra[text])) el.dataset.protoLabel = text;
    }));
    apply();
  });
})();
