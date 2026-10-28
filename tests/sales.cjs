const { chromium } = require('playwright');
const fs = require('fs');
const assert = require('assert/strict');
(async () => {
 const browser = await chromium.launch({headless:true});
 const page = await browser.newPage();
 await page.setContent(`<table><tbody>
 <tr><td><h3>250KT YANG</h3><span>İlan No: 29902744045</span><span>01.10.2026</span></td><td>MSP2 Global</td><td>500,00 TL</td><td>SATILDI</td><td><button data-index="0"><svg class="lucide lucide-file-text"></svg></button><button><svg class="lucide lucide-trash-2"></svg></button></td></tr>
 <tr><td><h3>50 ADET LUSİFER SANDIĞI</h3><span>İlan No: 918862320239</span></td><td>Sezar2</td><td>1.250,50 TL</td><td>Satıldı</td><td><button data-index="1"><svg class="lucide lucide-file-text"></svg></button></td></tr>
 <tr><td><h3>İptal ürün</h3><span>İlan No: 30000000001</span></td><td>Başka sunucu</td><td>900,00 TL</td><td>Satıldı</td><td><button data-index="2"><svg class="lucide lucide-file-text"></svg></button></td></tr>
 <tr><td><h3>Okunamayan</h3><span>İlan No: 30000000002</span></td><td>Sezar2</td><td>700,00 TL</td><td>Satıldı</td><td><button><svg class="lucide lucide-trash-2"></svg></button></td></tr>
 </tbody></table>`);
 await page.evaluate(() => {
 window.opens=0; window.deletes=0;
 document.querySelectorAll('svg.lucide-trash-2').forEach(el=>el.parentElement.onclick=()=>window.deletes++);
 document.querySelectorAll('button[data-index]').forEach(button=>button.onclick=()=>{
 window.opens++;
 const index=Number(button.dataset.index);
 const modal=document.createElement('div');modal.id='modal';modal.innerHTML=`<h2>İlan & Sipariş Detayı</h2><div>Sipariş ID: ${['01A1380A','01A1380B','01A1380C'][index]}</div><div>Ürün Bilgisi</div><div>Alıcı Bilgileri</div><div>Sipariş Tarihi ${index===1?'13.10.2026 23:59':'14.10.2026 04:50'}</div><div>Fiyat ${index===1?'1.250,50':'500,00'} TL</div><div>DURUM ${index===2?'İADE EDİLDİ':'TAMAMLANDI'} TESLİMAT BİLGİLERİ</div><button>Kapat</button>`;
 modal.querySelector('button').onclick=()=>modal.remove();document.body.append(modal);
 });
 });
 await page.addScriptTag({content:fs.readFileSync('overrides/SoldListings.js','utf8')});
 const result=await page.evaluate(()=>window.__niyaziScanSoldListings());
 assert.equal(result.Sales.length,2);
 assert.deepEqual(result.Sales[0],{OrderId:'01A1380A',ListingId:'29902744045',ItemTitle:'250KT YANG',ImageUrl:'',Server:'Bilinmeyen sunucu',Date:'14.10.2026',Time:'04:50',Price:'500,00',IsDelivered:true});
 assert.equal(result.Sales[1].Price,'1.250,50');
 assert.equal(result.Sales[1].Date,'13.10.2026');
 assert.equal(result.Errors.length,1);
 assert.equal(await page.evaluate(()=>window.deletes),0);
 await page.evaluate(()=>window.opens=0);
 const again=await page.evaluate(()=>window.__niyaziScanSoldListings(['29902744045','918862320239','30000000001']));
 assert.equal(again.Sales.length,0);assert.equal(await page.evaluate(()=>window.opens),0);
 await page.evaluate(()=>window.opens=0);
 const slice1=await page.evaluate(()=>window.__niyaziScanSoldListings([],0,{maxDetails:1}));
 assert.equal(slice1.PageComplete,false);assert.deepEqual(slice1.ProcessedIds,['29902744045']);
 const slice2=await page.evaluate(ids=>window.__niyaziScanSoldListings([],0,{maxDetails:1,skipIds:ids}),slice1.ProcessedIds);
 assert.equal(slice2.Sales[0].OrderId,'01A1380B');assert.equal(slice2.PageComplete,false);
 const slice3=await page.evaluate(ids=>window.__niyaziScanSoldListings([],0,{maxDetails:3,skipIds:ids}),[...slice1.ProcessedIds,...slice2.ProcessedIds]);
 assert.equal(slice3.PageComplete,true);assert.equal(slice3.Sales.length,0);assert.equal(slice3.Errors.length,1);
 assert.equal(await page.evaluate(()=>window.opens),3,'Resuming a page reopened already processed orders.');
 console.log('PASS: bounded slices resume without duplicate opens; refunds and failed rows cannot trap the cursor.');
 await page.setContent('<div>Henüz ilanınız yok</div>');
 assert.equal((await page.evaluate(()=>window.__niyaziScanSoldListings())).Empty,true);
 console.log('PASS: multiple servers, actual order timestamp instead of listing date, Turkish labels/prices, refunds excluded, safe detail clicks, known IDs skipped, missing details reported, empty list.');
 await browser.close();
})().catch(e=>{console.error(e);process.exit(1)});
