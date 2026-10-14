const {chromium}=require('playwright');const fs=require('fs');const assert=require('assert/strict');
(async()=>{
 const browser=await chromium.launch({headless:true});const page=await browser.newPage();
 await page.route('https://itemci.com/**',r=>r.fulfill({contentType:'text/html',body:`<meta charset="utf-8"><form><input type="text" maxlength="150" required><input type="number" required min="1" step="1"><select><option value="1">1 Saat</option><option value="2">2 Saat</option><option value="6">6 Saat</option><option value="12">12 Saat</option></select><select><option value="15">15 Gün</option><option value="30">30 Gün</option></select><div class="ql-container"><div class="ql-editor" contenteditable="true"></div></div><button type="button" role="switch" id="sellByUs" aria-checked="true">Sipariş</button><input type="file" multiple><button type="button" id="stock"><img alt="yang" src="https://assets.itemci.com/defaults/3.png"></button><span id="count">Yüklenen Resimler (0/4)</span><button type="submit">İlanı Yayınla</button></form>`}));
 await page.goto('https://itemci.com/panel/ilan-ekle');
 await page.evaluate(()=>{
 window.__NiyaziQuickListing=async()=>({ok:true});window.sent=0;
 document.querySelector('#stock').onclick=()=>document.querySelector('#count').textContent='Yüklenen Resimler (1/4)';
 document.querySelector('#sellByUs').onclick=e=>e.currentTarget.setAttribute('aria-checked',String(e.currentTarget.getAttribute('aria-checked')!=='true'));
 window.fetch=async()=>new Response(JSON.stringify({status:true,data:{id:'test-listing',url:'https://itemci.com/product/test/example'}}),{status:200,headers:{'Content-Type':'application/json'}});
 document.querySelector('form').onsubmit=e=>{e.preventDefault();window.sent++;fetch('/api/auth/product',{method:'POST',body:JSON.stringify({name:document.querySelector('input[type=text]').value,price:Number(document.querySelector('input[type=number]').value)})});};
 });
 await page.addScriptTag({content:fs.readFileSync('overrides/ListingComposer.js','utf8')});
 const payload={server:'Sezar2',title:'250 KT YANG',categoryUrl:'https://itemci.com/categories/sezar2',description:'İlan açıklaması.',price:500,delivery:2,validity:30,stock:true,orders:false};
 const prepared=await page.evaluate(p=>window.__NiyaziPrepareOwnListing(p),payload);
 assert(prepared.ok);assert.equal(await page.evaluate(()=>window.sent),0);
 assert.equal(await page.locator('.ql-editor').innerText(),payload.description);
 assert.equal(await page.locator('#sellByUs').getAttribute('aria-checked'),'false');
 const result=await page.evaluate(p=>window.__NiyaziSubmitOwnListing(p),payload);
 assert(result.ok);assert.equal(result.id,'test-listing');assert.equal(await page.evaluate(()=>window.sent),1);
 for(const [response,expected] of [
  [{status:true,data:{url:'/product/sezar2/new'}},'https://itemci.com/product/sezar2/new'],
  [{status:true,data:{slug:'250-kt'}},'https://itemci.com/product/sezar2/250-kt'],
  [{status:true,data:{url:'https://evil.example/product/a'}},'']
 ]){
  await page.evaluate(json=>window.fetch=async()=>new Response(JSON.stringify(json),{headers:{'Content-Type':'application/json'}}),response);
  const next=await page.evaluate(p=>window.__NiyaziSubmitOwnListing(p),payload);assert(next.ok);assert.equal(next.url,expected);
 }
 await page.evaluate(()=>window.fetch=async()=>new Response(JSON.stringify({status:false,message:'Lütfen bekleyiniz.'}),{status:429,headers:{'Content-Type':'application/json','Retry-After':'90'}}));
 const limited=await page.evaluate(p=>window.__NiyaziSubmitOwnListing(p),payload);assert.equal(limited.ok,false);assert.equal(limited.retryAfterSeconds,90);
 await page.evaluate(()=>window.fetch=async()=>new Response(JSON.stringify({status:false,message:'2 dakika bekleyiniz.'}),{status:429,headers:{'Content-Type':'application/json'}}));
 const timed=await page.evaluate(p=>window.__NiyaziSubmitOwnListing(p),payload);assert.equal(timed.retryAfterSeconds,120);
 await page.evaluate(()=>window.fetch=async()=>new Response('Sunucu hatası',{status:500}));
 const failed=await page.evaluate(p=>window.__NiyaziSubmitOwnListing(p),payload);assert.equal(failed.ok,false);assert.match(failed.message,/Itemci sunucusu/);
 await page.evaluate(()=>window.fetch=async()=>{await new Promise(r=>setTimeout(r,200));return new Response(JSON.stringify({status:true,data:{url:'/product/sezar2/verified'}}),{headers:{'Content-Type':'application/json'}});});
 const double=await page.evaluate(p=>Promise.all([window.__NiyaziSubmitOwnListing(p),window.__NiyaziSubmitOwnListing(p)]),payload);assert.equal(double[0].ok,true);assert.equal(double[1].ok,false);assert.match(double[1].message,/zaten gönderiliyor/);
 await assert.rejects(page.evaluate(p=>window.__NiyaziPrepareOwnListing({...p,title:'a'.repeat(151)}),payload));
 console.log('PASS: real-form selectors, Quill-compatible editing, stock picture, durations/order toggle, separate prepare/submit and confirmed response.');
 await browser.close();
})().catch(e=>{console.error(e);process.exit(1)});
