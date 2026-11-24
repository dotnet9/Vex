import rss from '@astrojs/rss';
import { loadChangelog } from '../lib/content';
import { SITE } from '../config/site';

/** 站内 RSS：维刻 Vex 的版本更新条目 */
export function GET(context) {
  const cl = loadChangelog();
  const site = context.site ?? SITE.url;
  return rss({
    title: '维刻 Vex · 更新日志',
    description: '维刻 Vex 的版本更新记录。',
    site,
    items: cl.entries.map((e) => ({
      title: `${SITE.brand.name} ${e.version || e.title}`.trim(),
      description: e.digest || SITE.brand.desc,
      link: new URL('/changelog/', site).toString(),
      pubDate: e.date ? new Date(e.date) : undefined,
      categories: ['更新日志'],
    })),
    customData: '<language>zh-CN</language>',
  });
}
