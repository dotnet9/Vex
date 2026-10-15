const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { pathToFileURL } = require('node:url');

// 只复制交付 HTML 到空目录；所有交互必须在零外部运行依赖下完成。
module.exports = function standaloneFixture(source) {
  const parent = path.resolve(process.env.CODEWF_AUDIT_TEMP || os.tmpdir());
  fs.mkdirSync(parent, { recursive: true });
  const directory = fs.mkdtempSync(path.join(parent, 'codewf-prototype-'));
  const target = path.join(directory, 'index.html');
  fs.copyFileSync(source, target);
  const url = pathToFileURL(target).href;
  const dependencies = [];
  return {
    url,
    async attach(page) {
      await page.route('**/*', route => {
        const request = route.request();
        const address = request.url();
        if (address.startsWith('data:') || address.startsWith('blob:') ||
            (request.isNavigationRequest() && address.split(/[?#]/)[0] === url)) return route.continue();
        dependencies.push(address);
        return route.abort();
      });
    },
    async verify(page) {
      assert.deepEqual(dependencies, [], 'Single-file prototype requested another file or network resource');
      assert.deepEqual(await page.locator('img').evaluateAll(images => images.filter(image =>
        !image.complete || image.naturalWidth === 0).map(image => image.getAttribute('src').slice(0, 120))), [], 'Broken embedded images');
    },
    dispose() {
      if (path.dirname(directory) !== parent || !path.basename(directory).startsWith('codewf-prototype-'))
        throw new Error('Unexpected prototype fixture directory');
      fs.rmSync(directory, { recursive: true, force: true });
    },
  };
};
