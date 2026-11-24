import { Marked } from 'marked';
import hljs from 'highlight.js';
import GithubSlugger from 'github-slugger';

const escapeHtml = (s) =>
  String(s).replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

const stripInline = (s) =>
  String(s)
    .replace(/<[^>]*>/g, '')
    .replace(/!?\[([^\]]*)\]\([^)]*\)/g, '$1')
    .replace(/[*_`~]/g, '')
    .trim();

// 源文档归一化：剥 UTF-8 BOM；首行 --- 视为 front matter 候选，整块移除到闭合行——
// marked v18 没有 front matter 概念，不剥的话 YAML 会被渲染成 hr 加 setext 标题，
// 还会劫持 title/excerpt/TOC；候选块里没有任何 key: 行时（正文以分隔线开头）保持原样不吞内容。
const normalizeSource = (md) => {
  const text = String(md).replace(/^\uFEFF/, '');
  const lines = text.split('\n');
  if ((lines[0] ?? '').trim() !== '---') return text;
  let close = -1;
  for (let i = 1; i < lines.length; i++) {
    if (lines[i].trim() === '---') {
      close = i;
      break;
    }
  }
  if (close < 0) return text;
  const candidate = lines.slice(1, close);
  if (!candidate.some((line) => /^[A-Za-z][\w-]*\s*:/.test(line.trimStart()))) return text;
  return lines.slice(close + 1).join('\n');
};

/**
 * 渲染 Markdown 为 HTML，附带产物：
 * - 移除正文首个 h1（由页面模板负责展示标题）
 * - 收集 h2/h3 生成 TOC（与标题 id 一致，均由 GithubSlugger 生成）
 * - 代码块服务端高亮（highlight.js）
 * 适配 marked v18 的 token 式 renderer API。
 */
export function renderMarkdown(md) {
  md = normalizeSource(md);
  const slugger = new GithubSlugger();
  const toc = [];

  const renderer = {
    heading(token) {
      const text = this.parser.parseInline(token.tokens);
      const clean = stripInline(token.text);
      if (token.depth >= 2 && token.depth <= 3) {
        const id = slugger.slug(clean);
        toc.push({ level: token.depth, text: clean, id });
        return `<h${token.depth} id="${id}">${text}</h${token.depth}>\n`;
      }
      if (token.depth === 1 && !toc._title) toc._title = clean;
      return `<h${token.depth}>${text}</h${token.depth}>\n`;
    },
    code(token) {
      const lang = (token.lang || '').trim().split(/\s+/)[0].toLowerCase();
      const code = token.text.replace(/\n$/, '');
      let body;
      try {
        if (lang && hljs.getLanguage(lang)) {
          body = hljs.highlight(code, { language: lang, ignoreIllegals: true }).value;
        } else {
          body = escapeHtml(code);
        }
      } catch {
        body = escapeHtml(code);
      }
      const label = escapeHtml(lang || 'text');
      return `<div class="codeblock"><span class="lang">${label}</span><pre class="code"><code>${body}</code></pre></div>\n`;
    },
  };

  const marked = new Marked();
  marked.use({ gfm: true, renderer });
  const tokens = marked.lexer(md);

  let title = '';
  let excerpt = '';
  for (const tok of tokens) {
    if (!title && tok.type === 'heading') title = stripInline(tok.text);
    if (!excerpt && tok.type === 'paragraph') excerpt = stripInline(tok.text).slice(0, 150);
    if (title && excerpt) break;
  }
  if (!title && toc._title) title = toc._title;

  let html = marked.parse(md);
  // 去掉正文中的第一个 h1（title 已交给页面模板展示），避免标题重复；
  // 源文档常在 h1 之前放置 Logo 与徽章等原始 HTML 块，因此不要求 h1 位于最前
  html = html.replace(/[ \t]*<h1[^>]*>[\s\S]*?<\/h1>\s*/i, '');

  return { html, toc, title: title || '文档', excerpt };
}
