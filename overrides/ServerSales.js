(() => {
 'use strict';
 const text = s => String(s || '').toLocaleLowerCase('tr-TR').normalize('NFD').replace(/[\u0300-\u036f]/g,'').replace(/ı/g,'i').replace(/\s+/g,' ').trim();
 function confirmedSale(html, url) {
  const doc = new DOMParser().parseFromString(html, 'text/html');
  const expected = new URL(url).pathname.replace(/\/$/,'');
  let confirmed = false;
  for (const script of doc.querySelectorAll('script[type="application/ld+json"]')) {
   try {
    const data = JSON.parse(script.textContent);
    const nodes = Array.isArray(data) ? data : [data, ...(data['@graph'] || [])];
    for (const node of nodes) {
     if (node['@type'] !== 'Product') continue;
     if (node.url && new URL(node.url,url).pathname.replace(/\/$/,'') !== expected) continue;
     const offers = Array.isArray(node.offers) ? node.offers : [node.offers];
     if (offers.some(o => /(?:^|\/)SoldOut$/.test(o?.availability || ''))) confirmed = true;
    }
   } catch {}
  }
  // Exact product notices only. No inference from a 404 or disappearing card.
  for (const el of doc.querySelectorAll('h1,h2,h3,p,span,div,[role="alert"]')) {
   if (el.closest('script,style,nav,aside')) continue;
   const link=el.closest('a[href]');
   if(link){try{if(new URL(link.getAttribute('href'),url).pathname.replace(/\/$/,'')!==expected)continue;}catch{continue;}}
   if (/^(?:bu (?:ilan|urun) (?:satildi|satilmistir)|ilan satildi|urun satildi)[.!]?$/.test(text(el.textContent))) confirmed = true;
  }
  return confirmed;
 }
 // Decode only JSON strings in Next.js flight frames; never execute site scripts.
 function productState(html,url){
  const doc=new DOMParser().parseFromString(html,'text/html');
  const expected=new URL(url);let flight='';
  for(const script of doc.querySelectorAll('script:not([src])')){
   const match=script.textContent.match(/self\.__next_f\.push\((\[[\s\S]*\])\)\s*;?\s*$/);
   if(!match)continue;
   try{const frame=JSON.parse(match[1]);if(frame[0]===1&&typeof frame[1]==='string')flight+=frame[1];}catch{}
  }
  let found=null;
  function visit(node,depth=0){
   if(!node||typeof node!=='object'||depth>40)return;
   const p=node.product;
   if(p&&typeof p==='object'&&p.type==='listing'&&p.slug&&p.sub_category?.slug){
    const path='/product/'+p.sub_category.slug+'/'+p.slug;
    const owner=p.owner_of;
    if(path===expected.pathname.replace(/\/$/,'')&&owner?.id&&typeof p.is_available==='boolean'){
     const count=owner.total_sales??owner.successful_sales??owner.sales_count;
     if(Number.isSafeInteger(count)&&count>=0)found={url:expected.href,available:p.is_available,sellerId:owner.id,seller:owner.username||'',salesCount:count,title:p.name||'',price:Number(p.price)||0,expiresAt:p.expires_at||''};
    }
   }
   for(const value of Object.values(node))if(value&&typeof value==='object')visit(value,depth+1);
  }
  for(const line of flight.split('\n')){
   const colon=line.indexOf(':');if(colon<1)continue;
   try{visit(JSON.parse(line.slice(colon+1)));}catch{}
  }
  return found;
 }
 window.__NiyaziPublicProductState=productState;
 window.__NiyaziConfirmedPublicSale = confirmedSale;
})();
