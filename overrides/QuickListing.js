(() => {
 'use strict';
 const norm = value => String(value || '').toLocaleLowerCase('tr-TR').normalize('NFD').replace(/[\u0300-\u036f]/g,'').replace(/ı/g,'i').replace(/[–—]/g,'-').replace(/\s+/g,' ').trim();
 const compact = value => norm(value).replace(/[\s&-]/g,'');
 const matches = (value,names) => names.some(name=>compact(value)===compact(name));
 const sleep = ms => new Promise(r=>setTimeout(r,ms));
 const visible = el => {for(let node=el;node&&node!==document.documentElement;node=node.parentElement){const style=getComputedStyle(node);if(style.display==='none'||style.visibility==='hidden'||node.hidden)return false;}return true;};
 const safe = el => !el.closest('header,nav,aside') && !el.matches('[type="submit"]');
 const titleInput = () => document.querySelector('form input[type="text"][required][maxlength="150"]') || [...document.querySelectorAll('input,textarea')].find(el=>visible(el)&&/ilan basligi|baslik|title/.test(norm([el.name,el.placeholder,el.getAttribute('aria-label')].join(' '))));
 async function choose(names, timeout=6500) {
  const end=Date.now()+timeout;
  do {
   for(const select of document.querySelectorAll('select')) {
    if(!visible(select)||!safe(select)) continue;
    const option=[...select.options].find(o=>!o.disabled&&matches(o.textContent,names));
    if(option) {
     Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype,'value').set.call(select,option.value);
     select.dispatchEvent(new Event('input',{bubbles:true}));select.dispatchEvent(new Event('change',{bubbles:true}));
     await sleep(400);return true;
    }
   }
   const nodes=[...document.querySelectorAll('button,[role="button"],[role="option"],label,h2,h3,h4,span,p,div')];
   const item=nodes.find(el=>visible(el)&&safe(el)&&matches(el.textContent,names));
   if(item) {
    let target=item.closest('button,[role="button"],[role="option"],label');
    if(!target) {
     for(let parent=item,i=0;parent&&i<5;parent=parent.parentElement,i++) {
      if(parent.classList.contains('cursor-pointer')&&safe(parent)) {target=parent;break;}
     }
    }
    if(target&&safe(target)) {target.scrollIntoView({block:'center'});target.click();await sleep(400);return true;}
   }
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
  const step=!serverSelected?'sunucu':!categorySelected?'Item&Yang kategorisi':'ilan detay formu';
  const options=[...document.querySelectorAll('button,[role="button"],[role="option"],h2,h3')].filter(el=>visible(el)&&safe(el)).map(el=>String(el.textContent||'').replace(/\s+/g,' ').trim()).filter(t=>t&&t.length<100).slice(0,18);
  return {ok:false,message:server+' · '+step+' seçimi tamamlanamadı. Görünen seçenekler: '+[...new Set(options)].join(' | ')};
 };
})();
