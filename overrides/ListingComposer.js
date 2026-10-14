(() => {
 'use strict';
 const sleep=ms=>new Promise(r=>setTimeout(r,ms));
 const norm=s=>String(s||'').toLocaleLowerCase('tr-TR').normalize('NFD').replace(/[\u0300-\u036f]/g,'').replace(/ı/g,'i').replace(/\s+/g,' ').trim();
 function set(el,value){if(!el)throw new Error('İlan alanı bulunamadı.');Object.getOwnPropertyDescriptor(el instanceof HTMLSelectElement?HTMLSelectElement.prototype:HTMLInputElement.prototype,'value').set.call(el,String(value));el.dispatchEvent(new Event('input',{bubbles:true}));el.dispatchEvent(new Event('change',{bubbles:true}));}
 function controls(){const form=[...document.forms].find(f=>f.querySelector('input[type="text"][maxlength="150"]'));if(!form)throw new Error('Itemci ilan formu bulunamadı. Oturumunu kontrol et.');return {form,title:form.querySelector('input[type="text"][maxlength="150"]'),price:form.querySelector('input[type="number"][required]'),delivery:[...form.querySelectorAll('select')].find(s=>[...s.options].some(o=>o.value==='12'&&norm(o.textContent).includes('saat'))),validity:[...form.querySelectorAll('select')].find(s=>[...s.options].some(o=>o.value==='30'&&norm(o.textContent).includes('gun'))),editor:form.querySelector('.ql-editor'),toggle:form.querySelector('[role="switch"]#sellByUs'),file:form.querySelector('input[type="file"]'),submit:form.querySelector('button[type="submit"]')};}
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
  if(p.stock){const button=c.form.querySelector('button img[alt="yang"]')?.closest('button');if(!button)throw new Error('Stok Yang görseli bulunamadı.');button.click();}
  else {
   if(!p.images?.length||p.images.length>4||!c.file)throw new Error('1–4 görsel seçmelisin.');
   const transfer=new DataTransfer();
   for(const image of p.images){const bytes=Uint8Array.from(atob(image.base64),c=>c.charCodeAt(0));if(bytes.length>5*1024*1024)throw new Error('Görsel 5 MB sınırını aşıyor.');transfer.items.add(new File([bytes],image.name,{type:image.type}));}
   c.file.files=transfer.files;c.file.dispatchEvent(new Event('change',{bubbles:true}));
  }
  const expected=p.stock?1:p.images.length;let uploaded=false;
  for(let attempt=0;attempt<240;attempt++){const count=norm(c.form.innerText).match(/yuklenen resimler\s*\(\s*(\d+)\s*\/\s*4\s*\)/);if(count&&Number(count[1])===expected){uploaded=true;break;}await sleep(200);}
  if(!uploaded)throw new Error('Görsel yüklemesi doğrulanamadı; ilan gönderilmedi.');
  await sleep(300);
  if(c.title.value!==p.title||Number(c.price.value)!==p.price||c.delivery.value!==String(p.delivery)||c.validity.value!==String(p.validity)||(c.toggle.getAttribute('aria-checked')==='true')!==p.orders||norm(c.editor.innerText)!==norm(p.description))throw new Error('Form alanları doğrulanamadı; ilan gönderilmedi.');
  return {ok:true};
 };
 window.__NiyaziSubmitOwnListing=async p=>{
  const c=controls();if(!c.form.reportValidity())throw new Error('Itemci formunda eksik zorunlu alan var.');
  const original=window.fetch;let outcome=null;let sent=false;
  window.fetch=async function(input,options){
   const response=await original.apply(this,arguments);
   try{
    const method=options?.method||input?.method||'GET';const body=options?.body;
    let data=null;if(typeof body==='string')try{data=JSON.parse(body)}catch{}
    if(body instanceof FormData)data=Object.fromEntries(body.entries());
    const fields=data?.data||data;
    if(method.toUpperCase()==='POST'&&fields&&(fields.title===p.title||fields.name===p.title)&&Number(fields.price)===p.price){
     sent=true;const json=await response.clone().json();
     if(!response.ok||json.status===false||json.success===false)outcome={ok:false,message:json.message||'Itemci ilanı kabul etmedi.'};
     else if(json.status===true||json.success===true){
      const product=json.data?.product||json.data||json.product||{};
      const url=product.url||product.link||'';
      outcome={ok:true,id:String(product.id||product.product_id||''),url:/^https:\/\/itemci\.com\/product\//.test(url)?url:'',message:json.message||'İlan oluşturuldu.'};
     }
    }
   }catch{}return response;
  };
  try{
   c.form.requestSubmit(c.submit);
   for(let attempt=0;attempt<300;attempt++){
    if(outcome)return outcome;
    const body=norm(document.body.innerText);
    if(/ilan(?:iniz)? basariyla (?:olusturuldu|eklendi|yayinlandi)/.test(body))return {ok:true,message:'Itemci ilanın oluşturulduğunu bildirdi.',id:'',url:''};
    await sleep(200);
   }
   return {ok:false,uncertain:true,message:sent?'Gönderim sonucu doğrulanamadı. Tekrar yayınlamadan önce İlanlarım bölümünü kontrol et.':'Yayınlama sonucu doğrulanamadı. Tekrar yayınlamadan önce İlanlarım bölümünü kontrol et.'};
  }finally{window.fetch=original;}
 };
})();
