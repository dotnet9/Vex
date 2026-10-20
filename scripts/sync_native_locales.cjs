const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm');
const root=path.resolve(__dirname,'..'),context={document:{createElement:()=>({}),head:{append(){}}}};context.window=context;vm.createContext(context);
for(const file of ['palettes.js','typography-catalog.js','desktop-resources.js','design-system.js'])vm.runInContext(fs.readFileSync(path.join(root,'design/assets',file),'utf8'),context);
const paletteNames={weft:'Weft',graphite:'Graphite',ocean:'Ocean',sand:'Sand',rose:'Rose',iris:'Iris',olive:'Olive',ember:'Terracotta',dotnet:'DotNet',aurora:'Aurora','studio-ember':'Ember',default:'Default',aquatic:'Aquatic',desert:'Desert',dusk:'Dusk','night-sky':'NightSky'};
const purposeEn=['Everyday writing · Open spacing','Work documents · Clear hierarchy','Long reads · Book typography','Technical documents · Structured sections','Opinion columns · Strong hierarchy','Editorial articles · Centered headings','Research papers · Serif typography','Personal journal · Unhurried reading','User guides · Easy navigation','General documents · Fine dividers','Quick notes · Plain headings','Personal essays · Warm accents','Long reads · Double rules','Creative writing · Violet panels','Everyday notes · Gentle green','Knowledge notes · Green side rules','Opinion pieces · Red underlines','Project notes · Cool blue panels','Technical reports · Blue side rules','Cultural essays · Fine teal rules','Lifestyle columns · Warm panels','Developer tutorials · Solid headings','Code handbooks · Monospace headings','Creative notes · Purple double rules','Popular science · Rounded green panels','Engineering documents · Blue sections','Illustrated columns · Symmetric headings'];
const purposeJa=['日常の執筆・ゆとりある余白','業務文書・明確な階層','長文の読書・明朝体','技術文書・明確な構造','論説・力強い見出し','雑誌記事・中央見出し','研究論文・落ち着いた明朝体','随筆・ゆったりしたリズム','操作手順・探しやすい章立て','一般文書・細い区切り線','メモ・装飾のない見出し','日常随筆・暖色の罫線','長文・墨色の二重線','創作・紫の背景','日々の記録・柔らかな緑','知識整理・緑の縦線','論説・朱色の下線','プロジェクト記録・青の背景','技術報告・青の縦線','文化随筆・青緑の細線','生活コラム・暖色の背景','開発解説・塗り見出し','コード解説・等幅見出し','創作記録・紫の二重線','科学解説・緑の角丸背景','設計文書・青の区切り','図文コラム・対称見出し'];
const extra={
 'zh-CN':{Appearance:'外观',ChoiceRecommended:'推荐',ChoiceCurrent:'当前',AllPalettes:'全部 16 套',AllTypographies:'全部 27 套',WBTypographyTitle:'正文排版',WBTypographyHint:'每张卡片展示独立效果。方向键浏览，点击或回车应用。',TypographyPreviewMarkdown:'# 文字的温度\n\n记录观察，整理思绪。\n\n## 让想法成形\n\n> 先写下来，再慢慢整理。'},
 'zh-Hant':{Appearance:'外觀',ChoiceRecommended:'推薦',ChoiceCurrent:'目前',AllPalettes:'全部 16 套',AllTypographies:'全部 27 套',WBTypographyTitle:'正文排版',WBTypographyHint:'每張卡片展示獨立效果。方向鍵瀏覽，點擊或 Enter 套用。',TypographyPreviewMarkdown:'# 文字的溫度\n\n記錄觀察，整理思緒。\n\n## 讓想法成形\n\n> 先寫下來，再慢慢整理。'},
 'en-US':{Appearance:'Appearance',ChoiceRecommended:'Recommended',ChoiceCurrent:'Current',AllPalettes:'All 16',AllTypographies:'All 27',WBTypographyTitle:'Article typography',WBTypographyHint:'Compare each style. Use arrow keys to browse; click or press Enter to apply.',TypographyPreviewMarkdown:'# The warmth of words\n\nObserve, record, and reflect.\n\n## Give ideas a shape\n\n> Write it down. Refine it later.'},
 'ja-JP':{Appearance:'外観',ChoiceRecommended:'おすすめ',ChoiceCurrent:'選択中',AllPalettes:'全 16 種',AllTypographies:'全 27 種',WBTypographyTitle:'本文の組版',WBTypographyHint:'各スタイルを比較できます。矢印キーで移動し、クリックか Enter で適用。',TypographyPreviewMarkdown:'# ことばの温度\n\n観察を記録し、考えを整理。\n\n## 想いをかたちに\n\n> まず書き留め、少しずつ整える。'}
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
   ,AuxiliaryInfo: {'zh-CN':'文章信息','zh-Hant':'文章資訊','en-US':'Article info','ja-JP':'記事情報'}[culture]
   ,AuxiliaryChapter: {'zh-CN':'章节详情','zh-Hant':'章節詳情','en-US':'Chapter details','ja-JP':'章の詳細'}[culture]
   ,ChooseChapter: {'zh-CN':'选择一个章节','zh-Hant':'選擇一個章節','en-US':'Select a chapter','ja-JP':'章を選択'}[culture]
   ,ChooseChapterHint: {'zh-CN':'在大纲或导图中选择章节，即可查看和编辑详情。','zh-Hant':'在大綱或導圖中選擇章節，即可查看和編輯詳情。','en-US':'Select a chapter in the outline or mind map to view and edit its details.','ja-JP':'アウトラインまたはマインドマップで章を選択すると、詳細を表示・編集できます。'}[culture]
 });
 context.VexPalettes.forEach((p,i)=>{const name=paletteNames[p.key];strings['Palette'+name]=culture==='zh-CN'?p.label:culture==='zh-Hant'?traditional(p.label):p.en;strings['PalettePurpose'+name]=culture.startsWith('zh')? (culture==='zh-Hant'?traditional(p.note):p.note):p.en+' · '+(['Everyday writing','Focused work','Technical work','Long reading','Personal notes','Creative work','Calm reading','Ideas and sketches'][i%8]);});
 context.VexTypes.forEach((t,i)=>{strings['Typography'+t.key]=culture==='zh-CN'?t.label:culture==='zh-Hant'?traditional(t.label):t.en;strings['TypographyPurpose'+t.key]=culture==='zh-CN'?t.note:culture==='zh-Hant'?traditional(t.note):culture==='ja-JP'?purposeJa[i]:purposeEn[i];});
 fs.writeFileSync(file,JSON.stringify(data,null,2)+'\n');
}
const data=JSON.parse(fs.readFileSync(path.join(root,'src/Vex/I18n/zh-CN.json'),'utf8'));
fs.writeFileSync(path.join(root,'src/Vex/I18n/Language.cs'),'// <auto-generated> Generated from I18n/zh-CN.json. </auto-generated>\nnamespace Vex;\npublic static class VexL\n{\n'+Object.keys(data.Vex.VexL).map(key=>'    public static readonly string '+key+' = "Vex.VexL.'+key+'";').join('\n')+'\n}\n');
console.log('Synchronized 16 palette and 27 typography labels in four languages.');
