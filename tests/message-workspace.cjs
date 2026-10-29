const {chromium}=require('playwright'),fs=require('fs'),assert=require('assert/strict');
(async()=>{
const browser=await chromium.launch({headless:true});const page=await browser.newPage({viewport:{width:1440,height:920}});
await page.route('https://itemci.com/**',r=>r.fulfill({contentType:'text/html; charset=utf-8',body:'<html><head></head><body><header style="position:fixed;z-index:99999">SITE HEADER</header></body></html>'}));await page.goto('https://itemci.com/panel/mesajlar');
await page.evaluate(tree=>{
 function make(n){const el=document.createElement(n.tag);el.className=n.classes.join(' ');if(n.tag==='INPUT'){el.type='text';el.placeholder=n.kind==='person-search'?'Kullanıcı ara...':n.kind==='message-input'?'Mesajınızı yazın':'Ara...';}
  if(n.tag==='H2')el.textContent='Mesajlar';if(n.tag==='H3')el.textContent='Test alıcı';if(n.tag==='H4')el.textContent='Test alıcı';if(n.tag==='P')el.textContent='Test önizleme';if(n.tag==='SPAN')el.textContent='12:30';
  if(n.classes.includes('whitespace-pre-wrap'))el.append(document.createTextNode(n.classes.includes('bg-orange-500')?'Test giden':'Test gelen'));
  for(const c of n.children)el.append(make(c));return el;
 }
 document.body.append(make(tree));window.sent=0;
 const form=document.querySelector('form');form.onsubmit=e=>{e.preventDefault();sent++;if(window.noAck)return;const parent=document.querySelector('[class~="space-y-4"]');const row=document.createElement('div');row.className='flex-col ml-auto items-end';const b=document.createElement('div');b.className='whitespace-pre-wrap';b.textContent=form.querySelector('input').value;row.append(b);parent.append(row);};
},JSON.parse(fs.readFileSync('tests/fixtures/message-real-structure.json','utf8')));
await page.addScriptTag({content:fs.readFileSync('overrides/QuickReplyDock.js','utf8')});await page.locator('#niyazi-message-workspace').waitFor();
assert.equal(await page.locator('#niyazi-message-workspace .bubble').count(),3);assert.equal(await page.locator('#niyazi-message-workspace .own').count(),1);
await page.locator('#niyazi-message-workspace .chip').first().click();assert.equal(await page.evaluate(()=>sent),0);
await page.locator('#niyazi-message-workspace #send').click();await page.locator('#niyazi-message-workspace #status').filter({hasText:'Mesaj Itemci konuşmasında göründü.'}).waitFor();assert.equal(await page.evaluate(()=>sent),1);assert.equal(await page.locator('#niyazi-message-workspace #draft').inputValue(),'');
for(const width of [1440,1050,760,480]){await page.setViewportSize({width,height:850});const b=await page.locator('#niyazi-message-workspace #send').boundingBox();assert(b.x>=0&&b.x+b.width<=width);}
await page.setViewportSize({width:1440,height:920});await page.screenshot({path:'test-results/message-workspace.png'});
await page.evaluate(()=>window.noAck=true);await page.locator('#niyazi-message-workspace #draft').fill('Belirsiz gönderim');await page.locator('#niyazi-message-workspace #send').click();await page.waitForFunction(()=>sent===2);assert.equal(await page.evaluate(()=>sent),2);assert.equal(await page.locator('#niyazi-message-workspace #send').isDisabled(),true);assert.equal(await page.locator('#niyazi-message-workspace #draft').inputValue(),'Belirsiz gönderim');
await page.locator('#niyazi-message-workspace #original').click();assert.equal(await page.locator('#niyazi-message-workspace').count(),0);assert.equal(await page.locator('input[placeholder="Mesajınızı yazın"]').inputValue(),'Belirsiz gönderim');
console.log('PASS: independent Shadow DOM UI reads real supplied structure, separates incoming/outgoing, preserves quick replies, confirms actual message appearance, prevents duplicate uncertain sends, responsive composer and original view.');await browser.close();
})().catch(e=>{console.error(e);process.exit(1)});
