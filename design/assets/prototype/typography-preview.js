/* Use the same document rules for miniature previews and exported pages. */
(function () {
  'use strict';
  var style = document.createElement('style');
  style.textContent = VexTypes.map(function (item) {
    var scope = '.type-swatch[data-preview-type="' + item.key + '"] .type-document';
    var live = ['.content-area .md-body', '.markdown-body'].map(function (selector) {
      var target = ':root[data-typography="' + item.key + '"] ' + selector;
      return VexTypeRules(item.key, false).replaceAll('BODY', target) +
        VexTypeRules(item.key, true).replaceAll('BODY', ':root[data-compact="true"][data-typography="' + item.key + '"] ' + selector);
    }).join('\n');
    return live + VexTypeRules(item.key, false).replaceAll('BODY', scope) +
      VexTypeRules(item.key, true).replaceAll('BODY', ':root[data-compact="true"] ' + scope);
  }).join('\n');
  document.head.appendChild(style);
  /* 卡片直接以该排版的真实样式渲染：## 排版名称 / 排版说明 / 引用（窄卡去掉 ### 行，避免卡片过高）。 */
  window.VexTypeCardPreview = function (key, item) {
    var label = item ? item.label : '';
    var note = item ? (item.note || item.description || '') : '';
    return '<span class="type-swatch" data-preview-type="' + key + '" aria-hidden="true"><span class="type-document"><h2>' + label + '</h2><p>' + note + '</p><blockquote><p>写下想法，看见脉络。</p></blockquote></span></span>';
  };
  window.VexTypePreview = function (key, readable) {
    return '<span class="type-swatch" data-preview-type="' + key + '"' + (readable ? '' : ' aria-hidden="true"') + '><span class="type-document"><h1>在文字之间，理清思路</h1><p class="type-lead">一篇文章，从一个想法开始。让结构清晰，也让阅读舒适。</p><h2>写作，让想法有迹可循</h2><p>先记录观察，再整理线索。标题、段落与留白共同构成阅读节奏。写作时，我们把零散的想法连成线；重新阅读时，再检查论述是否清楚，例子是否贴切。</p><p>不必一次写完。先用<strong>清晰的章节</strong>搭起结构，再补充事实、推敲表达。可以使用 <code>Markdown</code> 记录细节，让内容自然地生长。</p><blockquote><p>好的工具，让注意力回到内容本身。</p></blockquote><h3>今天的小计划</h3><ul><li>梳理章节结构，为每一节写下一句话。</li><li>补充具体的观察与例子，让观点有据可依。</li><li>保留必要的留白，完成后再从头读一遍。</li></ul><h2>用比较帮助判断</h2><p>不同的阅读任务需要不同的节奏。选择排版时，观察正文的密度、标题的分量，以及长段落是否容易跟读。</p><div class="md-table-scroll" tabindex="0" aria-label="表格"><table><thead><tr><th>内容</th><th>阅读重点</th></tr></thead><tbody><tr><td>随笔与专栏</td><td>段落呼吸、叙述节奏</td></tr><tr><td>技术与手册</td><td>结构清楚、信息易查</td></tr></tbody></table></div><pre><code># 记录想法\n\n先写下来，再慢慢整理。</code></pre><h4>重读时，留意细节</h4><p>文字的意义来自内容，排版帮助读者顺畅地抵达它。</p></span></span>';
  };
})();
