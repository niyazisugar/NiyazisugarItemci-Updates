const {chromium}=require('playwright');const fs=require('fs');const assert=require('assert/strict');
(async()=>{
 const browser=await chromium.launch({headless:true});const page=await browser.newPage();
 const row=(id,status,buttons)=>`<div class="listing"><h3>Server ${id} Yang</h3><div><span>İlan No: </span><span>${id}</span></div><span>${status}</span><span>700,00 TL</span>${buttons}</div>`;
 const free=(id,extra='')=>`<button data-id="${id}" title="İlanı Yukarı Taşı (ücretsiz)" ${extra}><svg class="lucide-arrow-up"></svg></button>`;
 const paid='<button class="paid" title="İlanı Öne Çıkar (ücretli)">Roket</button>';
 const html=`<meta charset="utf-8"><h1>İlanlarım</h1>${row('101','SATIŞTA',free('101')+paid)}${row('102','SATIŞTA',free('102','disabled')+paid)}${row('103','SATILDI',free('103'))}${row('104','PASİF',free('104'))}${row('105','SATIŞTA',paid)}${row('106','SATIŞTA',free('106','aria-disabled="true"'))}<a href="/panel/ilanlarim?page=2">2</a>`;
 await page.route('https://itemci.com/**',r=>r.fulfill({contentType:'text/html',body:html}));await page.goto('https://itemci.com/panel/ilanlarim?page=1');
 await page.evaluate(()=>{
 window.clicked=[];window.paidClicks=0;window.fetch=async()=>new Response(JSON.stringify({status:true,message:'İlan yukarı taşındı.'}),{headers:{'Content-Type':'application/json'}});
 for(const button of document.querySelectorAll('button[data-id]'))button.onclick=async()=>{window.clicked.push(button.dataset.id);const response=await fetch('/api/auth/product/'+button.dataset.id+'/up',{method:'POST',body:JSON.stringify({product_id:button.dataset.id})});if(response.ok){button.disabled=true;button.title='İlanı Yukarı Taşı (ücretsiz) · 01:00:00';}};
 for(const button of document.querySelectorAll('.paid'))button.onclick=()=>window.paidClicks++;
 });
 await page.addScriptTag({content:fs.readFileSync('overrides/AutoBump.js','utf8')});
 const read=await page.evaluate(()=>window.__niyaziReadBumpPage());assert.equal(read.hasNext,true);assert.deepEqual(read.listings.map(x=>[x.id,x.enabled]),[['101',true],['102',false],['106',false]]);assert.deepEqual(await page.evaluate(()=>window.clicked),[]);
 await page.evaluate(()=>document.querySelector('button[data-id="102"]').title='Tekrar 00:30:00 sonra');
 const cooldownRead=await page.evaluate(()=>window.__niyaziReadBumpPage());assert.equal(cooldownRead.listings.find(x=>x.id==='102').waitSeconds,1800);
 await page.evaluate(()=>{const b=document.querySelector('button[data-id="102"]');b.disabled=false;});
 const unlabeled=await page.evaluate(()=>window.__niyaziBumpOne('102'));assert(unlabeled.notSent,'an arrow icon without explicit free label must never be clicked');
 await page.evaluate(()=>document.querySelector('button[data-id="102"]').disabled=true);
 const successful=await page.evaluate(()=>window.__niyaziBumpOne('101'));assert.equal(successful.ok,true);assert.deepEqual(await page.evaluate(()=>window.clicked),['101']);
 const repeat=await page.evaluate(()=>window.__niyaziBumpOne('101'));assert(repeat.notSent);assert.equal(repeat.retryAfterSeconds,3600);
 for(const id of ['102','103','104','105','106']){const result=await page.evaluate(id=>window.__niyaziBumpOne(id),id);assert(result.notSent);}
 assert.equal(await page.evaluate(()=>window.paidClicks),0);assert.deepEqual(await page.evaluate(()=>window.clicked),['101']);
 await page.evaluate(()=>{const button=document.querySelector('button[data-id="101"]');button.disabled=false;button.title='İlanı Yukarı Taşı (ücretsiz)';window.fetch=async()=>new Response(JSON.stringify({status:false,message:'1 saat bekleyiniz.'}),{status:429,headers:{'Content-Type':'application/json','Retry-After':'3600'}});});
 const limited=await page.evaluate(()=>window.__niyaziBumpOne('101'));assert.equal(limited.ok,false);assert.equal(limited.retryAfterSeconds,3600);assert.equal(limited.rateLimited,true);
 await page.evaluate(()=>{const b=document.querySelector('button[data-id="101"]');b.disabled=false;window.fetch=async()=>new Response(JSON.stringify({status:false,message:'Çok fazla işlem yaptınız.'}),{headers:{'Content-Type':'application/json'}});});
 const textLimited=await page.evaluate(()=>window.__niyaziBumpOne('101'));assert.equal(textLimited.rateLimited,true);
 await page.evaluate(()=>{const b=document.querySelector('button[data-id="101"]');b.disabled=false;b.onclick=()=>{const alert=document.createElement('div');alert.setAttribute('role','alert');alert.textContent='Çok fazla işlem yaptınız';document.body.append(alert);};});
 const toastLimited=await page.evaluate(()=>window.__niyaziBumpOne('101'));assert.equal(toastLimited.rateLimited,true);assert.equal(toastLimited.retryAfterSeconds,300);
 await page.evaluate(()=>{const b=document.querySelector('button[data-id="101"]');b.onclick=async()=>{await fetch('/api/auth/product/101/up',{method:'POST',body:'{"id":"101"}'});};});
 await page.evaluate(()=>{const button=document.querySelector('button[data-id="101"]');button.disabled=false;window.fetch=async()=>{await new Promise(r=>setTimeout(r,300));return new Response(JSON.stringify({status:true}),{headers:{'Content-Type':'application/json'}});};});
 const parallel=await page.evaluate(()=>Promise.all([window.__niyaziBumpOne('101'),window.__niyaziBumpOne('101')]));assert(parallel[0].ok);assert(parallel[1].notSent);
 await page.evaluate(()=>{const button=document.querySelector('button[data-id="101"]');button.disabled=false;button.onclick=()=>{window.clicked.push('101');fetch('/api/auth/product/101/up',{method:'POST',body:'{"id":"101"}'}).catch(()=>{});};window.fetch=async()=>{throw new Error('offline');};});
 const uncertain=await page.evaluate(()=>window.__niyaziBumpOne('101'));assert(uncertain.uncertain);assert.equal(await page.evaluate(()=>window.paidClicks),0);
 await page.evaluate(()=>{
 const button=document.querySelector('button[data-id="101"]');button.disabled=false;
 window.fetch=async()=>new Response(JSON.stringify({status:true}),{headers:{'Content-Type':'application/json'}});
 button.onclick=()=>{const dialog=document.createElement('div');dialog.setAttribute('role','dialog');dialog.innerHTML='<p>İlanı ücretsiz yukarı taşımak istiyor musunuz?</p><button>Evet</button>';dialog.querySelector('button').onclick=async()=>{await fetch('/api/auth/product/101/up',{method:'POST',body:'{"id":"101"}'});dialog.remove();};document.body.append(dialog);};
 });
 const confirmed=await page.evaluate(()=>window.__niyaziBumpOne('101'));assert(confirmed.ok);assert.equal(await page.locator('[role="dialog"]').count(),0);
 await page.goto('https://itemci.com/product/another');await page.addScriptTag({content:fs.readFileSync('overrides/AutoBump.js','utf8')});await assert.rejects(page.evaluate(()=>window.__niyaziReadBumpPage()));
 console.log('PASS: own active listings only, exact free-action label, sold/inactive/disabled/paid exclusions, pagination, verified success, hourly cooldown, duplicate request guard and uncertain network outcome.');await browser.close();
})().catch(e=>{console.error(e);process.exit(1)});
