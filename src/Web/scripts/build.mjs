/**
 * 构建编排：同步内容 → 拉取指标 → astro build → pagefind 索引。
 *
 *   npm run build            全流程（本地模式同步）
 *   node scripts/build.mjs --no-sync   跳过同步（CI 已用 --ci 同步过）
 */
import { spawnSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const NO_SYNC = process.argv.includes('--no-sync');

const run = (label, cmd, args, { optional = false } = {}) => {
  console.log(`\n==> ${label}`);
  const r = spawnSync(cmd, args, { stdio: 'inherit', cwd: ROOT, shell: process.platform === 'win32' });
  if (r.status !== 0) {
    if (optional) {
      console.warn(`[跳过] ${label} 失败（不影响构建产物）`);
      return false;
    }
    process.exit(r.status ?? 1);
  }
  return true;
};

if (!NO_SYNC) run('同步 Vex 内容（本地模式）', 'node', ['scripts/sync-content.mjs']);
run('拉取实时指标', 'node', ['scripts/fetch-metrics.mjs'], { optional: true });
run('Astro 构建', 'node', [path.join('node_modules', 'astro', 'astro.js'), 'build']);

const pf = path.join(ROOT, 'node_modules', 'pagefind', 'lib', 'runner', 'bin.cjs');
if (fs.existsSync(pf)) {
  run('Pagefind 索引', 'node', [pf, '--site', 'dist']);
} else {
  console.warn('\n[提示] 未安装 pagefind，线上搜索索引将在 CI 中生成。');
}
console.log('\n构建完成 ✓');
