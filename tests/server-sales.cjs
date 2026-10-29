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
 const shortHtml=fs.readFileSync('tests/fixtures/itemci-short-lived-unavailable.html','utf8');
 const short=await page.evaluate(html=>window.__NiyaziPublicProductState(html,'https://itemci.com/product/sezar2-itemyang/2t-yang-6ad8a670cd8fa'),shortHtml);
 assert.equal(short.title,'2T YANG');assert.equal(short.price,1100);assert.equal(short.seller,'ENUYGUNCOM');assert.equal(short.available,false);
 assert.equal(short.createdAt,'2026-10-21T14:48:00+03:00');assert.equal(short.updatedAt,'2026-10-21T14:51:07+03:00');
 assert.equal(await page.evaluate(html=>window.__NiyaziConfirmedPublicSale(html,'https://itemci.com/product/sezar2-itemyang/2t-yang-6ad8a670cd8fa'),shortHtml),false);
 const categoryPriority=await page.evaluate(()=>window.__NiyaziSelectWatchQueue([], [{url:'category-only'}], [{url:'category-only',available:false,categoryOnly:true,categorySeenAtUtc:new Date().toISOString()}],true));
 assert.equal(categoryPriority[0].url,'category-only','Category-counter baselines were excluded from detail verification.');
 console.log('PASS: actual missed 2T product identity, price and lifecycle dates parsed; unavailable alone is not sold.');
 const sellerHtml=fs.readFileSync('tests/fixtures/itemci-seller-page.html','utf8');
 const sellerState=await page.evaluate(html=>({valid:window.__NiyaziSellerSnapshot(html,'https://itemci.com/user/store/ENUYGUNCOM'),wrong:window.__NiyaziSellerSnapshot(html,'https://itemci.com/user/store/other-seller')}),sellerHtml);
 assert.equal(sellerState.valid.salesCount,388);assert.equal(sellerState.valid.complete,true);assert(sellerState.valid.products.length>1);assert.equal(sellerState.wrong,null);
 console.log('PASS: actual public seller-page schema provides multiple monitored listings and one shared counter; identities are checked.');
 // Regression from the user's file: 119 active URLs, only 39 baselines and
 // unavailable records with DateTime.MinValue baselines must not consume priority slots.
 const planning=await page.evaluate(()=>{
  const rows=Array.from({length:119},(_,i)=>({url:'https://itemci.com/product/test/'+i}));
  const measured=rows.slice(0,39).map((r,i)=>({...r,available:true,observedAtUtc:new Date(100000+i*1000).toISOString(),lastAvailableAtUtc:new Date(100000+i*1000).toISOString()}));
  const unknown={url:'https://itemci.com/product/test/first-seen-unavailable'};
  measured.push({...unknown,available:false,observedAtUtc:new Date(200000).toISOString(),lastAvailableAtUtc:'0001-01-01T00:00:00'});
  const first=window.__NiyaziSelectWatchQueue(rows,[...rows,unknown],measured,true).map(r=>r.url);
  const failedRetry=window.__NiyaziSelectWatchQueue(rows,[...rows,unknown],measured,true,Object.fromEntries(first.map(u=>[u,new Date(300000).toISOString()]))).map(r=>r.url);
  for(const url of first)measured.push({url,available:true,observedAtUtc:new Date(300000).toISOString(),lastAvailableAtUtc:new Date(300000).toISOString()});
  const second=window.__NiyaziSelectWatchQueue(rows,[...rows,unknown],measured,true).map(r=>r.url);
  const departed={url:'https://itemci.com/product/test/departed'};
  measured.push({...departed,available:true,observedAtUtc:new Date(400000).toISOString(),lastAvailableAtUtc:new Date(400000).toISOString()});
  const priority=window.__NiyaziSelectWatchQueue(rows,[...rows,unknown,departed],measured,true).map(r=>r.url);
  const covered=rows.map(r=>({...r,available:true,observedAtUtc:new Date(400000).toISOString(),lastAvailableAtUtc:new Date(400000).toISOString()}));
  const backlog=Array.from({length:50},(_,i)=>({url:'https://itemci.com/product/test/backlog-'+i}));
  covered.push(...backlog.map(r=>({...r,available:true,observedAtUtc:new Date(200000).toISOString(),lastAvailableAtUtc:new Date(200000).toISOString()})));
  const draining=window.__NiyaziSelectWatchQueue(rows,[...rows,...backlog],covered,true).map(r=>r.url);
  return {first,second,priority,failedRetry,draining};
 });
 assert.deepEqual(planning.first,[39,40,41].map(i=>'https://itemci.com/product/test/'+i));
 assert.deepEqual(planning.failedRetry,[42,43,44].map(i=>'https://itemci.com/product/test/'+i));
 assert.deepEqual(planning.second,[42,43,44].map(i=>'https://itemci.com/product/test/'+i));
 assert.equal(planning.draining.length,3);assert(planning.draining.some(u=>!u.includes('/backlog-')),'Current listings must retain a slot while missing backlog drains.');
 assert(planning.priority[0].endsWith('/departed'));assert(planning.priority.slice(1).every(u=>!planning.first.includes(u)));
 const fairness=await page.evaluate(()=>{
  const live=[{url:'live-a'},{url:'live-b'}],repair=[{url:'old-price'}];
  return {
   one:Array.from({length:10},(_,i)=>window.__NiyaziPlanWatchDetails(live,[],repair,1,i).map(r=>r.url)),
   spare:window.__NiyaziPlanWatchDetails(live,[],repair,2,0).map(r=>r.url),
   normal:window.__NiyaziPlanWatchDetails(live,[],repair,2,1).map(r=>r.url),
   suspects:window.__NiyaziPlanWatchDetails(live,[{url:'just-checked'},{url:'not-checked'}],[],2,0,{'just-checked':new Date().toISOString()}).map(r=>r.url)
  };
 });
 assert(fairness.one.every(urls=>urls[0]==='live-a'),'Old price repairs starved the only live verification slot.');
 assert.deepEqual(fairness.spare,['live-a','old-price']);assert.deepEqual(fairness.normal,['live-a','live-b']);
 assert.deepEqual(fairness.suspects,['not-checked','live-a']);
 const noStarvation=await page.evaluate(()=>{
  const active=[{url:'new'}],known=[{url:'missing'}],measurements=[{url:'missing',available:true,lastAvailableAtUtc:new Date().toISOString()}];
  const attempts={},seen=[];
  for(let i=0;i<4;i++){
   const queue=window.__NiyaziSelectWatchQueue(active,known,measurements,true,attempts);
   const pick=window.__NiyaziPlanWatchDetails(queue,[],[],1,i,attempts)[0];
   seen.push(pick.url);attempts[pick.url]=new Date(Date.now()+i*60000).toISOString();
  }
  return seen;
 });
 assert.deepEqual(noStarvation,['missing','new','missing','new'],'A single detail slot must alternate instead of starving the new listing.');
 console.log('PASS: durable baseline coverage advances without overlaps; unmeasurable inactive records do not starve new checks.');
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
 const known=[{url:'https://itemci.com/product/msp2-global-yeni-itemyang/sold',title:'Önceden görülen ilan',price:100,seller:''}, {url:'https://itemci.com/product/msp2-global-yeni-itemyang/deleted',title:'Kaldırılan ilan',price:100}];
 const scan=await page.evaluate(known=>window.__NiyazisugarServerSalesScan('msp2-global-yeni',known,0),known);
 assert.equal(scan.complete,false);assert.equal(scan.sold.length,1);assert.equal(scan.sold[0].title,'Önceden görülen ilan');assert.equal(scan.failed,2);assert.equal(scan.nextCursor,1);
 await page.evaluate(()=>{window.mode='pages';window.calls=[];});
 const paged=await page.evaluate(()=>window.__NiyazisugarServerSalesScan('msp2-global-yeni',[],0,4));
 assert.equal(paged.rows.length,3);assert.equal(paged.nextPage,1);assert.equal(paged.sold.length,3,'Sold markers must be checked even while listing remains in category');
 const pages=await page.evaluate(()=>window.calls.filter(x=>x.includes('/categories/')).map(x=>Number(new URL(x).searchParams.get('page')||1)));
 assert.deepEqual(pages,[1,2,3]);
 await page.evaluate(()=>window.calls=[]);
 const recorded=[1,2,3].map(n=>'https://itemci.com/product/msp2-global-yeni-itemyang/active-'+n);
 const withoutDuplicates=await page.evaluate(urls=>window.__NiyazisugarServerSalesScan('msp2-global-yeni',[],0,1,[],{},[],[],urls,[{url:'https://itemci.com/product/test/old',title:'750KT YANG',price:350}]),recorded);
 assert.equal(withoutDuplicates.units[0].quantity,750);assert.equal(withoutDuplicates.units[0].unitLabel,'KT');assert(Math.abs(withoutDuplicates.units[0].unitPrice-350/750)<1e-12);
 assert.equal(withoutDuplicates.attemptedUrls.length,0,'Already recorded category listings consumed new sale-check slots.');
 assert.equal(await page.evaluate(()=>window.calls.filter(u=>u.includes('/product/')).length),0);
 await page.evaluate(()=>window.calls=[]);
 const pending=[1,2,3].map(n=>({url:'https://itemci.com/product/msp2-global-yeni-itemyang/missing-'+n,title:'Önceki pencere ilanı',price:100}));
 await page.evaluate(known=>window.__NiyazisugarServerSalesScan('msp2-global-yeni',known,0,38),pending);
 const detailOrder=await page.evaluate(()=>window.calls.filter(x=>x.includes('/product/')));
 assert(detailOrder[0].endsWith('/missing-1'));assert(detailOrder[1].includes('/active-'));assert(detailOrder[2].includes('/active-'),'Active listing baselines must retain a slot.');
 assert.equal(await page.evaluate(()=>window.calls.some(x=>/[?&]page=(?:4|38)/.test(x))),false);
 await page.evaluate(()=>window.calls=[]);
 const repairRow={url:'https://itemci.com/product/msp2-global-yeni-itemyang/price-repair',title:'Existing sale',price:175};
 const repairScan=await page.evaluate(row=>window.__NiyazisugarServerSalesScan('msp2-global-yeni',[],0,1,[],{},[row]),repairRow);
 const repairRequests=await page.evaluate(()=>window.calls.filter(x=>x.includes('/product/')));
 assert.equal(repairRequests.length,3);assert(repairRequests.some(u=>u.endsWith('/price-repair')),'Price repairs should still use spare capacity.');assert(repairRequests[0].includes('/active-'),'Live checks must precede old price repairs.');
 assert.equal(repairScan.attemptedUrls.length,3);
 await page.evaluate(()=>{
  window.calls=[];
  window.fetch=async url=>{
   const u=new URL(url);window.calls.push(u.href);
   if(u.pathname.startsWith('/categories/'))return new Response('<a href="/product/msp2-global-yeni-itemyang/current"><h3>100KT</h3><span class="tabular-nums">160,00 TL</span><span class="truncate font-semibold text-neutral-700">TestSeller</span></a>');
   if(u.pathname.startsWith('/user/store/')){
    const props={user:{id:'owner',username:'TestSeller',total_sales:11},products:[{slug:'current',name:'100KT',price:160,category:{slug:'msp2-global-yeni-itemyang'},sold_at:null}],pagination:{current_page:1,last_page:1}};
    return new Response('<script>self.__next_f.push('+JSON.stringify([1,'1:'+JSON.stringify(['$','$L',null,props])+'\n'])+')</script>');
   }
   return new Response('<h2>Bu ilan satıldı.</h2>');
  };
 });
 const missingRow={url:'https://itemci.com/product/msp2-global-yeni-itemyang/just-sold',title:'Observed listing',price:175,seller:'TestSeller'};
 const batch=await page.evaluate(row=>window.__NiyazisugarServerSalesScan('msp2-global-yeni',[row],0,1,[],{},[],[{url:'https://itemci.com/user/store/TestSeller',sellerId:'owner',seller:'TestSeller',salesCount:10,complete:true,observedAtUtc:new Date(Date.now()-60000).toISOString(),products:[row]}]),missingRow);
 assert.equal(batch.sellerResults.length,1);assert.equal(batch.sellerResults[0].salesCount,11);
 const detailBatch=await page.evaluate(()=>window.calls.filter(x=>x.includes('/product/')));assert(detailBatch[0].endsWith('/just-sold'));
 assert.equal(await page.evaluate(()=>window.calls.filter(x=>!x.includes('/categories/')).length),3,'Seller snapshots and product verification must share the old request budget.');
 assert.equal(batch.sold[0].url,missingRow.url);
 await page.evaluate(()=>{window.mode='limit';window.calls=[];window.fetch=async url=>{window.calls.push(String(url));return new Response('limit',{status:429});};});
 const limit=await page.evaluate(known=>window.__NiyazisugarServerSalesScan('msp2-global-yeni',known,0),known);
 assert.equal(limit.rateLimited,true);assert.equal(await page.evaluate(()=>window.calls.length),1);
 console.log('PASS: incomplete pagination still verifies sales, deleted pages excluded, retained category listings checked, bounded page rotation and 429 stops requests.');
 await browser.close();
})().catch(e=>{console.error(e);process.exit(1)});
