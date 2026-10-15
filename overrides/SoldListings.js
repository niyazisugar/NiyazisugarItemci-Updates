(() => {
    'use strict';
    const clean = value => String(value || '').replace(/\s+/g, ' ').trim();
    const norm = value => clean(value).toLocaleLowerCase('tr-TR').normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/ı/g, 'i');
    const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
    const visible = el => el && el.getBoundingClientRect().width > 0 && getComputedStyle(el).visibility !== 'hidden';
    async function waitFor(find, timeout = 12000) {
        const end = Date.now() + timeout;
        do { const result = find(); if (result) return result; await sleep(150); } while (Date.now() < end);
        return null;
    }
    function listingId(el) {
        // Read the ID label itself, not concatenated row text (which can
        // append the listing date directly to the number).
        for (const node of [el, ...el.querySelectorAll('span,p,div,small')]) {
            const match = norm(node.innerText).match(/^ilan\s*no\s*:?\s*(\d+)$/);
            if (match) return match[1];
        }
        return null;
    }
    function rows() {
        const result = [];
        const seen = new Set();
        const add = el => {
            const text = clean(el.innerText);
            const id = listingId(el);
            if (!id || seen.has(id) || !/satildi/.test(norm(text)) || !/\d[\d.,]*\s*(?:TL|₺)/i.test(text)) return;
            seen.add(id); result.push({el, id});
        };
        document.querySelectorAll('tr,[role="row"]').forEach(add);
        if (!result.length) {
            for (const el of document.querySelectorAll('span,p,div')) {
                if (!visible(el) || !/^ilan\s*no\s*:?\s*\d+$/.test(norm(el.innerText))) continue;
                for (let parent = el.parentElement, i = 0; parent && i < 7; parent = parent.parentElement, i++) {
                    const text = clean(parent.innerText);
                    if ((norm(text).match(/ilan\s*no\s*:?\s*\d+/g) || []).length > 1) break;
                    if (/satildi/.test(norm(text)) && /\d[\d.,]*\s*(?:TL|₺)/i.test(text)) { add(parent); break; }
                }
            }
        }
        return result;
    }
    function detailButton(row) {
        return [...row.querySelectorAll('button,a')].find(el => {
            const meta = norm([el.textContent,el.title,el.getAttribute('aria-label')].join(' '));
            if (/sil|delete|kaldır|kaldir/.test(meta) || el.querySelector('svg.lucide-trash,svg.lucide-trash-2')) return false;
            return /detay|detail|siparis/.test(meta) || !!el.querySelector('svg.lucide-file-text,svg.lucide-file,svg.lucide-clipboard-list,[data-lucide="file-text"]');
        });
    }
    function dialog() {
        const headers = [...document.querySelectorAll('h1,h2,h3,h4,[role="heading"],div,span')]
            .filter(el => visible(el) && /^ilan\s*(?:&|ve)\s*siparis\s*detayi$/i.test(norm(el.innerText)));
        for (const heading of headers) {
            for (let el = heading.parentElement, i = 0; el && i < 8 && el !== document.body; el = el.parentElement, i++) {
                const text = clean(el.innerText);
                if (/siparis tarihi/.test(norm(text)) && /\d{2}\.\d{2}\.\d{4}\s+\d{2}:\d{2}/.test(text) && /\d[\d.,]*\s*(?:TL|₺)/i.test(text)) return el;
            }
        }
        return null;
    }
    function serverFromRow(row) {
        const explicit=clean(row.getAttribute('data-server'));
        if(explicit)return explicit;
        const headings=[...row.closest('table')?.querySelectorAll('thead th') || []];
        const index=headings.findIndex(el=>/(?:^|[\s/])(sunucu|server)(?:$|[\s/])/.test(norm(el.textContent)));
        if(index>=0&&row.cells?.[index])return clean(row.cells[index].textContent)||'Bilinmeyen sunucu';
        for(const label of row.querySelectorAll('span,dt,label')) {
            if(/^(sunucu|server)\s*:?$/.test(norm(label.textContent))) {
                const value=clean(label.nextElementSibling?.textContent);if(value&&value.length<100)return value;
            }
        }
        return 'Bilinmeyen sunucu';
    }
    function parseDetail(text, row) {
        const normalized = norm(text);
        const order = normalized.match(/siparis\s*(?:id|no)\s*:?\s*([\w-]+)/)?.[1]?.toUpperCase();
        const date = normalized.match(/siparis\s*tarihi\s*:?\s*(\d{2}\.\d{2}\.\d{4})\s+(\d{2}:\d{2})/);
        const price = normalized.match(/fiyat\s*:?\s*([\d.,]+)\s*(?:tl|₺)/)?.[1];
        const product = text.match(/ÜRÜN\s*B[İiIı]LG[İiIı]S[İiIı]\s*(.*?)\s*(?:Metin2\s|AL[İiIı]C[İiIı]\s*B[İiIı]LG[İiIı]LER[İiIı]|F[İiIı]YAT)/i)?.[1];
        const title = clean(row.el.querySelector('h3,h4,h2,a[href*="/product/"]')?.innerText || product || '');
        if (!order || !date || !price || !title) throw new Error(`İlan ${row.id}: sipariş ayrıntısında ürün, tarih veya fiyat okunamadı.`);
        if (/iptal|iade/.test(normalized.match(/durum\s*(.*?)\s*(?:teslimat|alici|siparis\s*tarihi)/)?.[1] || '')) return null;
        return {OrderId:order, ListingId:row.id, ItemTitle:title, ImageUrl:row.el.querySelector("img")?.getAttribute("data-src") || row.el.querySelector("img")?.getAttribute("src") || "", Server:serverFromRow(row.el), Date:date[1], Time:date[2], Price:price, IsDelivered:/tamamlandi/.test(normalized)};
    }
    async function closeDialog(el) {
        const button = [...el.querySelectorAll('button')].find(b => /^kapat$/i.test(clean(b.innerText))) ||
            [...el.querySelectorAll('button')].find(b => /close|kapat/i.test(b.getAttribute('aria-label') || '') || b.querySelector('svg.lucide-x'));
        if (!button) throw new Error('Sipariş ayrıntısı kapatma düğmesi bulunamadı.');
        button.click();
        if (!await waitFor(() => !visible(el) || !el.isConnected, 4000)) throw new Error('Sipariş ayrıntısı kapanmadı.');
    }
    async function scan(knownIds = [], spacingMs = 0) {
        if (window.__niyaziSoldScanRunning) throw new Error('Önceki satış taraması hâlâ çalışıyor.');
        window.__niyaziSoldScanRunning = true;
        try {
            if (location.pathname.includes('/user/login')) throw new Error('Itemci hesabına giriş yapman gerekiyor.');
            const found = await waitFor(() => {
                const list = rows();
                if (list.length) return list;
                const text = norm(document.body.innerText);
                if (/ilan bulunamadı|ilan bulunamadi|ilanınız bulun|ilaniniz bulun|sonuc bulunamadi|ilaniniz yok|ilan yok|kayit bulunamadi/.test(text)) return [];
                return null;
            }, 15000);
            if (!found) throw new Error('Satılan ilan listesi okunamadı. Itemci oturumunu ve Satılanlar sayfasını kontrol et.');
            const known = new Set(knownIds);
            const sales = [], errors = [];
            for (const row of found) {
                if (known.has(row.id)) continue;
                let modal = null;
                try {
                    const button = detailButton(row.el);
                    if (!button) throw new Error(`İlan ${row.id}: ayrıntı düğmesi bulunamadı.`);
                    if(spacingMs>0)await sleep(spacingMs);
                    const notice=norm(document.body.innerText);
                    if(Date.now()<(window.__niyaziSitePauseUntil||0)||/cok fazla (?:deneme|islem)/.test(notice))throw new Error("Itemci işlem sınırı; tarama durduruldu.");
                    button.click();
                    modal = await waitFor(dialog);
                    if (!modal) throw new Error(`İlan ${row.id}: sipariş ayrıntısı yüklenmedi.`);
                    const sale = parseDetail(clean(modal.innerText), row);
                    if (sale) sales.push(sale);
                } catch (error) { if(/işlem sınırı/.test(error.message))throw error;errors.push(error.message); }
                finally {
                    modal = modal || dialog();
                    if (modal) await closeDialog(modal);
                }
            }
            return {Sales:sales, Errors:errors, ListingIds:found.map(row => row.id), Empty:found.length === 0};
        } finally { window.__niyaziSoldScanRunning = false; }
    }
    window.__niyaziScanSoldListings = scan;
})();
