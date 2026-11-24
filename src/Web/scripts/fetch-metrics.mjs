/**
 * 构建时拉取实时指标：仓库 star 数、最新 Release（版本号 / 日期 / 下载入口）。
 * 任何失败都不阻塞构建（保留旧值），支持 GITHUB_TOKEN / GH_TOKEN 提高限额。
 * 产物：src/generated/metrics.json
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const OUT = path.join(ROOT, 'src/generated/metrics.json');
const CONTENT = JSON.parse(fs.readFileSync(path.join(ROOT, 'src/data/content.json'), 'utf8'));
const TOKEN = process.env.GITHUB_TOKEN || process.env.GH_TOKEN || '';
const headers = {
  Accept: 'application/vnd.github+json',
  'User-Agent': 'vex-site-build',
  ...(TOKEN ? { Authorization: `Bearer ${TOKEN}` } : {}),
};
const OLD = fs.existsSync(OUT) ? JSON.parse(fs.readFileSync(OUT, 'utf8')) : {};

async function fetchJson(url, timeoutMs = 8000) {
  const ctrl = new AbortController();
  const t = setTimeout(() => ctrl.abort(), timeoutMs);
  try {
    const res = await fetch(url, { headers, signal: ctrl.signal });
    if (!res.ok) return null;
    return await res.json();
  } catch {
    return null;
  } finally {
    clearTimeout(t);
  }
}

const repoOf = (url) => {
  const m = url.match(/github\.com[/:]([^/]+)\/([^/.]+)/);
  return m ? `${m[1]}/${m[2]}` : null;
};

async function main() {
  const repo = repoOf(CONTENT.source.repo);
  if (!repo) throw new Error('未能从 content.json 解析仓库地址');
  const [info, latest] = await Promise.all([
    fetchJson(`https://api.github.com/repos/${repo}`),
    fetchJson(`https://api.github.com/repos/${repo}/releases/latest`),
  ]);

  const out = {
    fetchedAt: new Date().toISOString(),
    repo,
    stars: info?.stargazers_count ?? OLD.stars ?? 0,
    release: latest?.tag_name
      ? {
          version: latest.tag_name.replace(/^v/i, ''),
          date: (latest.published_at || '').slice(0, 10) || null,
          url: latest.html_url,
          assets: (latest.assets ?? []).map((a) => ({ name: a.name, size: a.size, url: a.browser_download_url })),
        }
      : (OLD.release ?? null),
  };
  fs.mkdirSync(path.dirname(OUT), { recursive: true });
  fs.writeFileSync(OUT, JSON.stringify(out, null, 1));
  console.log(`指标完成：star ${out.stars} · 最新版本 ${out.release?.version ?? '—'} · 附件 ${out.release?.assets?.length ?? 0} 个`);
}

main().catch((e) => {
  console.error('指标拉取失败（不阻塞构建）：', e.message);
  fs.mkdirSync(path.dirname(OUT), { recursive: true });
  fs.writeFileSync(OUT, JSON.stringify({ fetchedAt: null, repo: '', stars: 0, release: null }, null, 1));
});
