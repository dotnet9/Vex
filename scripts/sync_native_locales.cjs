const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm');
/* 支持 --check：不落盘，重新生成并与仓内现有文件比对，漂移即退出码 1（供 CI 门禁）。 */
const checkOnly=process.argv.includes('--check');
const root=path.resolve(__dirname,'..'),context={document:{createElement:()=>({}),head:{append(){}}}};context.window=context;vm.createContext(context);
for(const file of ['prototype/palettes.js','prototype/typography-catalog.js','prototype/desktop-resources.js','prototype/design-system.js'])vm.runInContext(fs.readFileSync(path.join(root,'design/assets',file),'utf8'),context);
const paletteNames=Object.fromEntries(context.VexPalettes.map(p=>[p.key,p.key[0].toUpperCase()+p.key.slice(1)]));
/* 英/日的「用途」行原型里没有对应字段（原型只写中文 note），这里按 key 逐条对照中文补齐。
 * 必须按 key 取：目录顺序会变（新增 SoftSage 就插到了首位），按数组下标对齐会整列错位且看不出来。
 * 缺条目直接抛错——宁可门禁红，也不要静默发出 undefined 标签。 */
const palettePurpose={
 graphite:{en:'Neutral · Value contrast',ja:'無彩色・明度の対比'},
 indigo:{en:'Classic blue · Everyday default',ja:'定番の青・日常の既定'},
 teal:{en:'Warm sage · Gentle writing',ja:'暖かなセージ・なめらかな執筆'},
 pine:{en:'Pine green · Calm focus',ja:'松の緑・落ち着いた集中'},
 sky:{en:'Sky blue · Open and clear',ja:'空の青・清朗で開放的'},
 amber:{en:'Warm amber · Long reading',ja:'暖かな琥珀・長時間の読書'},
 crimson:{en:'Crimson · Vivid energy',ja:'緋色・鮮やかな活力'},
 violet:{en:'Amethyst · Creative work',ja:'紫水晶・創造的な整理'},
 terracotta:{en:'Terracotta · Ideas and notes',ja:'暖かな陶土・ひらめきの手記'}
};
const typographyPurpose={
 SoftSage:{en:'Gentle writing · Sage green',ja:'なめらかな執筆・セージグリーン'},
 Basic:{en:'Work documents · Clear hierarchy',ja:'業務文書・明確な階層'},
 Simple:{en:'Everyday writing · Open spacing',ja:'日常の執筆・ゆとりある余白'},
 InkBlack:{en:'Long reads · Book typography',ja:'長文の読書・明朝体'},
 GeekBlack:{en:'Code handbooks · Monospace headings',ja:'コード解説・等幅見出し'},
 OrangeHeart:{en:'Personal essays · Warm accents',ja:'日常随筆・暖色の罫線'},
 RedScarlet:{en:'Opinion pieces · Red underlines',ja:'論説・朱色の下線'},
 ColorfulPurple:{en:'Creative writing · Violet panels',ja:'創作・紫の背景'},
 Yamabuki:{en:'Lifestyle columns · Warm panels',ja:'生活コラム・暖色の背景'},
 TechnologyBlue:{en:'Technical documents · Structured sections',ja:'技術文書・明確な構造'},
 LanQing:{en:'Cultural essays · Fine teal rules',ja:'文化随筆・青緑の細線'},
 TenderGreen:{en:'Everyday notes · Gentle green',ja:'日々の記録・柔らかな緑'},
 WeChatFormat:{en:'Editorial articles · Centered headings',ja:'雑誌記事・中央見出し'}
};
const purposeOf=(table,kind,key)=>{const hit=table[key];if(!hit)throw new Error(`${kind}「${key}」缺英/日用途说明：在 scripts/sync_native_locales.cjs 的 ${kind}Purpose 表补一条（中文取自原型 note）。`);return hit;};
const extra={
 'zh-CN':{Appearance:'外观',ChoiceRecommended:'推荐',ChoiceCurrent:'当前',WBTypographyTitle:'正文排版'},
 'zh-Hant':{Appearance:'外觀',ChoiceRecommended:'推薦',ChoiceCurrent:'目前',WBTypographyTitle:'正文排版'},
 'en-US':{Appearance:'Appearance',ChoiceRecommended:'Recommended',ChoiceCurrent:'Current',WBTypographyTitle:'Article typography'},
 'ja-JP':{Appearance:'外観',ChoiceRecommended:'おすすめ',ChoiceCurrent:'選択中',WBTypographyTitle:'本文の組版'}
};
const traditional=s=>s.replace(/[观记录写让温图体绿蓝细线书术简专栏报头圆页选开关应]/g,c=>({'观':'觀','记':'記','录':'錄','写':'寫','让':'讓','温':'溫','图':'圖','体':'體','绿':'綠','蓝':'藍','细':'細','线':'線','书':'書','术':'術','简':'簡','专':'專','栏':'欄','报':'報','头':'頭','圆':'圓','页':'頁','选':'選','开':'開','关':'關','应':'應'}[c]));
const generated=[];let zhCnData=null;
for(const culture of Object.keys(extra)){
 const file=path.join(root,'src/Vex/I18n',culture+'.json'),data=JSON.parse(fs.readFileSync(file,'utf8')),strings=data.Vex.VexL;
 Object.assign(strings,extra[culture]);
 Object.assign(strings,{
   SidebarFilesFilterPlaceholder: {'zh-CN':'按文件名查找…','zh-Hant':'依檔名尋找…','en-US':'Find by filename…','ja-JP':'ファイル名で検索…'}[culture],
   ClearSearch: {'zh-CN':'清空搜索','zh-Hant':'清除搜尋','en-US':'Clear search','ja-JP':'検索をクリア'}[culture]
   ,DocumentAuxiliary: {'zh-CN':'文档辅助','zh-Hant':'文件輔助','en-US':'Document tools','ja-JP':'文書ツール'}[culture]
   ,SaveFailureKept: {'zh-CN':'保存失败 · 内容已保留','zh-Hant':'儲存失敗 · 內容已保留','en-US':'Save failed · Content preserved','ja-JP':'保存失敗 · 内容は保持されています'}[culture]
   ,ExportFailureKept: {'zh-CN':'导出失败 · 内容与排版已保留','zh-Hant':'匯出失敗 · 內容與排版已保留','en-US':'Export failed · Content and typography preserved','ja-JP':'エクスポート失敗 · 内容と組版は保持されています'}[culture]
   ,RetrySave: {'zh-CN':'重试保存','zh-Hant':'重試儲存','en-US':'Retry save','ja-JP':'保存を再試行'}[culture]
   ,RetryExport: {'zh-CN':'重试导出','zh-Hant':'重試匯出','en-US':'Retry export','ja-JP':'エクスポートを再試行'}[culture]
   ,ReviewRecoveryTitle: {'zh-CN':'发现恢复草稿 · 请先预览','zh-Hant':'發現復原草稿 · 請先預覽','en-US':'Recovery draft found · Preview first','ja-JP':'復元用の下書きがあります'}[culture]
   ,ReviewExternalTitle: {'zh-CN':'检测到外部版本 · 请先比较','zh-Hant':'偵測到外部版本 · 請先比較','en-US':'External changes detected · Compare first','ja-JP':'外部の変更を検出 · 比較してください'}[culture]
   ,ReviewRecoveryHint: {'zh-CN':'确认后打开为独立的未保存文档。来源文件保持原样，当前文章的修改会先进行保存确认。','zh-Hant':'確認後開啟為獨立的未儲存文件。來源檔案保持原樣，目前文章的修改會先進行儲存確認。','en-US':'Open as a separate unsaved document. The source file stays intact. Unsaved changes in the current article require a choice first.','ja-JP':'独立した未保存文書として開きます。元のファイルは維持され、現在の変更は先に保存確認します。'}[culture]
   ,ReviewExternalHint: {'zh-CN':'比较两个完整版本。保留或另存当前内容，或载入外部版本；载入后仍可撤销。','zh-Hant':'比較兩個完整版本。保留或另存目前內容，或載入外部版本；載入後仍可復原。','en-US':'Compare both complete versions. Keep or save a copy of your content, or load the external version. Loading can be undone.','ja-JP':'両方の全文を比較できます。現在の内容を保持・別名保存するか、外部の版を読み込みます。読み込み後も元に戻せます。'}[culture]
   ,ReviewCurrent: {'zh-CN':'当前编辑内容','zh-Hant':'目前編輯內容','en-US':'Current content','ja-JP':'現在の編集内容'}[culture]
   ,ReviewDraft: {'zh-CN':'查看草稿','zh-Hant':'檢視草稿','en-US':'Preview draft','ja-JP':'下書きを確認'}[culture]
   ,ReviewExternal: {'zh-CN':'外部版本','zh-Hant':'外部版本','en-US':'External version','ja-JP':'外部の版'}[culture]
   ,ReviewCompare: {'zh-CN':'比较版本…','zh-Hant':'比較版本…','en-US':'Compare versions…','ja-JP':'版を比較…'}[culture]
   ,ReviewKeep: {'zh-CN':'保留当前版本','zh-Hant':'保留目前版本','en-US':'Keep current','ja-JP':'現在の版を保持'}[culture]
   ,ReviewCopy: {'zh-CN':'另存当前副本','zh-Hant':'另存目前副本','en-US':'Save current copy','ja-JP':'現在のコピーを保存'}[culture]
   ,ReviewLoad: {'zh-CN':'载入外部版本','zh-Hant':'載入外部版本','en-US':'Load external version','ja-JP':'外部の版を読み込む'}[culture]
   ,ReviewRecover: {'zh-CN':'恢复为独立文档','zh-Hant':'復原為獨立文件','en-US':'Recover as a new document','ja-JP':'新しい文書として復元'}[culture]
   ,KeepDraft: {'zh-CN':'保留草稿并退出','zh-Hant':'保留草稿並退出','en-US':'Keep draft and exit','ja-JP':'下書きを保持して終了'}[culture]
   ,DialogExitHint: {'zh-CN':'是否保存更改？','zh-Hant':'是否儲存變更？','en-US':'Save changes?','ja-JP':'変更を保存しますか？'}[culture]
   ,DialogSwitchHint: {'zh-CN':'是否保存更改？','zh-Hant':'是否儲存變更？','en-US':'Save changes?','ja-JP':'変更を保存しますか？'}[culture]
   ,DialogLoseChangesHint: {'zh-CN':'如果不保存，您的更改将会丢失。','zh-Hant':'如果不儲存，您的變更將會遺失。','en-US':'If you don\u2019t save, your changes will be lost.','ja-JP':'保存しない場合、変更は失われます。'}[culture]
   ,DialogDiscardAndSwitch: {'zh-CN':'放弃更改','zh-Hant':'放棄變更','en-US':'Discard Changes','ja-JP':'変更を破棄'}[culture]
   ,DialogSaveAndExit: {'zh-CN':'保存','zh-Hant':'儲存','en-US':'Save','ja-JP':'保存'}[culture]
   ,DialogDiscardAndExit: {'zh-CN':'丢弃','zh-Hant':'丟棄','en-US':'Discard','ja-JP':'破棄'}[culture]
   ,DialogSaveAndSwitch: {'zh-CN':'保存','zh-Hant':'儲存','en-US':'Save','ja-JP':'保存'}[culture]
   ,DialogContinueEditing: {'zh-CN':'取消','zh-Hant':'取消','en-US':'Cancel','ja-JP':'キャンセル'}[culture]
   ,AuxiliaryInfo: {'zh-CN':'文章信息','zh-Hant':'文章資訊','en-US':'Article info','ja-JP':'記事情報'}[culture]
   ,AuxiliaryChapter: {'zh-CN':'章节详情','zh-Hant':'章節詳情','en-US':'Chapter details','ja-JP':'章の詳細'}[culture]
   ,ChooseChapter: {'zh-CN':'选择一个章节','zh-Hant':'選擇一個章節','en-US':'Select a chapter','ja-JP':'章を選択'}[culture]
   ,ChooseChapterHint: {'zh-CN':'在大纲或导图中选择章节，即可查看和编辑详情。','zh-Hant':'在大綱或導圖中選擇章節，即可查看和編輯詳情。','en-US':'Select a chapter in the outline or mind map to view and edit its details.','ja-JP':'アウトラインまたはマインドマップで章を選択すると、詳細を表示・編集できます。'}[culture]
   ,GroupByFolder: {'zh-CN':'按文件夹分类','zh-Hant':'按資料夾分類','en-US':'Group by folder','ja-JP':'フォルダ別に分類'}[culture]
   ,UntitledDocument: {'zh-CN':'未命名文档','zh-Hant':'未命名文件','en-US':'Untitled document','ja-JP':'無題の文書'}[culture]
   ,WBSaveAndApply: {'zh-CN':'保存并应用','zh-Hant':'儲存並套用','en-US':'Save and apply','ja-JP':'保存して適用'}[culture]
   ,CustomDefaultName: {'zh-CN':'自定义','zh-Hant':'自訂','en-US':'Custom','ja-JP':'カスタム'}[culture]
   ,CustomColorInvalid: {'zh-CN':'存在无效颜色，请使用 #RRGGBB 格式。','zh-Hant':'存在無效顏色，請使用 #RRGGBB 格式。','en-US':'Invalid color. Use the #RRGGBB format.','ja-JP':'無効な色があります。#RRGGBB 形式で入力してください。'}[culture]
   /* 自定义配色浮层（原型 theme.js #palette-dialog）。原型侧此对话框只有中文，四语由应用补齐。 */
   ,WBPaletteAdd: {'zh-CN':'新增配色','zh-Hant':'新增配色','en-US':'New palette','ja-JP':'配色を追加'}[culture]
   ,WBPaletteEdit: {'zh-CN':'编辑配色','zh-Hant':'編輯配色','en-US':'Edit palette','ja-JP':'配色を編集'}[culture]
   ,WBPaletteNameHint: {'zh-CN':'名称','zh-Hant':'名稱','en-US':'Name','ja-JP':'名称'}[culture]
   ,WBPaletteNoteHint: {'zh-CN':'描述','zh-Hant':'描述','en-US':'Description','ja-JP':'説明'}[culture]
   ,WBPaletteGroupLight: {'zh-CN':'浅色态','zh-Hant':'淺色態','en-US':'Light mode','ja-JP':'ライト'}[culture]
   ,WBPaletteGroupDark: {'zh-CN':'深色态','zh-Hant':'深色態','en-US':'Dark mode','ja-JP':'ダーク'}[culture]
   ,WBPaletteColorPaper: {'zh-CN':'纸面','zh-Hant':'紙面','en-US':'Paper','ja-JP':'紙面'}[culture]
   ,WBPaletteColorSurface: {'zh-CN':'面板','zh-Hant':'面板','en-US':'Panel','ja-JP':'パネル'}[culture]
   ,WBPaletteColorInk: {'zh-CN':'墨色','zh-Hant':'墨色','en-US':'Ink','ja-JP':'墨色'}[culture]
   ,WBPaletteColorAccent: {'zh-CN':'强调','zh-Hant':'強調','en-US':'Accent','ja-JP':'アクセント'}[culture]
   ,WBPaletteReport: {'zh-CN':'4 色 × 双明暗编写（规则 §7），其余语义键自动派生。','zh-Hant':'4 色 × 雙明暗編寫（規則 §7），其餘語義鍵自動派生。','en-US':'Edit 4 colors × light/dark (rule §7); the rest derive automatically.','ja-JP':'4 色 × ライト/ダークを入力（規則 §7）。残りのキーは自動導出。'}[culture]
   /* 自定义排版浮层（原型 typography.js #typography-dialog）。 */
   ,WBTypographyAdd: {'zh-CN':'新增排版','zh-Hant':'新增排版','en-US':'New typography','ja-JP':'組版を追加'}[culture]
   ,WBTypographyEdit: {'zh-CN':'编辑排版','zh-Hant':'編輯排版','en-US':'Edit typography','ja-JP':'組版を編集'}[culture]
   ,WBTypographyNameHint: {'zh-CN':'名称','zh-Hant':'名稱','en-US':'Name','ja-JP':'名称'}[culture]
   ,WBTypographyColorAccentLight: {'zh-CN':'强调·浅','zh-Hant':'強調·淺','en-US':'Accent · light','ja-JP':'アクセント（浅）'}[culture]
   ,WBTypographyColorAccentDark: {'zh-CN':'强调·深','zh-Hant':'強調·深','en-US':'Accent · dark','ja-JP':'アクセント（深）'}[culture]
   ,WBTypographyColorDecoLight: {'zh-CN':'装饰·浅','zh-Hant':'裝飾·淺','en-US':'Decor · light','ja-JP':'装飾（浅）'}[culture]
   ,WBTypographyColorDecoDark: {'zh-CN':'装饰·深','zh-Hant':'裝飾·深','en-US':'Decor · dark','ja-JP':'装飾（深）'}[culture]
   ,WBTypographyFontSans: {'zh-CN':'无衬线','zh-Hant':'無襯線','en-US':'Sans-serif','ja-JP':'ゴシック'}[culture]
   ,WBTypographyFontSerif: {'zh-CN':'衬线','zh-Hant':'襯線','en-US':'Serif','ja-JP':'明朝'}[culture]
   ,WBTypographyFontMono: {'zh-CN':'等宽','zh-Hant':'等寬','en-US':'Monospace','ja-JP':'等幅'}[culture]
   ,WBTypographyLeadingCompact: {'zh-CN':'紧凑 1.7','zh-Hant':'緊湊 1.7','en-US':'Compact 1.7','ja-JP':'コンパクト 1.7'}[culture]
   ,WBTypographyLeadingNormal: {'zh-CN':'正常 1.9','zh-Hant':'正常 1.9','en-US':'Normal 1.9','ja-JP':'標準 1.9'}[culture]
   ,WBTypographyLeadingRelaxed: {'zh-CN':'舒展 2.1','zh-Hant':'舒展 2.1','en-US':'Relaxed 2.1','ja-JP':'ゆったり 2.1'}[culture]
   ,WBTypographyFontLabel: {'zh-CN':'字体','zh-Hant':'字體','en-US':'Font','ja-JP':'フォント'}[culture]
   ,WBTypographyLeadingLabel: {'zh-CN':'行距','zh-Hant':'行距','en-US':'Leading','ja-JP':'行送り'}[culture]
   ,WBTypographyH2Label: {'zh-CN':'H2 装饰','zh-Hant':'H2 裝飾','en-US':'H2 decoration','ja-JP':'H2 装飾'}[culture]
   ,WBTypographyH2LeftBar: {'zh-CN':'左竖线','zh-Hant':'左直線','en-US':'Left bar','ja-JP':'左縦線'}[culture]
   ,WBTypographyH2Underline: {'zh-CN':'下划线','zh-Hant':'下劃線','en-US':'Underline','ja-JP':'下線'}[culture]
   ,WBTypographyH2Tint: {'zh-CN':'色块','zh-Hant':'色塊','en-US':'Tint','ja-JP':'配色ブロック'}[culture]
   ,WBTypographyH2None: {'zh-CN':'无','zh-Hant':'無','en-US':'None','ja-JP':'なし'}[culture]
   ,WBTypographyReport: {'zh-CN':'以内置「简约」为模板派生，仅覆盖名称、颜色、字体、行距与 H2 装饰。','zh-Hant':'以內置「簡約」為模板派生，僅覆蓋名稱、顏色、字體、行距與 H2 裝飾。','en-US':'Derived from the built-in Simple style; only name, colors, font, leading and H2 decoration are overridden.','ja-JP':'内蔵の「シンプル」を基に、名称・色・フォント・行送り・H2 装飾だけ上書きします。'}[culture]
   /* 窄窗进三视图自动收起文件导航的提示（原型 workspace.js 的 toastKey('ToastFileNavCollapsed')，四语取自 vex-data.js）。 */
   ,ToastFileNavCollapsed: {'zh-CN':'已收起文件导航，为三视图留出空间。点击底栏“文件”可展开。','zh-Hant':'已收起檔案導覽，為三視圖留出空間。點擊底欄「文件」可展開。','en-US':'File navigation collapsed to make room for the three views — click “Files” on the status bar to expand.','ja-JP':'ファイルナビゲーションを折りたたみました。ステータスバーの「ファイル」で展開できます。'}[culture]
   /* 查找条动作钮（原型 Vex.html .find-strip 的「上一个」，实现侧此前只有「下一个」）。 */
   ,FindBarPrev: {'zh-CN':'上一个','zh-Hant':'上一個','en-US':'Previous','ja-JP':'前へ'}[culture]
   /* 查找条命中徽标（原型 workbench.js updateFind 写进 #find-count 的两条文案）。 */
   ,FindCountMatches: {'zh-CN':'{0} 处匹配','zh-Hant':'{0} 處匹配','en-US':'{0} matches','ja-JP':'{0} 件一致'}[culture]
   ,FindCountNone: {'zh-CN':'当前正文无匹配','zh-Hant':'目前正文無匹配','en-US':'No matches in this text','ja-JP':'この本文に一致なし'}[culture]
   /* 图表块就地编辑（原型 diagram-editor.js 无这三个提示，属实现侧增强，仍需四语）。 */
   ,DiagramEditButton: {'zh-CN':'编辑','zh-Hant':'編輯','en-US':'Edit','ja-JP':'編集'}[culture]
   ,DiagramEditHint: {'zh-CN':'Esc 或点击块外完成编辑','zh-Hant':'Esc 或點擊塊外完成編輯','en-US':'Press Esc or click outside the block to finish editing','ja-JP':'Esc またはブロック外クリックで編集を終了'}[culture]
   ,DiagramPreviewNote: {'zh-CN':'预览由渲染器出图；语法有误时保留源码显示。','zh-Hant':'預覽由渲染器出圖；語法有誤時保留原始碼顯示。','en-US':'The preview is drawn by the renderer; invalid syntax keeps the source visible.','ja-JP':'プレビューはレンダラーが描画します。構文エラーではソース表示を保持します。'}[culture]
 });
 context.VexPalettes.forEach(p=>{const name=paletteNames[p.key],purpose=purposeOf(palettePurpose,'配色',p.key);strings['Palette'+name]=culture==='zh-CN'?p.label:culture==='zh-Hant'?traditional(p.label):p.en;strings['PalettePurpose'+name]=culture==='zh-CN'?p.note:culture==='zh-Hant'?traditional(p.note):culture==='ja-JP'?purpose.ja:purpose.en;});
 context.VexTypes.forEach(t=>{const purpose=purposeOf(typographyPurpose,'排版',t.key);strings['Typography'+t.key]=culture==='zh-CN'?t.label:culture==='zh-Hant'?traditional(t.label):t.en;strings['TypographyPurpose'+t.key]=culture==='zh-CN'?t.note:culture==='zh-Hant'?traditional(t.note):culture==='ja-JP'?purpose.ja:purpose.en;});
 /* 目录里已删的旧主题键随快照一起清除，只保留生成器与原型认得的配色与排版标签。 */
 const allowed=new Set(['Typography']);
 context.VexPalettes.forEach(p=>{allowed.add('Palette'+paletteNames[p.key]);allowed.add('PalettePurpose'+paletteNames[p.key]);});
 context.VexTypes.forEach(t=>{allowed.add('Typography'+t.key);allowed.add('TypographyPurpose'+t.key);});
 for(const key of Object.keys(strings)) if(/^(Palette|Typography)/.test(key)&&!allowed.has(key)) delete strings[key];
 generated.push({file, text: JSON.stringify(data,null,2)+'\n'});
 if(culture==='zh-CN') zhCnData=data;
}
/* Language.cs 由计算后的 zh-CN 数据生成（--check 模式不落盘，不能重读磁盘旧值）。 */
generated.push({file: path.join(root,'src/Vex/I18n/Language.cs'), text: '// <auto-generated> Generated from I18n/zh-CN.json. </auto-generated>\nnamespace Vex;\npublic static class VexL\n{\n'+Object.keys((zhCnData??data).Vex.VexL).map(key=>'    public static readonly string '+key+' = "Vex.VexL.'+key+'";').join('\n')+'\n}\n'});
/* ReleaseTags.cs：《更新日志》正文的标签词表。词形（UpdateTagSource*）与胶囊文案（UpdateTag*）都是 JSON 资源键，
   产品代码里不再出现任何语言的字面量——加一类标签只改四份 JSON 的一对键，生成器跟着多一行。与 I18n/ReleaseTags.tt 逐字同式。 */
const zhCnStrings=(zhCnData??data).Vex.VexL;
const tagSuffixes=Object.keys(zhCnStrings).map(key=>/^UpdateTag(?!Source)([A-Z]\w*)$/.exec(key)?.[1]).filter(Boolean);
generated.push({file: path.join(root,'src/Vex/I18n/ReleaseTags.cs'), text: [
 '// <auto-generated> Generated from I18n/zh-CN.json. </auto-generated>',
 '#nullable enable',
 'using System;',
 'using System.Collections.Generic;',
 '',
 'namespace Vex;',
 '',
 '/// <summary>《更新日志》正文标签词的识别表：词形与界面文案都是 I18n 资源键，这里不出现任何语言的字面量。',
 '/// 一组键 = UpdateTagSource 前缀加后缀（正文方括号里的词形）与 UpdateTag 前缀加同一后缀（胶囊显示的文案）。</summary>',
 'public static class VexReleaseTag',
 '{',
 '    /// <summary>正文标签词不在词表里（自定义发布说明）时返回 null，该行按无标签条目渲染。</summary>',
 '    /// <param name="resolve">资源键 → 当前界面语言的文案；词形比较用它现取，切语言不必重新生成。</param>',
 '    public static string? KeyOf(string source, Func<string, string> resolve)',
 '    {',
 '        foreach (var pair in Pairs)',
 '        {',
 '            if (string.Equals(resolve(pair.SourceKey), source, StringComparison.Ordinal)) return pair.LabelKey;',
 '        }',
 '',
 '        return null;',
 '    }',
 '',
 '    private static readonly IReadOnlyList<(string SourceKey, string LabelKey)> Pairs =',
 '    [',
 ...tagSuffixes.map(suffix=>'        (VexL.UpdateTagSource'+suffix+', VexL.UpdateTag'+suffix+'),'),
 '    ];',
 '}',
 '',
].join('\n')});
if(checkOnly){
 const drifted=[];
 for(const {file,text} of generated){
  const existing=fs.existsSync(file)?fs.readFileSync(file,'utf8').replace(/\r\n/g,'\n'):null;
  if(existing!==text.replace(/\r\n/g,'\n')) drifted.push(path.relative(root,file));
 }
 if(drifted.length){
  console.error(`I18n 资源与原型契约不同步（${drifted.length} 个文件漂移）：`);
  for(const file of drifted) console.error('  '+file);
  console.error('请运行: node scripts/sync_native_locales.cjs');
  process.exit(1);
 }
 console.log(`Check passed: ${generated.length} i18n files match the prototype contract.`);
} else {
 for(const {file,text} of generated) fs.writeFileSync(file,text);
 console.log('Synchronized '+context.VexPalettes.length+' palette and '+context.VexTypes.length+' typography labels in four languages.');
}
