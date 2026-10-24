const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm');
const root=path.resolve(__dirname,'..'),context={document:{createElement:()=>({}),head:{append(){}}}};context.window=context;vm.createContext(context);
for(const file of ['palettes.js','typography-catalog.js','desktop-resources.js','design-system.js'])vm.runInContext(fs.readFileSync(path.join(root,'design/assets',file),'utf8'),context);
const paletteNames=Object.fromEntries(context.VexPalettes.map(p=>[p.key,p.key[0].toUpperCase()+p.key.slice(1)]));
const palettePurposeEn=['Neutral · Value contrast','Classic blue · Everyday default','Cool teal · Fresh notes','Pine green · Calm focus','Sky blue · Open and clear','Warm amber · Long reading','Crimson · Vivid energy','Amethyst · Creative work','Terracotta · Ideas and notes'];
const palettePurposeJa=['無彩色・明度の対比','定番の青・日常の既定','冷たい青緑・爽やかな記録','松の緑・落ち着いた集中','空の青・清朗で開放的','暖かな琥珀・長時間の読書','緋色・鮮やかな活力','紫水晶・創造的な整理','暖かな陶土・ひらめきの手記'];
const purposeEn=['Work documents · Clear hierarchy','Everyday writing · Open spacing','Long reads · Book typography','Code handbooks · Monospace headings','Personal essays · Warm accents','Opinion pieces · Red underlines','Creative writing · Violet panels','Lifestyle columns · Warm panels','Technical documents · Structured sections','Cultural essays · Fine teal rules','Everyday notes · Gentle green','Editorial articles · Centered headings'];
const purposeJa=['業務文書・明確な階層','日常の執筆・ゆとりある余白','長文の読書・明朝体','コード解説・等幅見出し','日常随筆・暖色の罫線','論説・朱色の下線','創作・紫の背景','生活コラム・暖色の背景','技術文書・明確な構造','文化随筆・青緑の細線','日々の記録・柔らかな緑','雑誌記事・中央見出し'];
const extra={
 'zh-CN':{Appearance:'外观',ChoiceRecommended:'推荐',ChoiceCurrent:'当前',AllPalettes:'全部 9 套',AllTypographies:'全部 12 套',WBTypographyTitle:'正文排版',WBTypographyHint:'每张卡片展示独立效果。方向键浏览，点击或回车应用。',TypographyPreviewMarkdown:'# 文字的温度\n\n记录观察，整理思绪。\n\n## 让想法成形\n\n> 先写下来，再慢慢整理。'},
 'zh-Hant':{Appearance:'外觀',ChoiceRecommended:'推薦',ChoiceCurrent:'目前',AllPalettes:'全部 9 套',AllTypographies:'全部 12 套',WBTypographyTitle:'正文排版',WBTypographyHint:'每張卡片展示獨立效果。方向鍵瀏覽，點擊或 Enter 套用。',TypographyPreviewMarkdown:'# 文字的溫度\n\n記錄觀察，整理思緒。\n\n## 讓想法成形\n\n> 先寫下來，再慢慢整理。'},
 'en-US':{Appearance:'Appearance',ChoiceRecommended:'Recommended',ChoiceCurrent:'Current',AllPalettes:'All 9',AllTypographies:'All 12',WBTypographyTitle:'Article typography',WBTypographyHint:'Compare each style. Use arrow keys to browse; click or press Enter to apply.',TypographyPreviewMarkdown:'# The warmth of words\n\nObserve, record, and reflect.\n\n## Give ideas a shape\n\n> Write it down. Refine it later.'},
 'ja-JP':{Appearance:'外観',ChoiceRecommended:'おすすめ',ChoiceCurrent:'選択中',AllPalettes:'全 9 種',AllTypographies:'全 12 種',WBTypographyTitle:'本文の組版',WBTypographyHint:'各スタイルを比較できます。矢印キーで移動し、クリックか Enter で適用。',TypographyPreviewMarkdown:'# ことばの温度\n\n観察を記録し、考えを整理。\n\n## 想いをかたちに\n\n> まず書き留め、少しずつ整える。'}
};
const traditional=s=>s.replace(/[观记录写让温图体绿蓝细线书术简专栏报头圆页选开关应]/g,c=>({'观':'觀','记':'記','录':'錄','写':'寫','让':'讓','温':'溫','图':'圖','体':'體','绿':'綠','蓝':'藍','细':'細','线':'線','书':'書','术':'術','简':'簡','专':'專','栏':'欄','报':'報','头':'頭','圆':'圓','页':'頁','选':'選','开':'開','关':'關','应':'應'}[c]));
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
 });
 context.VexPalettes.forEach((p,i)=>{const name=paletteNames[p.key];strings['Palette'+name]=culture==='zh-CN'?p.label:culture==='zh-Hant'?traditional(p.label):p.en;strings['PalettePurpose'+name]=culture.startsWith('zh')? (culture==='zh-Hant'?traditional(p.note):p.note):culture==='ja-JP'?palettePurposeJa[i]:palettePurposeEn[i];});
 context.VexTypes.forEach((t,i)=>{strings['Typography'+t.key]=culture==='zh-CN'?t.label:culture==='zh-Hant'?traditional(t.label):t.en;strings['TypographyPurpose'+t.key]=culture==='zh-CN'?t.note:culture==='zh-Hant'?traditional(t.note):culture==='ja-JP'?purposeJa[i]:purposeEn[i];});
 /* 规则 §9 定稿后只保留 12 套排版、9 套配色的标签；旧主题键随快照一起清除。 */
 const allowed=new Set(['PaletteSelectionSummary','Typography','TypographyPreviewMarkdown']);
 context.VexPalettes.forEach(p=>{allowed.add('Palette'+paletteNames[p.key]);allowed.add('PalettePurpose'+paletteNames[p.key]);});
 context.VexTypes.forEach(t=>{allowed.add('Typography'+t.key);allowed.add('TypographyPurpose'+t.key);});
 for(const key of Object.keys(strings)) if(/^(Palette|Typography)/.test(key)&&!allowed.has(key)) delete strings[key];
 fs.writeFileSync(file,JSON.stringify(data,null,2)+'\n');
}
const data=JSON.parse(fs.readFileSync(path.join(root,'src/Vex/I18n/zh-CN.json'),'utf8'));
fs.writeFileSync(path.join(root,'src/Vex/I18n/Language.cs'),'// <auto-generated> Generated from I18n/zh-CN.json. </auto-generated>\nnamespace Vex;\npublic static class VexL\n{\n'+Object.keys(data.Vex.VexL).map(key=>'    public static readonly string '+key+' = "Vex.VexL.'+key+'";').join('\n')+'\n}\n');
console.log('Synchronized '+context.VexPalettes.length+' palette and '+context.VexTypes.length+' typography labels in four languages.');
