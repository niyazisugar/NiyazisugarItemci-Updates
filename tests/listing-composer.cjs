const {chromium}=require('playwright');const fs=require('fs');const assert=require('assert/strict');
(async()=>{
 const browser=await chromium.launch({headless:true});const page=await browser.newPage();
 await page.route('https://itemci.com/**',r=>r.fulfill({contentType:'text/html',body:`<form><input type="text" maxlength="150" required><input type="number" required min="1" step="1"><select><option value="1">1 Saat</option><option value="2">2 Saat</option><option value="6">6 Saat</option><option value="12">12 Saat</option></select><select><option value="15">15 Gün</option><option value="30">30 Gün</option></select><div class="ql-container"><div class="ql-editor" contenteditable="true"></div></div><button type="button" role="switch" id="sellByUs" aria-checked="true">Sipariş</button><input type="file" multiple><button type="button" id="stock"><img alt="yang" src="https://assets.itemci.com/defaults/3.png"></button><span id="count">Yüklenen Resimler (0/4)</span><button type="submit">İlanı Yayınla</button></form>`}));
 await page.goto('https://itemci.com/panel/ilan-ekle');
 await page.evaluate(()=>{
 window.__NiyaziQuickListing=async()=>({ok:true});window.sent=0;
 document.querySelector('#stock').onclick=()=>document.querySelector('#count').textContent='Yüklenen Resimler (1/4)';
 document.querySelector('#sellByUs').onclick=e=>e.currentTarget.setAttribute('aria-checked',String(e.currentTarget.getAttribute('aria-checked')!=='true'));
 window.fetch=async()=>new Response(JSON.stringify({status:true,data:{id:'test-listing',url:'https://itemci.com/product/test/example'}}),{status:200,headers:{'Content-Type':'application/json'}});
 document.querySelector('form').onsubmit=e=>{e.preventDefault();window.sent++;fetch('/api/auth/product',{method:'POST',body:JSON.stringify({name:document.querySelector('input[type=text]').value,price:Number(document.querySelector('input[type=number]').value)})});};
 });
 await page.addScriptTag({content:fs.readFileSync('overrides/ListingComposer.js','utf8')});
 const payload={server:'Sezar2',title:'250 KT YANG',description:'İlan açıklaması.',price:500,delivery:2,validity:30,stock:true,orders:false};
 const prepared=await page.evaluate(p=>window.__NiyaziPrepareOwnListing(p),payload);
 assert(prepared.ok);assert.equal(await page.evaluate(()=>window.sent),0);
 assert.equal(await page.locator('.ql-editor').innerText(),payload.description);
 assert.equal(await page.locator('#sellByUs').getAttribute('aria-checked'),'false');
 const result=await page.evaluate(p=>window.__NiyaziSubmitOwnListing(p),payload);
 assert(result.ok);assert.equal(result.id,'test-listing');assert.equal(await page.evaluate(()=>window.sent),1);
 await assert.rejects(page.evaluate(p=>window.__NiyaziPrepareOwnListing({...p,title:'a'.repeat(151)}),payload));
 console.log('PASS: real-form selectors, Quill-compatible editing, stock picture, durations/order toggle, separate prepare/submit and confirmed response.');
 await browser.close();
})().catch(e=>{console.error(e);process.exit(1)});
