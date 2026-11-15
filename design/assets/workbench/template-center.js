/* 模板中心（原型）：模板 = 纯 Markdown 文件（无 YAML 元数据），分组对应模板目录的子文件夹。
 * 唯一标识 = 分组 + 模板名：另存同名覆盖「我的模板」，系统自带模板（软件研发/运维）不可修改与覆盖。
 * 内置 12 套模板按 GB/T 8567-2006（软件研发）与 GB/T 28827.1-2022（运维）章节骨架生成；
 * 「我的模板」存 localStorage 模拟用户模板目录。正式实现由 Vex.Core 提供（迁移自 Vox）。 */
(function () {
  'use strict';
  var esc = function (value) { return String(value).replace(/[&<>"']/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]; }); };
  var t = function (text) { return window.VexL ? VexL.t(text) : text; };

  /* ---------- 内置模板（章节骨架；正式版整文迁自 Vox.Core/Templates） ---------- */
  function doc(title, standard, sections) {
    var body = '# ' + title + '\n\n> 依据 ' + standard + ' 章节结构生成，正文可整段改写或删除。\n';
    sections.forEach(function (section) {
      body += '\n## ' + section.h + '\n\n';
      if (section.sub) {
        section.sub.forEach(function (sub) { body += '### ' + sub + '\n\n在此补充' + sub + '的内容。\n\n'; });
      } else {
        body += '在此补充「' + section.h + '」的内容。\n';
      }
    });
    return body;
  }
  var builtins = [
    { id: 'feasibility', group: 'dev', name: '可行性分析报告', standard: 'GB/T 8567-2006', tags: ['立项', '评审'], desc: '立项前评估技术、经济与社会可行性，给出结论建议。',
      body: doc('可行性分析报告', 'GB/T 8567-2006', [
        { h: '1 引言', sub: ['1.1 编写目的', '1.2 项目背景', '1.3 定义', '1.4 参考资料'] },
        { h: '2 可行性研究的前提', sub: ['2.1 要求', '2.2 目标', '2.3 条件、假定和限制', '2.4 研究方法'] },
        { h: '3 对现有系统的分析' },
        { h: '4 所建议的系统', sub: ['4.1 对现有系统的改进', '4.2 影响', '4.3 技术可行性'] },
        { h: '5 投资及收益分析' },
        { h: '6 社会因素方面的可行性' },
        { h: '7 结论意见' }
      ]) },
    { id: 'dev-plan', group: 'dev', name: '项目开发计划', standard: 'GB/T 8567-2006', tags: ['计划', '管理'], desc: '任务分解、进度、预算与支持条件的总体安排。',
      body: doc('项目开发计划', 'GB/T 8567-2006', [
        { h: '1 引言', sub: ['1.1 编写目的', '1.2 背景', '1.3 定义', '1.4 参考资料'] },
        { h: '2 项目概述', sub: ['2.1 工作内容', '2.2 参加人员', '2.3 产品与交付物', '2.4 完成期限'] },
        { h: '3 实施总计划', sub: ['3.1 任务分解', '3.2 进度安排', '3.3 预算', '3.4 关键问题'] },
        { h: '4 支持条件', sub: ['4.1 计算机系统支持', '4.2 用户承担的工作', '4.3 外单位提供的条件'] },
        { h: '5 专题计划要点' }
      ]) },
    { id: 'srs', group: 'dev', name: '软件需求规格说明', standard: 'GB/T 8567-2006', tags: ['需求', '规格'], desc: '完整描述功能、性能、接口与运行环境需求，作为设计验收基线。',
      body: doc('软件需求规格说明', 'GB/T 8567-2006', [
        { h: '1 引言', sub: ['1.1 编写目的', '1.2 背景', '1.3 定义', '1.4 参考资料'] },
        { h: '2 任务概述', sub: ['2.1 目标', '2.2 用户特点', '2.3 假定和约束'] },
        { h: '3 需求规定', sub: ['3.1 功能需求', '3.2 性能需求', '3.3 输入输出要求', '3.4 数据管理能力要求', '3.5 故障处理要求'] },
        { h: '4 运行环境规定', sub: ['4.1 设备', '4.2 支撑软件', '4.3 接口', '4.4 控制'] }
      ]) },
    { id: 'sdd', group: 'dev', name: '软件设计说明', standard: 'GB/T 8567-2006', tags: ['设计', '架构'], desc: '总体设计、接口、运行设计与数据结构的实现蓝图。',
      body: doc('软件设计说明', 'GB/T 8567-2006', [
        { h: '1 引言', sub: ['1.1 编写目的', '1.2 背景', '1.3 定义', '1.4 参考资料'] },
        { h: '2 总体设计', sub: ['2.1 需求规定', '2.2 运行环境', '2.3 处理流程', '2.4 系统结构'] },
        { h: '3 接口设计', sub: ['3.1 用户接口', '3.2 外部接口', '3.3 内部接口'] },
        { h: '4 运行设计' },
        { h: '5 系统数据结构设计' },
        { h: '6 系统出错处理设计' }
      ]) },
    { id: 'test-plan', group: 'dev', name: '测试计划', standard: 'GB/T 8567-2006', tags: ['测试', '计划'], desc: '测试内容、进度、环境、用例设计与评价准则。',
      body: doc('测试计划', 'GB/T 8567-2006', [
        { h: '1 引言', sub: ['1.1 编写目的', '1.2 背景', '1.3 定义', '1.4 参考资料'] },
        { h: '2 计划', sub: ['2.1 软件说明', '2.2 测试内容', '2.3 测试工具与环境', '2.4 进度与控制'] },
        { h: '3 测试设计说明', sub: ['3.1 功能测试', '3.2 边界与异常测试', '3.3 性能测试'] },
        { h: '4 评价准则', sub: ['4.1 通过准则', '4.2 停止与恢复条件'] }
      ]) },
    { id: 'test-report', group: 'dev', name: '测试报告', standard: 'GB/T 8567-2006', tags: ['测试', '报告'], desc: '测试执行结果、缺陷分布与功能结论。',
      body: doc('测试报告', 'GB/T 8567-2006', [
        { h: '1 引言', sub: ['1.1 编写目的', '1.2 背景', '1.3 定义', '1.4 参考资料'] },
        { h: '2 测试概要', sub: ['2.1 执行情况', '2.2 用例统计'] },
        { h: '3 测试结果及发现', sub: ['3.1 缺陷清单', '3.2 缺陷分析'] },
        { h: '4 对软件功能的结论' },
        { h: '5 分析摘要', sub: ['5.1 能力', '5.2 缺陷和限制', '5.3 建议'] },
        { h: '6 测试资源消耗' }
      ]) },
    { id: 'dev-summary', group: 'dev', name: '项目开发总结报告', standard: 'GB/T 8567-2006', tags: ['总结', '收尾'], desc: '对照计划总结实际开发结果与经验教训。',
      body: doc('项目开发总结报告', 'GB/T 8567-2006', [
        { h: '1 引言', sub: ['1.1 编写目的', '1.2 背景', '1.3 定义', '1.4 参考资料'] },
        { h: '2 实际开发结果', sub: ['2.1 产品与交付物', '2.2 主要性能与功能'] },
        { h: '3 开发工作评价', sub: ['3.1 对生产效率的评价', '3.2 对质量的评价', '3.3 对技术方法的评价'] },
        { h: '4 经验与教训' }
      ]) },
    { id: 'ops-plan', group: 'ops', name: '运维服务方案', standard: 'GB/T 28827.1-2022', tags: ['运维', 'SLA'], desc: '运维服务体系、服务内容与服务级别承诺。',
      body: doc('运维服务方案', 'GB/T 28827.1-2022', [
        { h: '1 总则', sub: ['1.1 目的', '1.2 适用范围', '1.3 参考标准'] },
        { h: '2 服务体系', sub: ['2.1 组织架构', '2.2 角色与职责'] },
        { h: '3 服务内容', sub: ['3.1 日常巡检', '3.2 监控与告警', '3.3 备份与恢复', '3.4 变更管理'] },
        { h: '4 服务级别', sub: ['4.1 SLA 指标', '4.2 响应与恢复时效'] },
        { h: '5 应急预案衔接' }
      ]) },
    { id: 'incident', group: 'ops', name: '事件故障报告', standard: 'GB/T 28827.1-2022', tags: ['故障', '复盘'], desc: '故障时间线、影响范围、根因与改进措施。',
      body: doc('事件故障报告', 'GB/T 28827.1-2022', [
        { h: '1 事件概述', sub: ['1.1 事件编号与级别', '1.2 时间线'] },
        { h: '2 影响范围' },
        { h: '3 处置过程', sub: ['3.1 发现与上报', '3.2 定位', '3.3 恢复'] },
        { h: '4 根因分析' },
        { h: '5 改进措施', sub: ['5.1 短期措施', '5.2 长期改进'] }
      ]) },
    { id: 'change-log', group: 'ops', name: '变更记录', standard: 'GB/T 28827.1-2022', tags: ['变更', '留痕'], desc: '变更申请、实施步骤与回退方案的留痕。',
      body: doc('变更记录', 'GB/T 28827.1-2022', [
        { h: '1 变更申请', sub: ['1.1 变更编号与类型', '1.2 变更原因'] },
        { h: '2 变更内容' },
        { h: '3 实施计划', sub: ['3.1 实施窗口', '3.2 实施步骤', '3.3 回退方案'] },
        { h: '4 验证与关闭' }
      ]) },
    { id: 'inspection', group: 'ops', name: '巡检报告', standard: 'GB/T 28827.1-2022', tags: ['巡检', '报告'], desc: '周期巡检项目、结果与异常建议。',
      body: doc('巡检报告', 'GB/T 28827.1-2022', [
        { h: '1 巡检概况', sub: ['1.1 巡检范围', '1.2 时间与人员'] },
        { h: '2 巡检项与结果', sub: ['2.1 主机与存储', '2.2 网络', '2.3 备份', '2.4 安全'] },
        { h: '3 异常与建议' }
      ]) },
    { id: 'emergency', group: 'ops', name: '应急预案', standard: 'GB/T 28827.1-2022', tags: ['应急', '演练'], desc: '分级预警、响应流程与演练安排。',
      body: doc('应急预案', 'GB/T 28827.1-2022', [
        { h: '1 总则', sub: ['1.1 目的', '1.2 适用范围'] },
        { h: '2 应急组织与职责' },
        { h: '3 预警与分级' },
        { h: '4 应急响应流程', sub: ['4.1 场景与处置', '4.2 恢复与总结'] },
        { h: '5 演练与修订' }
      ]) }
  ];

  /* ---------- 我的模板（localStorage 模拟用户模板目录；子文件夹即分组） ---------- */
  var STORE_KEY = 'vex-design-user-templates';
  var userTemplates = [];
  try { userTemplates = JSON.parse(VexStorage.getItem(STORE_KEY) || '[]') || []; } catch (_) { userTemplates = []; }
  function persistUser() { try { VexStorage.setItem(STORE_KEY, JSON.stringify(userTemplates)); } catch (_) {} }

  function allTemplates() { return builtins.concat(userTemplates); }
  function byId(id) { return allTemplates().find(function (item) { return item.id === id; }); }
  function identity(group, name) { return group + '/' + name; }
  function findUser(group, name) { return userTemplates.find(function (item) { return item.group === group && item.name === name; }); }
  function findBuiltin(group, name) { return builtins.find(function (item) { return item.group === group && item.name === name; }); }

  var groupLabel = function (group) { return group === 'dev' ? '软件研发' : group === 'ops' ? '运维' : group === 'my' ? '我的模板' : group; };
  var STANDARD_BADGE = { 'dev': 'GB/T 8567', 'ops': 'GB/T 28827' };
  /* 分组选项：固定两组 + 动态出现过的自定义分组 + 我的模板。 */
  function groupOptions() {
    var seen = {}, options = [['dev', '软件研发'], ['ops', '运维']];
    userTemplates.forEach(function (item) { if (item.group !== 'dev' && item.group !== 'ops' && item.group !== 'my' && !seen[item.group]) { seen[item.group] = true; options.push([item.group, item.group]); } });
    options.push(['my', '我的模板']);
    return options;
  }
  function fillGroupSelect(select, value) {
    select.innerHTML = groupOptions().map(function (pair) {
      return '<option value="' + esc(pair[0]) + '"' + (pair[0] === value ? ' selected' : '') + '>' + esc(t(pair[1])) + '</option>';
    }).join('');
  }

  /* ---------- 选择器渲染 ---------- */
  var listEl, groupsEl, filterEl, tagsEl, metaEl, previewEl, useBtn, applyBtn;
  var selectedGroup = 'all', selectedId = null, query = '', activeTag = null;

  function visibleTemplates() {
    return allTemplates().filter(function (item) {
      if (selectedGroup !== 'all' && item.group !== selectedGroup) return false;
      if (activeTag && !(item.tags || []).some(function (tag) { return tag === activeTag; })) return false;
      if (!query) return true;
      var haystack = item.name + ' ' + item.desc + ' ' + (item.tags || []).join(' ');
      return haystack.toLowerCase().indexOf(query.toLowerCase()) >= 0;
    });
  }
  function renderGroups() {
    var groups = [['all', '全部'], ['dev', '软件研发'], ['ops', '运维']];
    userTemplates.forEach(function (item) {
      if (item.group !== 'dev' && item.group !== 'ops' && item.group !== 'my' && !groups.some(function (pair) { return pair[0] === item.group; })) groups.push([item.group, item.group]);
    });
    groups.push(['my', '我的模板']);
    groupsEl.innerHTML = groups.map(function (pair) {
      return '<button type="button" data-group="' + esc(pair[0]) + '" aria-pressed="' + String(selectedGroup === pair[0]) + '">' + esc(t(pair[1])) + '</button>';
    }).join('');
  }
  function renderTags() {
    var tags = {};
    visibleTemplates().forEach(function (item) { (item.tags || []).forEach(function (tag) { tags[tag] = (tags[tag] || 0) + 1; }); });
    var names = Object.keys(tags).sort(function (a, b) { return tags[b] - tags[a] || a.localeCompare(b); });
    tagsEl.hidden = !names.length;
    tagsEl.innerHTML = names.map(function (tag) {
      return '<button type="button" class="template-tag' + (activeTag === tag ? ' on' : '') + '" data-tag="' + esc(tag) + '" aria-pressed="' + String(activeTag === tag) + '"># ' + esc(tag) + '</button>';
    }).join('');
  }
  function renderList() {
    var items = visibleTemplates();
    if (!items.length) {
      listEl.innerHTML = '<div class="empty">' + esc(selectedGroup === 'my'
        ? '还没有自定义模板。当前文档编辑后可通过「另存当前文档为模板」保存到这里。'
        : '没有匹配的模板') + '</div>';
      return;
    }
    listEl.innerHTML = items.map(function (item) {
      var badge = item.isCustom ? esc(t('自定义')) : esc(STANDARD_BADGE[item.group] || '');
      var tags = (item.tags || []).slice(0, 3).map(function (tag) { return '<span class="t-tag"># ' + esc(tag) + '</span>'; }).join('');
      var actions = item.isCustom
        ? '<span class="t-actions"><button type="button" class="t-act" data-t-action="edit" title="' + esc(t('修改内容')) + '" aria-label="' + esc(t('修改内容')) + '"><svg class="ico" aria-hidden="true"><use href="#i-edit"/></svg></button><button type="button" class="t-act danger" data-t-action="delete" title="' + esc(t('删除')) + '" aria-label="' + esc(t('删除')) + '"><svg class="ico" aria-hidden="true"><use href="#i-trash"/></svg></button></span>'
        : '';
      return '<div class="template-item' + (item.id === selectedId ? ' selected' : '') + '" data-id="' + esc(item.id) + '" role="option" aria-selected="' + String(item.id === selectedId) + '" tabindex="0">'
        + '<div class="t-head"><span class="t-text">' + esc(item.name) + '</span>' + (badge ? '<span class="t-standard">' + badge + '</span>' : '') + actions + '</div>'
        + '<div class="t-desc">' + esc(item.desc || '') + '</div>'
        + (tags ? '<div class="t-tags">' + tags + '</div>' : '')
        + '</div>';
    }).join('');
    if (!selectedId || !items.some(function (item) { return item.id === selectedId; })) select(items[0] && items[0].id);
  }
  function renderPreview() {
    var item = byId(selectedId);
    var doc = window.VexWorkbench && VexWorkbench.documents && VexWorkbench.documents[VexWorkbench.state.doc];
    var readOnly = !!(doc && doc.readOnly);
    if (!item) { metaEl.innerHTML = ''; previewEl.innerHTML = ''; useBtn.disabled = true; applyBtn.disabled = true; return; }
    useBtn.disabled = false;
    applyBtn.disabled = readOnly;
    applyBtn.title = readOnly ? t('当前文档是只读的，不能应用模板') : t('用模板内容替换当前打开文档的全部内容，可 Ctrl+Z 撤销');
    var metaTags = (item.tags || []).map(function (tag) { return '<span class="m-tag"># ' + esc(tag) + '</span>'; }).join('');
    metaEl.innerHTML = '<strong>' + esc(item.name) + '</strong><span class="m-standard">' + esc(item.standard) + '</span>'
      + '<span class="m-desc">' + esc(item.desc || '') + '</span>' + (metaTags ? '<span class="m-tags">' + metaTags + '</span>' : '');
    previewEl.innerHTML = TexEditor.render(item.body);
  }
  function select(id) { selectedId = id; renderList(); renderPreview(); }
  function refresh() { renderGroups(); renderTags(); renderList(); renderPreview(); }

  function open() {
    refresh();
    listEl.scrollTop = 0;
    if (window.VexProto && VexProto.closeMenu) VexProto.closeMenu();
    VexProto.handleAction('dlg-templates');
  }
  function closeLayer() {
    var layer = document.getElementById('layer-templates');
    if (layer && !layer.hidden) {
      var button = layer.querySelector('[data-close]');
      if (button) button.click(); else layer.hidden = true;
    }
  }
  function use() {
    var item = byId(selectedId); if (!item) return;
    if (!VexDocumentWindow.open({ document: { name: item.name + '.md', text: item.body, isNew: true } })) { VexProto.toastKey('ToastAllowPopups'); return; }
    closeLayer();
    VexProto.toast(t('已按模板「') + item.name + t('」新建文档'));
  }
  /* 应用到当前文档：整体替换正文，applyText 进撤销栈，Ctrl+Z 可找回原内容。 */
  function apply() {
    var item = byId(selectedId); if (!item) return;
    var W = window.VexWorkbench;
    var doc = W && W.documents && W.documents[W.state.doc];
    if (!doc || doc.readOnly) { VexProto.toastKey('ToastDocumentReadOnly'); return; }
    W.finish();
    VexWorkbench.applyText(item.body);
    closeLayer();
    VexProto.toast(t('已应用模板「') + item.name + t('」，Ctrl+Z 可撤销'));
  }

  /* ---------- 另存当前文档为模板（无预览；同名覆盖我的模板） ---------- */
  function openSaveDialog() {
    var W = window.VexWorkbench;
    var doc = W && W.documents && W.documents[W.state.doc];
    if (!doc) return;
    if (!W.text || !W.text.trim()) { VexProto.toast(t('当前文档是空的，没有可保存的内容')); return; }
    document.getElementById('template-save-name').value = doc.name.replace(/\.md$/i, '');
    fillGroupSelect(document.getElementById('template-save-group'), 'my');
    document.getElementById('template-save-tags').value = '';
    hideMessage('template-save-error'); hideMessage('template-save-hint');
    syncSaveHint();
    closeLayer();
    VexProto.handleAction('dlg-template-save');
  }
  var INVALID_NAME = /[\\/:*?"<>|]/;
  function hideMessage(id) { var el = document.getElementById(id); if (el) el.classList.add('is-hidden'); }
  function showMessage(id, text) { var el = document.getElementById(id); if (!el) return; if (text) el.textContent = text; el.classList.remove('is-hidden'); }
  function parseTags(value) {
    return value.split(/[,，;；\s]+/).map(function (tag) { return tag.trim(); }).filter(function (tag, index, all) { return tag && all.indexOf(tag) === index; }).slice(0, 8);
  }
  function syncSaveHint() {
    var name = document.getElementById('template-save-name').value.trim();
    var group = document.getElementById('template-save-group').value;
    var ok = document.getElementById('template-save-ok');
    if (findBuiltin(group, name)) { showMessage('template-save-error', t('此分组已有同名系统模板，系统模板不可覆盖，请换个名称或分组。')); hideMessage('template-save-hint'); ok.disabled = true; return; }
    hideMessage('template-save-error');
    var existing = findUser(group, name);
    if (existing) { showMessage('template-save-hint', t('「我的模板」中已有同名模板，保存将覆盖其内容。')); ok.disabled = false; ok.textContent = t('覆盖模板'); return; }
    hideMessage('template-save-hint'); ok.disabled = false; ok.textContent = t('保存模板');
  }
  function saveCurrent() {
    var nameInput = document.getElementById('template-save-name');
    var name = nameInput.value.trim();
    if (!name || INVALID_NAME.test(name)) { showMessage('template-save-error'); nameInput.focus(); return; }
    var group = document.getElementById('template-save-group').value;
    var tags = parseTags(document.getElementById('template-save-tags').value);
    var text = VexWorkbench.text;
    var existing = findUser(group, name);
    if (existing) {
      existing.body = text; existing.tags = tags; existing.desc = t('来自当前文档') + ' · ' + groupLabel(group);
    } else {
      userTemplates.unshift({ id: 'user-' + Date.now(), group: group, name: name, tags: tags, standard: t('自定义'), desc: t('来自当前文档') + ' · ' + groupLabel(group), body: text, isCustom: true });
    }
    persistUser();
    var saveLayer = document.getElementById('layer-template-save');
    if (saveLayer) saveLayer.hidden = true;
    selectedGroup = group; activeTag = null;
    open();
    VexProto.toast((existing ? t('已覆盖模板（演示）') : t('已保存为模板（演示）')) + '：' + name);
  }

  /* ---------- 修改 / 删除我的模板 ---------- */
  var editingId = null;
  function openEditDialog(id) {
    var item = byId(id);
    if (!item || !item.isCustom) return;
    editingId = id;
    document.getElementById('template-edit-name').value = item.name;
    fillGroupSelect(document.getElementById('template-edit-group'), item.group);
    document.getElementById('template-edit-tags').value = (item.tags || []).join(', ');
    document.getElementById('template-edit-content').value = item.body;
    hideMessage('template-edit-error');
    VexProto.handleAction('dlg-template-edit');
  }
  function saveEdit() {
    var item = byId(editingId);
    if (!item) return;
    var nameInput = document.getElementById('template-edit-name');
    var name = nameInput.value.trim();
    var group = document.getElementById('template-edit-group').value;
    if (!name || INVALID_NAME.test(name)) { showMessage('template-edit-error', t('名称不能包含 \\ / : * ? " < > | 等字符')); nameInput.focus(); return; }
    if (findBuiltin(group, name)) { showMessage('template-edit-error', t('系统自带模板不可覆盖，请换个名称或分组。')); return; }
    var clash = userTemplates.find(function (other) { return other !== item && other.group === group && other.name === name; });
    if (clash) { showMessage('template-edit-error', t('该分组下已有同名模板，请先删除或换一个名称。')); return; }
    item.name = name; item.group = group;
    item.tags = parseTags(document.getElementById('template-edit-tags').value);
    item.body = document.getElementById('template-edit-content').value;
    item.desc = t('来自当前文档') + ' · ' + groupLabel(group);
    persistUser();
    document.getElementById('layer-template-edit').hidden = true;
    selectedGroup = group; activeTag = null;
    refresh();
    VexProto.toast(t('模板已更新') + '：' + name);
  }
  /* 两击确认删除：第一次变红要求确认，4 秒后自动复位。 */
  var deleteArmed = null;
  function requestDelete(button, id) {
    if (deleteArmed === id) {
      clearTimeout(button._resetTimer);
      var item = byId(id);
      userTemplates = userTemplates.filter(function (other) { return other.id !== id; });
      persistUser();
      deleteArmed = null;
      if (selectedId === id) { selectedId = null; }
      refresh();
      VexProto.toast(t('已删除模板') + '：' + (item ? item.name : ''));
      return;
    }
    deleteArmed = id;
    button.classList.add('confirm');
    button.title = t('再点一次确认删除');
    button._resetTimer = setTimeout(function () {
      deleteArmed = null;
      button.classList.remove('confirm');
      button.title = t('删除');
    }, 4000);
  }

  /* ---------- 事件 ---------- */
  function bind() {
    listEl = document.getElementById('template-list');
    groupsEl = document.getElementById('template-groups');
    filterEl = document.getElementById('template-filter');
    tagsEl = document.getElementById('template-tags');
    metaEl = document.getElementById('template-meta');
    previewEl = document.getElementById('template-preview-body');
    useBtn = document.getElementById('template-use');
    applyBtn = document.getElementById('template-apply');
    groupsEl.addEventListener('click', function (event) {
      var button = event.target.closest('[data-group]'); if (!button) return;
      selectedGroup = button.dataset.group; activeTag = null; refresh();
      listEl.scrollTop = 0;
    });
    tagsEl.addEventListener('click', function (event) {
      var button = event.target.closest('[data-tag]'); if (!button) return;
      activeTag = activeTag === button.dataset.tag ? null : button.dataset.tag;
      renderTags(); renderList();
      listEl.scrollTop = 0;
    });
    listEl.addEventListener('click', function (event) {
      var act = event.target.closest('[data-t-action]');
      if (act) {
        event.stopPropagation();
        var id = act.dataset.tAction === 'edit' ? act.closest('.template-item').dataset.id : act.dataset.tAction === 'delete' ? act.closest('.template-item').dataset.id : null;
        if (act.dataset.tAction === 'edit') openEditDialog(id);
        else requestDelete(act, id);
        return;
      }
      var item = event.target.closest('.template-item'); if (item) select(item.dataset.id);
    });
    listEl.addEventListener('keydown', function (event) {
      if (event.key !== 'ArrowDown' && event.key !== 'ArrowUp') return;
      event.preventDefault();
      var items = Array.from(listEl.querySelectorAll('.template-item'));
      var index = items.indexOf(document.activeElement);
      var next = items[event.key === 'ArrowDown' ? Math.min(items.length - 1, index + 1) : Math.max(0, index - 1)];
      if (next) { next.focus(); select(next.dataset.id); }
    });
    filterEl.addEventListener('input', function () { query = filterEl.value.trim(); renderTags(); renderList(); listEl.scrollTop = 0; });
    /* 分组与 Tag 单行溢出时，鼠标滚轮直接横向滚动；到边缘放行给页面滚动。 */
    var wheelToHorizontal = function (el) {
      el.addEventListener('wheel', function (event) {
        if (!event.deltaY) return;
        var max = el.scrollWidth - el.clientWidth;
        if (max <= 0) return;
        if ((event.deltaY > 0 && el.scrollLeft >= max) || (event.deltaY < 0 && el.scrollLeft <= 0)) return;
        event.preventDefault();
        el.scrollLeft += event.deltaY;
      }, { passive: false });
    };
    wheelToHorizontal(groupsEl);
    wheelToHorizontal(tagsEl);
    document.getElementById('template-filter-clear').addEventListener('click', function () { filterEl.value = ''; query = ''; renderTags(); renderList(); filterEl.focus(); });
    useBtn.addEventListener('click', use);
    applyBtn.addEventListener('click', apply);
    document.getElementById('template-save-current').addEventListener('click', openSaveDialog);
    document.getElementById('template-save-ok').addEventListener('click', saveCurrent);
    document.getElementById('template-save-name').addEventListener('input', function () { hideMessage('template-save-error'); syncSaveHint(); });
    document.getElementById('template-save-group').addEventListener('change', syncSaveHint);
    document.getElementById('template-edit-ok').addEventListener('click', saveEdit);
  }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', bind); else bind();

  /* ---------- 动作接入：菜单「从模板新建 / 另存为模板」 ---------- */
  var previousHandle = window.VexProto && VexProto.handleAction;
  VexProto.handleAction = function (action, item) {
    if (action === 'new-from-template') { open(); return true; }
    if (action === 'save-as-template') { openSaveDialog(); return true; }
    return previousHandle.call(this, action, item);
  };
  window.VexTemplateCenter = { open: open, openSaveDialog: openSaveDialog, templates: allTemplates };
})();
