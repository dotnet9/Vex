/* 统一的 HTML 转义：此前 9 处各自定义的同义实现收敛到这里，full 覆盖文本与属性两种场景。 */
(function () {
  const MAP = { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' };
  window.VexEsc = value => String(value).replace(/[&<>"']/g, char => MAP[char]);
})();
