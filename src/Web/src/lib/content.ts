import fs from 'node:fs';
import path from 'node:path';
import manifest from '../data/content.json';

/** Astro 打包后运行期文件一律以站点根（vex/）解析 */
const fromRoot = (...segs: string[]) => path.resolve(process.cwd(), ...segs);

export const CONTENT = manifest;
export const SRC = manifest.source;

function readJson<T>(rel: string, fallback: T): T {
  try {
    return JSON.parse(fs.readFileSync(fromRoot(rel), 'utf8')) as T;
  } catch {
    return fallback;
  }
}

/* ── 文档 ─────────────────────────── */
export interface DocPage {
  path: string;
  title: string;
  excerpt: string;
  html: string;
  toc: { level: number; text: string; id: string }[];
  group: string;
  /** content.json order 里的位置，用于侧栏排序（未列出为极大值） */
  order?: number;
  sourcePath: string;
}
export interface DocsData {
  pages: DocPage[];
  updatedAt: string | null;
}

export const loadDocs = (): DocsData =>
  readJson('src/generated/docs/vex.json', { pages: [], updatedAt: null });

export interface DocGroup {
  key: string;
  label: string;
  desc: string;
  items: DocPage[];
  index: DocPage | null;
}

/** 组内排序：content.json 的 order 优先，其余按路径（数字前缀自然成序） */
const byOrder = (a: DocPage, b: DocPage) =>
  (a.order ?? Number.MAX_SAFE_INTEGER) - (b.order ?? Number.MAX_SAFE_INTEGER) ||
  a.path.localeCompare(b.path, 'zh-Hans', { numeric: true });

/** 按 content.json 的分组声明组织侧栏：概述页在前，其后按 order/路径排序 */
export function docGroups(docs: DocsData): DocGroup[] {
  const rules = CONTENT.docs.groupRules as Record<string, { label: string; desc: string }>;
  return Object.entries(rules).map(([key, g]) => {
    const inGroup = docs.pages.filter((p) => p.group === key).sort(byOrder);
    return {
      key,
      label: g.label,
      desc: g.desc,
      items: inGroup.filter((p) => p.path !== ''),
      index: inGroup.find((p) => p.path === '') ?? null,
    };
  });
}

/** 侧栏/翻页顺序：分组顺序 + 组内顺序，概述页排在其分组最前 */
export function orderedDocs(docs: DocsData): DocPage[] {
  return docGroups(docs).flatMap((g) => [...(g.index ? [g.index] : []), ...g.items]);
}

export const docUrl = (pagePath: string) => `/docs/${pagePath ? pagePath.split('/').map(encodeURIComponent).join('/') + '/' : ''}`;

/** 检查构建产物中是否存在某静态资源（截图 / 原型） */
export const hasAsset = (rel: string) => {
  try {
    return fs.existsSync(fromRoot('public', 'assets', 'vex', rel));
  } catch {
    return false;
  }
};

/** 设计原型是否已同步（源仓库存在 design/ 时才有） */
export const hasDesign = () => {
  try {
    return fs.existsSync(fromRoot('public', 'design', 'index.html'));
  } catch {
    return false;
  }
};

export { assetUrl, media, pickMedia, staticFile } from './media';

export const repoBlob = (sourcePath: string) =>
  `${SRC.repo}/blob/HEAD/${sourcePath.split('/').map(encodeURIComponent).join('/')}`;

/* ── 更新日志 ─────────────────────── */
export interface ChangelogEntry {
  version: string;
  date: string | null;
  title: string;
  digest: string;
  html: string;
}
export interface ChangelogData {
  source: string | null;
  updatedAt: string | null;
  entries: ChangelogEntry[];
}

export const loadChangelog = (): ChangelogData =>
  readJson('src/generated/changelog/vex.json', { source: null, updatedAt: null, entries: [] });

/* ── 指标与同步信息 ───────────────── */
export interface Metrics {
  fetchedAt: string | null;
  repo: string;
  stars: number;
  release: { version: string; date: string | null; url: string; assets?: { name: string; size: number; url: string }[] } | null;
}

export const loadMetrics = (): Metrics =>
  readJson('src/generated/metrics.json', { fetchedAt: null, repo: '', stars: 0, release: null });

export interface SyncInfo {
  mode: string;
  generatedAt: string;
  updatedAt: string | null;
  docs: number;
  changelog: number;
  assets: number;
  design: boolean;
  logo: string | null;
  warnings: string[];
}

export const loadSyncInfo = (): SyncInfo =>
  readJson('src/generated/sync.json', {
    mode: '-',
    generatedAt: '',
    updatedAt: null,
    docs: 0,
    changelog: 0,
    assets: 0,
    design: false,
    logo: null,
    warnings: [],
  });

export type { DocPage as DocPageType };
