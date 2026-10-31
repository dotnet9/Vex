const {chromium}=require('playwright');
const assert=require('node:assert/strict');
(async()=>{
 const browser=await chromium.launch({headless:true});
 try {
  const page=await browser.newPage({viewport:{width:1440,height:900}});
  await page.goto((process.env.VEX_PREVIEW_URL||'http://127.0.0.1:4317/design/')+'Vex.html?preview=1&device=desktop');
  await page.waitForFunction(()=>window.VexWorkspace&&window.VexTheme);
  await page.addStyleTag({content:'*,*::before,*::after{transition:none!important;animation:none!important}'});
  const result=await page.evaluate(()=>{
   VexWorkbench.applyText('# Root\n\n## First\n\nSummary\n\n## Second');VexWorkspace.setLayout('map');
   const context=document.createElement('canvas').getContext('2d',{willReadFrequently:true});
   function rgba(css){context.clearRect(0,0,1,1);context.fillStyle=css;context.fillRect(0,0,1,1);return [...context.getImageData(0,0,1,1).data].map((n,i)=>i===3?n/255:n);}
   function background(el){if(!el)return [255,255,255,1];const a=rgba(getComputedStyle(el).backgroundColor),b=background(el.parentElement);return a.slice(0,3).map((v,i)=>v*a[3]+b[i]*(1-a[3])).concat(1);}
   function lum(rgb){return rgb.slice(0,3).map(n=>{n/=255;return n<=.04045?n/12.92:((n+.055)/1.055)**2.4}).reduce((s,n,i)=>s+n*[.2126,.7152,.0722][i],0);}
   const failures=[];let checked=0;
   for(const mode of ['light','dark'])for(const palette of VexPalettes){
    VexTheme.setMode(mode);VexTheme.setPalette(palette.key);
    for(const style of ['quiet','colorful','outline','capsule','underline','mono']){
     document.querySelector('#map-scene').dataset.mapStyle=style;
     for(const title of document.querySelectorAll('#map-scene .map-node-text')){
      const a=lum(rgba(getComputedStyle(title).color)),b=lum(background(title)),ratio=(Math.max(a,b)+.05)/(Math.min(a,b)+.05);
      checked++;if(ratio<4.5)failures.push({mode,palette:palette.key,style,title:title.textContent,ratio});
     }
    }
   }
   return {checked,failures};
  });
  assert.deepEqual(result.failures,[],'All rendered map titles must remain readable');
  console.log(`PASS ${result.checked} rendered mind-map titles across all desktop palettes, modes and styles`);
 } finally {await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});
