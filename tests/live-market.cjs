const { chromium } = require('playwright');
const fs = require('fs');
const assert = require('assert/strict');
(async()=>{
 const browser=await chromium.launch({headless:true});
 const page=await browser.newPage();
 const source=fs.readFileSync('overrides/MarketAnalyzer.js','utf8').replace('    window.__NiyazisugarLiveListings = async','    window.__testLiveRow = liveListingRow;\n    window.__NiyazisugarLiveListings = async');
 await page.addScriptTag({content:source});
 const exposed=await page.evaluate(()=>JSON.parse(window.__NiyazisugarGetServersJson()));assert(exposed.every(s=>typeof s.url==='string'&&s.url.startsWith('https://itemci.com/categories/')),'desktop server export must retain real category URLs');
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
 const local=await page.evaluate(()=>window.__NiyazisugarWatchUnitRows('msp2-global-yeni',[{title:'700KT 350TL ANINDA TESLİM',price:350}]));
 assert.equal(local[0].quantity,700);assert.equal(local[0].unitPrice,0.5);
 console.log('PASS: real reported title formats including quoted unit rates, adjoining TL and no-network saved-row calculation.');
 console.log('PASS: KT, herb and fruit unit prices; unknown quantities remain unknown.');
 await browser.close();
})().catch(e=>{console.error(e);process.exit(1)});
