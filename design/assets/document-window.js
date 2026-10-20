/* The browser simulates an independent app launch with a full workspace tab. */
(function () {
  'use strict';
  function read() {
    var query=new URLSearchParams(location.search), file=query.get('file');
    var builtin=Object.values(window.VexDocuments||{}).find(function (doc) {return doc.file===file;});
    if(builtin)return {name:builtin.name,text:builtin.text,readOnly:true,path:file,builtin:true};
    var token=query.get('launch');if(!token)return null;
    try {var payload=JSON.parse(sessionStorage.getItem('vex-document-launch:'+token));return payload&&typeof payload.text==='string'&&typeof payload.name==='string'?payload:null;}catch(_){return null;}
  }
  function open(options) {
    options=options||{};var target=new URL('Vex.html',location.href);
    ['platform','theme','palette','lang','preview'].forEach(function (key) {var value=new URLSearchParams(location.search).get(key);if(value)target.searchParams.set(key,value);});
    target.searchParams.set('device','desktop');
    if(options.intent)target.searchParams.set('intent',options.intent);
    if(options.builtin){target.searchParams.set('file',VexDocuments[options.builtin].file);target.searchParams.set('layout','read');}
    var token;
    if(options.document){token=crypto.randomUUID();try{sessionStorage.setItem('vex-document-launch:'+token,JSON.stringify(options.document));}catch(_){return false;}target.searchParams.set('launch',token);}
    var page=window.open(target.href,'_blank');if(page)page.opener=null;
    if(token)sessionStorage.removeItem('vex-document-launch:'+token);
    return !!page;
  }
  window.VexDocumentWindow={read:read,open:open};
})();
