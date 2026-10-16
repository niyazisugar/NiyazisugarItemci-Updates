const {chromium}=require('playwright');const fs=require('fs');const assert=require('assert/strict');
(async()=>{
 const browser=await chromium.launch({headless:true});const page=await browser.newPage();
 await page.addScriptTag({content:fs.readFileSync('overrides/ServerSales.js','utf8')});
 const results=await page.evaluate(()=>{
  const url='https://itemci.com/product/test/item-123';
  const check=html=>window.__NiyaziConfirmedPublicSale(html,url);
  return [
   check('<span>Bu ilan satılmıştır.</span>'),
   check('<a href="/product/test/another"><span>Bu ilan satılmıştır.</span></a>'),
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
 assert.deepEqual(results,[true,false,false,false,false,true,true,false,true,false]);
 console.log('PASS: deleted/missing pages, generic text and out-of-stock excluded; explicit sold marker and matching sold-out product accepted.');
 const realHtml=fs.readFileSync('tests/fixtures/itemci-unavailable-product.html','utf8');
 const publicState=await page.evaluate(html=>({state:window.__NiyaziPublicProductState(html,'https://itemci.com/product/sezar2-itemyang/486m-yang-6ad153435405c'),confirmed:window.__NiyaziConfirmedPublicSale(html,'https://itemci.com/product/sezar2-itemyang/486m-yang-6ad153435405c'),wrong:window.__NiyaziPublicProductState(html,'https://itemci.com/product/sezar2-itemyang/another')}),realHtml);
 assert.equal(publicState.state.available,false);assert.equal(publicState.state.salesCount,384);assert.equal(publicState.state.seller,'ENUYGUNCOM');assert.equal(publicState.state.price,1090);assert.equal(publicState.confirmed,false);assert.equal(publicState.wrong,null);
 console.log('PASS: real public-page metadata, seller count, exact product identity and unavailable is not confirmed sale.');
 // Exercise the actual server scanner, not only its sold-marker helper.
 await page.route('https://itemci.com/**',r=>r.fulfill({contentType:'text/html',body:'<html></html>'}));
 await page.goto('https://itemci.com/categories/msp2-global-yeni-itemyang');
 await page.addScriptTag({content:fs.readFileSync('overrides/ServerSales.js','utf8')});
 await page.addScriptTag({content:fs.readFileSync('overrides/MarketAnalyzer.js','utf8')});
 await page.evaluate(()=>{
  window.calls=[];window.mode='incomplete';
  window.fetch=async url=>{
   const u=new URL(url);window.calls.push(u.href);
   if(u.pathname.startsWith('/product/')){
    if(u.pathname.endsWith('/deleted'))return new Response('<h1>İlan bulunamadı</h1>',{status:404});
    return new Response('<span>Bu ilan satılmıştır.</span>');
   }
   if(window.mode==='limit')return new Response('limit',{status:429});
   if(window.mode==='incomplete')return new Response('<p>Sayfa yüklenemedi</p>');
   const n=Number(u.searchParams.get('page')||1);
   return new Response(`<a href="/product/msp2-global-yeni-itemyang/active-${n}"><h3>100KT YANG</h3><span class="tabular-nums">200,00 TL</span></a><a href="?page=${n+1}">Sonraki</a>`);
  };
 });
 const known=[{url:'https://itemci.com/product/msp2-global-yeni-itemyang/sold',title:'Önceden görülen ilan',price:100,seller:'Test'}, {url:'https://itemci.com/product/msp2-global-yeni-itemyang/deleted',title:'Kaldırılan ilan',price:100}];
 const scan=await page.evaluate(known=>window.__NiyazisugarServerSalesScan('msp2-global-yeni',known,0),known);
 assert.equal(scan.complete,false);assert.equal(scan.sold.length,1);assert.equal(scan.sold[0].title,'Önceden görülen ilan');assert.equal(scan.failed,2);assert.equal(scan.nextCursor,1);
 await page.evaluate(()=>{window.mode='pages';window.calls=[];});
 const paged=await page.evaluate(()=>window.__NiyazisugarServerSalesScan('msp2-global-yeni',[],0,4));
 assert.equal(paged.rows.length,3);assert.equal(paged.nextPage,1);assert.equal(paged.sold.length,3,'Sold markers must be checked even while listing remains in category');
 const pages=await page.evaluate(()=>window.calls.filter(x=>x.includes('/categories/')).map(x=>Number(new URL(x).searchParams.get('page')||1)));
 assert.deepEqual(pages,[1,2,3]);
 await page.evaluate(()=>window.calls=[]);
 const pending=[1,2,3].map(n=>({url:'https://itemci.com/product/msp2-global-yeni-itemyang/missing-'+n,title:'Önceki pencere ilanı',price:100}));
 await page.evaluate(known=>window.__NiyazisugarServerSalesScan('msp2-global-yeni',known,0,38),pending);
 const detailOrder=await page.evaluate(()=>window.calls.filter(x=>x.includes('/product/')));
 assert(detailOrder[0].endsWith('/missing-1'));assert(detailOrder[1].endsWith('/missing-2'));assert(detailOrder[2].includes('/active-'),'Active listing baselines must retain a slot.');
 assert.equal(await page.evaluate(()=>window.calls.some(x=>/[?&]page=(?:4|38)/.test(x))),false);
 await page.evaluate(()=>{window.mode='limit';window.calls=[];});
 const limit=await page.evaluate(known=>window.__NiyazisugarServerSalesScan('msp2-global-yeni',known,0),known);
 assert.equal(limit.rateLimited,true);assert.equal(await page.evaluate(()=>window.calls.length),1);
 console.log('PASS: incomplete pagination still verifies sales, deleted pages excluded, retained category listings checked, bounded page rotation and 429 stops requests.');
 await browser.close();
})().catch(e=>{console.error(e);process.exit(1)});
