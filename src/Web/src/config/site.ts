/** 站点级配置：域名、品牌、仓库、giscus、统计与下载矩阵元数据。 */
export const SITE = {
  /** 线上地址（astro.config.mjs 的 site 与此保持一致） */
  url: 'https://vex.codewf.com',
  /** 品牌 */
  brand: {
    name: '维刻',
    en: 'Vex',
    slogan: '写下想法，看见脉络',
    desc: '本地优先的开源 Markdown 写作空间。',
  },
  /** 源仓库 */
  repo: 'https://github.com/dotnet9/Vex',
  releases: 'https://github.com/dotnet9/Vex/releases',
  releasesLatest: 'https://github.com/dotnet9/Vex/releases/latest',
  issues: 'https://github.com/dotnet9/Vex/issues/new/choose',
  /** 母站与作者 */
  home: 'https://codewf.com',
  docsSite: 'https://doc.codewf.com',
  author: '沙漠尽头的狼 / CodeWF',
  /**
   * giscus 评论。启用步骤（缺一步都会退回「评论即将开启」提示，不会渲染坏掉的评论区）：
   *   1. dotnet9/Vex → Settings → General → Features → 勾选 Discussions；
   *   2. 在该仓库安装 https://github.com/apps/giscus；
   *   3. 打开 https://giscus.app 选中该仓库，把生成的 categoryId 填到下面。
   * repoId 与 categoryId 需从当前仓库取得；mapping 用 url，因此每页评论互不串台。
   */
  giscus: {
    enabled: false,
    repo: 'dotnet9/Vex',
    repoId: '', // 启用时从当前 Vex 仓库取得，不能沿用旧仓库 ID
    category: 'General',
    categoryId: '', // TODO: 开启 Discussions 后填入分类 ID
    mapping: 'url',
    reactionsEnabled: '1',
  },
  /** 访问统计：国内云统计（51.La），key 留空则不注入脚本 */
  analytics: {
    provider: '51la',
    key: '', // TODO: 51.La 站点 ID（vex.codewf.com）
  },
} as const;

/** 平台下载矩阵：主 CTA 统一指向最新 Release，页面按此表给出选择建议与签名现状。 */
export const DOWNLOADS = [
  {
    platform: 'Windows', arch: 'x64',
    file: 'Vex-v{version}-win-x64-setup.exe',
    hint: '安装器或 ZIP 绿色版，无需安装 .NET 运行时。',
    note: '签名与系统兼容性见对应版本说明。', available: true,
  },
  {
    platform: 'macOS', arch: 'Apple Silicon · ARM64',
    file: 'Vex-{version}-osx-arm64.dmg',
    hint: '挂载 DMG 并将 Vex.app 拖到 Applications，也提供 ZIP。',
    note: '签名与公证状态见对应版本说明。', available: true,
  },
  {
    platform: 'macOS', arch: 'Intel · x64',
    file: 'Vex-{version}-osx-x64.dmg',
    hint: '为 Intel 芯片选择 x64 安装包。',
    note: '签名与公证状态见对应版本说明。', available: true,
  },
  {
    platform: 'Linux', arch: 'x64 · amd64',
    file: 'Vex-v{version}-linux-x64.deb',
    hint: 'Debian/Ubuntu 使用 DEB，其他环境可选择 tar.gz 绿色版。',
    note: '', available: true,
  },
  {
    platform: 'Linux', arch: 'ARM64 · aarch64',
    file: 'Vex-v{version}-linux-arm64.deb',
    hint: '按系统架构选择 ARM64 DEB 或 tar.gz。',
    note: '', available: true,
  },
] as const;
