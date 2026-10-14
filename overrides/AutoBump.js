(() => {
 'use strict';
 const clean=s=>String(s||'').replace(/\s+/g,' ').trim();
 const norm=s=>clean(s).toLocaleLowerCase('tr-TR').normalize('NFD').replace(/[\u0300-\u036f]/g,'').replace(/ı/g,'i');
 const sleep=ms=>new Promise(r=>setTimeout(r,ms));
 function ownPage(){if(location.hostname!=='itemci.com'||location.pathname.replace(/\/$/,'')!=='/panel/ilanlarim')throw new Error('Otomatik yukarı taşıma için Itemci hesabındaki İlanlarım sayfası gerekli.');}
 function visible(el){if(!el?.isConnected)return false;for(let p=el;p;p=p.parentElement){const s=getComputedStyle(p);if(p.hidden||s.display==='none'||s.visibility==='hidden')return false;}return true;}
 function meta(el){const ids=(el.getAttribute('aria-describedby')||'').split(/\s+/).filter(Boolean);return norm([el.textContent,el.title,el.getAttribute('aria-label'),el.getAttribute('data-tooltip-content'),el.getAttribute('data-tooltip'),...ids.map(id=>document.getElementById(id)?.textContent||'')].join(' '));}
 function freeButton(row){return [...row.querySelectorAll('button,a[role="button"]')].find(b=>{const t=meta(b);return /\bilani\s+yukari\s+tasi\b/.test(t)&&/\bucretsiz\b/.test(t)&&!/(?:ucretli|vitrin|vip|one cikar|bakiye|\d\s*(?:tl|₺))/.test(t);});}
 function enabled(b){if(!visible(b)||b.disabled||b.hasAttribute('disabled')||b.getAttribute('aria-disabled')==='true'||b.hasAttribute('data-disabled'))return false;for(let p=b;p;p=p.parentElement)if(getComputedStyle(p).pointerEvents==='none')return false;return true;}
 function rows(){
  const found=new Map();
  for(const node of document.querySelectorAll('span,small,p,div,td')){
   const id=norm(node.innerText).match(/^ilan\s*no\s*:?\s*(\d+)$/)?.[1];if(!id||found.has(id))continue;
   for(let parent=node.parentElement,depth=0;parent&&depth<9;parent=parent.parentElement,depth++){
    const text=norm(parent.innerText);if((text.match(/ilan\s*no\s*:?\s*\d+/g)||[]).length>1)break;
    const active=[...parent.querySelectorAll('span,div,small,p,[data-status]')].some(el=>norm(el.textContent)==='satista');
    if(!active)continue;
    const button=freeButton(parent);if(button){found.set(id,{id,el:parent,button});break;}
   }
  }
  return [...found.values()];
 }
 function waitSeconds(button){
  const t=meta(button);let m=t.match(/\b(\d{1,2}):(\d{2})(?::(\d{2}))?\b/);
  if(m)return m[3]!==undefined?Number(m[1])*3600+Number(m[2])*60+Number(m[3]):Number(m[1])*60+Number(m[2]);
  let seconds=0;for(const match of t.matchAll(/(\d+)\s*(saat|dakika|saniye)/g))seconds+=Number(match[1])*({saat:3600,dakika:60,saniye:1}[match[2]]);
  return Math.min(86400,seconds);
 }
 function nextPage(){
  const current=Number(new URL(location.href).searchParams.get('page')||1);
  for(const link of document.querySelectorAll('a[href]')){try{const u=new URL(link.href);if(u.hostname==='itemci.com'&&u.pathname==='/panel/ilanlarim'&&Number(u.searchParams.get('page'))===current+1)return true;}catch{}}
  return [...document.querySelectorAll('button')].some(b=>enabled(b)&&[b.textContent,b.getAttribute('aria-label'),b.title].some(value=>/^(?:sonraki(?: sayfa)?|next(?: page)?)$/.test(norm(value))));
 }
 window.__niyaziReadBumpPage=async()=>{
  ownPage();
  for(let attempt=0;attempt<100;attempt++){
   const found=rows();if(found.length)return {listings:found.map(row=>({id:row.id,enabled:enabled(row.button),waitSeconds:waitSeconds(row.button)})),hasNext:nextPage(),empty:false};
   const text=norm(document.body.innerText);
   if(/ilan(?:iniz)? (?:yok|bulunamadi)|ilaniniz bulunmuyor|kayit bulunamadi/.test(text))return {listings:[],hasNext:nextPage(),empty:true};
   // A page containing only sold/inactive listings has nothing to bump.
   if(attempt>=10&&/ilan\s*no\s*:?\s*\d+/.test(text))return {listings:[],hasNext:nextPage(),empty:false,unsupported:!/(?:satildi|pasif|suresi doldu)/.test(text)};
   await sleep(100);
  }
  throw new Error('İlan listesi veya ücretsiz yukarı taşıma düğmesi okunamadı. Oturumunu ve İlanlarım sayfasını kontrol et.');
 };
 function requestMatches(input,options,id){
  const method=String(options?.method||input?.method||'GET').toUpperCase();if(!['POST','PATCH','PUT'].includes(method))return false;
  try{const u=new URL(typeof input==='string'?input:input.url,location.href);if(u.hostname!=='itemci.com')return false;if(u.pathname.split('/').includes(id))return true;}catch{return false;}
  let body=options?.body;
  if(typeof body==='string')try{body=JSON.parse(body);}catch{return false;}
  if(body instanceof FormData||body instanceof URLSearchParams)body=Object.fromEntries(body.entries());
  const values=body?.data||body;return ['id','product_id','productId','listing_id','listingId'].some(key=>String(values?.[key]||'')===id);
 }
 function retryAfter(response,json){const value=Number(response.headers.get('Retry-After')||json.retryAfterSeconds||json.retry_after);if(value>0&&Number.isFinite(value))return Math.min(86400,Math.ceil(value));const date=Date.parse(response.headers.get('Retry-After')||'');return Number.isFinite(date)?Math.max(0,Math.ceil((date-Date.now())/1000)):response.status===429?3600:0;}
 function notices(){return [...document.querySelectorAll('[role="alert"],[role="status"],[data-sonner-toast],.Toastify__toast,[class*="toast"]')].filter(visible);}
 let running=false;
 window.__niyaziBumpOne=async id=>{
  ownPage();if(running)return {ok:false,message:'Önceki yukarı taşıma devam ediyor.',notSent:true};
  const row=rows().find(r=>r.id===String(id));if(!row||!enabled(row.button))return {ok:false,notSent:true,message:'Ücretsiz düğme henüz aktif değil.',retryAfterSeconds:row?waitSeconds(row.button):0};
  running=true;const original=window.fetch;let outcome=null,sent=false;
  const existing=new Set(notices());
  window.fetch=async function(input,options){
   const matched=requestMatches(input,options,String(id));if(matched)sent=true;
   let response;try{response=await original.apply(this,arguments);}catch(error){if(matched)outcome={ok:false,uncertain:true,message:'Bağlantı kesildi; yukarı taşımanın sonucu doğrulanamadı.'};throw error;}
   if(matched){try{let json={};try{json=await response.clone().json();}catch{}
    if(!response.ok||json?.status===false||json?.success===false)outcome={ok:false,message:typeof json?.message==='string'?json.message:'Itemci yukarı taşımayı kabul etmedi.',retryAfterSeconds:retryAfter(response,json||{})};
    else if(json?.status===true||json?.success===true)outcome={ok:true,message:'İlan ücretsiz yukarı taşındı.'};
   }catch{}}
   return response;
  };
  try{
   // Recheck immediately before the only action. Never fall back to an icon,
   // paid boost, delete/edit button, or a guessed endpoint.
   const fresh=rows().find(r=>r.id===String(id));if(!fresh||!enabled(fresh.button))return {ok:false,notSent:true,message:'Ücretsiz düğme aktif değil.'};
   fresh.button.click();
   let confirmedDialog=false;
   for(let attempt=0;attempt<100;attempt++){
    if(outcome)return outcome;
    if(!confirmedDialog){
     const dialog=[...document.querySelectorAll('[role="dialog"]')].find(el=>{const t=norm(el.innerText);return visible(el)&&t.includes('ilan')&&t.includes('yukari')&&!/ucret(?!siz)|vip|vitrin|bakiye|\d[\d.,]*\s*(?:tl|₺)/.test(t);});
     const confirm=dialog&&[...dialog.querySelectorAll('button')].find(b=>enabled(b)&&/^(?:evet|onayla|(?:ucretsiz )?(?:ilani )?yukari tasi)$/.test(norm(b.textContent)));
     if(confirm){confirmedDialog=true;confirm.click();}
    }
    if(notices().some(el=>!existing.has(el)&&/(?:ilan(?:iniz)?(?: basariyla)? yukari (?:tasindi|cikarildi)|yukari tasima basarili)/.test(norm(el.textContent))))return {ok:true,message:'Itemci yukarı taşımayı doğruladı.'};
    const current=rows().find(r=>r.id===String(id));if(attempt>=10&&current&&!enabled(current.button)&&waitSeconds(current.button)>=60)return {ok:true,message:'Itemci yeni bekleme süresini gösterdi.'};
    await sleep(100);
   }
   return {ok:false,uncertain:true,message:sent?'Yukarı taşıma sonucu doğrulanamadı.':'Düğmeye basıldı; işlem sonucu doğrulanamadı.'};
  }finally{window.fetch=original;running=false;}
 };
})();
