/**
 * 内联 SVG 图标路径表（24×24，stroke=currentColor，线性风格）。
 * 统一图形语言，避免 emoji 在不同平台渲染不一致。
 * 每个值是若干段 path 的 `d`，用 `|` 分隔。
 */
export const ICONS = {
  /* 视图 */
  edit: 'M4 20h4L18 10l-4-4L4 16v4z|M14 6l4 4',
  code: 'M9 18l-6-6 6-6|M15 6l6 6-6 6',
  book: 'M5 4h13v16H5z|M5 4a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2|M9 8h6|M9 12h6',
  mindmap: 'M5 12h4|M9 12V6h4|M9 12v6h4|M15 6h4|M15 18h4',
  /* 导图能力 */
  tree: 'M12 4v6|M6 20v-4h12v4|M6 16v-3h12v3|M12 10v3',
  cursor: 'M5 3l6 16 2.5-6.5L20 10z',
  search: 'M11 4a7 7 0 1 0 0 14 7 7 0 0 0 0-14z|M16 16l4 4',
  image: 'M3 5h18v14H3z|M8 11a1.5 1.5 0 1 0 0-3 1.5 1.5 0 0 0 0 3|M4 17l5-5 4 4 3-3 4 4',
  /* 功能 */
  sigma: 'M18 5H6l6 7-6 7h12',
  chart: 'M4 20V10|M10 20V4|M16 20v-7|M22 20H2',
  keyboard: 'M3 7h18v10H3z|M7 11h.01|M11 11h.01|M15 11h.01|M8 14h8',
  folder: 'M3 6h6l2 2h10v10H3z',
  palette: 'M12 3a9 9 0 1 0 0 18c1.5 0 2-1 2-2s-.6-2-1.5-2H14a2 2 0 0 1 0-4h3a4 4 0 0 0 4-4c0-3.3-4-6-9-6z|M7.5 10h.01|M10.5 7h.01|M14.5 7.5h.01',
  outbox: 'M12 16V4|M8 8l4-4 4 4|M4 16v3a1 1 0 0 0 1 1h14a1 1 0 0 0 1-1v-3',
  /* 文件与数据 */
  lock: 'M12 3l7 3v6c0 4.2-3 7.6-7 9-4-1.4-7-4.8-7-9V6z|M9.5 12l2 2 3.5-4',
  toggle: 'M7 8h10a4 4 0 0 1 0 8H7a4 4 0 0 1 0-8z|M7.2 12h.01',
  history: 'M4 12a8 8 0 1 0 3-6.2|M4 4v4h4|M12 8v4l3 2',
  /* 下载与平台 */
  download: 'M12 4v11|M8 11l4 4 4-4|M4 19h16',
  desktop: 'M3 5h18v11H3z|M9 20h6|M12 16v4',
  phone: 'M7 3h10v18H7z|M11 18h2',
  linux: 'M12 3c2 0 3 1.6 3 3.5 0 2.4 2.5 4 2.5 7 0 2.5-2 4.5-5.5 4.5S6.5 16 6.5 13.5c0-3 2.5-4.6 2.5-7C9 4.6 10 3 12 3z|M10 9h.01|M14 9h.01',
  /* 其他 */
  sparkle: 'M12 3l1.8 5.2L19 10l-5.2 1.8L12 17l-1.8-5.2L5 10l5.2-1.8z',
  check: 'M5 13l4 4 10-10',
  arrow: 'M5 12h14|M13 6l6 6-6 6',
  link: 'M14 4h6v6|M20 4l-8 8|M18 14v5a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V7a1 1 0 0 1 1-1h5',
  issue: 'M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18z|M12 8v5|M12 16h.01',
  terminal: 'M4 5h16v14H4z|M8 10l2.4 2.4L8 14.8|M13.5 15h3',
  layout: 'M3 4h18v16H3z|M3 9h18|M9 9v11',
} as const;

export type IconName = keyof typeof ICONS;
