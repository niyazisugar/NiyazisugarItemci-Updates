(() => {
 'use strict';
 // Itemci > İlanlarım (Satışta) tablosunu, sayfa tarayıcıda çizildikten sonra okur.
 // Yalnızca okur: hiçbir düğmeye basmaz, hiçbir ilanı değiştirmez.
 const sleep = ms => new Promise(r => setTimeout(r, ms));
 const clean = value => String(value || '').replace(/\s+/g, ' ').trim();
 function readRow(row) {
  const link = row.querySelector('a[href*="/product/"]');
  if (!link) return null;
  let url = '';
  try {
   const u = new URL(link.getAttribute('href'), location.origin);
   if (u.hostname !== 'itemci.com' || !u.pathname.startsWith('/product/')) return null;
   url = u.origin + u.pathname;
  } catch { return null; }
  const title = clean(link.getAttribute('title') || link.querySelector('h3,h4')?.textContent || link.textContent);
  const price = (row.innerText.match(/(\d{1,3}(?:\.\d{3})*(?:,\d{2})?)\s*TL/) || [])[1] || '';
  // Sunucu hücresi: "Osi2" / "Osi2 Item&Yang" — ilk satır sunucu adıdır.
  const cells = [...row.children];
  const serverCell = cells.find(td => !td.querySelector('a,input') && /item\s*&\s*yang|karakter|hesap|item/i.test(td.innerText));
  // İlk yaprak öğe sunucu adıdır; hiç iç öğe yoksa "… Item&Yang" kısmı atılır.
  const leaves = serverCell ? [...serverCell.querySelectorAll('*')].filter(el => !el.children.length).map(el => clean(el.textContent)).filter(Boolean) : [];
  const server = leaves[0] || clean((serverCell?.innerText || '').split('\n')[0]).replace(/\s*(item\s*&\s*yang|karakter|hesap)$/i, '');
  const image = row.querySelector('img[src^="https://"]')?.getAttribute('src') || '';
  const number = (row.innerText.match(/İlan No:\s*(\d{12}|\d+)/i) || [])[1] || '';
  return { title, url, price: price ? price + ' TL' : '', server, image, number };
 }
 window.__niyaziReadOwnActiveListings = async (timeoutMs = 15000) => {
  const end = Date.now() + timeoutMs;
  while (Date.now() < end) {
   if (/^\/user\/login/.test(location.pathname)) throw new Error('Itemci oturumu kapalı. Itemci bölümünden giriş yapıp tekrar dene.');
   const text = (document.querySelector('main') || document.body)?.innerText || '';
   const totalMatch = text.match(/TOPLAM\s+(\d+)\s+İLAN/i);
   const rows = [...document.querySelectorAll('tr')].map(readRow).filter(Boolean);
   if (rows.length || (totalMatch && Number(totalMatch[1]) === 0))
    return { total: totalMatch ? Number(totalMatch[1]) : null, rows };
   await sleep(250);
  }
  throw new Error('İlanlarım sayfası zamanında yüklenmedi.');
 };
})();
