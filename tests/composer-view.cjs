const {chromium}=require('playwright');
const fs=require('fs');const path=require('path');const assert=require('assert/strict');
const description='Ürün hakkında detaylı bilgi almak için mesaj gönderebilirsiniz. Farklı ihtiyaçlarınızı da mesaj yoluyla iletebilirsiniz. Hayırlı olsun.';
(async()=>{
 const browser=await chromium.launch({headless:true});
 const page=await browser.newPage({viewport:{width:1100,height:790},deviceScaleFactor:1});
 const errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.addInitScript(()=>{window.messages=[];window.chrome={webview:{postMessage:m=>window.messages.push(m),addEventListener:()=>{}}};});
 const html=fs.readFileSync('overrides/ComposerView.html','utf8');
 await page.route('https://composer-test.local/',r=>r.fulfill({contentType:'text/html',body:html}));
 // Local test image: no illustrated sample artwork is substituted into the released UI.
 const png=Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Wl2nwoAAAAASUVORK5CYII=','base64');
 await page.route('https://composer-images.local/**',r=>r.fulfill({contentType:'image/png',body:png}));
 await page.goto('https://composer-test.local/');
 const base={servers:[{id:'sezar2',name:'Sezar2'},{id:'msp',name:'MSP'}],serverId:'sezar2',draft:{title:'Sezar2 Yang • Hızlı ve Güvenli Teslimat',description,price:5000,delivery:1,validity:15,stock:true,orders:true,images:[]},busy:false,uncertain:false,remaining:0,kind:'idle',message:'Taslak kaydedildi.',url:''};
 await page.evaluate(s=>window.__composerReceive(s),base);
 await page.waitForTimeout(1500);
 fs.mkdirSync('test-results',{recursive:true});
 await page.screenshot({path:'test-results/quick-listing-desktop.png'});
 assert.equal(await page.locator('#net').innerText(),'4.500,00 TL');
 assert.equal(await page.locator('#preview-title').innerText(),base.draft.title);
 await page.locator('#title').fill('Yeni başlık <b>Türkçe İlan</b>');
 assert.equal(await page.locator('#preview-title').innerText(),'Yeni başlık <b>Türkçe İlan</b>');assert.equal(await page.locator('#preview-title b').count(),0);
 await page.locator('#description').fill('Güncel açıklama.');assert.equal(await page.locator('#preview-description').innerText(),'Güncel açıklama.');
 await page.locator('#price').fill('50');assert.equal(await page.locator('#commission').innerText(),'5,00 TL');assert.equal(await page.locator('#net').innerText(),'45,00 TL');
 await page.locator('#orders').click();assert.equal(await page.locator('#orders').getAttribute('aria-checked'),'false');
 await page.locator('#publish').dblclick({delay:50});
 assert.equal(await page.evaluate(()=>window.messages.filter(m=>m.action==='publish').length),1,'rapid clicks must emit one publish request');
 await page.evaluate(s=>window.__composerReceive({...s,busy:true}),base);assert(await page.locator('#title').isDisabled());assert(await page.locator('#save').isDisabled());
 await page.evaluate(s=>window.__composerReceive({...s,remaining:60,kind:'success',url:'https://itemci.com/product/sezar2/actual-listing',message:'İlan yayınlandı.'}),base);
 assert(await page.locator('#publish').isDisabled());assert(await page.locator('#link-actions').isVisible());
 await page.locator('#copy-link').click();await page.locator('#open-link').click();assert.equal(await page.evaluate(()=>window.messages.at(-1).action),'openLink');
 await page.evaluate(s=>window.__composerReceive({...s,remaining:0}),base);assert(await page.locator('#publish').isEnabled(),'timer completion must unlock publishing');
 await page.evaluate(s=>window.__composerReceive({...s,uncertain:true}),base);assert(await page.locator('#publish').isDisabled(),'uncertain results cannot auto-repeat');
 const imgs=[1,2,3,4,5].map(n=>({id:`image-${n}.png`,name:`Hazır görsel ${n}`,url:`https://composer-images.local/image-${n}.png`}));
 await page.evaluate(({base,imgs})=>window.__composerReceive({...base,draft:{...base.draft,stock:false,images:imgs.slice(0,3)},gallery:imgs}),{base,imgs});
 assert.equal(await page.locator('#images .image-card').count(),3);
 await page.locator('#images .image-card').nth(2).locator('.cover-button').click();
 assert((await page.locator('#cover').getAttribute('src')).endsWith('/image-3.png'));
 // Native browser drag events check order and resulting cover payload.
 await page.locator('#images .image-card').nth(0).dragTo(page.locator('#images .image-card').nth(2));
 const ordered=await page.evaluate(()=>window.messages.filter(m=>m.action==='draft').at(-1).draft.images);assert.deepEqual(ordered,['image-1.png','image-2.png','image-3.png']);
 await page.locator('#images .image-card').nth(1).locator('.remove').click();assert.equal(await page.locator('#images .image-card').count(),2);
 await page.locator('#gallery-open').click();assert(await page.locator('#gallery').isVisible());
 await page.getByRole('button',{name:'Hazır görsel 4 görselini ilana ekle'}).click();await page.getByRole('button',{name:'Hazır görsel 5 görselini ilana ekle'}).click();
 await page.getByRole('button',{name:'Hazır görsel 2 görselini ilana ekle'}).click();
 assert.equal(await page.locator('#images .image-card').count(),4,'gallery must enforce 4-image limit');
 assert.equal(await page.locator('#gallery-note').innerText(),'4 / 4 görsel seçili');
 await page.locator('#gallery-done').click();
 await page.locator('#server-add').click();await page.locator('#server-name').fill('Yeni Sunucu');await page.locator('#server-url').fill('https://itemci.com/categories/new-server');await page.locator('#server-save').click();
 assert.equal(await page.evaluate(()=>window.messages.at(-1).action),'addServer');
 await page.evaluate(s=>window.__composerReceive({...s,serverResult:{ok:true}}),base);assert(await page.locator('#server-modal').isHidden());
 for(const size of [{width:480,height:600},{width:800,height:520},{width:1100,height:790}]){
  await page.setViewportSize(size);await page.evaluate(s=>window.__composerReceive(s),base);
  await page.locator('#scroll').evaluate(el=>el.scrollTop=el.scrollHeight);
  const footer=await page.locator('.footer').boundingBox(),button=await page.locator('#publish').boundingBox();
  assert(footer.y>=0&&footer.y+footer.height<=size.height+1);assert(button.x>=0&&button.x+button.width<=size.width+1);assert(button.y+button.height<=size.height+1);
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false,'no horizontal clipping');
  if(size.width===480){await page.locator('#scroll').evaluate(el=>el.scrollTop=0);await page.screenshot({path:'test-results/quick-listing-small.png'});}
 }
 assert.deepEqual(errors,[]);
 console.log('PASS: live title/description/price/commission preview, one publish request, busy/uncertain/cooldown guards, true link actions, cover selection, drag order, image removal, 4-image gallery limit, add-server messaging, responsive fixed footer.');
 await browser.close();
})().catch(e=>{console.error(e);process.exit(1)});
