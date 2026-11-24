import { renderMarkdown } from './markdown.mjs';

const VERSION_RE = /v?\d+(?:\.\d+)+(?:[-._][\w.]+)?/i;
const DATE_RE = /(20\d{2}[-/.]\d{1,2}[-/.]\d{1,2})|(20\d{2})\s*年\s*(\d{1,2})\s*月\s*(\d{1,2})\s*日?/;

function normalizeDate(raw) {
  if (!raw) return null;
  if (/年/.test(raw)) {
    const m = raw.match(/(20\d{2})\s*年\s*(\d{1,2})\s*月\s*(\d{1,2})/);
    if (m) return `${m[1]}-${String(m[2]).padStart(2, '0')}-${String(m[3]).padStart(2, '0')}`;
    return null;
  }
  const m = raw.match(/(20\d{2})[-/.](\d{1,2})[-/.](\d{1,2})/);
  if (m) return `${m[1]}-${String(m[2]).padStart(2, '0')}-${String(m[3]).padStart(2, '0')}`;
  return null;
}

const firstDigest = (text) => {
  for (const line of text.split(/\r?\n/)) {
    const t = line.replace(/^\s*(?:[-*+]\s+|\d+[.)]\s+|#{1,6}\s+)/, '').replace(/\*\*/g, '').trim();
    if (t) return t.slice(0, 120);
  }
  return '';
};

/**
 * 解析更新日志 Markdown 为条目数组。
 * 按 h1/h2 标题分节；从标题中提取版本号与日期；正文渲染为 HTML。
 * 若整个文件没有任何带版本的标题，则视为单条目（标题用文件名语义）。
 */
export function parseChangelog(md) {
  const lines = md.split(/\r?\n/);
  const sections = [];
  let current = null;
  for (const line of lines) {
    if (/^#{1,2}\s+/.test(line)) {
      current = { heading: line.replace(/^#{1,2}\s+/, '').trim(), body: [] };
      sections.push(current);
    } else if (current) {
      current.body.push(line);
    }
  }

  const entries = [];
  for (const s of sections) {
    const vMatch = s.heading.match(VERSION_RE);
    const dMatch = s.heading.match(DATE_RE);
    if (sections.length > 1 && !vMatch && !dMatch) continue; // 忽略「更新日志」这类无信息标题
    const body = s.body.join('\n');
    const { html } = renderMarkdown(body);
    entries.push({
      version: vMatch ? vMatch[0] : '',
      date: normalizeDate(dMatch ? dMatch[0] : null),
      title: s.heading,
      digest: firstDigest(body),
      html,
    });
  }

  if (entries.length === 0 && md.trim()) {
    const { html } = renderMarkdown(md);
    entries.push({ version: '', date: null, title: '更新日志', digest: firstDigest(md), html });
  }
  return entries;
}
