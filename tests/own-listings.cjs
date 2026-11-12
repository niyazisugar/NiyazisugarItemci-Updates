const {chromium}=require('playwright');const fs=require('fs');const assert=require('assert/strict');
// Itemci > İlanlarım (Satışta) tablosunun gerçek yapısına göre fixture.
const row=(title,href,price,server,img)=>`<tr><td><span><input type="checkbox" aria-label="${title} ilanını seç"></span></td><td><div><div><img alt="${title}" src="${img}"></div><div><a title="${title}" href="${href}"><h3>${title}</h3></a><span>İlan No: 541030474589</span><div><span>11.11.2026</span><span>Vitrinde · 12 saat 59 dk</span></div></div></div></td><td><div><span>${server}</span><span>${server} Item&amp;Yang</span></div></td><td>${price}</td><td>Satışta</td><td><button>Düzenle</button></td></tr>`;
(async()=>{
 const browser=await chromium.launch({headless:true});const page=await browser.newPage();
 let body='';
 await page.route('https://itemci.com/**',r=>r.fulfill({contentType:'text/html',body}));
 body=`<meta charset="utf-8"><main><h1>İlanlarım</h1><p>TOPLAM 3 İLAN</p><table><thead><tr><th>İlan</th></tr></thead><tbody>
 ${row('3000 MİTRİL','/product/osi2-itemyang/3000-mitril-6af48a5cbdb60','4.500,00 TL','Osi2','https://assets.itemci.com/a.png')}
 ${row('STOK BİTTİ','/product/osi2-itemyang/50-kt-6af48964dcd74','1.025,00 TL','Osi2','https://assets.itemci.com/b.png')}
 ${row('YANG-MİTRİL-DERECE MEYVESİ-COR- PASİF SET MESAJ AT','/product/osi2-itemyang/yang-6aec6681407e5','999,00 TL','Osi2','https://assets.itemci.com/c.png')}
 </tbody></table></main>`;
 await page.goto('https://itemci.com/panel/ilanlarim?page=1&status=1');
 await page.addScriptTag({content:fs.readFileSync('src/NiyazisugarItemci/Web/OwnListings.js','utf8')});
 const result=await page.evaluate(()=>window.__niyaziReadOwnActiveListings(3000));
 assert.equal(result.total,3);assert.equal(result.rows.length,3);
 assert.deepEqual(result.rows[0],{title:'3000 MİTRİL',url:'https://itemci.com/product/osi2-itemyang/3000-mitril-6af48a5cbdb60',price:'4.500,00 TL',server:'Osi2',image:'https://assets.itemci.com/a.png',number:'541030474589'});
 assert.equal(result.rows[2].title,'YANG-MİTRİL-DERECE MEYVESİ-COR- PASİF SET MESAJ AT','a title containing PASİF is still an active listing');
 body=`<meta charset="utf-8"><main><h1>İlanlarım</h1><p>TOPLAM 0 İLAN</p><table><tbody></tbody></table></main>`;
 await page.goto('https://itemci.com/panel/ilanlarim?page=1&status=1');await page.addScriptTag({content:fs.readFileSync('src/NiyazisugarItemci/Web/OwnListings.js','utf8')});
 const empty=await page.evaluate(()=>window.__niyaziReadOwnActiveListings(3000));assert.deepEqual(empty,{total:0,rows:[]});
 // Sayfa geç çizilirse beklenir.
 body=`<meta charset="utf-8"><main id="m"></main><script>setTimeout(()=>{document.getElementById('m').innerHTML='<p>TOPLAM 1 İLAN</p><table><tbody>${row('Geç gelen','/product/osi2-itemyang/late-1','10,00 TL','Osi2','https://assets.itemci.com/d.png').replace(/'/g,"\\'")}</tbody></table>'},700)</script>`;
 await page.goto('https://itemci.com/panel/ilanlarim?page=1&status=1');await page.addScriptTag({content:fs.readFileSync('src/NiyazisugarItemci/Web/OwnListings.js','utf8')});
 const late=await page.evaluate(()=>window.__niyaziReadOwnActiveListings(5000));assert.equal(late.rows.length,1);assert.equal(late.rows[0].title,'Geç gelen');
 body='<meta charset="utf-8"><main>Giriş Yap</main>';
 await page.goto('https://itemci.com/user/login');await page.addScriptTag({content:fs.readFileSync('src/NiyazisugarItemci/Web/OwnListings.js','utf8')});
 await assert.rejects(page.evaluate(()=>window.__niyaziReadOwnActiveListings(2000)),/oturumu kapalı/);
 await browser.close();
 console.log('PASS: own active listings read from the rendered İlanlarım table: title, link, price, server, image; empty list, late render and logged-out page.');
})().catch(e=>{console.error(e);process.exit(1)});
