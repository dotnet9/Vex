(function () {
  'use strict';

  var $ = function (selector) { return document.querySelector(selector); };
  var open = false, running = false, paused = false, automating = false, session = null;
  var pendingContinue = null, gate = null;
  var stepIndex = -1, previousFocus = null, runToken = 0, timers = [];
  var focusTargets = [], focusKind = 'control', focusPreferred = 'bottom';
  var resultTargets = [], resultKind = 'control', resultPreferred = 'bottom';
  var layer = document.createElement('div');
  layer.id = 'vex-guide'; layer.className = 'tour-layer story-tour'; layer.hidden = true;
  layer.innerHTML = '<svg class="tour-mask" aria-hidden="true"><defs><mask id="tour-cutouts" maskUnits="userSpaceOnUse"><rect width="100%" height="100%" fill="white"></rect><g class="tour-cutouts" fill="black"></g></mask></defs><rect class="tour-scrim" width="100%" height="100%" mask="url(#tour-cutouts)"></rect></svg><div class="tour-rings"></div><div class="tour-cursor" aria-hidden="true">↗</div><div class="tour-light-tip" role="status" aria-live="polite" aria-hidden="true" hidden></div><section class="tour-card story-card" role="dialog" aria-modal="false" aria-labelledby="tour-title" aria-describedby="tour-description"></section>';
  document.body.appendChild(layer);
  var card = $('.story-card'), cutouts = $('.tour-cutouts'), rings = $('.tour-rings'), cursor = $('.tour-cursor'), lightTip = $('.tour-light-tip');
  var app = function () { return $('.app-window'); };
  var guideSeenKey = 'vex-design-guide-seen';

  // 引导文案统一进 VEX_DATA（Guide* 键，四语言），zh 兜底，保证离线可跑。
  function g(key, zh) {
    try {
      var culture = window.VexL && window.VexL.culture ? window.VexL.culture : 'zh-CN';
      var value = window.VEX_DATA.languages[culture]['Guide' + key];
      if (typeof value === 'string' && value) return value;
    } catch (error) { /* 字典缺失时退回中文 */ }
    return zh;
  }
  function actTitle(act) { return g(act.titleKey, act.titleFallback); }
  function actSummary(act) { return g(act.summaryKey, act.summaryFallback); }

  var stage0 = '# 成都，慢慢走\n\n这个周末，去成都。把脚步放慢一点，给茶香和街巷多留些时间。\n';
  var stage1 = '# 成都，慢慢走\n\n这个周末，去成都。先喝一碗盖碗茶，给茶香和街巷多留些时间。\n\n## 出发清单\n\n- [ ] 带上相机\n- [ ] 预订车票\n\n## 第一天行程\n\n### 人民公园\n\n坐在茶社里，喝一碗盖碗茶。\n\n### 宽窄巷子\n\n沿着青砖小巷，看看老院子。\n\n## 第二天行程\n\n### 熊猫基地\n\n早一点出发，去看大熊猫。\n\n## 锦里夜游\n\n等灯笼亮起来，再去锦里走走。\n\n### 旅途随手记\n\n记下今天最喜欢的一碗茶和一条巷子。\n';
  var stage1WithEnding = stage1 + '\n沿着这条路线，慢慢走。';
  var route = '\n## 成都慢游路线\n\n\`\`\`mermaid\nflowchart LR\n  A[人民公园] --> B[宽窄巷子]\n  B --> C[锦里夜游]\n\`\`\`\n';
  var stage2 = stage1WithEnding + route;
  var routeEdited = stage1WithEnding + route.replace('B[宽窄巷子]', 'B[宽窄巷子 · 喝茶漫步]');

  /* 六幕：写作过程程序演示，关键入口由用户亲手点（门）。 */
  var acts = [
    {
      titleKey: 'Title1', titleFallback: '即时与源码',
      summaryKey: 'Act1Summary', summaryFallback: '同一篇游记，两种方式都能继续写。',
      prepare: function () {
        setLayout('instant');
        withAutomation(function () { VexWorkbench.applyText('', { history: false }); });
        focusTargets = ['#instant-pane']; focusKind = 'area'; focusPreferred = 'top';
      },
      run: function (done) {
        typeText('# 成都，慢慢走\n\n', 28, function () {
          pauseForContinue(g('Act1Step1', '写下这趟旅行的标题，内容会即时呈现。'), ['#instant-pane'], 'area', function () {
            typeText(stage0, 28, function () {
              pauseForContinue(g('Act1Step2', '内容已经写下，先读一遍刚才的结果。'), ['#instant-pane'], 'area', function () {
                waitForClick(['.workspace-viewswitch [data-view-link="source"]'], g('GateLayoutSource', '点标签行的「源码」，直接切换视图'), function () {
                  later(350, function () {
                    transitionText(stage0, stage1, 10, function () {
                      pauseForContinue(g('Act1Step3', '刚才的文字都在这里——源码里也能继续修改。'), ['#markdown-input'], 'text', function () {
                        waitForClick(['#workspace-layout'], g('GateLayoutInstant', '点「更多」，即时编辑和组合视图都在里面'), function () {
                          later(260, function () {
                            waitForClick(['#workspace-layout-menu [data-layout="instant"]'], g('GateLayoutInstant', '再切换回「即时编辑」'), { scope: '#workspace-layout-menu' }, function () {
                              typeText(stage1WithEnding, 24, done);
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
        });
      }
    },
    {
      titleKey: 'Title2', titleFallback: '大纲与导图',
      summaryKey: 'Act2Summary', summaryFallback: '章节与全局，随时切换视角。',
      prepare: function () {
        ensureText(stage1WithEnding); setLayout('instant'); ensureOutline(); closeLayers();
        focusTargets = ['#auxiliary-outline', '#outline-body']; focusKind = 'control'; focusPreferred = 'left';
      },
      run: function (done) {
        ensureText(stage1WithEnding); setLayout('instant'); ensureOutline();
        later(280, function () {
          pauseForContinue(g('Act2Step1', '大纲会列出所有标题。'), ['#auxiliary-outline', '#outline-body'], 'control', function () {
              clickOutline('第一天行程');
              later(420, function () {
                pauseForContinue(g('Act2Step2', '大纲点一下，正文就到位。'), ['#instant-pane'], 'area', function () {
                  waitForClick(['.workspace-viewswitch [data-view-link="map"]'], g('GateLayoutMap', '点标签行的「导图」，把章节画成图'), function () {
                    later(450, function () {
                      cue(g('Act2Step3', '导图把标题关系画在这里。'), ['#map-viewport'], 'area');
                      later(1500, function () {
                        pauseForContinue(g('Act2Step4', '点击节点可看全文，摘要入口在「工具」菜单。'), ['#map-viewport'], 'area', done);
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
      titleKey: 'Title3', titleFallback: '正文排版',
      summaryKey: 'Act3Summary', summaryFallback: '排版会同步应用到阅读和导出。',
      anchor: '#typography-panel',
      prepare: function () { ensureText(stage1WithEnding); setLayout('read'); closeLayers(); focusTargets = ['[data-action="typography"]:not([hidden])']; focusKind = 'control'; focusPreferred = 'bottom'; },
      run: function (done) {
        ensureText(stage1WithEnding); setLayout('read'); $('.reading-scroll').scrollTop = 0;
        waitForClick(['[data-action="typography"]:not([hidden])'], g('GateTypography', '点「排版」，给文字选一套样式'), function () {
          later(300, function () {
            cycleTypography(['Simple', 'InkBlack', 'GeekBlack', 'WeChatFormat'], function () {
              VexTypography.close();
              /* 外观必须真的展开一次：只高亮折起的摘要，用户看不到里面的控件。 */
              waitForClick(['#vex-appearance-menu > summary'], g('GateAppearance', '点标题栏的「外观」，展开配色与界面密度'), function () {
                later(320, function () {
                  pauseForContinue(g('Act3Appearance', '这里调整界面密度、配色。'), ['#vex-appearance-menu .appearance-choices'], 'control', function () {
                    withAutomation(function () { var menu = document.getElementById('vex-appearance-menu'); if (menu) menu.open = false; });
                    done();
                  });
                });
              });
            });
          });
        });
      }
    },
    {
      titleKey: 'Title4', titleFallback: 'Mermaid 图表',
      summaryKey: 'Act4Summary', summaryFallback: '想加图表时，一段文字就够。',
      prepare: function () { ensureText(stage1WithEnding); setLayout('source'); focusTargets = ['#markdown-input']; focusPreferred = 'right'; },
      run: function (done) {
        ensureText(stage1WithEnding); setLayout('source');
        pauseForContinue(g('Act4Step1', '接下来的图表，完全用文字写成。'), ['#markdown-input'], 'text', function () {
          typeText(stage1WithEnding + '\n## 成都慢游路线\n\n', 8, function () {
            // 不完整的围栏不进演示：图表只在 Markdown 块完整后出现，与桌面端一致。
            withAutomation(function () { VexWorkbench.applyText(stage2, { history: false }); });
            setLayout('source-render');
            $('.source-scroll').scrollTop = $('.source-scroll').scrollHeight;
            $('.reading-scroll').scrollTop = $('.reading-scroll').scrollHeight;
            /* Mermaid 异步出图：等 SVG 真正落到预览里再停顿讲解，避免只看到源码。 */
            var tries = 0;
            (function waitDiagram() {
              if (!open || !running) return;
              var drawn = document.querySelector('#markdown-preview .md-diagram svg');
              if (!drawn && tries++ < 24) { later(160, waitDiagram); return; }
              $('.reading-scroll').scrollTop = $('.reading-scroll').scrollHeight;
              pauseForContinue(g('Act4Step2', '一段文字，变成了清楚的路线。'), ['#markdown-preview .md-diagram'], 'area', function () {
                cue(g('Act4Step3', '图里的这一站，也跟着更新了。'), ['#markdown-input'], 'text');
                transitionText(stage2, routeEdited, 20, function () {
                  pauseForContinue(g('Act4Step3', '图里的这一站，也跟着更新了。'), ['#markdown-preview .md-diagram'], 'area', done);
                });
              });
            })();
          });
        });
      }
    },
    {
      titleKey: 'Title5', titleFallback: '演示模式',
      summaryKey: 'Act5Summary', summaryFallback: '讲完以后，回到游记继续完善。',
      prepare: function () { ensureText(routeEdited); setLayout('read'); focusTargets = ['#markdown-preview']; focusPreferred = 'left'; },
      run: function (done) {
        ensureText(routeEdited); setLayout('read');
        pauseForContinue(g('Act5Step1', '这篇游记，也可以直接拿来讲。'), ['#markdown-preview'], 'area', function () {
          withAutomation(function () { VexProto.handleAction('presentation'); });
          /* 同一目标的连续讲解合并为一次停顿，中间步骤用轻提示自动推进（不再连弹卡片）。 */
          cue(g('Act5Step2', '沿着章节，一段一段展开。'), ['.vex-deck'], 'area');
          later(1800, function () {
            click('.vex-deck [data-deck="next"]');
            cue(g('Act5Step3', '想先讲全局，也可以从导图开始。'), ['.vex-deck [data-deck="map"]'], 'control');
            later(1800, function () {
              click('.vex-deck [data-deck="map"]');
              cue(g('Act5Summary', '讲完以后，回到游记继续完善。'), ['.vex-deck'], 'area');
              later(1700, function () {
                click('.vex-deck [data-deck="exit"]');
                later(420, done);
              });
            });
          });
        });
      }
    },
    {
      titleKey: 'Title6', titleFallback: '保存与分享',
      summaryKey: 'Act6Summary', summaryFallback: '一篇游记已经成形。', final: true,
      prepare: function () { ensureText(routeEdited); setLayout('read'); focusTargets = ['.tb-menuitem[data-menu^="文件"]']; focusPreferred = 'bottom'; },
      run: function (done) {
        ensureText(routeEdited); setLayout('read');
        /* 菜单里需要高亮的几个按钮一起高亮（加粗高亮环已可读）。 */
        pauseForContinue(g('Act6Step1', '想继续写，可以保存这份 Markdown。'), ['.tb-menuitem[data-menu^="文件"]'], 'control', function () {
          openMenu('文件(F)');
          cue(g('Act6Menu', '保存、导出与发布平台，都在这个菜单里。'), ['.menu-flyout'], 'control');
          later(2000, function () {
            click('.menu-flyout [data-menu-label="导出"]');
            pauseForContinue(g('Act6Step2', '想交给别人阅读，有现成的格式。'), ['.menu-flyout .submenu'], 'control', function () {
              closeMenu(); openMenu('文件(F)');
              cue(g('Act6Step3', '准备发布时，从这里选择对应的平台。'), ['.menu-flyout [data-menu-label="复制到微信公众号"]'], 'control');
              later(2000, function () {
                closeMenu();
                pauseForContinue(g('Act6Summary', '一篇游记已经成形。'), ['#markdown-preview'], 'area', done);
              });
            });
          });
        });
      }
    }
  ];
  var totalGuideSteps = acts.length + 1;

  function visible(node) { return !!node && !node.closest('[hidden]') && node.getBoundingClientRect().width > 0 && node.getBoundingClientRect().height > 0; }
  function setLayout(id) { if (window.VexWorkspace) VexWorkspace.setLayout(id); }
  function chooseLayout(id) { if ($('#workspace-layout-menu').hidden) click('#workspace-layout'); click('#workspace-layout-menu [data-layout="' + id + '"]'); }
  function closeMenu() { if (window.VexProto) VexProto.closeMenu(); }
  function openMenu(menu) { var key = menu.replace(/\(.*/, ''); click('.tb-menuitem[data-menu^="' + key + '"]'); }
  /* 按展开状态设置，避免开启动画期间重复点击开关把大纲收回。 */
  function ensureOutline() {
    withAutomation(function () { VexWorkbench.showStructure(true); });
  }
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
  function cycleTypography(keys, done, index) {
    index = index || 0;
    if (!open || !running || index >= keys.length) { if (done) done(); return; }
    var key = keys[index], selector = '#typography-panel [data-typography-key="' + key + '"]', button = $(selector);
    if (!button) { cycleTypography(keys, done, index + 1); return; }
    click(selector);
    /* 卡片已无独立标题行：名称从 aria-label 前缀读取（"名称，应用此正文排版"）。 */
    var label = (button.getAttribute('aria-label') || '').split('，')[0];
    cue('排版：' + label, [selector], 'control');
    later(1200, function () { cycleTypography(keys, done, index + 1); });
  }
  function pauseForContinue(message, targets, kind, next) {
    if (!open || !running) return;
    cancelTimers();
    paused = true;
    pendingContinue = next;
    cue(message, targets, kind, { showCard: true });
  }
  /* 门：等待用户亲手点目标；「继续」= 程序代点；两级门内点错项给纠正提示。 */
  function waitForClick(allowed, message, options, done) {
    if (typeof options === 'function') { done = options; options = {}; }
    if (!open || !running) return;
    cancelTimers();
    paused = false; pendingContinue = null;
    options = options || {};
    gate = { allowed: Array.isArray(allowed) ? allowed : [allowed], scope: options.scope || '', kind: options.kind || 'control', done: done };
    cue(message, allowed, gate.kind, { showCard: true });
  }
  function completeGate() {
    if (!gate) return;
    var done = gate.done;
    gate = null;
    later(120, done);
  }
  function proxyGate() {
    if (!gate) return;
    var selector = gate.allowed[0];
    withAutomation(function () { var node = document.querySelector(selector); if (node) node.click(); });
    later(120, completeGate);
  }
  function correctGate() {
    if (!gate) return;
    var expected = '';
    var node = document.querySelector(gate.allowed[0]);
    if (node) {
      /* 布局卡片的 textContent 含说明与勾选符号，纠正文案只取按钮主标题。 */
      var label = node.querySelector('strong');
      expected = (label ? label.textContent : node.getAttribute('aria-label') || node.textContent || '').trim();
    }
    /* 「继续」按钮由 cue() 依据 gate 状态直接渲染，无需额外更新按钮。 */
    cue(g('GateWrongFormat', '这次选「{0}」').replace('{0}', expected), gate.allowed, gate.kind, { showCard: true });
  }
  function gateAllows(target) {
    if (!gate) return false;
    return gate.allowed.some(function (selector) {
      var nodes = document.querySelectorAll(selector);
      for (var i = 0; i < nodes.length; i++) {
        if (nodes[i] === target || nodes[i].contains(target)) return true;
      }
      return false;
    });
  }
  function gateWrong(target) {
    if (!gate || !gate.scope) return false;
    var scope = document.querySelector(gate.scope);
    return !!scope && (scope === target || scope.contains(target));
  }
  function continueGuide() {
    if (!open || !running || !paused || typeof pendingContinue !== 'function') return;
    var next = pendingContinue;
    pendingContinue = null;
    paused = false;
    next();
  }
  function hideLightTip() {
    lightTip.hidden = true;
    lightTip.setAttribute('aria-hidden', 'true');
    lightTip.textContent = '';
  }
  function storyMeta(title) {
    return '<div class="story-meta"><h2 id="tour-title" class="story-titlebar">' + VexEsc(title) + '</h2><button class="tour-close" data-tour="close" aria-label="' + VexEsc(g('IntroSkip', '退出引导')) + '">×</button></div>';
  }
  function storyCount(count) {
    return '<span class="story-count action-count" aria-label="第 ' + count.replace('/', ' / ') + ' 步">' + count + '</span>';
  }
  /* 点状进度：每幕一颗，当前幕加长高亮（参考评审稿）。 */
  function storyDots() {
    var out = '';
    for (var i = 0; i < acts.length; i++) {
      out += '<i class="story-dot' + (i === stepIndex ? ' current' : i < stepIndex ? ' passed' : '') + '"></i>';
    }
    return out;
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
    /* 动作行：点状进度居左；上一步 / 继续（暂停或门）/ 下一步居右，均无箭头、随时可跳幕。 */
    var resume = paused ? '<button class="tour-continue tour-next" data-tour="continue">继续</button>'
      : gate ? '<button class="tour-continue tour-next" data-tour="proxy">继续</button>' : '';
    card.innerHTML = storyMeta(actTitle(acts[stepIndex])) + '<p class="tour-feedback story-live" aria-live="polite">' + VexEsc(message) + '</p><div class="story-actions"><span class="story-dots">' + storyDots() + '</span><span class="story-flex"></span>'
      + (stepIndex ? '<button class="tour-previous" data-tour="previous">上一步</button>' : '')
      + resume
      + '<button class="tour-skip" data-tour="skip">下一步</button></div>';
    compactCard('live');
    layout();
  }
  function renderIntro() {
    hideLightTip();
    layer.classList.remove('demo-running'); card.classList.remove('demo-hidden'); card.removeAttribute('aria-hidden');
    layer.dataset.mode = 'intro'; card.dataset.mode = 'intro'; focusTargets = []; cursor.hidden = true; layer.hidden = false;
    card.innerHTML = storyMeta(g('IntroTitle', '快速上手')) + '<p class="tour-description" id="tour-description">' + VexEsc(g('IntroText', '运行软件即可立即创作哟，跟着演示看一遍核心功能。')) + '</p><div class="story-actions intro-actions"><span class="story-dots">' + storyDots() + '</span><span class="story-flex"></span><button class="tour-skip" data-tour="close">先不看</button><button class="tour-continue tour-next" data-tour="start">开始吧</button></div>';
    compactCard('intro');
    centerCard();
  }
  function renderResult(skipped) {
    var act = acts[stepIndex], final = !!act.final;
    resultTargets = focusTargets.slice(); resultKind = focusKind; resultPreferred = focusPreferred;
    hideLightTip();
    layer.classList.remove('demo-running'); card.classList.remove('demo-hidden'); card.removeAttribute('aria-hidden');
    card.dataset.mode = 'result'; focusTargets = resultTargets.slice(); focusKind = resultKind; focusPreferred = resultPreferred; cursor.hidden = true; pointer.hidden = true; layer.dataset.mode = final ? 'final' : 'result';
    card.innerHTML = storyMeta(actTitle(act)) + '<p class="tour-description" id="tour-description">' + VexEsc(actSummary(act)) + '</p><div class="story-actions"><span class="story-dots">' + storyDots() + '</span><span class="story-flex"></span>' + (stepIndex ? '<button class="tour-previous" data-tour="previous">上一步</button>' : '') + '<button class="tour-continue tour-next" data-tour="next">' + (final ? g('FinishTour', '结束导览') : g('Next', '下一步')) + '</button></div>';
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
      if (introDescription) introDescription.textContent = g('IntroText', '运行软件即可立即创作哟，跟着演示看一遍核心功能。');
      if (introCopy) introCopy.remove();
      return;
    }
    var nextNote = card.querySelector('.story-next-note');
    var replay = card.querySelector('[data-tour="replay"]');
    var previous = card.querySelector('[data-tour="previous"]');
    if (nextNote) nextNote.remove();
    if (replay) replay.remove();
    if (previous) previous.textContent = '← ' + g('Previous', '上一步');
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
  function finishAct(skipped) { if (!open) return; cancelTimers(); running = false; paused = false; pendingContinue = null; gate = null; closeLayers(); if (acts[stepIndex].final) renderResult(skipped); else startAct(stepIndex + 1); }
  function baselineFor(index) { return [stage0, stage1WithEnding, stage1WithEnding, stage1WithEnding, routeEdited, routeEdited][index] || stage0; }
  function startAct(index) {
    if (!open) return;
    cancelTimers(); running = true; paused = false; pendingContinue = null; gate = null; stepIndex = Math.max(0, Math.min(acts.length - 1, index)); layer.dataset.step = String(stepIndex + 1); if (app()) app().dataset.guideStep = String(stepIndex + 1);
    closeLayers(); acts[stepIndex].prepare();
    later(260, function () { acts[stepIndex].run(function () { finishAct(false); }); });
  }
  function skipAct() {
    if (!running) return;
    if (acts[stepIndex].final) { finishAct(true); return; }
    cancelTimers(); running = false; paused = false; pendingContinue = null; gate = null; withAutomation(function () { VexWorkbench.applyText(baselineFor(stepIndex), { history: false }); }); closeLayers(); if (acts[stepIndex].final) renderResult(true); else startAct(stepIndex + 1);
  }
  function startStory() {
    if (!open || session) return;
    previousFocus = document.activeElement; session = VexProto.beginGuideSession(); running = true; startAct(0);
  }
  function close() {
    if (!open) return;
    cancelTimers(); running = false; paused = false; pendingContinue = null; gate = null; open = false; hideLightTip(); layer.classList.remove('demo-running'); card.classList.remove('demo-hidden'); card.removeAttribute('aria-hidden'); layer.hidden = true; cursor.hidden = true; pointer.hidden = true; if (app()) delete app().dataset.guideStep; closeLayers();
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
    /* 引导期间钉住三条悬浮工具条：排版/导图样式等高亮目标不会是空的。 */
    ['pinEditor', 'pinReading', 'pinMap'].forEach(function (key) { VexPreferences.set(key, true); });
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
    else if (action === 'previous') startAct(stepIndex - 1);
    else if (action === 'proxy' && gate) proxyGate();
    else if (action === 'continue' && paused) continueGuide();
    else if (action === 'skip') skipAct();
    else if (action === 'replay') startAct(stepIndex);
    else if (action === 'next' && !running) { if (acts[stepIndex].final) close(); else startAct(stepIndex + 1); }
  });
  ['pointerdown', 'click', 'dblclick', 'contextmenu'].forEach(function (eventName) {
    document.addEventListener(eventName, function (event) {
      if (!open || automating || card.contains(event.target)) return;
      if (gate) {
        if (gateAllows(event.target)) {
          // 放行目标点击，让应用自身处理；点击完成后进入下一段。
          if (eventName === 'click') later(0, completeGate);
          return;
        }
        if (gateWrong(event.target)) {
          if (eventName === 'click') { event.preventDefault(); event.stopImmediatePropagation(); correctGate(); }
          return;
        }
      }
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
