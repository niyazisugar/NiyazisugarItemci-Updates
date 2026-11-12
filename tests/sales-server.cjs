const {chromium}=require('playwright');const fs=require('fs');const assert=require('assert/strict');
// Itemci Satılanlar tablosu: sunucu "Kategori" sütununda, "Osi2" / "Osi2 Item&Yang".
const row=(id,title,server)=>`<tr><td><input type="checkbox"></td><td><div><a href="/product/osi2-itemyang/${id}"><h3>${title}</h3></a><span>İlan No: ${id}</span><span>11.11.2026</span></div></td><td><div><span>${server}</span><span>${server} Item&amp;Yang</span></div></td><td>1.500,00 TL</td><td>Satıldı</td><td><button>Detay</button></td></tr>`;
(async()=>{
 const browser=await chromium.launch({headless:true});const page=await browser.newPage();
 await page.route('https://itemci.com/**',r=>r.fulfill({contentType:'text/html',body:`<meta charset="utf-8"><main><table><thead><tr><th></th><th>İlan Bilgisi</th><th>Kategori</th><th>Fiyat</th><th>Durum</th><th>İşlemler</th></tr></thead><tbody>${row('921927118390','45KT + 275 MİTRİL','Osi2')}${row('134123915965','4M DERECE MEYVESİ','Mitril2')}</tbody></table></main>`}));
 await page.goto('https://itemci.com/panel/ilanlarim?page=1&status=4');
 await page.addScriptTag({content:fs.readFileSync('src/NiyazisugarItemci/Web/SoldListings.js','utf8')});
 // Bilinen satışlar ayrıntı açılmadan geçilir; sunucuları yine de liste satırından döner.
 const result=await page.evaluate(()=>window.__niyaziScanSoldListings(['921927118390','134123915965'],0,{}));
 assert.equal(result.Sales.length,0);
 assert.deepEqual(result.ListingServers,{'921927118390':'Osi2','134123915965':'Mitril2'});
 // Hızlı tamamlama: ayrıntı açmadan ilan no + başlık + sunucu okunur.
 const quick=await page.evaluate(()=>window.__niyaziReadSoldServers());
 assert.equal(quick.Empty,false);
 assert.deepEqual(quick.Rows,[{Id:'921927118390',Title:'45KT + 275 MİTRİL',Server:'Osi2'},{Id:'134123915965',Title:'4M DERECE MEYVESİ',Server:'Mitril2'}]);
 await browser.close();
 console.log('PASS: sold listing server read from the Kategori column (first value), returned for every row so older sales are filled in.');
})().catch(e=>{console.error(e);process.exit(1)});
