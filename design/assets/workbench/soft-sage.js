/* Prototype palette alias: present the existing teal slot as the requested warm Sage theme.
   Keep the generated nine-palette catalog and all theme-selection behavior intact. */
(function(){
  var sage=window.VexPalettes&&window.VexPalettes.find(function(item){return item.key==='teal';});
  if(sage){
    sage.label='鼠尾草';
    sage.en='Sage';
    sage.note='暖白底色 · 鼠尾草绿';
    sage.swatch=['#FFFDF9','#F1F4EE','#34443B','#6F8F7D','#DC2626','#15803D','#B45309','#789889'];
  }
  if(window.VexPaletteRulesData)window.VexPaletteRulesData.default='teal';
})();
