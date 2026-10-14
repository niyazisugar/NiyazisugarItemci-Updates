const { chromium } = require('playwright');
const fs = require('fs');
const assert = require('assert/strict');
(async () => {
 const browser = await chromium.launch({headless:true});
 const page = await browser.newPage({viewport:{width:1800,height:1000}});
 await page.route('**/*', r => r.abort());
 await page.setContent(fs.readFileSync('tests/itemci-banners.html','utf8'));
 const before = await page.locator('a[title="Reklam"]').count();
 assert(before >= 3, 'Live HTML must contain top and side advertisements');
 await page.addScriptTag({content:fs.readFileSync('overrides/AdBlock.js','utf8')});
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
 const writes = await page.evaluate(async()=>{
 let count=0; const observer=new MutationObserver(rs=>count+=rs.length);
 observer.observe(document.documentElement,{attributes:true,subtree:true});
 window.__niyazisugarAdBlockRefreshV276();
 await new Promise(r=>setTimeout(r,500)); observer.disconnect();return count;
 });
 assert.equal(writes,0,'Repeated scan must not rewrite blocked elements');
 console.log(`PASS: ${before} real banners hidden; late tall banner hidden; product/login preserved; no repeated hiding writes.`);
 await browser.close();
})().catch(e=>{console.error(e);process.exit(1)});
