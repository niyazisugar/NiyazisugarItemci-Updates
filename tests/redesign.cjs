const {chromium}=require('playwright');const fs=require('fs');const assert=require('assert/strict');
(async()=>{
 const browser=await chromium.launch({headless:true});const page=await browser.newPage({viewport:{width:1400,height:950}});
 await page.route('https://itemci.com/**',r=>r.fulfill({contentType:'text/html',body:`<html><body><main><p>Mesajlar</p><form><textarea placeholder="Mesajınızı yazın"></textarea><button type="button" id="send">Gönder</button></form><script>window.sent=0;document.getElementById('send').onclick=()=>sent++;</script></main></body></html>`}));await page.goto('https://itemci.com/panel/mesajlar');
 await page.addScriptTag({content:fs.readFileSync('overrides/QuickReplyDock.js','utf8')});await page.locator('#niyazi-quick-replies').waitFor();
 await page.locator('#niyazi-quick-replies button').first().click();assert.equal(await page.locator('textarea').inputValue(),'CH1 depocudayım, bekliyorum.');assert.equal(await page.evaluate(()=>window.sent),0);
 const strip=await page.locator('#niyazi-quick-replies').boundingBox(),input=await page.locator('textarea').boundingBox();assert(strip.y+strip.height<=input.y);
 await page.locator('#send').click();assert.equal(await page.evaluate(()=>window.sent),1);
 await page.evaluate(()=>localStorage.setItem('niyazi_quick_replies_custom_v1',JSON.stringify([{id:'custom',short:'Kaydım',text:'Türkçe kayıtlı cevap'}])));await page.reload();await page.addScriptTag({content:fs.readFileSync('overrides/QuickReplyDock.js','utf8')});await page.getByRole('button',{name:'Kaydım',exact:true}).click();assert.equal(await page.locator('textarea').inputValue(),'Türkçe kayıtlı cevap');assert.equal(await page.evaluate(()=>window.sent),0);
 // Report checks operate on isolated fixtures; no sample listing enters the app.
 await page.goto('https://itemci.com/categories/msp2-global-yeni-item-yang');
 let source=fs.readFileSync('overrides/MarketAnalyzer.js','utf8').replace('    window.__NiyazisugarRunMarket = runAnalysis;','    window.__testReport=showReport; window.__testCategory=categoryResult; window.__testServer=()=>loadServers()[0];\n    window.__NiyazisugarRunMarket = runAnalysis;');await page.addScriptTag({content:source});
 await page.evaluate(()=>{window.__niyaziVerifiedOwnStoreUrl='https://itemci.com/user/store/my-account';window.__testReport([
 {title:'100KT YANG',price:200,seller:'Satıcı',sellerUrl:'https://itemci.com/user/store/another-account',time:'1 saat önce',ageHours:1,url:'https://itemci.com/product/a',suspiciousText:false},
 {title:'100KT YANG',price:300,seller:'Hesabım',sellerUrl:window.__niyaziVerifiedOwnStoreUrl,time:'2 saat önce',ageHours:2,url:'https://itemci.com/product/b',suspiciousText:false}
 ],1,2,window.__testServer());});
 assert(await page.locator('#itemci-v17 .lowest-unit').count()>0);assert(await page.locator('#itemci-v17 .own-label').count()>0);
 assert.equal(await page.locator('#itemci-v17 tr[data-seller-url="https://itemci.com/user/store/another-account"] .own-label').count(),0);
 assert.equal(await page.locator('#itemci-v17 .stat').filter({hasText:'Medyan'}).count(),0);
 const outliers=await page.evaluate(()=>window.__testCategory([1,2,100].map((price,i)=>({title:'1KT YANG',price,ageHours:i===2?null:1,suspiciousText:false})),{id:'yang',name:'Yang',mode:'token',tokens:['KT'],keywords:[],unitLabel:'KT'}));assert.equal(outliers.valid.length,3,'outlier and unknown date must not silently hide listings');
 await page.setViewportSize({width:1796,height:390});
 const firstRow=await page.locator('#itemci-v17 .view.active tbody tr').first().boundingBox();assert(firstRow&&firstRow.y+firstRow.height<=390,'first result must be visible in the reported short viewport');
 await page.addScriptTag({content:fs.readFileSync('overrides/AdBlock.js','utf8')});await page.waitForTimeout(5500);
 assert(await page.locator('#itemci-v17 .view.active tbody tr').first().isVisible(),'ad blocker must preserve the analysis table');
 fs.mkdirSync('test-results',{recursive:true});await page.screenshot({path:'test-results/market-analysis-short.png',fullPage:false});
 await page.setViewportSize({width:1400,height:950});
 await page.locator('#itemci-v17 .search').first().fill('eşleşmeyen');assert.equal(await page.locator('#itemci-v17 tbody').first().locator('tr:visible').count(),0);
 await browser.close();console.log('PASS: persistent saved replies, no automatic send, input placement, report minimum, verified ownership and filtering.');
})().catch(e=>{console.error(e);process.exit(1);});
