const {chromium}=require('playwright');const fs=require('fs');const assert=require('assert/strict');
(async()=>{
 const browser=await chromium.launch({headless:true});const page=await browser.newPage();
 await page.addScriptTag({content:fs.readFileSync('overrides/ServerSales.js','utf8')});
 const results=await page.evaluate(()=>{
  const url='https://itemci.com/product/test/item-123';
  const check=html=>window.__NiyaziConfirmedPublicSale(html,url);
  return [
   check('<h1>İlan bulunamadı</h1>'),
   check('<h1>Bu ilan kaldırılmıştır</h1>'),
   check('<p>Benzer ilan satıldı</p>'),
   check('<h2>Bu ilan satılmıştır.</h2>'),
   check('<div role="alert">Bu ürün satıldı</div>'),
   check('<script type="application/ld+json">{"@type":"Product","offers":{"availability":"https://schema.org/OutOfStock"}}</script>'),
   check('<script type="application/ld+json">{"@type":"Product","url":"https://itemci.com/product/test/item-123","offers":{"availability":"https://schema.org/SoldOut"}}</script>'),
   check('<script type="application/ld+json">{"@type":"Product","url":"https://itemci.com/product/test/another","offers":{"availability":"https://schema.org/SoldOut"}}</script>')
  ];
 });
 assert.deepEqual(results,[false,false,false,true,true,false,true,false]);
 console.log('PASS: deleted/missing pages, generic text and out-of-stock excluded; explicit sold marker and matching sold-out product accepted.');
 await browser.close();
})().catch(e=>{console.error(e);process.exit(1)});
