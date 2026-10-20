(function () {
  'use strict';

  var $ = function (selector) { return document.querySelector(selector); };
  var open = false, running = false, paused = false, automating = false, session = null;
  var pendingContinue = null;
  var stepIndex = -1, previousFocus = null, runToken = 0, timers = [];
  var focusTargets = [], focusKind = 'control', focusPreferred = 'bottom';
  var resultTargets = [], resultKind = 'control', resultPreferred = 'bottom';
  var totalGuideSteps = 9;
  var layer = document.createElement('div');
  layer.id = 'vex-guide'; layer.className = 'tour-layer story-tour'; layer.hidden = true;
  layer.innerHTML = '<svg class="tour-mask" aria-hidden="true"><defs><mask id="tour-cutouts" maskUnits="userSpaceOnUse"><rect width="100%" height="100%" fill="white"></rect><g class="tour-cutouts" fill="black"></g></mask></defs><rect class="tour-scrim" width="100%" height="100%" mask="url(#tour-cutouts)"></rect></svg><div class="tour-rings"></div><div class="tour-cursor" aria-hidden="true">↗</div><div class="tour-light-tip" role="status" aria-live="polite" aria-hidden="true" hidden></div><section class="tour-card story-card" role="dialog" aria-modal="false" aria-labelledby="tour-title" aria-describedby="tour-description"></section>';
  document.body.appendChild(layer);
  var card = $('.story-card'), cutouts = $('.tour-cutouts'), rings = $('.tour-rings'), cursor = $('.tour-cursor'), lightTip = $('.tour-light-tip');
  var app = function () { return $('.app-window'); };
  var guideSeenKey = 'vex-design-guide-seen';

  var stage0 = '# 成都，慢慢走\n\n这个周末，去成都。把脚步放慢一点，给茶香和街巷多留些时间。\n';
  var stage1 = '# 成都，慢慢走\n\n这个周末，去成都。先喝一碗盖碗茶，给茶香和街巷多留些时间。\n\n## 出发清单\n\n- [ ] 带上相机\n- [ ] 预订车票\n\n## 第一天行程\n\n### 人民公园\n\n坐在茶社里，喝一碗盖碗茶。\n\n### 宽窄巷子\n\n沿着青砖小巷，看看老院子。\n\n## 第二天行程\n\n### 熊猫基地\n\n早一点出发，去看大熊猫。\n\n## 锦里夜游\n\n等灯笼亮起来，再去锦里走走。\n\n### 旅途随手记\n\n记下今天最喜欢的一碗茶和一条巷子。\n';
  var stage1WithEnding = stage1 + '\n沿着这条路线，慢慢走。';
  var route = '\n## 成都慢游路线\n\n\`\`\`mermaid\nflowchart LR\n  A[人民公园] --> B[宽窄巷子]\n  B --> C[锦里夜游]\n\`\`\`\n';
  var stage2 = stage1WithEnding + route;
  var routeEdited = stage1WithEnding + route.replace('B[宽窄巷子]', 'B[宽窄巷子 · 喝茶漫步]');
  var acts = [
    {
      name: '第一幕', title: '即时模式', short: '直接输入，实时看到排版。',
      resultTitle: '即时模式', result: '直接输入，实时看到排版。',
      prepare: function () {
        setLayout('instant');
        withAutomation(function () { VexWorkbench.applyText('', { history: false }); });
        focusTargets = ['#instant-pane']; focusKind = 'area'; focusPreferred = 'top';
      },
      run: function (done) {
        pauseForContinue('在这里写下游记。', ['#instant-pane'], 'area', function () {
          cue('写下这趟旅行的标题。', ['#instant-pane']);
          typeText('# 成都，慢慢走\n\n', 28, function () {
            cue('内容会即时呈现。', ['#instant-pane']);
            typeText(stage0, 28, function () {
              pauseForContinue('内容已经写下，先读一遍刚才的结果。', ['#instant-pane'], 'area', done);
            });
          });
        });
      }
    },
    {
      name: '第二幕', title: '两种写法', short: '支持源码模式与即时模式',
      resultTitle: '两种写法', result: '支持源码模式与即时模式',
      prepare: function () { ensureText(stage0); setLayout('instant'); focusTargets = ['#workspace-layout']; focusKind = 'control'; focusPreferred = 'bottom'; },
      run: function (done) {
        ensureText(stage0); setLayout('instant');
        pauseForContinue('从顶部视图菜单选择源码编辑，也能打开多视图对照。', ['#workspace-layout'], 'control', function () {
          chooseLayout('source');
          cue('切到源码，刚才的文字都在这里。', ['#workspace-layout'], 'control');
          later(500, function () {
            cue('在源码里，也能继续修改。', ['#markdown-input'], 'text');
            transitionText(stage0, '# 成都，慢慢走\n\n这个周末，去成都。先喝一碗盖碗茶，给茶香和街巷多留些时间。\n', 26, function () {
              cue('加一份清单，再把行程写下来。', ['#markdown-input'], 'text');
              typeText(stage1, 8, function () {
                pauseForContinue('从同一入口选择即时编辑，继续直接写作。', ['#workspace-layout'], 'control', function () {
                chooseLayout('instant');
                cue('切回即时渲染，继续直接写一段。', ['#instant-pane'], 'area');
                later(450, function () {
                  typeText(stage1WithEnding, 34, function () {
                    pauseForContinue('同一篇游记，两种方式都能继续写。', ['#instant-pane'], 'area', done);
                  });
                });
              });
              });
            });
          });
        });
      }
    },
    {
      name: '第三幕', title: '大纲与导图', short: '定位章节，整理文章结构。',
      resultTitle: '大纲与导图', result: '定位章节，整理文章结构。',
      prepare: function () {
        ensureText(stage1WithEnding); setLayout('instant'); closeLayers();
        focusTargets = ['#outline-toggle']; focusKind = 'control'; focusPreferred = 'top';
      },
      run: function (done) {
        ensureText(stage1WithEnding); setLayout('instant');
        pauseForContinue('点击底栏“大纲”，展开右侧辅助面板。', ['#outline-toggle'], 'control', function () {
          withAutomation(function () { VexAuxiliary.show('outline', $('#outline-toggle'), false); });
          later(280, function () {
            pauseForContinue('大纲会列出标题，点击“第一天行程”定位内容。', ['#outline-body'], 'control', function () {
              clickOutline('第一天行程');
              later(280, function () {
                pauseForContinue('从顶部视图菜单选择思维导图，查看全局结构。', ['#workspace-layout'], 'control', function () {
                  withAutomation(function () { VexAuxiliary.close(false); }); chooseLayout('map');
                  later(420, function () {
                    pauseForContinue('导图把标题关系画在这里。', ['#map-viewport'], 'area', function () {
                      var source = chapterSelector('锦里夜游'), target = chapterSelector('第一天行程');
                      pauseForContinue('把锦里夜游安排到第一天晚上。', [source, target], 'control', function () {
                        later(650, function () {
                          var sourceNode = chapterNode('锦里夜游'), targetNode = chapterNode('第一天行程');
                          if (sourceNode && targetNode && window.VexChapters) {
                            withAutomation(function () { VexChapters.move(sourceNode, targetNode, 'child'); });
                          }
                          if ($('#node-inspector')) $('#node-inspector').hidden = true;
                          pauseForContinue('章节和下面的随手记，一起移动了。', [chapterSelector('锦里夜游'), chapterSelector('第一天行程')], 'control', function () {
                            withAutomation(function () { VexWorkbench.history('undo'); });
                            pauseForContinue('想比较一下，也可以撤销回来。', ['#map-viewport'], 'area', done);
                          });
                        });
                      });
                    });
                  });
                });
              });
            });
          });
        });
      }
    },
    {
      name: '第四幕', title: '导图布局', short: '切换布局和样式，查看全局。',
      resultTitle: '导图布局', result: '切换布局和样式，查看全局。',
      anchor: '#map-appearance-panel',
      prepare: function () { ensureText(stage1WithEnding); setLayout('map'); closeLayers(); focusTargets = ['.map-appearance-trigger:not([hidden])']; focusKind = 'control'; focusPreferred = 'bottom'; },
      run: function (done) {
        ensureText(stage1WithEnding); setLayout('map');
        pauseForContinue('点击右侧“导图样式”，看看布局。', ['.map-appearance-trigger:not([hidden])'], 'control', function () {
          click('.map-appearance-trigger:not([hidden])');
          later(260, function () {
            cycleMapLayouts(['right', 'balanced', 'radial', 'down'], function () {
              pauseForContinue('结构布局可以随时切换。', ['#map-appearance-panel [data-map-layout]'], 'control', function () {
                click('#map-tab-style');
                later(220, function () {
                  cycleMapStyles(['quiet', 'colorful', 'mono'], function () {
                    pauseForContinue('风格也可以一键切换。', ['#map-appearance-panel [data-map-style-choice]'], 'control', function () {
                      VexWorkspace.closeAppearance();
                      pauseForContinue('需要查看节点摘要时，打开这里。', ['#map-settings-button'], 'control', function () {
                        VexProto.handleAction('map-settings');
                        pauseForContinue('摘要已经显示在节点下方。', ['#map-settings'], 'control', function () {
                          VexWorkbench.fit();
                          pauseForContinue('拉远一点，把全局装进视野。', ['#map-viewport'], 'area', done);
                        });
                      });
                    });
                  });
                });
              });
            });
          });
        });
      }
    },
    {
      name: '第五幕', title: '正文排版', short: '调整标题、段落和列表。',
      resultTitle: '正文排版', result: '调整标题、段落和列表。',
      anchor: '#typography-panel',
      prepare: function () { ensureText(stage1WithEnding); setLayout('read'); closeLayers(); focusTargets = ['[data-action="typography"]:not([hidden])']; focusKind = 'control'; focusPreferred = 'bottom'; },
      run: function (done) {
        ensureText(stage1WithEnding); setLayout('read'); $('.reading-scroll').scrollTop = 0;
        pauseForContinue('点击“排版”，选择正文样式。', ['[data-action="typography"]:not([hidden])'], 'control', function () {
          var anchor = Array.from(document.querySelectorAll('[data-action="typography"]')).find(visible);
          VexTypography.toggle(anchor);
          later(220, function () {
            cycleTypography(['Simple', 'InkBlack', 'Column', 'WeChatFormat'], function () {
              pauseForContinue('排版会同步应用到阅读和导出。', ['#typography-panel [data-typography-key]'], 'control', function () {
                VexTypography.close();
                pauseForContinue('回到正文，继续打磨内容。', ['#markdown-preview'], 'area', done);
              });
            });
          });
        });
      }
    },
    {
      name: '第六幕', title: 'Mermaid 图表', short: '用文字生成路线图，修改内容同步更新。',
      resultTitle: 'Mermaid 图表', result: '用文字生成路线图，修改内容同步更新。',
      prepare: function () { ensureText(stage1WithEnding); setLayout('source'); focusTargets = ['#markdown-input']; focusPreferred = 'right'; },
      run: function (done) {
        ensureText(stage1WithEnding); setLayout('source');
        pauseForContinue('把几个想去的地方，连成一条路线。', ['#markdown-input'], 'text', function () {
          var head = stage1WithEnding + '\n## 成都慢游路线\n\n';
          typeText(head, 8, function () {
            // Keep the incomplete fence out of the demo: the chart appears only
            // once the Markdown block is complete, as it does in the app.
            withAutomation(function () { VexWorkbench.applyText(stage2, { history: false }); });
            setLayout('source-render');
            $('.source-scroll').scrollTop = $('.source-scroll').scrollHeight;
            $('.reading-scroll').scrollTop = $('.reading-scroll').scrollHeight;
            pauseForContinue('一段文字，变成了清楚的路线。', ['#markdown-preview'], 'area', function () {
              cue('给这一站，补上想做的事情。', ['#markdown-input']);
              transitionText(stage2, routeEdited, 20, function () {
                pauseForContinue('图里的这一站，也跟着更新了。', ['#markdown-preview'], 'area', done);
              });
            });
          });
        });
      }
    },
    {
      name: '第七幕', title: '演示模式', short: '从正文切到导图，按章节讲解。',
      resultTitle: '演示模式', result: '从正文切到导图，按章节讲解。',
      prepare: function () { ensureText(routeEdited); setLayout('read'); focusTargets = ['#markdown-preview']; focusPreferred = 'left'; },
      run: function (done) {
        ensureText(routeEdited); setLayout('read');
        pauseForContinue('这篇游记，也可以直接拿来讲。', ['#markdown-preview'], 'area', function () {
          withAutomation(function () { VexProto.handleAction('presentation'); });
          pauseForContinue('沿着章节，一段一段展开。', ['.vex-deck'], 'area', function () {
          click('.vex-deck [data-deck="next"]');
            pauseForContinue('想先讲全局，也可以从导图开始。', ['.vex-deck [data-deck="map"]'], 'control', function () {
          click('.vex-deck [data-deck="map"]');
              pauseForContinue('导图能先把全局讲清楚。', ['.vex-deck'], 'area', function () {
            click('.vex-deck [data-deck="exit"]');
                pauseForContinue('讲完以后，回到游记继续完善。', ['#markdown-preview'], 'area', done);
              });
            });
          });
        });
      }
    },
    {
      name: '第八幕', title: '保存与分享', short: '保存、导出或分享作品。',
      resultTitle: '保存与分享', result: '保存、导出或分享作品。', final: true,
      prepare: function () { ensureText(routeEdited); setLayout('read'); focusTargets = ['.tb-menuitem[data-menu^="文件"]']; focusPreferred = 'bottom'; },
      run: function (done) {
        ensureText(routeEdited); setLayout('read');
        pauseForContinue('想继续写，可以保存这份 Markdown。', ['.tb-menuitem[data-menu^="文件"]'], 'control', function () {
          openMenu('文件(F)');
          pauseForContinue('这里是保存入口。', ['.menu-flyout [data-menu-action="save"]'], 'control', function () {
            click('.menu-flyout [data-menu-label="导出"]');
            pauseForContinue('想交给别人阅读，也能选择合适的格式。', ['.submenu [data-menu-action="export-html"]', '.submenu [data-menu-action="export-pdf"]', '.submenu [data-menu-label="导出 PNG 长图"]', '.submenu [data-menu-label="导出 Word（.docx）"]'], 'control', function () {
              closeMenu(); openMenu('工具(T)');
              pauseForContinue('准备发布时，从这里选择对应的平台。', ['.menu-flyout [data-menu-action^="copy-"]'], 'control', function () {
                closeMenu();
                pauseForContinue('一篇游记已经成形。', ['#markdown-preview'], 'area', done);
              });
            });
          });
        });
      }
    }
  ];

  function visible(node) { return !!node && !node.closest('[hidden]') && node.getBoundingClientRect().width > 0 && node.getBoundingClientRect().height > 0; }
  function setLayout(id) { if (window.VexWorkspace) VexWorkspace.setLayout(id); }
  function chooseLayout(id) { if ($('#workspace-layout-menu').hidden) click('#workspace-layout'); click('#workspace-layout-menu [data-layout="' + id + '"]'); }
  function closeMenu() { if (window.VexProto) VexProto.closeMenu(); }
  function openMenu(menu) { var key = menu.replace(/\(.*/, ''); click('.tb-menuitem[data-menu^="' + key + '"]'); }
  function closeLayers() { closeMenu(); if (window.VexTypography) VexTypography.close(); if (window.VexWorkspace) VexWorkspace.closeAppearance(); document.querySelectorAll('.win-layer').forEach(function (node) { node.hidden = true; }); $('#map-settings').hidden = true; $('#map-settings-button').setAttribute('aria-expanded', 'false'); $('#node-inspector').hidden = true; }
  function withAutomation(fn) { automating = true; try { fn(); } finally { automating = false; } }
  function click(selector) { withAutomation(function () { var node = $(selector); if (node) node.click(); }); }
  function clickOutline(title) { withAutomation(function () { var node = Array.from(document.querySelectorAll('.outline-item')).find(function (item) { return item.textContent.trim() === title; }); if (node) node.click(); }); }
  function chapterNode(title) { return window.VexWorkbench && VexWorkbench.state && VexWorkbench.state.nodes ? VexWorkbench.state.nodes.find(function (node) { return node.kind === 'heading' && node.text === title; }) : null; }
  function chapterSelector(title) { var node = chapterNode(title); return node ? '.map-node[data-line="' + node.line + '"]' : '#map-viewport'; }
  function ensureText(text) { withAutomation(function () { if (VexWorkbench.text !== text) VexWorkbench.applyText(text, { history: false }); }); }
  function commonPrefix(a, b) { var i = 0; while (i < a.length && i < b.length && a[i] === b[i]) i++; return i; }
  function later(delay, fn) {
    var token = runToken, timer = setTimeout(function () { timers = timers.filter(function (item) { return item !== timer; }); if (open && token === runToken) fn(); }, delay);
    timers.push(timer); return timer;
  }
  function cancelTimers() { runToken++; timers.splice(0).forEach(clearTimeout); }
  function typeText(target, speed, done) {
    var current = VexWorkbench.text, token = runToken, prefix = commonPrefix(current, target), i = prefix;
    if (current.length > prefix) { withAutomation(function () { VexWorkbench.applyText(current.slice(0, prefix), { history: false }); }); }
    function next() {
      if (!open || token !== runToken) return;
      if (i >= target.length) { if (done) done(); return; }
      i += 1; withAutomation(function () { VexWorkbench.applyText(target.slice(0, i), { history: false }); });
      later(speed, next);
    }
    next();
  }
  function transitionText(from, to, speed, done) {
    ensureText(from);
    var prefix = commonPrefix(from, to), fromSuffix = from.length, toSuffix = to.length;
    while (fromSuffix > prefix && toSuffix > prefix && from[fromSuffix - 1] === to[toSuffix - 1]) { fromSuffix--; toSuffix--; }
    var token = runToken, middle = to.slice(prefix, toSuffix), i = 0;
    function next() {
      if (!open || token !== runToken) return;
      if (i > middle.length) { if (done) done(); return; }
      withAutomation(function () { VexWorkbench.applyText(to.slice(0, prefix) + middle.slice(0, i) + to.slice(toSuffix), { history: false }); });
      i += 1; later(speed, next);
    }
    next();
  }
  function cycleMapLayouts(keys, done, index) {
    index = index || 0;
    if (!open || !running || index >= keys.length) { if (done) done(); return; }
    var key = keys[index], selector = '#map-appearance-panel [data-map-layout="' + key + '"]', button = $(selector);
    if (!button) { cycleMapLayouts(keys, done, index + 1); return; }
    click(selector);
    cue('布局：' + button.textContent.trim(), [selector], 'control');
    later(1200, function () { cycleMapLayouts(keys, done, index + 1); });
  }
  function cycleMapStyles(keys, done, index) {
    index = index || 0;
    if (!open || !running || index >= keys.length) { if (done) done(); return; }
    var key = keys[index], selector = '#map-appearance-panel [data-map-style-choice="' + key + '"]', button = $(selector);
    if (!button) { cycleMapStyles(keys, done, index + 1); return; }
    click(selector);
    cue('风格：' + button.textContent.trim(), [selector], 'control');
    later(1200, function () { cycleMapStyles(keys, done, index + 1); });
  }
  function cycleTypography(keys, done, index) {
    index = index || 0;
    if (!open || !running || index >= keys.length) { if (done) done(); return; }
    var key = keys[index], selector = '#typography-panel [data-typography-key="' + key + '"]', button = $(selector);
    if (!button) { cycleTypography(keys, done, index + 1); return; }
    click(selector);
    cue('排版：' + button.querySelector('.typography-card-name').textContent, [selector], 'control');
    later(1200, function () { cycleTypography(keys, done, index + 1); });
  }
  function pauseForContinue(message, targets, kind, next) {
    if (!open || !running) return;
    cancelTimers();
    paused = true;
    pendingContinue = next;
    cue(message, targets, kind, { showCard: true });
    var button = $('.tour-next');
    if (button) {
      button.disabled = false;
      button.innerHTML = '继续 <span aria-hidden="true">→</span>';
    }
  }
  function continueGuide() {
    if (!open || !running || !paused || typeof pendingContinue !== 'function') return;
    var next = pendingContinue;
    pendingContinue = null;
    paused = false;
    var button = $('.tour-next');
    if (button) {
      button.disabled = true;
      button.innerHTML = '演示中… <span aria-hidden="true">→</span>';
    }
    next();
  }
  function hideLightTip() {
    lightTip.hidden = true;
    lightTip.setAttribute('aria-hidden', 'true');
    lightTip.textContent = '';
  }
  function storyMeta(title) {
    return '<div class="story-meta"><h2 id="tour-title" class="story-titlebar">' + title + '</h2><button class="tour-close" data-tour="close" aria-label="退出引导">×</button></div>';
  }
  function storyCount(count) {
    return '<span class="story-count action-count" aria-label="第 ' + count.replace('/', ' / ') + ' 步">' + count + '</span>';
  }
  function cue(message, targets, kind, options) {
    focusTargets = Array.isArray(targets) ? targets : [targets]; focusKind = kind || 'control';
    var showCard = !!(options && options.showCard);
    layer.dataset.mode = 'live';
    layer.classList.toggle('demo-running', !showCard);
    card.classList.toggle('demo-hidden', !showCard);
    if (showCard) card.removeAttribute('aria-hidden'); else card.setAttribute('aria-hidden', 'true');
    if (showCard) {
      hideLightTip();
    } else {
      lightTip.textContent = message;
      lightTip.hidden = false;
      lightTip.setAttribute('aria-hidden', 'false');
    }
    cursor.hidden = true;
    pointer.hidden = true;
    card.dataset.mode = 'live';
    card.innerHTML = storyMeta(acts[stepIndex].title) + '<p class="tour-feedback story-live" aria-live="polite">' + message + '</p><div class="story-actions"><button class="tour-skip" data-tour="skip">跳过本段</button>' + storyCount((stepIndex + 2) + '/' + totalGuideSteps) + (stepIndex ? '<button class="tour-previous" data-tour="previous">← 上一步</button>' : '') + '<button class="tour-next" data-tour="next">' + (paused ? '继续' : '演示中…') + ' <span aria-hidden="true">→</span></button></div>';
    compactCard('live');
    $('.tour-next').disabled = !paused; layout();
  }
  function renderIntro() {
    hideLightTip();
    layer.classList.remove('demo-running'); card.classList.remove('demo-hidden'); card.removeAttribute('aria-hidden');
    layer.dataset.mode = 'intro'; card.dataset.mode = 'intro'; focusTargets = []; cursor.hidden = true; layer.hidden = false;
    card.innerHTML = storyMeta('快速上手') + '<p class="tour-description" id="tour-description">运行软件即可立即创作哟，跟着演示看一遍核心功能。</p><div class="story-actions intro-actions">' + storyCount('1/' + totalGuideSteps) + '<button class="tour-skip" data-tour="close">先不看</button><button class="tour-next" data-tour="start">开始吧 <span aria-hidden="true">→</span></button></div>';
    compactCard('intro');
    centerCard();
  }
  function renderResult(skipped) {
    var act = acts[stepIndex], final = !!act.final;
    resultTargets = focusTargets.slice(); resultKind = focusKind; resultPreferred = focusPreferred;
    hideLightTip();
    layer.classList.remove('demo-running'); card.classList.remove('demo-hidden'); card.removeAttribute('aria-hidden');
    card.dataset.mode = 'result'; focusTargets = resultTargets.slice(); focusKind = resultKind; focusPreferred = resultPreferred; cursor.hidden = true; pointer.hidden = true; layer.dataset.mode = final ? 'final' : 'result';
    card.innerHTML = storyMeta(act.resultTitle) + '<p class="tour-description" id="tour-description">' + act.result + '</p><div class="story-actions">' + storyCount((final ? totalGuideSteps : stepIndex + 2) + '/' + totalGuideSteps) + (stepIndex ? '<button class="tour-previous" data-tour="previous">← 上一步</button>' : '') + '<button class="tour-next" data-tour="next">' + (final ? '开始自己的写作' : '下一步') + ' <span aria-hidden="true">→</span></button></div>';
    compactCard('result');
    layout();
  }
  function compactCard(mode) {
    var status = card.querySelector('.story-count');
    if (status && !status.textContent.trim()) status.remove();
    var resultMark = card.querySelector('.story-result-mark');
    if (resultMark) resultMark.remove();
    if (mode === 'live') {
      var liveDescription = card.querySelector('.tour-description');
      if (liveDescription) liveDescription.remove();
      return;
    }
    if (mode === 'intro') {
      var introTitle = card.querySelector('#tour-title');
      var introDescription = card.querySelector('.tour-description');
      var introCopy = card.querySelector('.story-intro-copy');
      if (introDescription) introDescription.textContent = '运行软件即可立即创作哟，跟着演示看一遍核心功能。';
      if (introCopy) introCopy.remove();
      return;
    }
    var nextNote = card.querySelector('.story-next-note');
    var replay = card.querySelector('[data-tour="replay"]');
    var previous = card.querySelector('[data-tour="previous"]');
    if (nextNote) nextNote.remove();
    if (replay) replay.remove();
    if (previous) previous.textContent = '← 上一步';
  }
  function centerCard() {
    var bounds = app().getBoundingClientRect(), width = Math.min(card.offsetWidth || 420, bounds.width - 32), height = card.offsetHeight || 230;
    card.style.left = Math.max(bounds.left + 16, bounds.left + (bounds.width - width) / 2) + 'px';
    card.style.top = Math.max(bounds.top + 16, bounds.top + (bounds.height - height) / 2) + 'px';
    pointer.hidden = true;
  }
  function placeLightTip(found, bounds) {
    if (lightTip.hidden || !found.length) return;
    var target = found[0], size = lightTip.getBoundingClientRect(), width = size.width || 240, height = size.height || 32;
    var x = target.x + (target.width - width) / 2;
    x = Math.max(bounds.left + 12, Math.min(x, bounds.right - width - 12));
    var y = target.y - height - 12;
    if (y < bounds.top + 12) y = target.y + target.height + 12;
    y = Math.max(bounds.top + 12, Math.min(y, bounds.bottom - height - 12));
    lightTip.style.left = Math.round(x) + 'px'; lightTip.style.top = Math.round(y) + 'px';
  }
  function finishAct(skipped) { if (!open) return; cancelTimers(); running = false; paused = false; pendingContinue = null; closeLayers(); if (acts[stepIndex].final) renderResult(skipped); else startAct(stepIndex + 1); }
  function baselineFor(index) { return [stage0, stage0, stage1WithEnding, stage1WithEnding, stage1WithEnding, stage1WithEnding, routeEdited, routeEdited][index] || stage0; }
  function startAct(index) {
    if (!open) return;
    cancelTimers(); running = true; paused = false; pendingContinue = null; stepIndex = Math.max(0, Math.min(acts.length - 1, index)); layer.dataset.step = String(stepIndex + 1); if (app()) app().dataset.guideStep = String(stepIndex + 1);
    closeLayers(); acts[stepIndex].prepare();
    later(260, function () { acts[stepIndex].run(function () { finishAct(false); }); });
  }
  function skipAct() {
    if (!running) return;
    if (acts[stepIndex].final) { finishAct(true); return; }
    cancelTimers(); running = false; paused = false; pendingContinue = null; withAutomation(function () { VexWorkbench.applyText(baselineFor(stepIndex), { history: false }); }); closeLayers(); if (acts[stepIndex].final) renderResult(true); else startAct(stepIndex + 1);
  }
  function startStory() {
    if (!open || session) return;
    previousFocus = document.activeElement; session = VexProto.beginGuideSession(); running = true; startAct(0);
  }
  function close() {
    if (!open) return;
    cancelTimers(); running = false; paused = false; pendingContinue = null; open = false; hideLightTip(); layer.classList.remove('demo-running'); card.classList.remove('demo-hidden'); card.removeAttribute('aria-hidden'); layer.hidden = true; cursor.hidden = true; pointer.hidden = true; if (app()) delete app().dataset.guideStep; closeLayers();
    var snapshot = session; session = null; if (snapshot) VexProto.endGuideSession(snapshot);
    var target = previousFocus && previousFocus.isConnected && visible(previousFocus) ? previousFocus : $('#workspace-layout');
    if (target) target.focus({ preventScroll: true });
  }
  function start(options) {
    options = options || {};
    if (open) return;
    if (options.automatic && window.VexStorage && VexStorage.getItem(guideSeenKey) === '1') return;
    if (window.VexStorage) VexStorage.setItem(guideSeenKey, '1');
    open = true; previousFocus = document.activeElement; layer.hidden = false; renderIntro(); schedule();
  }

  // Overlay geometry intentionally follows the real controls. The same mask
  // is used for menus, editors, the map and result cards, so the pointer never
  // drifts when the workbench reflows.
  var pointer = document.createElement('div'); pointer.className = 'vex-tour-pointer'; pointer.setAttribute('aria-hidden', 'true'); layer.appendChild(pointer);
  function rects() {
    var bounds = app().getBoundingClientRect(), found = [];
    focusTargets.forEach(function (selector) {
      var nodes = selector === '@chapters' ? Array.from(document.querySelectorAll('.map-node')) : selector === '@shares' ? Array.from(document.querySelectorAll('.menu-flyout [data-menu-action^=\"copy-\"]')) : Array.from(document.querySelectorAll(selector));
      nodes.forEach(function (node) {
        if (!visible(node)) return; var r = node.getBoundingClientRect();
        var x = Math.max(bounds.left + 4, r.left - 5), y = Math.max(bounds.top + 4, r.top - 5), right = Math.min(bounds.right - 4, r.right + 5), bottom = Math.min(bounds.bottom - 4, r.bottom + 5);
        if (right > x && bottom > y) found.push({ x: x, y: y, width: right - x, height: bottom - y, kind: focusKind });
      });
    });
    return found;
  }
  function draw(found) {
    cutouts.replaceChildren(); rings.replaceChildren();
    found.forEach(function (rect) {
      var hole = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
      [['x', rect.x], ['y', rect.y], ['width', rect.width], ['height', rect.height], ['rx', focusKind === 'text' ? 2 : 7]].forEach(function (pair) { hole.setAttribute(pair[0], pair[1]); });
      cutouts.appendChild(hole);
      var ring = document.createElement('div'); ring.className = 'tour-focus'; ring.dataset.kind = focusKind;
      Object.assign(ring.style, { left: rect.x + 'px', top: rect.y + 'px', width: rect.width + 'px', height: rect.height + 'px' }); rings.appendChild(ring);
    });
    var target = found[0];
    if (!target || card.dataset.mode !== 'live' || layer.classList.contains('demo-running') || focusKind === 'control') { cursor.hidden = true; return; }
    cursor.hidden = false; cursor.style.left = (target.x + target.width / 2 - 9) + 'px'; cursor.style.top = (target.y + target.height / 2 - 9) + 'px';
  }
  function layout() {
    if (!open) return;
    var mode = layer.dataset.mode;
    if (mode === 'intro') { hideLightTip(); centerCard(); return; }
    var bounds = app().getBoundingClientRect(), found = rects();
    draw(found);
    if (mode === 'final') { hideLightTip(); centerCard(); return; }
    if (mode === 'result') {
      hideLightTip();
      if (!found.length) { centerCard(); return; }
      var result = found[0]; VexTourPlacement.attach(card, pointer, result, bounds, resultPreferred, found.slice(1)); pointer.hidden = true; return;
    }
    if (!found.length) { if (!card.classList.contains('demo-hidden')) centerCard(); return; }
    if (card.classList.contains('demo-hidden')) {
      pointer.hidden = true;
      placeLightTip(found, bounds);
      return;
    }
    hideLightTip();
    var target = found[0]; VexTourPlacement.attach(card, pointer, target, bounds, focusPreferred, found.slice(1)); pointer.hidden = false;
  }
  function schedule() { if (!open) return; requestAnimationFrame(function () { layout(); if (open) schedule(); }); }

  card.addEventListener('click', function (event) {
    var button = event.target.closest('[data-tour]'); if (!button) return;
    event.stopPropagation(); var action = button.dataset.tour;
    if (action === 'close') close();
    else if (action === 'start') startStory();
    else if (action === 'skip') skipAct();
    else if (action === 'replay') startAct(stepIndex);
    else if (action === 'previous') startAct(stepIndex - 1);
    else if (action === 'next' && running && paused) continueGuide();
    else if (action === 'next' && !running) { if (acts[stepIndex].final) close(); else startAct(stepIndex + 1); }
  });
  ['pointerdown', 'click', 'dblclick', 'contextmenu'].forEach(function (eventName) {
    document.addEventListener(eventName, function (event) {
      if (!open || automating || card.contains(event.target)) return;
      event.preventDefault(); event.stopImmediatePropagation();
    }, true);
  });
  document.addEventListener('keydown', function (event) {
    if (!open || event.isComposing) return;
    if (event.key === 'Escape') { event.preventDefault(); event.stopImmediatePropagation(); close(); return; }
    if (event.key === 'Tab') { event.preventDefault(); event.stopImmediatePropagation(); $('.tour-next')?.focus({ preventScroll: true }); }
  }, true);
  window.addEventListener('resize', layout);
  new MutationObserver(layout).observe(app(), { subtree: true, childList: true, attributes: true, attributeFilter: ['hidden', 'style', 'class', 'data-active'] });
  window.VexGuide = { start: start, close: close, allows: function (target) { return !open || automating || card.contains(target); }, get active() { return open; } };
  // 写作不被自动弹窗打断；只有评审场景自动打开，平时由常驻入口重看。
  if (new URLSearchParams(location.search).get('scenario') === 'guide') setTimeout(function () { start({ automatic: true }); }, 300);
})();
