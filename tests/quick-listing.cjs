const{chromium}=require('playwright');const fs=require('fs');const assert=require('assert/strict');
(async()=>{
 const browser=await chromium.launch({headless:true});const page=await browser.newPage();
 await page.route('https://itemci.com/**',r=>r.fulfill({contentType:'text/html',body:`<form><select id="server"><option value="">Sunucu</option><option value="msp">MSP2 Global - Yeni</option><option value="sezar">Sezar2</option></select><select id="category"><option value="">Kategori</option><option value="yang">Item&amp;Yang</option></select><input name="title" placeholder="İlan başlığı"><button type="submit">Yayınla</button></form>`}));
 await page.goto('https://itemci.com/panel/ilan-ekle');
 await page.evaluate(()=>{window.submits=0;document.querySelector('form').onsubmit=e=>{e.preventDefault();window.submits++;};});
 await page.addScriptTag({content:fs.readFileSync('overrides/QuickListing.js','utf8')});
 let result=await page.evaluate(()=>window.__NiyaziQuickListing('MSP2 Global - Yeni'));
 assert.equal(result.ok,true);assert.equal(await page.locator('#server').inputValue(),'msp');assert.equal(await page.locator('#category').inputValue(),'yang');
 await page.locator('input[name="title"]').fill('Korunacak ilan taslağı');
 result=await page.evaluate(()=>window.__NiyaziQuickListing('Sezar2'));
 assert.equal(result.draft,true);assert.equal(await page.locator('#server').inputValue(),'msp');assert.equal(await page.locator('input[name="title"]').inputValue(),'Korunacak ilan taslağı');
 assert.equal(await page.evaluate(()=>window.submits),0);
 await page.setContent('<meta charset="utf-8"><aside><button>Sezar2</button></aside><main id="wizard"><button id="game"><h3>Metin2 PvP Serverler</h3><small>Sunuculara göz at</small></button></main>');
 await page.evaluate(()=>{
  const wizard=document.querySelector('#wizard');
  document.querySelector('#game').onclick=()=>{
   wizard.innerHTML='<div class="cursor-pointer" id="server-card"><h3>Sezar 2</h3><span>2 kategori</span></div>';
   document.querySelector('#server-card').onclick=()=>{
    wizard.innerHTML='<button id="category-card"><span>Sezar2 Item&amp;Yang</span><small>İlan kategorisi</small></button>';
    document.querySelector('#category-card').onclick=()=>wizard.innerHTML='<form><input type="text" required maxlength="150"><button type="submit">Yayınla</button></form>';
   };
  };
 });
 const wizardResult=await page.evaluate(()=>window.__NiyaziQuickListing('Sezar2'));
 assert.equal(wizardResult.ok,true);assert.equal(await page.locator('form input[maxlength="150"]').count(),1);
 await page.goto('https://itemci.com/user/login');await page.addScriptTag({content:fs.readFileSync('overrides/QuickListing.js','utf8')});
 result=await page.evaluate(()=>window.__NiyaziQuickListing('Sezar2'));assert.equal(result.ok,false);
 console.log('PASS: automatic server/category selection, existing draft preserved, login route reported, no publishing.');
 await browser.close();
})().catch(e=>{console.error(e);process.exit(1)});
