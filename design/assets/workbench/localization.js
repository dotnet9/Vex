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
    '回到中心':['回到中心','回到中心','Center on root node','ルートへ戻る'],
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
    '自定义目录路径，例如 D:\\Notes':['自定义目录路径，例如 D:\\Notes','自訂目錄路徑，例如 D:\\Notes','Custom folder path, e.g. D:\\Notes','カスタムフォルダパス（例: D:\\Notes）'],
    '推荐':['推荐','推薦','Recommended','おすすめ'],
    '石墨':['石墨','石墨','Graphite','グラファイト'],
    '靛蓝':['靛蓝','靛藍','Indigo','インディゴ'],
    '鼠尾草':['鼠尾草','鼠尾草','Sage','セージ'],
    '松绿':['松绿','鬆綠','Pine','パイン'],
    '天青':['天青','天青','Sky','スカイ'],
    '琥珀':['琥珀','琥珀','Amber','アンバー'],
    '绯红':['绯红','緋紅','Crimson','クリムゾン'],
    '紫晶':['紫晶','紫晶','Violet','バイオレット'],
    '陶土':['陶土','陶土','Terracotta','テラコッタ'],
    '新增配色':['新增配色','新增配色','New palette','配色を追加'],
    '新增自定义配色':['新增自定义配色','新增自訂配色','Add custom palette','カスタム配色を追加'],
    '从工作区移除…':['从工作区移除…','從工作區移除…','Remove from workspace…','ワークスペースから除去…'],
    '脚注':['脚注','腳註','Footnote','脚注'],
    '删除线':['删除线','刪除線','Strikethrough','取り消し線'],
    '高亮':['高亮','螢光標記','Highlight','ハイライト'],
    '上标':['上标','上標','Superscript','上付き'],
    '下标':['下标','下標','Subscript','下付き'],
    '显示词数':['显示词数','顯示字數','Show word count','文字数を表示'],
    '显示文档水印':['显示文档水印','顯示文件浮水印','Show document watermark','文書の透かしを表示'],
    '偏好设置':['偏好设置','偏好設定','Preferences','環境設定'],
    '关闭偏好设置':['关闭偏好设置','關閉偏好設定','Close preferences','環境設定を閉じる'],
    '返回设置分类':['返回设置分类','返回設定分類','Back to categories','設定カテゴリに戻る'],
    '设置分类':['设置分类','設定分類','Settings categories','設定カテゴリ'],
    '通用与文件':['通用与文件','通用與檔案','General & files','一般とファイル'],
    '编辑器':['编辑器','編輯器','Editor','エディター'],
    '窗口与动效':['窗口与动效','視窗與動效','Window & motion','ウィンドウとモーション'],
    '启动时':['启动时','啟動時','On startup','起動時'],
    '打开 Vex 后首先看到的内容。':['打开 Vex 后首先看到的内容。','開啟 Vex 後首先看到的內容。','What you see first when Vex opens.','Vex を開いたときに最初に表示する内容。'],
    '自动保存':['自动保存','自動保存','Auto save','自動保存'],
    '编辑过程中自动写入磁盘，无需手动保存。':['编辑过程中自动写入磁盘，无需手动保存。','編輯過程中自動寫入磁碟，無需手動保存。','Writes to disk as you type — no manual saving needed.','入力中に自動でディスクへ書き込みます。'],
    '停止输入后保存':['停止输入后保存','停止輸入後保存','Save after idle','入力停止後に保存'],
    '自动保存开启后，停顿多久执行一次写入。':['自动保存开启后，停顿多久执行一次写入。','自動保存開啟後，停頓多久執行一次寫入。','How long to wait after typing stops before writing.','入力停止後の書き込み待ち時間。'],
    '保留恢复草稿':['保留恢复草稿','保留恢復草稿','Keep recovery drafts','復元ドラフトを保持'],
    '异常退出后可恢复未保存的内容。':['异常退出后可恢复未保存的内容。','異常退出後可復原未儲存的內容。','Recover unsaved content after a crash.','異常終了後に未保存の内容を復元します。'],
    '新文档默认值':['新文档默认值','新文件預設值','New document defaults','新規文書の既定値'],
    '编码':['编码','編碼','Encoding','エンコーディング'],
    '仅作用于新建文档，不影响已打开文件。':['仅作用于新建文档，不影响已打开文件。','僅作用於新建文件，不影響已開啟檔案。','Applies to new documents only, not opened files.','新規文書のみに適用します。'],
    '换行符':['换行符','換行符','Line endings','改行コード'],
    '编辑区字体':['编辑区字体','編輯區字體','Editor font','エディターのフォント'],
    '缩进':['缩进','縮排','Indentation','インデント'],
    '代码与源码':['代码与源码','程式碼與原始碼','Code & source','コードとソース'],
    '代码自动换行':['代码自动换行','程式碼自動換行','Code word wrap','コードの折り返し'],
    '显示源码行号':['显示源码行号','顯示原始碼行號','Show source line numbers','ソースの行番号を表示'],
    '大纲显示章节序号':['大纲显示章节序号','大綱顯示章節序號','Number sections in outline','アウトラインに章番号を表示'],
    '文章自带序号时不重复，未手写序号可开。':['文章自带序号时不重复，未手写序号可开。','文章自帶序號時不重複，未手寫序號可開。','Skip when the article already numbers its sections; enable otherwise.','本文に番号がある場合は二重に付けません。'],
    '插入时复制图片':['插入时复制图片','插入時複製圖片','Copy images on insert','挿入時に画像をコピー'],
    '使用相对路径':['使用相对路径','使用相對路徑','Use relative paths','相対パスを使用'],
    '资源目录':['资源目录','資源目錄','Assets folder','リソースフォルダー'],
    '选择目录':['选择目录','選擇目錄','Choose folder','フォルダーを選択'],
    '界面动效':['界面动效','介面動效','UI motion','UI モーション'],
    '精简只保留淡入淡出，关闭后所有过渡立即到位。':['精简只保留淡入淡出，关闭后所有过渡立即到位。','精簡只保留淡入淡出，關閉後所有過渡立即到位。','Reduced keeps fades only; Off removes all transitions.','精簡はフェードのみ、オフは即時に切り替わります。'],
    '完整':['完整','完整','Full','フル'],
    '精简':['精简','精簡','Reduced','精簡'],
    '关闭':['关闭','關閉','Off','オフ'],
    '界面圆角':['界面圆角','介面圓角','Corner radius','コーナー半径'],
    '只作用于控件圆角，胶囊与头像保持原形。':['只作用于控件圆角，胶囊与头像保持原形。','只作用於控制項圓角，膠囊與頭像保持原形。','Applies to control corners; pills and avatars stay round.','控制項の角のみに適用。ピルとアバターは丸いまま。'],
    '直角':['直角','直角','Square','スクエア'],
    '紧凑':['紧凑','緊湊','Compact','コンパクト'],
    '标准':['标准','標準','Standard','標準'],
    '水印文字':['水印文字','浮水印文字','Watermark text','透かしテキスト'],
    '水印字号':['水印字号','浮水印字號','Watermark size','透かしサイズ'],
    '前景色':['前景色','前景色','Foreground','前景色'],
    '粗细':['粗细','粗細','Weight','太さ'],
    '不透明度':['不透明度','不透明度','Opacity','不透明度'],
    '导出后打开':['导出后打开','匯出後開啟','Open after export','書き出し後に開く'],
    '纸张':['纸张','紙張','Paper','用紙'],
    '页边距':['页边距','頁邊距','Page margins','余白'],
    '保留当前文档排版':['保留当前文档排版','保留目前文件排版','Keep current typography','現在の組版を保持'],
    '服务':['服务','服務','Service','サービス'],
    '客户端':['客户端','用戶端','Clients','クライアント'],
    '审计':['审计','稽核','Audit','監査'],
    '启用 MCP 服务':['启用 MCP 服务','啟用 MCP 服務','Enable MCP service','MCP サービスを有効化'],
    '仅监听 127.0.0.1':['仅监听 127.0.0.1','僅監聽 127.0.0.1','Listen on 127.0.0.1 only','127.0.0.1 のみで待ち受ける'],
    '服务已停止':['服务已停止','服務已停止','Service stopped','サービスは停止中'],
    '原型演示':['原型演示','原型演示','Prototype demo','プロトタイプのデモ'],
    '端口':['端口','連接埠','Port','ポート'],
    '访问令牌':['访问令牌','存取權杖','Access token','アクセストークン'],
    '显示或隐藏令牌':['显示或隐藏令牌','顯示或隱藏權杖','Show or hide token','トークンの表示を切り替え'],
    '重新生成令牌':['重新生成令牌','重新產生權杖','Regenerate token','トークンを再生成'],
    '授权范围':['授权范围','授權範圍','Authorized scopes','認可スコープ'],
    '修改前确认':['修改前确认','修改前確認','Confirm before writes','変更前に確認する'],
    '新增':['新增','新增','New','新機能'],
    '优化':['优化','優化','Improved','改善'],
    '修复':['修复','修復','Fixed','修正'],
    '删除':['删除','刪除','Removed','削除'],
    '稍后升级':['稍后升级','稍後升級','Later','後で更新'],
    '立即升级':['立即升级','立即升級','Upgrade now','今すぐ更新'],
    '立即安装':['立即安装','立即安裝','Install now','今すぐインストール'],
    '重试':['重试','重試','Retry','再試行'],
    '完成':['完成','完成','Done','完了'],
    '更新':['更新','更新','Update','更新'],
    '上一个':['上一个','上一個','Previous','前へ'],
    '下一个':['下一个','下一個','Next','次へ'],
    'Enter 下一个 · Shift+Enter 上一个 · Esc 关闭':['Enter 下一个 · Shift+Enter 上一个 · Esc 关闭','Enter 下一個 · Shift+Enter 上一個 · Esc 關閉','Enter next · Shift+Enter previous · Esc closes','Enter で次へ · Shift+Enter で前へ · Esc で閉じる'],
    '子章节':['子章节','子章節','Sub-section','サブ章'],
    '同级':['同级','同層級','Sibling','同レベル'],
    '仅标题':['仅标题','僅標題','Headings only','見出しのみ'],
    '收起分支':['收起分支','收起分支','Collapse branches','ブランチを折りたたむ'],
    '展开全部':['展开全部','展開全部','Expand all','すべて展開'],
    '对照源码':['对照源码','對照原始碼','Compare source','ソースと対照'],
    '导图样式':['导图样式','導圖樣式','Map style','マップスタイル'],
    '工具条已钉住，点击取消钉住':['工具条已钉住，点击取消钉住','工具列已釘選，點擊取消釘選','Toolbar pinned — click to unpin','ツールバーをピン固定中。クリックで解除'],
    '新增子章节':['新增子章节','新增子章節','Add sub-section','サブ章を追加'],
    '新增同级章节':['新增同级章节','新增同層級章節','Add sibling section','同レベルの章を追加'],
    '只显示章节标题':['只显示章节标题','只顯示章節標題','Headings only','見出しのみ表示'],
    '清雅':['清雅','清雅','Quiet','静'],
    '缤纷':['缤纷','繽紛','Colorful','彩'],
    '线框':['线框','線框','Outline','線'],
    '胶囊':['胶囊','膠囊','Capsule','カプセル'],
    '脉络':['脉络','脈絡','Vein','脈'],
    '黑白':['黑白','黑白','Mono','白黒'],
    '单侧树形':['单侧树形','單側樹形','One-sided tree','片側ツリー'],
    '双侧平衡':['双侧平衡','雙側平衡','Balanced tree','両側ツリー'],
    '中心放射':['中心放射','中心放射','Radial','放射状'],
    '向下树形':['向下树形','向下樹形','Top-down tree','トップダウン'],
    '目录树':['目录树','目錄樹','Directory tree','ディレクトリツリー'],
    '鱼骨图':['鱼骨图','魚骨圖','Fishbone','フィッシュボーン'],
    '单色 · 跟随强调色':['单色 · 跟随强调色','單色 · 跟隨強調色','Monochrome · follows accent','単色 · アクセントに追従'],
    '分支各一色':['分支各一色','分支各一色','One color per branch','ブランチごとに色'],
    '根节点只勾轮廓':['根节点只勾轮廓','根節點只勾輪廓','Root drawn as outline','ルートは輪郭のみ'],
    '圆角胶囊底':['圆角胶囊底','圓角膠囊底','Rounded capsule fill','角丸カプセル地'],
    '子节点留底线':['子节点留底线','子節點留底線','Underline on children','子ノードに下線'],
    '打印 · 截图':['打印 · 截图','列印 · 截圖','Print · screenshot','印刷 · スクリーンショット'],
    '章节标题':['章节标题','章節標題','Section title','章タイトル'],
    '主导航':['主导航','主導航','Primary navigation','メインナビゲーション'],
    '视图切换':['视图切换','視圖切換','View switch','ビュー切替'],
    '工作区视图':['工作区视图','工作區視圖','Workspace views','ワークスペースビュー'],
    '设置':['设置','設置','Settings','設定'],
    '搜索':['搜索','搜尋','Search','検索'],
    '更多':['更多','更多','More','その他'],
    '排版':['排版','排版','Typography','組版'],
    '字数':['字数','字數','Words','文字数'],'选中':['选中','選取','Selected','選択'],'导图缩放':['导图缩放','導圖縮放','Map zoom','マップズーム'],'结构':['结构','結構','Structure','構造'],'风格':['风格','風格','Style','スタイル'],'导图结构':['导图结构','導圖結構','Map layout','マップ構造'],'导图风格':['导图风格','導圖風格','Map style','マップスタイル'],
    '文件列表':['文件列表','文件列表','File list','ファイル一覧'],
    '字号':['字号','字號','Font size','フォントサイズ'],
    '文字颜色':['文字颜色','文字顏色','Text color','文字色'],
    '背景颜色':['背景颜色','背景顏色','Highlight color','背景色'],
    '发现新版本，点击查看更新日志':['发现新版本，点击查看更新日志','發現新版本，點擊查看更新日誌','New version available — click for release notes','新バージョンあり。更新ログを表示'],
    '正在下载更新，点击恢复窗口':['正在下载更新，点击恢复窗口','正在下載更新，點擊恢復視窗','Downloading — click to restore the window','ダウンロード中。クリックで復元'],
    '新版本已就绪，点击恢复窗口':['新版本已就绪，点击恢复窗口','新版本已就緒，點擊恢復視窗','Update ready — click to restore the window','更新準備完了。クリックで復元'],
  };
  const cultures = ['zh-CN','zh-Hant','en-US','ja-JP'];
  let culture = VexStorage.getItem('vex-prototype-language') || 'zh-CN';
  const normalize = text => text.replace(/[\s.…（）()·]/g, '');
  const byText = new Map(Object.entries(VEX_DATA.languages['zh-CN']).filter(([,value]) => typeof value === 'string').map(([key,value]) => [normalize(value),key]));
  /* 动态文案模板：键为匹配 zh 文案的正则，值为按文化返回的模板函数（捕获组按序注入 $1..）。 */
  const templates = [
    [/^摘要 (\d+) 字$/, ['摘要 $1 字','摘要 $1 字','Summary: $1 chars','要約 $1 字']],
    [/^Vex最新([\d.]+)版本来了！$/, ['Vex最新$1版本来了！','Vex最新$1版本來了！','Vex $1 is here!','Vex $1 が登場！']],
    [/^您目前使用版本为([\d.]+)版本，新版本更新日志如下：$/, ['您目前使用版本为$1版本，新版本更新日志如下：','您目前使用版本為$1版本，新版本更新日誌如下：','You are on $1. What\'s new since then:','現在のバージョンは $1。最新の更新ログ：']],
    [/^发现新版本 V([\d.]+)，点击查看更新日志$/, ['发现新版本 V$1，点击查看更新日志','發現新版本 V$1，點擊查看更新日誌','V$1 available — click for release notes','新バージョン V$1。更新ログを見るにはクリック']],
    [/^布局：(.+)$/, ['布局：$1','佈局：$1','Layout: $1','レイアウト: $1']],
    [/^当前视图：(.+)，点击更换$/, ['当前视图：$1，点击更换','目前檢視：$1，點擊更換','Current view: $1 — click to change','現在のビュー: $1（クリックで変更）']],
    [/^字数 ([\d,]+)$/, ['字数 $1','字數 $1','Words $1','文字数 $1']]
  ];
  /* 布局名由 workspace.js presets 动态拼装，走同一张表。 */
  const LAYOUT_WORDS = {
    '即时编辑': ['即時編輯', 'Instant editing', '即時編集'], '源码编辑': ['源碼編輯', 'Source editing', 'ソース編集'],
    '阅读': ['閱讀', 'Reading', '閲読'], '思维导图': ['思維導圖', 'Mind map', 'マインドマップ'],
    '源码 + 预览': ['源碼 + 預覽', 'Source + preview', 'ソース + プレビュー'], '源码 + 导图': ['源碼 + 導圖', 'Source + map', 'ソース + マップ'],
    '即时编辑 + 导图': ['即時編輯 + 導圖', 'Instant + map', '即時編集 + マップ'], '三视图': ['三視圖', 'Three panes', '3ペイン']
  };
  function get(key) { return VEX_DATA.languages[culture][key] || VEX_DATA.languages['en-US'][key] || key; }
  function label(text) { if (culture === 'zh-CN') return text;
    for (const [pattern, values] of templates) {
      const match = pattern.exec(text);
      if (match) { const index = cultures.indexOf(culture); let out = values[index].replace(/\$(\d)/g, (_, d) => match[+d]);
        out = out.replace(/\$(\d)/g, (_, d) => match[+d]);
        for (const [zh, words] of Object.entries(LAYOUT_WORDS)) { const wi = cultures.indexOf(culture) - 1; if (out.includes(zh) && words[wi]) out = out.split(zh).join(words[wi]); }
        return out; }
    }
    const key = aliases[text] || byText.get(normalize(text)); if (key) return get(key);
    const bare = text.replace(/…$/, ""), hit = extra[text] || extra[bare];
    if (!hit) return text;
    const value = hit[cultures.indexOf(culture)];
    return value !== undefined && text.endsWith("…") && !/[….]$/.test(value) ? value + "…" : value || text; }
  /* 扫描：把动态构建、未经 data-l10n 标注的界面文案按文本匹配登记（extra/byText/模板），
   * 含 title / aria-label / placeholder 属性与「图标+文字」混合按钮的纯文本子节点。
   * 根集合之外的容器可由各构建脚本调用 VexL.sync(容器) 局部登记+翻译。 */
  /* 全量扫描：登记命中字典/模板的界面文案（精确匹配才会被翻译，样例正文不会误翻）。
   * SKIP 排除纯内容区（正文渲染、导图节点、文件名列表、更新日志正文、水印示例等）。 */
  const SCAN_ROOTS = 'body';
  const SCAN_SKIP = '.md-body,.map-stage,.vex-watermark-sample,#about-copyright,.lang-chip,#update-list,.upd-pop-body,.upd-quote,.structure-sidebar li,.file-item,.file-root-name,.chapter-rename';
  const CJK_RE = /[\u3400-\u9fff]/;
  function textMatch(text) {
    const bare = text.replace(/…$/, '');
    return !!(aliases[text] || extra[text] || extra[bare] || byText.has(normalize(text)) || templates.some(([pattern]) => pattern.test(text)));
  }
  function scan(root) {
    const roots = root ? [root] : document.querySelectorAll(SCAN_ROOTS);
    roots.forEach(rootEl => rootEl.querySelectorAll('*').forEach(el => {
      if (!root && el.closest(SCAN_SKIP)) return;
      for (const [attr, key] of [['title','protoTitle'],['aria-label','protoAria'],['placeholder','protoPlaceholder']]) {
        const value = el.getAttribute(attr);
        if (value && CJK_RE.test(value) && textMatch(value)) el.dataset[key] = value;
      }
      const own = [...el.childNodes].filter(n => n.nodeType === 3).map(n => n.textContent).join("").trim();
      if (!own || !CJK_RE.test(own) || !textMatch(own)) return;
      if (!el.children.length && !el.hasAttribute('data-l10n')) el.dataset.protoLabel = own;
      else el.dataset.protoText = own;
    }));
  }
  function translate(root) {
    scan(root);
    const scope = root || document;
    scope.querySelectorAll('[data-proto-label]').forEach(el => { el.textContent = label(el.dataset.protoLabel); });
    scope.querySelectorAll('[data-proto-text]').forEach(el => {
      const want = el.dataset.protoText;
      for (const node of el.childNodes) if (node.nodeType === 3 && node.textContent.trim() === want) { node.textContent = label(want); break; }
    });
    scope.querySelectorAll('[data-proto-title]').forEach(el => { const value = label(el.dataset.protoTitle); el.title = value; el.setAttribute('aria-label', value); });
    scope.querySelectorAll('[data-proto-aria]').forEach(el => el.setAttribute('aria-label', label(el.dataset.protoAria)));
    scope.querySelectorAll('[data-proto-placeholder]').forEach(el => el.placeholder = label(el.dataset.protoPlaceholder));
  }
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
    translate();
    window.dispatchEvent(new CustomEvent('vex-language-changed'));
  }
  window.VexL = { get, label, t: label, sync: translate, scan, set(value) { if (!cultures.includes(value)) return; culture = value; VexStorage.setItem('vex-prototype-language', value); apply(); }, get culture() { return culture; } };
  document.addEventListener('DOMContentLoaded', () => {
    const roots = document.querySelectorAll('.titlebar,.work-toolbar,.edit-toolbar,.side-tabs,.pane-head,.about-rows,.about-hero,.mini-titlebar');
    roots.forEach(root => root.querySelectorAll('*').forEach(el => {
      if (el.children.length || el.hasAttribute('data-l10n') || el.closest('.about-rows')) return;
      const text = el.textContent.trim(); if (text && (aliases[text] || byText.has(normalize(text)) || extra[text])) el.dataset.protoLabel = text;
    }));
    apply();
  });
})();
