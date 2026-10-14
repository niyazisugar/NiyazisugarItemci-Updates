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
 console.log('PASS: KT, herb and fruit unit prices; unknown quantities remain unknown.');
 await browser.close();
})().catch(e=>{console.error(e);process.exit(1)});
