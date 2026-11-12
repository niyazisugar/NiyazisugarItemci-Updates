const {chromium}=require('playwright');const fs=require('fs');const assert=require('assert/strict');
// Takip turu: mağaza sayfası okunmaz (artık ham HTML'de veri yok), 8 ürün sayfası kontrol edilir,
// kaybolan ilanlar önce gelir; ürün sayfasından stok da okunur.
(async()=>{
 const browser=await chromium.launch({headless:true});const page=await browser.newPage();
 await page.route('https://itemci.com/**',r=>r.fulfill({contentType:'text/html',body:'<html></html>'}));
 await page.goto('https://itemci.com/categories/osi2-itemyang');
 await page.evaluate(()=>{localStorage.setItem('itemci_osi2_migration_v1','done');localStorage.setItem('itemci_v15_servers',JSON.stringify([{id:'osi2',name:'OSİ2',url:'https://itemci.com/categories/osi2-itemyang',template:'global-120'}]));});
 await page.addScriptTag({content:fs.readFileSync('src/NiyazisugarItemci/Web/ServerSales.js','utf8')});
 await page.addScriptTag({content:fs.readFileSync('src/NiyazisugarItemci/Web/MarketAnalyzer.js','utf8')});
 const product=(slug,available,stock,count)=>{const props={product:{type:'listing',slug,name:'Test '+slug,price:50,stock,is_available:available,sub_category:{slug:'osi2-itemyang'},owner_of:{id:'owner-1',username:'Seller1',total_sales:count},created_at:'2026-11-12T10:00:00+03:00',updated_at:'2026-11-12T11:00:00+03:00',expires_at:'2026-11-27T10:00:00+03:00'}};return '<script>self.__next_f.push('+JSON.stringify([1,'1:'+JSON.stringify(['$','$L',null,props])+'\n'])+')</script>';};
 const state=await page.evaluate(html=>window.__NiyaziPublicProductState(html,'https://itemci.com/product/osi2-itemyang/stocked'),product('stocked',true,4,77));
 assert.equal(state.stock,4);assert.equal(state.salesCount,77);assert.equal(state.available,true);
 await page.exposeFunction('productHtml',slug=>product(slug,!slug.startsWith('missing'),1,10));
 await page.evaluate(()=>{
  window.calls=[];
  window.fetch=async url=>{
   const u=new URL(url);window.calls.push(u.href);
   if(u.pathname.startsWith('/product/'))return new Response(await window.productHtml(u.pathname.split('/').pop()));
   if(u.pathname.startsWith('/user/store/'))return new Response('<html>client rendered</html>');
   const n=Number(u.searchParams.get('page')||1);
   let cards='';for(let i=0;i<5;i++)cards+=`<a href="/product/osi2-itemyang/active-${n}-${i}"><h3>100KT YANG</h3><span class="tabular-nums">200,00 TL</span><span class="truncate font-semibold text-neutral-700">Seller${i}</span></a>`;
   return new Response(cards+`<script type="application/json">{"current_page":${n},"last_page":10}</script>`);
  };
 });
 const now=new Date().toISOString();
 const known=Array.from({length:6},(_,i)=>({url:'https://itemci.com/product/osi2-itemyang/missing-'+i,title:'Kaybolan '+i,price:100,seller:'Seller'+i}));
 const measurements=known.map(r=>({url:r.url,available:true,sellerId:'owner-1',salesCount:9,lastAvailableAtUtc:now,observedAtUtc:now}));
 const scan=await page.evaluate(([known,measurements])=>window.__NiyazisugarServerSalesScan('osi2',known,0,1,measurements,{},[],[],[],[],{detailBudget:8,stores:false}),[known,measurements]);
 const calls=await page.evaluate(()=>window.calls);
 const productCalls=calls.filter(u=>u.includes('/product/')),storeCalls=calls.filter(u=>u.includes('/user/store/'));
 assert.equal(storeCalls.length,0,'Store pages must not be requested when stores=false.');
 assert.equal(productCalls.length,8,'Eight product pages should be checked per turn.');
 assert(productCalls.slice(0,4).every(u=>u.includes('/missing-')),'Disappeared listings must be checked first.');
 assert.equal(scan.observations.length,8);
 // Varsayılan (eski) çağrı 3 kontrolde kalır.
 await page.evaluate(()=>window.calls=[]);
 await page.evaluate(([known,measurements])=>window.__NiyazisugarServerSalesScan('osi2',known,0,1,measurements),[known,measurements]);
 assert.equal((await page.evaluate(()=>window.calls)).filter(u=>u.includes('/product/')).length<=3,true);
 await browser.close();
 console.log('PASS: watch turn skips client-rendered store pages, checks 8 products with disappeared listings first, and reads product stock.');
})().catch(e=>{console.error(e);process.exit(1)});
