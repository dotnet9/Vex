/**
 * 内容同步（维刻 Vex 官网）：站点已与本仓同库（web/），直接读取上级仓库根的
 * 文档 / 更新日志 / logo / 设计原型，渲染为站点数据。不再有跨仓克隆与通知。
 *
 * 产物（均为 gitignore，构建时生成）：
 *   public/logos/vex.*                    项目 logo
 *   public/assets/vex/**                  文档引用的图片等静态资源
 *   public/design/**                      可交互设计原型（design/ 目录）
 *   src/generated/docs/vex.json           渲染好的文档页（html/toc/标题/分组/来源路径）
 *   src/generated/changelog/vex.json      解析好的更新日志条目
 *   src/generated/sync.json               同步元信息（文档数、最后提交时间、告警）
 */
import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { renderMarkdown } from './lib/markdown.mjs';
import { parseChangelog } from './lib/changelog.mjs';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const CONTENT = JSON.parse(fs.readFileSync(path.join(ROOT, 'src/data/content.json'), 'utf8'));
const SRC = CONTENT.source;
const MD_RE = /\.(md|markdown)$/i;
/** 只同步文档真正会显示的资源类型；性能 JSON 归档不进站（正文链接改写为 GitHub） */
const ASSET_RE = /\.(png|jpe?g|gif|webp|svg|avif|ico|bmp|zip)$/i;
/** 无扩展名的仓库根文件：GitHub 链接用 blob 而不是 tree */
const ROOT_FILES = new Set(['LICENSE', 'NOTICE', 'CHANGELOG', 'README', 'CONTRIBUTING', 'SECURITY', 'AUTHORS']);

const warnings = [];
const warn = (msg) => {
  warnings.push(msg);
  console.log(`  警告：${msg}`);
};

const walk = (dir, out = []) => {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    if (e.name === '.git') continue;
    const full = path.join(dir, e.name);
    if (e.isDirectory()) walk(full, out);
    else out.push(full);
  }
  return out;
};

const norm = (p) => p.split(path.sep).join('/');

function gitLastDate(repoDir) {
  try {
    return execFileSync('git', ['-C', repoDir, 'log', '-1', '--format=%cs'], { encoding: 'utf8' }).trim();
  } catch {
    return null;
  }
}

function sourceRoot() {
  return path.resolve(ROOT, SRC.repoPath);
}

/**
 * 文档分组：按 `groupRules` 的目录深度判断，而不是逐个文件名——
 * 源仓库会重命名/移动文档（英文名改中文名、新增子目录），按深度分组不会因此漏页。
 *   use：仓库根与 docs/ 顶层的用户向文档（depth 0）
 *   dev：docs/ 子目录里的组件包、MCP、脚本与验收记录（depth ≥ 1）
 */
function groupOf(ns) {
  const rules = CONTENT.docs.groupRules;
  const depth = ns.split('/').length - 1;
  if (rules.use.alwaysUse.includes(ns)) return 'use';
  if (depth >= (rules.dev.depthAtLeast ?? 1)) return 'dev';
  if (depth <= (rules.use.depth ?? 0)) return 'use';
  return null;
}

/** 侧栏顺序：content.json 的 order 优先，未列出的按名称排在后面 */
function orderIndex(ns) {
  const i = CONTENT.docs.order.indexOf(ns);
  return i < 0 ? Number.MAX_SAFE_INTEGER : i;
}

/** 仓库路径 → 站内命名空间（去掉文档根目录前缀） */
function toSiteNs(repoPath) {
  const clean = repoPath.replace(/^\.\//, '');
  const root = SRC.docRoots[0];
  if (clean === root) return '';
  if (clean.startsWith(root + '/')) return clean.slice(root.length + 1);
  return clean;
}

/** 站内命名空间 → 文档 URL（README 为索引页） */
const docUrl = (ns, anchor = '') => {
  const noExt = ns.replace(MD_RE, '');
  const isIndex = noExt === CONTENT.docs.overview.replace(MD_RE, '') || /^(readme|index)$/i.test(noExt);
  const tail = isIndex ? '' : noExt.split('/').map(encodeURIComponent).join('/') + '/';
  return `/docs/${tail}${anchor ? `#${anchor}` : ''}`;
};
/** 越界引用 → GitHub 仓库链接（目录用 tree，文件用 blob） */
function repoUrl(repoRelative) {
  const clean = repoRelative.replace(/^(\.\.?\/)+/, '').replace(/\/+$/, '');
  if (!clean) return `${SRC.repo}/tree/HEAD/${SRC.docRoots[0]}`;
  const base = clean.split('/').pop() ?? '';
  const isFile = /\.[a-z0-9]+$/i.test(base) || base.startsWith('.') || ROOT_FILES.has(base);
  return `${SRC.repo}/${isFile ? 'blob' : 'tree'}/HEAD/${clean.split('/').map(encodeURIComponent).join('/')}`;
}

/** 重写 Markdown 中的相对链接：站内文档 / 站内资源 / GitHub 兜底 */
function makeRewriter(sets, repoDir) {
  const rewrite = (href) => {
    if (/^(https?:|mailto:|#|data:|\/\/)/i.test(href)) return href;
    const [target, anchor = ''] = href.split('#');
    if (!target) return href;
    // 仓库内相对路径（`../` 逃出文档根时按根内路径兜底，与源仓库的引用习惯一致）
    const repoRelative = path.posix
      .normalize(path.posix.join(repoDir, target.replace(/\\/g, '/')))
      .replace(/^(\.\.?\/)+/, '');
    const siteNs = toSiteNs(repoRelative);
    const anchorPart = anchor ? `#${anchor}` : '';

    if (/changelog\.md$/i.test(siteNs)) return `/changelog/${anchorPart}`;
    if (sets.md.has(siteNs)) return docUrl(siteNs, anchor);
    if (sets.asset.has(siteNs)) {
      return `/assets/${SRC.slug}/${siteNs.split('/').map(encodeURIComponent).join('/')}`;
    }
    // 同目录简写（`readme.md` 这类不带路径的引用）按文件名兜底
    if (!siteNs.includes('/')) {
      const base = siteNs.toLowerCase();
      const mdHit = sets.mdByBase.get(base);
      if (mdHit) return docUrl(mdHit, anchor);
      const assetHit = sets.assetByBase.get(base);
      if (assetHit) {
        return `/assets/${SRC.slug}/${assetHit.split('/').map(encodeURIComponent).join('/')}`;
      }
    }
    return repoUrl(repoRelative);
  };
  return (text) =>
    text
      // [text](href "title")
      .replace(/(\]\()([^)\s]+)((?:\s+"[^"]*")?\))/g, (m, pre, href, post) => pre + rewrite(href) + post)
      // <img src="..."> 与 <a href="...">（源文档里的原始 HTML 区块）
      .replace(/(<(?:img|a)\b[^>]*?(?:src|href)=)(["'])([^"']+)\2/gi, (m, pre, q, href) => pre + q + rewrite(href) + q);
}

/** 设计原型：复制 HTML/资源，修正共享字体路径，并加一条返回官网的入口 */
function copyDesign(srcRoot) {
  const from = path.join(srcRoot, 'design');
  const to = path.join(ROOT, 'public', 'design');
  fs.rmSync(to, { recursive: true, force: true });
  if (!fs.existsSync(from)) {
    console.log('  跳过设计原型：源目录不存在 design/');
    return false;
  }
  fs.mkdirSync(to, { recursive: true });
  const skip = new Set(['scripts', 'tests', 'README.md']);
  for (const e of fs.readdirSync(from, { withFileTypes: true })) {
    if (skip.has(e.name)) continue;
    fs.cpSync(path.join(from, e.name), path.join(to, e.name), { recursive: true });
  }
  // 原型共享应用内代码字体，源路径指向仓库内的 apps/vex/assets/fonts/
  const fontDir = path.join(srcRoot, 'apps/vex/assets/fonts');
  const fontCss = path.join(to, 'assets/fonts.css');
  if (fs.existsSync(fontCss) && fs.existsSync(fontDir)) {
    fs.mkdirSync(path.join(to, 'apps-fonts'), { recursive: true });
    for (const f of fs.readdirSync(fontDir)) {
      if (f.toLowerCase().endsWith('.ttf')) {
        fs.copyFileSync(path.join(fontDir, f), path.join(to, 'apps-fonts', f));
      }
    }
    fs.writeFileSync(
      fontCss,
      fs
        .readFileSync(fontCss, 'utf8')
        .replace(/\.\.\/\.\.\/apps\/vex\/assets\/fonts\//g, '../apps-fonts/'),
    );
  }
  const back = `<a href="/" style="position:fixed;left:14px;bottom:14px;z-index:99999;padding:6px 13px;border-radius:999px;background:#24766b;color:#fff;font:12px/1.7 system-ui,sans-serif;text-decoration:none;box-shadow:0 4px 14px rgba(0,0,0,.18)">← 返回维刻官网</a>`;
  for (const f of fs.readdirSync(to)) {
    const p = path.join(to, f);
    if (!f.endsWith('.html') || !fs.statSync(p).isFile()) continue;
    const html = fs.readFileSync(p, 'utf8');
    if (!/<\/body>/i.test(html) || html.includes('返回维刻官网')) continue;
    fs.writeFileSync(p, html.replace(/<\/body>/i, `${back}\n</body>`));
  }
  return true;
}

function main() {
  console.log(`== ${SRC.slug} ==`);
  const srcRoot = sourceRoot();
  if (!fs.existsSync(srcRoot)) {
    console.error(`源目录不存在：${srcRoot}`);
    process.exitCode = 1;
    return;
  }

  // 1. 收集 Markdown 与资源（命名空间 = 站内路径）
  const mdFiles = [];
  const assetFiles = [];
  const excludes = (CONTENT.docs.exclude ?? []).map((p) => p.replace(/\/$/, ''));
  const pushEntry = (abs) => {
    const repoPath = norm(path.relative(srcRoot, abs));
    const ns = toSiteNs(repoPath);
    if (!ns) return;
    if (excludes.some((x) => ns === x || ns.startsWith(x + '/'))) return;
    (MD_RE.test(ns) ? mdFiles : ASSET_RE.test(ns) ? assetFiles : []).push({ ns, abs, repoPath });
  };
  for (const root of SRC.docRoots) {
    const abs = path.join(srcRoot, root);
    if (!fs.existsSync(abs)) {
      warn(`文档根目录不存在：${root}`);
      continue;
    }
    for (const f of walk(abs)) pushEntry(f);
  }
  for (const extra of SRC.extraDocs) {
    const abs = path.join(srcRoot, extra);
    if (!fs.existsSync(abs)) {
      warn(`补充文档不存在：${extra}`);
      continue;
    }
    pushEntry(abs);
  }

  // 2. 资源复制 → public/assets/vex/
  const assetSet = new Set(assetFiles.map((a) => a.ns));
  const assetByBase = new Map(assetFiles.map((a) => [a.ns.split('/').pop().toLowerCase(), a.ns]));
  const assetDest = path.join(ROOT, 'public', 'assets', SRC.slug);
  fs.rmSync(assetDest, { recursive: true, force: true });
  for (const a of assetFiles) {
    const dest = path.join(assetDest, a.ns);
    fs.mkdirSync(path.dirname(dest), { recursive: true });
    fs.copyFileSync(a.abs, dest);
  }

  // 3. Markdown 渲染、链接重写、分组
  const mdSet = new Set(mdFiles.map((m) => m.ns));
  const mdByBase = new Map(mdFiles.map((m) => [m.ns.split('/').pop().toLowerCase(), m.ns]));
  const lastDate = gitLastDate(srcRoot);
  const titles = CONTENT.docs.titles ?? {};
  const pages = [];
  for (const m of mdFiles) {
    const raw = fs.readFileSync(m.abs, 'utf8');
    const repoDir = path.posix.dirname(m.repoPath);
    const rewritten = makeRewriter({ md: mdSet, mdByBase, asset: assetSet, assetByBase }, repoDir === '.' ? '' : repoDir)(raw);
    const { html, toc, title, excerpt } = renderMarkdown(rewritten);
    const group = groupOf(m.ns) ?? 'use';
    const noExt = m.ns.replace(MD_RE, '');
    const isOverview = noExt === CONTENT.docs.overview.replace(MD_RE, '');
    pages.push({
      path: isOverview ? '' : noExt,
      title: titles[m.ns] ?? title,
      excerpt,
      html,
      toc,
      group,
      order: orderIndex(m.ns),
      sourcePath: m.repoPath,
    });
  }
  fs.mkdirSync(path.join(ROOT, 'src/generated/docs'), { recursive: true });
  fs.writeFileSync(
    path.join(ROOT, 'src/generated/docs', `${SRC.slug}.json`),
    JSON.stringify({ pages, updatedAt: lastDate }, null, 1),
  );

  // 4. 更新日志（去掉文件首行标题，避免与页面标题重复）
  const clAbs = path.join(srcRoot, SRC.changelog);
  let changelog = { source: null, updatedAt: null, entries: [] };
  if (fs.existsSync(clAbs)) {
    const raw = fs.readFileSync(clAbs, 'utf8').replace(/^\s*#\s+[^\n]*\n+/, '');
    changelog = { source: SRC.changelog, updatedAt: lastDate, entries: parseChangelog(raw) };
  } else {
    warn(`更新日志不存在：${SRC.changelog}`);
  }
  fs.mkdirSync(path.join(ROOT, 'src/generated/changelog'), { recursive: true });
  fs.writeFileSync(
    path.join(ROOT, 'src/generated/changelog', `${SRC.slug}.json`),
    JSON.stringify(changelog, null, 1),
  );

  // 5. logo
  fs.mkdirSync(path.join(ROOT, 'public/logos'), { recursive: true });
  let logo = null;
  for (const name of SRC.logo) {
    const abs = path.join(srcRoot, name);
    if (fs.existsSync(abs)) {
      logo = `${SRC.slug}${path.extname(name)}`;
      fs.copyFileSync(abs, path.join(ROOT, 'public/logos', logo));
      break;
    }
  }
  if (!logo) warn('未找到 logo.svg / logo.png / logo.ico');

  // 6. 设计原型
  const design = copyDesign(srcRoot);

  fs.writeFileSync(
    path.join(ROOT, 'src/generated/sync.json'),
    JSON.stringify(
      {
        mode: 'local',
        generatedAt: new Date().toISOString(),
        updatedAt: lastDate,
        docs: pages.length,
        changelog: changelog.entries.length,
        assets: assetFiles.length,
        design,
        logo,
        warnings,
      },
      null,
      2,
    ),
  );
  console.log(
    `  文档 ${pages.length} 页 · 日志 ${changelog.entries.length} 条 · 资源 ${assetFiles.length} 个 · 原型 ${design ? '已同步' : '未同步'} · 警告 ${warnings.length}`,
  );
}

main();
