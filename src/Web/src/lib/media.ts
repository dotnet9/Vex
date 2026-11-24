/**
 * 演示媒体解析：优先使用明确映射的当前交互 GIF，再使用当前 Avalonia 控件截图。
 * 同步脚本将它们复制到 `/assets/vex/图片/`，采集方式见源仓库的截图说明。
 * 当前截图缺失时，再按候选文件名查找 `docs/media/` 下的已有媒体。
 *
 * 约定（录制后放进 Vex 的 docs/media/）：
 *   views-instant.gif / views-source.gif / views-read.gif / views-mindmap.gif  四种视图
 *   mindmap-structures.gif   六种导图结构切换
 *   markdown-formulas.gif    公式 / 矩阵 / 化学表达式
 *   markdown-mermaid.gif     流程图 / 时序图 / 甘特图
 *   appearance-themes.gif    配色与正文排版切换
 *   export-formats.gif       导出与演示
 * 同名 `.png` 作为静态兜底；两者都不存在时组件退回图标卡片。
 */
import fs from 'node:fs';
import path from 'node:path';

const fromRoot = (...segs: string[]) => path.resolve(process.cwd(), ...segs);

export const exists = (rel: string) => {
  try {
    return fs.existsSync(fromRoot('public', 'assets', 'vex', rel));
  } catch {
    return false;
  }
};

export const assetUrl = (rel: string) => `/assets/vex/${rel.split('/').map(encodeURIComponent).join('/')}`;

/** 按候选顺序返回第一个存在的媒体（含是否为动图），都没有则返回 null */
export function pickMedia(candidates: string[]): { src: string; animated: boolean } | null {
  for (const rel of candidates) {
    const full = rel.startsWith('media/') ? rel : `media/${rel}`;
    if (exists(full)) return { src: assetUrl(full), animated: /\.gif$/i.test(full) };
  }
  return null;
}

/** 当前桌面版截图与源仓库快速开始共用一套文件，避免优先展示过期动图。 */
const currentShots: Record<string, string> = {
  'views-instant': '主界面',
  'workspace': '工作区与大纲',
  'views-source': '源码对照',
  'source-preview': '源码对照',
  'views-read': '阅读视图',
  'views-mindmap': '思维导图',
  'mindmap-structures': '思维导图',
  'mindmap-dark': '思维导图',
  'appearance-themes': '外观与配色',
  'markdown-formulas': '公式与代码',
  'markdown-mermaid': 'Mermaid图表',
  'export-formats': '导出设置',
};

export const media = (base: string) => {
  // 只优先选择本轮实际采集的动图，避免旧目录中的过时 GIF 抢占当前 PNG。
  const recordings: Record<string, string> = {
    'views-switch': '四视图切换',
    'outline-reading': '大纲导航与阅读跟随',
    'appearance-themes': '外观切换',
  };
  const recording = recordings[base] ? `图片/${recordings[base]}.gif` : null;
  if (recording && exists(recording)) return { src: assetUrl(recording), animated: true };
  const current = currentShots[base] ? `图片/${currentShots[base]}.png` : null;
  return current && exists(current)
    ? { src: assetUrl(current), animated: false }
    : pickMedia([`${base}.gif`, `${base}.png`]);
};

/** 站点自带示意图（放在 public/ 根下，如导出格式 SVG），始终可用 */
export const staticFile = (name: string): { src: string; animated: boolean } => ({
  src: `/${name}`,
  animated: false,
});
