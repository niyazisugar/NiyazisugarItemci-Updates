const {chromium}=require('playwright');const fs=require('fs');const assert=require('assert/strict');
// Mesajlar: son mesajı "25" olan okunmuş konuşma 25 okunmamış sayılmamalı; gerçek rozet (1) sayılmalı.
const src=fs.readFileSync('src/NiyazisugarItemci/App/MainForm.cs','utf8');
const start=src.indexOf('var inboxScript = $$"""')+'var inboxScript = $$"""'.length;const script=src.slice(start,src.indexOf('""";',start)).replace('{{globalUnread}}','0');
const row=(name,preview,read,badge)=>`<button style="display:flex;width:420px;height:72px;margin:8px 0"><div><img alt="" src="data:image/gif;base64,R0lGODlhAQABAAAAACw="></div><div style="flex:1"><div><h4 class="text-sm font-black truncate">${name}</h4><span class="text-[10px]">1 saat önce</span></div><div class="flex items-center justify-between gap-2"><p class="text-xs truncate ${read?'text-neutral-500 font-medium':'text-neutral-800 font-black'}">${preview}</p>${badge?`<span class="flex h-4 min-w-[16px] items-center justify-center rounded-full">${badge}</span>`:''}</div></div></button>`;
(async()=>{
 const browser=await chromium.launch({headless:true});const page=await browser.newPage({viewport:{width:1200,height:900}});
 await page.setContent(`<meta charset="utf-8"><title>Mesajlar - itemci.com</title><nav style="height:200px">Mesajlar</nav><main>${row('milaat43','25',true,0)}${row('mehmet778','25',true,0)}${row('spark','tamamdır geliyorum',false,1)}</main>`);
 const result=JSON.parse(await page.evaluate(script));
 assert.equal(result.count,1,'Only the real unread badge should count.');
 assert.deepEqual(result.conversations.map(c=>[c.name,c.unread]),[['spark',1]]);
 await page.setContent(`<meta charset="utf-8"><title>Mesajlar - itemci.com</title><nav style="height:200px">Mesajlar</nav><main>${row('milaat43','25',true,0)}${row('mehmet778','12',true,0)}</main>`);
 const none=JSON.parse(await page.evaluate(script));
 assert.equal(none.count,0,'Read conversations whose last message is a number must not count as unread.');
 await browser.close();
 console.log('PASS: unread count comes only from badges; numeric message previews like "25" are not unread counts.');
})().catch(e=>{console.error(e);process.exit(1)});
