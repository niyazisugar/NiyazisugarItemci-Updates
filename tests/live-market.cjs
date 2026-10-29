const { chromium } = require('playwright');
const fs = require('fs');
const assert = require('assert/strict');
(async()=>{
 const browser=await chromium.launch({headless:true});
 const page=await browser.newPage();
 await page.route('https://itemci.com/**',r=>r.fulfill({contentType:'text/html; charset=utf-8',body:'<html><body></body></html>'}));await page.goto('https://itemci.com/categories/test');
 const source=fs.readFileSync('overrides/MarketAnalyzer.js','utf8').replace('    window.__NiyazisugarLiveListings = async','    window.__testLiveRow = liveListingRow;\n    window.__NiyazisugarLiveListings = async');
 await page.evaluate(()=>localStorage.setItem('itemci_v15_servers',JSON.stringify([
 {id:'msp2-global-yeni',name:'MSP2 Global - Yeni',url:'https://itemci.com/categories/msp2-global-yeni-itemyang'},
 {id:'custom-sezar',name:'Sezar 2',url:'https://itemci.com/categories/sezar2'},
 {id:'keep',name:'Diğer',url:'https://itemci.com/categories/other',categories:[]}
 ])));
 await page.addScriptTag({content:source});
 const exposed=await page.evaluate(()=>JSON.parse(window.__NiyazisugarGetServersJson()));assert(exposed.every(s=>typeof s.url==='string'&&s.url.startsWith('https://itemci.com/categories/')),'desktop server export must retain real category URLs');
 assert.deepEqual(exposed.map(s=>s.id),['osi2','keep','barbaros2']);
 assert.equal(exposed[0].name,'OSİ2');assert.equal(exposed[0].url,'https://itemci.com/categories/osi2');
 const mitril=await page.evaluate(()=>window.__NiyazisugarWatchUnitRows('osi2',['100 mitril','100 Mitril','100 mıtrıl','100 MİTRİL','Mıtrıl 100 adet','100 soluk kır otu','100 ot'].map(title=>({title,price:200}))));
 assert.deepEqual(mitril.map(r=>r.quantity),[100,100,100,100,100,null,null]);assert.equal(mitril[0].unitPrice,2);assert.equal(mitril[0].unitLabel,'MİTRİL');
 const persisted=await page.evaluate(()=>JSON.parse(localStorage.getItem('itemci_v15_servers')));assert.deepEqual(persisted.map(s=>s.id),['osi2','keep','barbaros2']);
 console.log('PASS: persisted MSP/Sezar migration, unrelated servers retained, Mitril Turkish spelling variants and per-item pricing.');
 const barbaros=exposed.find(s=>s.id==='barbaros2');assert.equal(barbaros.url,'https://itemci.com/categories/barbaros2');assert.equal(barbaros.categoryCount,1);
 const barbarosUnits=await page.evaluate(()=>window.__NiyazisugarWatchUnitRows('barbaros2',['500M YANG','1,5M yang','Yang 500K','100 mitril','100KT YANG'].map(title=>({title,price:1000}))));
 assert.deepEqual(barbarosUnits.map(r=>r.quantity),[500,1.5,0.5,null,null]);assert.equal(barbarosUnits[0].unitLabel,'M');assert.equal(barbarosUnits[0].unitPrice,2);
 assert.equal((await page.evaluate(()=>JSON.parse(window.__NiyazisugarGetServersJson()))).filter(s=>s.id==='barbaros2').length,1);
 const tUnits=await page.evaluate(()=>window.__NiyazisugarWatchUnitRows('barbaros2',['1T yang','2t YANG','1,5T yang','1T 300M yang','1t300m yang','1.000M yang','500K yang','1T veya 500M yang','1T / 2T yang','1 tane yang','1M Derece Meyvesi'].map(title=>({title,price:2600}))));
 assert.deepEqual(tUnits.map(r=>r.quantity),[1000,2000,1500,1300,1300,1000,0.5,null,null,null,null]);assert.equal(tUnits[3].unitPrice,2);assert.equal(tUnits[3].unitLabel,'M');
 console.log('PASS: Barbaros2 legacy stored category supports 1T=1000M, compound/decimal amounts and common market/watch pricing.');
 console.log('PASS: Barbaros2 added once with Yang-only magnitude pricing, no OSI KT/Mitril profile leakage.');
 const result=await page.evaluate(()=>{
 const server={id:'test',name:'Test',url:'https://itemci.com/categories/test',categories:[
 {id:'yang',name:'Yang',mode:'token',tokens:['KT'],keywords:[],unitLabel:'KT'},
 {id:'ot',name:'Soluk',mode:'token',tokens:['soluk kır otu','soluk'],keywords:[],unitLabel:'OT'},
 {id:'meyve',name:'Derece',mode:'magnitude',tokens:[],keywords:['derece meyvesi'],unitLabel:'M'}]};
 return ['250KT YANG','100 Soluk Kır Otu','2M Derece Meyvesi','Miktarı belirsiz ürün'].map((title,i)=>window.__testLiveRow({title,price:[500,1000,600,500][i]},server));
 });
 assert.equal(result[0].quantity,250);assert.equal(result[0].unitPrice,2);assert.equal(result[0].unitLabel,'KT');
 assert.equal(result[1].unitPrice,10);assert.equal(result[1].unitLabel,'OT');
 assert.equal(result[2].unitPrice,300);assert.equal(result[2].unitLabel,'M');
 assert.equal(result[3].unitPrice,null);
 const actualTitles=await page.evaluate(()=>{
 const server={id:'test',categories:[{id:'yang',name:'Yang',mode:'token',tokens:['KT'],keywords:[],unitLabel:'KT'},{id:'ot',name:'Ot',mode:'token',tokens:['soluk kır','ot'],keywords:[],unitLabel:'OT'}]};
 return ['ANINDA TESLİM 500KT 0.50DEN SON KTLER','700KT 350TL ANINDA TESLİM','2000 Soluk kır','468KT 250 TL HEMENN','(1KT=0.52Krş) 677KT Yang Hemen Teslimat.','500kt250tl !!!!!!','426 KT YANG ONLINE','1400 Ot H.o','320 KT VURAN HESABIN İTEMLERİ SATILIR'].map(title=>window.__testLiveRow({title,price:350},server));
 });
 assert.deepEqual(actualTitles.map(r=>r.quantity),[500,700,2000,468,677,500,426,1400,null]);
 await page.evaluate(()=>{window.fetch=()=>{throw Error('Unit calculation must not use network');};});
 const local=await page.evaluate(()=>window.__NiyazisugarWatchUnitRows('osi2',[{title:'700KT 350TL ANINDA TESLİM',price:350}]));
 assert.equal(local[0].quantity,700);assert.equal(local[0].unitPrice,0.5);
 console.log('PASS: real reported title formats including quoted unit rates, adjoining TL and no-network saved-row calculation.');
 const globalRows=await page.evaluate(()=>{
  const server={id:'global-test',template:'global-120',categories:[{id:'yang',name:'Yang',mode:'token',tokens:['KT'],keywords:[],unitLabel:'KT'}]};
  return ['1KT','100KT','1000KT','1S','1 S 300 KT','1s300kt','2s 500kt','1,5S','1.5S','1.000KT','SANDIK 300 ADET','1 saat teslim','1S / 2S','1S veya 300KT','1S + 300KT'].map(title=>window.__testLiveRow({title,price:2600},server));
 });
 assert.deepEqual(globalRows.map(r=>r.quantity),[1,100,1000,1000,1300,1300,2500,1500,1500,1000,null,null,null,null,1300]);
 assert.equal(globalRows[4].unitPrice,2);assert.equal(globalRows[4].unitLabel,'KT');
 const migrated=await page.evaluate(()=>window.__NiyazisugarWatchUnitRows('osi2',[{title:'1S 300KT',price:2600}]));assert.equal(migrated[0].quantity,1300);assert.equal(migrated[0].unitPrice,2);
 await page.evaluate(()=>{window.answers=['Test Global','https://itemci.com/categories/test-global-itemyang'];window.prompt=()=>window.answers.shift()??null;window.__NiyazisugarAddServer();});
 await page.getByLabel('Sunucu şablonu').waitFor();assert.equal(await page.getByLabel('Sunucu şablonu').inputValue(),'global-120');await page.getByRole('button',{name:'Şablonu kullan',exact:true}).click();
 const saved=await page.evaluate(()=>JSON.parse(localStorage.getItem('itemci_v15_servers')).find(s=>s.name==='Test Global'));assert.equal(saved.template,'global-120');assert.equal(saved.categories.length,3);assert.equal(saved.categories.find(c=>c.unitLabel==='KT').quantityStyle,'global-120');
 const customUnit=await page.evaluate(id=>window.__NiyazisugarWatchUnitRows(id,[{title:'1S 300KT',price:2600}]),saved.id);assert.equal(customUnit[0].quantity,1300);
 console.log('PASS: 1-120 Global template persists with all MSP categories, old MSP migrates, S/KT compound and decimal units normalize to KT; unrelated S words and alternatives excluded.');
 console.log('PASS: KT, herb and fruit unit prices; unknown quantities remain unknown.');
 await browser.close();
})().catch(e=>{console.error(e);process.exit(1)});
