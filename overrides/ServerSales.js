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
  for (const el of doc.querySelectorAll('h1,h2,h3,[role="alert"]')) {
   if (/^(?:bu (?:ilan|urun) (?:satildi|satilmistir)|ilan satildi|urun satildi)[.!]?$/.test(text(el.textContent))) confirmed = true;
  }
  return confirmed;
 }
 window.__NiyaziConfirmedPublicSale = confirmedSale;
})();
