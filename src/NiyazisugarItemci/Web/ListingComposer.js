(() => {
 'use strict';
 const sleep=ms=>new Promise(r=>setTimeout(r,ms));
 const norm=s=>String(s||'').toLocaleLowerCase('tr-TR').normalize('NFD').replace(/[\u0300-\u036f]/g,'').replace(/ı/g,'i').replace(/\s+/g,' ').trim();
 function set(el,value){if(!el)throw new Error('İlan alanı bulunamadı.');Object.getOwnPropertyDescriptor(el instanceof HTMLSelectElement?HTMLSelectElement.prototype:HTMLInputElement.prototype,'value').set.call(el,String(value));el.dispatchEvent(new Event('input',{bubbles:true}));el.dispatchEvent(new Event('change',{bubbles:true}));}
 function controls(){const form=[...document.forms].find(f=>f.querySelector('input[type="text"][maxlength="150"]'));if(!form)throw new Error('Itemci ilan formu bulunamadı. Oturumunu kontrol et.');return {form,title:form.querySelector('input[type="text"][maxlength="150"]'),price:form.querySelector('input[type="number"][required]'),delivery:[...form.querySelectorAll('select')].find(s=>[...s.options].some(o=>o.value==='12'&&norm(o.textContent).includes('saat'))),validity:[...form.querySelectorAll('select')].find(s=>[...s.options].some(o=>o.value==='30'&&norm(o.textContent).includes('gun'))),editor:form.querySelector('.ql-editor'),toggle:form.querySelector('[role="switch"]#sellByUs'),file:form.querySelector('input[type="file"]'),submit:form.querySelector('button[type="submit"]')};}

 // Do not depend on one translated heading or assume the site processes all
 // files from a single change event. Only accept a count or a server-returned
 // image URL that also appears in this form's preview.
 function imageCount(form){
  const text=norm(form.innerText);
  const count=text.match(/(?:yuklenen|eklenen)\s*(?:resim(?:ler)?|gorsel(?:ler)?|fotograf(?:lar)?)\s*[:(]?\s*(\d+)\s*\/\s*4\b/)||
   text.match(/(?:uploaded|selected)\s*(?:images|photos)\s*[:(]?\s*(\d+)\s*\/\s*4\b/);
  return count&&Number(count[1])<=4?Number(count[1]):null;
 }
 function imageKey(value){
  try{const url=new URL(value,location.href);return /^https?:$/.test(url.protocol)?url.origin+url.pathname:'';}catch{return '';}
 }
 function responseImages(value,key='',depth=0){
  if(depth>8||value==null)return [];
  if(typeof value==='string')return /(?:url|src|path|image|location)/i.test(key)||/\.(?:png|jpe?g|webp)(?:[?#]|$)/i.test(value)?[imageKey(value)].filter(Boolean):[];
  if(typeof value!=='object')return [];
  return Object.entries(value).flatMap(([name,child])=>responseImages(child,name,depth+1));
 }
 async function prepareImages(c,p){
  const expected=p.stock?1:p.images?.length;
  if(!p.stock&&(!expected||expected>4||!c.file))throw new Error('1–4 görsel seçmelisin.');
  const deadline=Date.now()+48000;
  const original=window.fetch,accepted=new Set();let uploadError='';
  const previousAlerts=new Set([...document.querySelectorAll('[role="alert"],[data-sonner-toast],.Toastify__toast')].map(el=>norm(el.innerText)));
  if(!p.stock)window.fetch=async function(input,options){
   const body=options?.body;
   const fileUpload=body instanceof File||body instanceof Blob||(body instanceof FormData&&[...body.values()].some(v=>v instanceof Blob));
   let response;
   try{response=await original.apply(this,arguments);}catch(error){if(fileUpload)uploadError='Görsel aktarılırken bağlantı kesildi.';throw error;}
   if(fileUpload){
    let json={};try{json=await response.clone().json()||{};}catch{}
    if(!response.ok||json.status===false||json.success===false||typeof json.error==='string'&&json.error)
     uploadError=platformMessage(json,response.status);
    else for(const url of responseImages(json))accepted.add(url);
   }
   return response;
  };
  const waitForCount=async count=>{
   while(Date.now()<deadline){
    if(Date.now()<(window.__niyaziSitePauseUntil||0))throw new Error('Itemci işlem sınırı nedeniyle görsel yüklemesi durdu; ilan gönderilmedi. Bekleme süresi bittikten sonra tekrar dene.');
    if(uploadError)throw new Error('Görsel yüklenemedi: '+uploadError+' İlan gönderilmedi.');
    for(const el of document.querySelectorAll('[role="alert"],[data-sonner-toast],.Toastify__toast')){
     const text=norm(el.innerText);
     if(el.getClientRects().length&&!previousAlerts.has(text)&&/(?:gorsel|resim|fotograf|dosya|image|file)/.test(text)&&/(?:hata|basarisiz|yuklenem|buyuk|boyut|sinir|gecersiz|error|failed|large|invalid)/.test(text))
      throw new Error('Itemci görseli kabul etmedi: '+String(el.innerText).trim()+' İlan gönderilmedi.');
    }
    const current=controls().form;
    const previewKeys=new Set([...current.querySelectorAll('img[src]')].map(img=>imageKey(img.getAttribute('src'))));
    const confirmed=[...accepted].filter(url=>previewKeys.has(url)).length;
    if(imageCount(current)===count||confirmed===count)return;
    await sleep(200);
   }
   throw new Error('Görsel yüklemesi doğrulanamadı; ilan gönderilmedi. '+(accepted.size?'Itemci yüklemeye yanıt verdi ancak formdaki görsel önizlemesi doğrulanamadı.':'Itemci görsel yüklemesinin tamamlandığını göstermedi. Bağlantını ve görsel dosyasını kontrol et.'));
  };
  try{
   if(p.stock){
    const button=c.form.querySelector('button img[alt="yang"]')?.closest('button');
    if(!button)throw new Error('Stok Yang görseli bulunamadı.');
    button.click();await waitForCount(1);
   }else{
    for(let index=0;index<p.images.length;index++){
     const image=p.images[index],bytes=Uint8Array.from(atob(image.base64),c=>c.charCodeAt(0));
     if(!bytes.length||bytes.length>5*1024*1024)throw new Error('Görsel boş veya 5 MB sınırını aşıyor.');
     const file=controls().file;if(!file)throw new Error('Itemci görsel yükleme alanı bulunamadı; ilan gönderilmedi.');
     const transfer=new DataTransfer();transfer.items.add(new File([bytes],image.name,{type:image.type}));
     file.value='';file.files=transfer.files;
     file.dispatchEvent(new Event('input',{bubbles:true}));
     file.dispatchEvent(new Event('change',{bubbles:true}));
     await waitForCount(index+1);
    }
   }
  }finally{if(!p.stock)window.fetch=original;}
 }
 window.__NiyaziPrepareOwnListing=async p=>{
  if(!p.title?.trim()||p.title.length>150||!Number.isInteger(p.price)||p.price<1||String(p.description||'').length>1200)throw new Error('Başlık, fiyat veya açıklama geçersiz.');
  const selected=await window.__NiyaziQuickListing(p.server);
  if(!selected.ok)throw new Error(selected.message);
  const c=controls();set(c.title,p.title);set(c.price,p.price);set(c.delivery,p.delivery);set(c.validity,p.validity);
  if(!c.editor||!c.toggle)throw new Error('Açıklama veya sipariş alanı bulunamadı.');
  const quill=c.editor.parentElement?.__quill||window.Quill?.find?.(c.editor.parentElement);
  if(quill?.setText)quill.setText(p.description,'user');
  else {c.editor.focus();const range=document.createRange();range.selectNodeContents(c.editor);const selection=getSelection();selection.removeAllRanges();selection.addRange(range);if(!document.execCommand('insertText',false,p.description))throw new Error('Açıklama editörü doldurulamadı.');}
  if((c.toggle.getAttribute('aria-checked')==='true')!==p.orders)c.toggle.click();
  await prepareImages(c,p);
  await sleep(300);
  if(c.title.value!==p.title||Number(c.price.value)!==p.price||c.delivery.value!==String(p.delivery)||c.validity.value!==String(p.validity)||(c.toggle.getAttribute('aria-checked')==='true')!==p.orders||norm(c.editor.innerText)!==norm(p.description))throw new Error('Form alanları doğrulanamadı; ilan gönderilmedi.');
  return {ok:true};
 };
 function safeProductLink(product,p){
  let candidate=product.url||product.link||'';
  if(!candidate&&product.slug){const category=product.category?.slug||new URL(p.categoryUrl||location.href).pathname.match(/^\/categories\/([^/]+)/)?.[1];if(category)candidate='/product/'+encodeURIComponent(decodeURIComponent(category))+'/'+encodeURIComponent(product.slug);}
  try{const u=new URL(candidate,location.origin);return candidate&&u.protocol==='https:'&&u.hostname==='itemci.com'&&!u.username&&!u.password&&!u.port&&u.pathname.startsWith('/product/')?u.href:'';}catch{return '';}
 }
 function retrySeconds(response,json){
  const raw=response.headers.get('Retry-After');const number=Number(raw||json.retryAfterSeconds||json.retry_after||json.retryAfter);
  if(Number.isFinite(number)&&number>0)return Math.min(86400,Math.ceil(number));
  const date=raw?Date.parse(raw):NaN;if(Number.isFinite(date))return Math.min(86400,Math.max(0,Math.ceil((date-Date.now())/1000)));
  const match=String(json.message||'').match(/(\d+)\s*(saniye|dakika)/i);if(match)return Math.min(86400,Number(match[1])*(match[2].toLowerCase()==='dakika'?60:1));
  return response.status===429?60:0;
 }
 function platformMessage(json,status){
  let message=typeof json.message==='string'?json.message:typeof json.error==='string'?json.error:'';
  if(!message&&json.errors){message=Object.values(json.errors).flat().filter(x=>typeof x==='string').join(' ');}
  const normalized=norm(message);
  if(status===429||/too many|rate limit|please wait|once.*minute/.test(normalized))return 'Itemci yayınlama sınırına ulaşıldı. Bekleme süresi bittikten sonra tekrar deneyebilirsin.';
  if(status===401||/unauthenticated|unauthorized/.test(normalized))return 'Itemci oturumun sona ermiş. Itemci bölümünden giriş yapıp tekrar dene.';
  if(status===403)return 'Itemci bu işlemi kabul etmedi. Hesap izinlerini ve oturumunu kontrol et.';
  if(status>=500)return 'Itemci sunucusu şu anda yanıt veremiyor. Biraz sonra tekrar deneyebilirsin.';
  return message||'Itemci ilanı kabul etmedi. Form alanlarını kontrol et.';
 }
 let publishing=false;
 window.__NiyaziSubmitOwnListing=async p=>{
  if(publishing)return {ok:false,message:'Bu ilan zaten gönderiliyor. Sonucu bekle.'};
  const c=controls();if(!c.form.reportValidity())throw new Error('Itemci formunda eksik zorunlu alan var.');
  publishing=true;
  const original=window.fetch;let outcome=null;let sent=false;
  window.fetch=async function(input,options){
   const method=options?.method||input?.method||'GET',body=options?.body;
   let data=null;if(typeof body==='string')try{data=JSON.parse(body)}catch{}
   if(body instanceof FormData)data=Object.fromEntries(body.entries());
   const fields=data?.data||data;
   const matched=method.toUpperCase()==='POST'&&fields&&(fields.title===p.title||fields.name===p.title)&&Number(fields.price)===p.price;
   if(matched)sent=true;
   let response;
   try{response=await original.apply(this,arguments);}catch(error){if(matched)outcome={ok:false,uncertain:true,message:'Bağlantı kesildi; gönderimin sonucu doğrulanamadı. Tekrar yayınlamadan önce İlanlarım bölümünü kontrol et.'};throw error;}
   if(matched){
    try{
     let json={};try{json=await response.clone().json();}catch{}
     if(!response.ok||json.status===false||json.success===false)outcome={ok:false,message:platformMessage(json,response.status),retryAfterSeconds:retrySeconds(response,json)};
     else if(json.status===true||json.success===true){const product=json.data?.product||json.data||json.product||{};outcome={ok:true,id:String(product.id||product.product_id||''),url:safeProductLink(product,p),message:'İlan yayınlandı.'};}
    }catch{}
   }
   return response;
  };
  try{
   c.form.requestSubmit(c.submit);
   for(let attempt=0;attempt<300;attempt++){
    if(outcome)return outcome;
    const body=norm(document.body.innerText);
    if(/ilan(?:iniz)? basariyla (?:olusturuldu|eklendi|yayinlandi)/.test(body)){
     const link=[...document.querySelectorAll('a[href]')].find(a=>/ilani (?:gor|incele|ac)/.test(norm(a.textContent))&&safeProductLink({url:a.href},p));
     return {ok:true,message:'İlan yayınlandı.',id:'',url:link?.href||''};
    }
    await sleep(200);
   }
   return {ok:false,uncertain:true,message:sent?'Gönderim sonucu doğrulanamadı. Tekrar yayınlamadan önce İlanlarım bölümünü kontrol et.':'Yayınlama sonucu doğrulanamadı. Tekrar yayınlamadan önce İlanlarım bölümünü kontrol et.'};
  }finally{window.fetch=original;publishing=false;}
 };
})();
