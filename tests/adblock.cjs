const { chromium } = require('playwright');
const fs = require('fs');
const assert = require('assert/strict');
(async () => {
 const browser = await chromium.launch({headless:true});
 const page = await browser.newPage({viewport:{width:1800,height:1000}});
 await page.route('**/*', r => r.request().isNavigationRequest() ? r.fulfill({contentType:'text/html',body:fs.readFileSync('tests/itemci-banners.html','utf8')}) : r.abort());
 await page.goto('https://itemci.com/');
 const before = await page.locator('a[title="Reklam"]').count();
 assert(before >= 3, 'Live HTML must contain top and side advertisements');
 await page.addScriptTag({content:fs.readFileSync('overrides/AdBlock.js','utf8')});
 assert(await page.evaluate(()=>document.cookie.includes('site_login_advert=1')), 'Welcome advertisement cookie must be set');
 assert.equal(await page.locator('a[title="Reklam"]').evaluateAll(els => els.filter(e => e.getBoundingClientRect().height > 0).length),0);
 await page.evaluate(() => {
 const host=document.createElement('div');
 host.innerHTML='<div id="late" style="position:fixed;width:160px;height:800px"><a title="Reklam" href="https://example.com"><img alt="Reklam"></a></div><a id="product" href="/product/item"><img alt="İtem" style="width:250px;height:100px"></a><form id="login"><input type="password"><button type="submit">Giriş</button></form>';
 document.body.append(host);
 });
 await page.waitForTimeout(500);
 assert.equal(await page.locator('#late').evaluate(e=>getComputedStyle(e).display),'none');
 assert.notEqual(await page.locator('#product').evaluate(e=>getComputedStyle(e).display),'none');
 assert.notEqual(await page.locator('#login').evaluate(e=>getComputedStyle(e).display),'none');
 await page.evaluate(() => {
 document.body.classList.add('splash-open'); document.body.style.overflow='hidden';
 const popup=document.createElement('div');popup.setAttribute('role','dialog');popup.setAttribute('aria-label','itemci karşılama reklamı');
 popup.innerHTML='<button aria-label="Reklamı kapat">Siteye Git</button>';
 popup.querySelector('button').onclick=()=>popup.remove();document.body.append(popup);
 const order=document.createElement('div');order.id='safe-order';order.setAttribute('role','dialog');order.textContent='Sipariş Detayı';document.body.append(order);
 });
 await page.waitForTimeout(500);
 assert.equal(await page.locator('[aria-label="itemci karşılama reklamı"]').count(),0);
 assert.notEqual(await page.locator('#safe-order').evaluate(e=>getComputedStyle(e).display),'none');
 assert.equal(await page.evaluate(()=>document.body.classList.contains('splash-open')),false);
 const writes = await page.evaluate(async()=>{
 let count=0; const observer=new MutationObserver(rs=>count+=rs.length);
 observer.observe(document.documentElement,{attributes:true,subtree:true});
 window.__niyazisugarAdBlockRefreshV284();
 await new Promise(r=>setTimeout(r,500)); observer.disconnect();return count;
 });
 assert.equal(writes,0,'Repeated scan must not rewrite blocked elements');
 await page.waitForTimeout(6000);
 const scans=await page.evaluate(async()=>{
  const original=document.querySelectorAll.bind(document);let count=0;
  document.querySelectorAll=(selector)=>{if(selector==='div,section,aside,a')count++;return original(selector);};
  const text=document.createElement('div');text.id='normal-chat-message';document.body.append(text);
  for(let i=0;i<20;i++){text.className='chat-animation-'+i;text.textContent='Normal message '+i;await new Promise(r=>setTimeout(r,20));}
  await new Promise(r=>setTimeout(r,700));document.querySelectorAll=original;return count;
 });
 assert.equal(scans,0,'Ordinary chat and animation mutations triggered full-page layout scans.');
 console.log('PASS: ordinary text/animation changes do not trigger full-page ad scans.');
 console.log(`PASS: ${before} real banners hidden; late tall banner hidden; product/login preserved; no repeated hiding writes.`);
 await browser.close();
})().catch(e=>{console.error(e);process.exit(1)});
