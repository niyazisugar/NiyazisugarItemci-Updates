(() => {
 'use strict';
 const norm = value => String(value || '').toLocaleLowerCase('tr-TR').normalize('NFD').replace(/[\u0300-\u036f]/g,'').replace(/ı/g,'i').replace(/[–—]/g,'-').replace(/\s+/g,' ').trim();
 const sleep = ms => new Promise(r=>setTimeout(r,ms));
 const visible = el => {const r=el.getBoundingClientRect();return r.width>0 && r.height>0 && getComputedStyle(el).visibility!=='hidden';};
 const safe = el => !el.closest('header,nav,aside') && !el.matches('[type="submit"]');
 const titleInput = () => [...document.querySelectorAll('input,textarea')].find(el=>visible(el)&&/ilan basligi|baslik|title/.test(norm([el.name,el.placeholder,el.getAttribute('aria-label')].join(' '))));
 async function choose(names, timeout=6500) {
  const wanted=names.map(norm);const end=Date.now()+timeout;
  do {
   for(const select of document.querySelectorAll('select')) {
    if(!visible(select)||!safe(select)) continue;
    const option=[...select.options].find(o=>!o.disabled&&wanted.includes(norm(o.textContent)));
    if(option) {
     Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype,'value').set.call(select,option.value);
     select.dispatchEvent(new Event('input',{bubbles:true}));select.dispatchEvent(new Event('change',{bubbles:true}));
     await sleep(400);return true;
    }
   }
   const item=[...document.querySelectorAll('button,[role="button"],[role="option"],label')]
    .find(el=>visible(el)&&safe(el)&&wanted.includes(norm(el.textContent)));
   if(item) {item.scrollIntoView({block:'center'});item.click();await sleep(400);return true;}
   await sleep(150);
  } while(Date.now()<end);
  return false;
 }
 window.__NiyaziQuickListing = async server => {
  if(!/^\/panel\/ilan-(?:ekle|olustur|ver)(?:\/|$)/.test(location.pathname)) return {ok:false,message:'İlan ekleme formu hazır değil. Itemci hesabına giriş yap.'};
  // Preserve an existing draft. Never submit, reset or replace typed listing fields.
  if(titleInput()?.value.trim()) return {ok:false,draft:true,message:'Mevcut taslağın korundu; sunucu ve kategori değiştirilmedi.'};
  let serverSelected=await choose([server],1000);
  if(!serverSelected) {
   await choose(['Metin2 PvP Serverler','Metin2 PVP'],2500);
   const search=[...document.querySelectorAll('input')].find(el=>visible(el)&&/sunucu.*ara|server.*ara/.test(norm(el.placeholder)));
   if(search) {
    Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(search,server);
    search.dispatchEvent(new Event('input',{bubbles:true}));
   }
   serverSelected=await choose([server],9000);
  }
  const categorySelected=serverSelected&&await choose([server+' Item&Yang',server+' Item & Yang','Item&Yang','Item & Yang','İtem&Yang'],9000);
  const end=Date.now()+5000;while(!titleInput()&&Date.now()<end) await sleep(150);
  if(serverSelected&&categorySelected&&titleInput()) {titleInput().focus();return {ok:true,message:server+' · Sunucu ve kategori seçildi. İlanını doldurabilirsin.'};}
  return {ok:false,message:server+' · Form açıldı; sunucu ve kategori seçimini kontrol et.'};
 };
})();
