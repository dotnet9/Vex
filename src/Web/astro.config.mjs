import { defineConfig } from 'astro/config';
import sitemap from '@astrojs/sitemap';

// https://astro.build/config
export default defineConfig({
  site: 'https://vex.codewf.com',
  integrations: [sitemap()],
  build: {
    format: 'directory',
  },
});
