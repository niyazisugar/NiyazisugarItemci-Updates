(function () {
    'use strict';

    const DEFAULT_SERVER = {
        id: 'msp2-global-yeni',
        name: 'MSP2 Global - Yeni',
        url: 'https://itemci.com/categories/msp2-global-yeni-itemyang',
        categories: [
            {
                id: 'msp-yang',
                name: 'Yang',
                mode: 'token',
                keywords: [],
                tokens: ['KT'],
                unitLabel: 'KT'
            },
            {
                id: 'msp-soluk',
                name: 'Soluk Kır Otu',
                mode: 'token',
                keywords: [],
                tokens: ['soluk kır otu', 'soluk kır', 'soluk', 'ot'],
                unitLabel: 'OT'
            },
            {
                id: 'msp-derece',
                name: 'Derece Meyvesi',
                mode: 'magnitude',
                keywords: ['derece meyvesi', 'derece', 'meyve'],
                tokens: [],
                unitLabel: 'M'
            }
        ]
    };

    const SERVER_LIST_KEY = 'itemci_v15_servers';
    const ACTIVE_SERVER_KEY = 'itemci_v15_active_server';

    const MAX_PAGES = 40;
    const PAGE_DELAY = 250;

    const sleep = ms => new Promise(r => setTimeout(r, ms));

    function storageGet(key, fallback = null) {
        try {
            if (typeof GM_getValue === 'function') {
                return GM_getValue(key, fallback);
            }
        } catch {}

        try {
            const value = localStorage.getItem(key);
            return value === null ? fallback : value;
        } catch {
            return fallback;
        }
    }

    function storageSet(key, value) {
        try {
            if (typeof GM_setValue === 'function') {
                GM_setValue(key, value);
                return;
            }
        } catch {}

        try {
            localStorage.setItem(key, value);
        } catch {}
    }

    function categoryIdFromName(name) {
        const base = normalizeSearchText(name)
            .replace(/[^a-z0-9]+/g, '-')
            .replace(/^-+|-+$/g, '')
            .slice(0, 40) || 'kategori';

        return `${base}-${Date.now()}-${Math.floor(Math.random() * 10000)}`;
    }

    function csvParts(value) {
        return String(value || '')
            .split(',')
            .map(x => x.trim())
            .filter(Boolean);
    }

    function normalizeCategory(category) {
        if (!category || typeof category !== 'object') return null;

        const mode = ['token', 'magnitude', 'search'].includes(category.mode)
            ? category.mode
            : 'search';

        const name = String(category.name || 'Kategori').trim();
        const keywords = Array.isArray(category.keywords)
            ? category.keywords.map(x => String(x || '').trim()).filter(Boolean)
            : [];

        const tokens = Array.isArray(category.tokens)
            ? category.tokens.map(x => String(x || '').trim()).filter(Boolean)
            : [];

        const unitLabel = String(category.unitLabel || '').trim().toUpperCase();

        return {
            id: String(category.id || categoryIdFromName(name)),
            name,
            mode,
            keywords: [...new Set(keywords)],
            tokens: [...new Set(tokens)],
            unitLabel: unitLabel || (mode === 'search' ? '' : 'ADET')
        };
    }

    function defaultCategoriesClone() {
        return DEFAULT_SERVER.categories.map(x => normalizeCategory({
            ...x,
            keywords: [...x.keywords],
            tokens: [...x.tokens]
        }));
    }

    function categoriesForServer(server) {
        if (Array.isArray(server?.categories)) {
            return server.categories.map(normalizeCategory).filter(Boolean);
        }

        // V16 gibi eski sürümlerde Yang profili varsa onu yeni kategori sistemine taşı.
        if (server?.yang && typeof server.yang === 'object') {
            const aliases = Array.isArray(server.yang.aliases)
                ? server.yang.aliases
                : [server.yang.label || 'KT'];

            return [
                normalizeCategory({
                    id: categoryIdFromName('Yang'),
                    name: 'Yang',
                    mode: 'token',
                    keywords: ['yang'],
                    tokens: aliases,
                    unitLabel: server.yang.label || aliases[0] || 'KT'
                })
            ];
        }

        if (server?.id === DEFAULT_SERVER.id) {
            return defaultCategoriesClone();
        }

        return [];
    }

    function promptCategory(existing = null) {
        const current = existing ? normalizeCategory(existing) : null;

        const name = prompt(
            'Kategori adını yaz.\n\n' +
            'Örnekler:\n' +
            'Yang\n' +
            'Soluk Kır Otu\n' +
            'Derece Meyvesi\n' +
            'Hava Kılıcı Beceri Kitabı',
            current?.name || ''
        );

        if (!name || !name.trim()) return null;

        const typeAnswer = prompt(
            'Kategori türü:\n\n' +
            '1 = MİKTARLI / BİRİM FİYAT\n' +
            '    Örn: 40KT, 15M Yang, 100 Ot\n\n' +
            '2 = K/M ÖLÇEKLİ MİKTAR\n' +
            '    Örn: 500k Derece Meyvesi, 1m Derece\n\n' +
            '3 = SADECE İLAN / FİYAT LİSTESİ\n' +
            '    Örn: Hava Kılıcı Beceri Kitabı\n\n' +
            'Seçim:',
            current
                ? (current.mode === 'token' ? '1' : current.mode === 'magnitude' ? '2' : '3')
                : '1'
        );

        if (typeAnswer === null) return null;

        const mode =
            String(typeAnswer).trim() === '1' ? 'token' :
            String(typeAnswer).trim() === '2' ? 'magnitude' :
            String(typeAnswer).trim() === '3' ? 'search' :
            null;

        if (!mode) {
            alert('Kategori türü 1, 2 veya 3 olmalı.');
            return null;
        }

        let keywords = [];
        let tokens = [];
        let unitLabel = '';

        if (mode === 'token') {
            const keywordText = prompt(
                'Bu kategori için ilan başlığında ayrıca aranacak kelime/ifadeler var mı?\n' +
                'Birden fazlaysa virgülle ayır.\n\n' +
                'Örnek Yang için: yang\n' +
                'MSP2 KT ilanlarında sadece KT yeterliyse boş bırakabilirsin.\n\n' +
                'NOT: M veya S gibi kısa birimlerde yanlış eşleşmeyi azaltmak için "yang" yazman iyi olur.',
                current?.mode === 'token' ? current.keywords.join(', ') : ''
            );

            if (keywordText === null) return null;
            keywords = csvParts(keywordText);

            const tokenText = prompt(
                'Miktar sayısından sonra kullanılan ifade/ifadeleri yaz.\n' +
                'Birden fazlaysa virgülle ayır.\n\n' +
                'Örnekler:\n' +
                'KT\n' +
                'M\n' +
                'S\n' +
                'ot, soluk, soluk kır, soluk kır otu',
                current?.mode === 'token' ? current.tokens.join(', ') : ''
            );

            if (tokenText === null) return null;
            tokens = csvParts(tokenText);

            if (!tokens.length) {
                alert('Miktarlı kategoride en az bir miktar ifadesi gerekli.');
                return null;
            }

            unitLabel = prompt(
                'Tabloda gösterilecek birim adını yaz.\n\nÖrnek: KT, M, S, OT',
                current?.mode === 'token'
                    ? current.unitLabel
                    : String(tokens[0] || '').toUpperCase()
            );

            if (!unitLabel || !unitLabel.trim()) return null;
        }

        if (mode === 'magnitude') {
            const keywordText = prompt(
                'Ürünü tanımak için kullanılan kelimeleri/ifadeleri yaz.\n' +
                'Birden fazlaysa virgülle ayır.\n\n' +
                'Örnek: derece, derece meyvesi, meyve',
                current?.mode === 'magnitude' ? current.keywords.join(', ') : ''
            );

            if (keywordText === null) return null;
            keywords = csvParts(keywordText);

            if (!keywords.length) {
                alert('K/M ölçekli kategoride en az bir ürün kelimesi gerekli.');
                return null;
            }

            unitLabel = prompt(
                'Gösterilecek ana birim. Genelde M kullanılır.',
                current?.mode === 'magnitude' ? current.unitLabel : 'M'
            );

            if (!unitLabel || !unitLabel.trim()) return null;
        }

        if (mode === 'search') {
            const keywordText = prompt(
                'Bu kategoriye girecek ilanları bulmak için kelime/ifadeleri yaz.\n' +
                'Birden fazlaysa virgülle ayır.\n\n' +
                'Örnek:\n' +
                'hava kılıcı beceri kitabı, hava kılıcı',
                current?.mode === 'search' ? current.keywords.join(', ') : ''
            );

            if (keywordText === null) return null;
            keywords = csvParts(keywordText);

            if (!keywords.length) {
                alert('Fiyat listesi kategorisinde en az bir arama ifadesi gerekli.');
                return null;
            }
        }

        return normalizeCategory({
            id: current?.id || categoryIdFromName(name.trim()),
            name: name.trim(),
            mode,
            keywords,
            tokens,
            unitLabel
        });
    }

    function categorySummary(category) {
        const c = normalizeCategory(category);
        if (!c) return '';

        if (c.mode === 'token') {
            return `Miktarlı · ${c.unitLabel} · ${c.tokens.join(', ')}${c.keywords.length ? ' · Filtre: ' + c.keywords.join(', ') : ''}`;
        }

        if (c.mode === 'magnitude') {
            return `K/M ölçekli · ${c.unitLabel} · ${c.keywords.join(', ')}`;
        }

        return `Fiyat listesi · ${c.keywords.join(', ')}`;
    }

    function loadServers() {
        let saved = [];

        try {
            const raw = storageGet(SERVER_LIST_KEY, '[]');
            saved = Array.isArray(raw) ? raw : JSON.parse(raw || '[]');
        } catch {
            saved = [];
        }

        saved = saved
            .filter(x =>
                x &&
                typeof x.id === 'string' &&
                typeof x.name === 'string' &&
                typeof x.url === 'string'
            )
            .map(x => ({
                ...x,
                categories: categoriesForServer(x)
            }));

        if (!saved.some(x => x.id === DEFAULT_SERVER.id)) {
            saved.unshift({
                ...DEFAULT_SERVER,
                categories: defaultCategoriesClone()
            });
        }

        // Aynı URL iki kere kayıtlıysa tekini tut.
        const seen = new Set();
        saved = saved.filter(x => {
            const key = x.url.toLowerCase();
            if (seen.has(key)) return false;
            seen.add(key);
            return true;
        });

        return saved;
    }

    function notifyDesktopServers(servers = null) {
        try {
            const list = Array.isArray(servers) ? servers : loadServers();

            const payload = list.map(s => ({
                id: s.id,
                name: s.name,
                categoryCount: categoriesForServer(s).length,
                url: s.url || '',
                isDefault: s.id === DEFAULT_SERVER.id
            }));

            if (
                window.chrome &&
                window.chrome.webview &&
                typeof window.chrome.webview.postMessage === 'function'
            ) {
                window.chrome.webview.postMessage(
                    JSON.stringify({
                        type: 'serversChanged',
                        servers: payload
                    })
                );
            }
        } catch {}
    }

    function saveServers(servers) {
        storageSet(SERVER_LIST_KEY, JSON.stringify(servers));
        notifyDesktopServers(servers);
    }

    function getActiveServer() {
        const servers = loadServers();
        const activeId = storageGet(ACTIVE_SERVER_KEY, DEFAULT_SERVER.id);

        return servers.find(x => x.id === activeId) || servers[0] || { ...DEFAULT_SERVER, categories: defaultCategoriesClone() };
    }

    function setActiveServer(serverId) {
        storageSet(ACTIVE_SERVER_KEY, serverId);
    }

    function normalizeServerCategoryUrl(input) {
        let raw = String(input || '').trim();
        if (!raw) return null;

        if (raw.startsWith('/')) {
            raw = 'https://itemci.com' + raw;
        }

        if (!/^https?:\/\//i.test(raw)) {
            raw = 'https://' + raw;
        }

        let u;
        try {
            u = new URL(raw);
        } catch {
            return null;
        }

        const host = u.hostname.toLowerCase();

        if (host !== 'itemci.com' && !host.endsWith('.itemci.com')) {
            return null;
        }

        const path = u.pathname.replace(/\/+$/, '');

        if (!path.startsWith('/categories/')) {
            return null;
        }

        // Kullanıcının verdiği kategori yolunu olduğu gibi kaydet.
        // Item&Yang alt kategorisi tarama sırasında otomatik bulunacak.
        return 'https://itemci.com' + path;
    }

    function absoluteItemciUrl(href) {
        try {
            const u = new URL(href, 'https://itemci.com');
            return `https://itemci.com${u.pathname}${u.search}`;
        } catch {
            return null;
        }
    }

    function discoverItemYangUrl(doc, sourceUrl) {
        const anchors = [...doc.querySelectorAll('a[href]')];

        const candidates = anchors
            .map(a => {
                const href = a.getAttribute('href') || '';
                const text = normalizeSearchText(a.textContent || '');
                const url = absoluteItemciUrl(href);

                if (!url) return null;

                let path = '';
                try {
                    path = new URL(url).pathname.toLowerCase();
                } catch {
                    return null;
                }

                if (!path.startsWith('/categories/')) return null;

                let score = 0;

                if (path.endsWith('-itemyang')) score += 10;

                if (
                    text.includes('item&yang') ||
                    text.includes('item yang') ||
                    text.includes('itemyang') ||
                    (text.includes('item') && text.includes('yang'))
                ) {
                    score += 20;
                }

                return score > 0 ? { url, score } : null;
            })
            .filter(Boolean)
            .sort((a, b) => b.score - a.score);

        if (candidates.length) {
            return candidates[0].url;
        }

        // Verilen sayfanın kendisi zaten Item&Yang olabilir.
        const sourcePath = new URL(sourceUrl).pathname.toLowerCase();
        const pageText = normalizeSearchText(doc.body?.textContent || '');

        if (
            sourcePath.endsWith('-itemyang') ||
            pageText.includes('item&yang') ||
            pageText.includes('item yang')
        ) {
            return sourceUrl;
        }

        return null;
    }

    async function resolveItemYangCategory(serverUrl) {
        const cleanUrl = normalizeServerCategoryUrl(serverUrl);

        if (!cleanUrl) {
            throw new Error('Server kategori linki geçersiz.');
        }

        // 1) Kullanıcının verdiği linki aç.
        try {
            const html = await requestHtml(cleanUrl);
            const doc = new DOMParser().parseFromString(html, 'text/html');
            const found = discoverItemYangUrl(doc, cleanUrl);

            if (found) return found;
        } catch (err) {
            // Eski V15'te otomatik eklenen yanlış "-itemyang" linkleri için
            // aşağıdaki geri dönüş yolunu deneyeceğiz.
        }

        // 2) Kayıt eski sürümden geliyorsa "-itemyang" eklenmiş olabilir.
        if (/-itemyang$/i.test(cleanUrl)) {
            const mainUrl = cleanUrl.replace(/-itemyang$/i, '');

            try {
                const html = await requestHtml(mainUrl);
                const doc = new DOMParser().parseFromString(html, 'text/html');
                const found = discoverItemYangUrl(doc, mainUrl);

                if (found) return found;
            } catch {}
        }

        throw new Error(
            'Bu server için Item&Yang kategorisi otomatik bulunamadı. ' +
            'Serverin Itemci ana kategori linkini kontrol et.'
        );
    }


    function serverIdFromName(name) {
        const base = normalizeSearchText(name)
            .replace(/[^a-z0-9]+/g, '-')
            .replace(/^-+|-+$/g, '')
            .slice(0, 45) || 'server';

        return `${base}-${Date.now()}`;
    }

    function clean(s) {
        return String(s || '').replace(/\s+/g, ' ').trim();
    }

    function trNumber(s) {
        s = clean(s).replace(/\s/g, '');
        if (s.includes('.') && s.includes(',')) {
            s = s.replace(/\./g, '').replace(',', '.');
        } else if (s.includes(',')) {
            s = s.replace(',', '.');
        }
        const n = Number(s);
        return Number.isFinite(n) ? n : null;
    }

    function fmt(n, digits = 2) {
        return Number(n).toLocaleString('tr-TR', {
            minimumFractionDigits: digits,
            maximumFractionDigits: digits
        });
    }

    function median(arr) {
        if (!arr.length) return 0;
        const a = [...arr].sort((x, y) => x - y);
        const m = Math.floor(a.length / 2);
        return a.length % 2 ? a[m] : (a[m - 1] + a[m]) / 2;
    }

    function esc(s) {
        return String(s).replace(/[&<>"']/g, c => ({
            '&': '&amp;',
            '<': '&lt;',
            '>': '&gt;',
            '"': '&quot;',
            "'": '&#39;'
        })[c]);
    }

    function normalizeSearchText(value) {
        return clean(value)
            .toLocaleLowerCase('tr-TR')
            .normalize('NFD')
            .replace(/[\u0300-\u036f]/g, '')
            .replace(/ı/g, 'i');
    }

    function queryMatches(text, query) {
        const q = normalizeSearchText(query);
        if (!q) return true;

        const hay = normalizeSearchText(text);
        const terms = q.split(/\s+/).filter(Boolean);

        // Aramadaki bütün kelimeler ilanda geçsin.
        return terms.every(term => hay.includes(term));
    }

    function countNumber(s) {
        s = clean(s).replace(/\s/g, '');

        // 1.000 / 10.000 gibi Türkçe binlik yazımlar.
        if (/^\d{1,3}(?:\.\d{3})+$/.test(s)) {
            const n = Number(s.replace(/\./g, ''));
            return Number.isFinite(n) ? n : null;
        }

        // Bazen 1,000 şeklinde de yazılabilir; adet bilgisinde bunu da binlik kabul et.
        if (/^\d{1,3}(?:,\d{3})+$/.test(s)) {
            const n = Number(s.replace(/,/g, ''));
            return Number.isFinite(n) ? n : null;
        }

        const n = Number(s.replace(',', '.'));
        return Number.isFinite(n) ? n : null;
    }

    function solukAmountFromTitle(title) {
        const t = normalizeSearchText(title);

        // Kabul edilen isimler:
        // "soluk", "soluk kir", "soluk kir otu", "ot"
        const item = '(?:soluk\\s+kir(?:\\s+otu)?|soluk|ot)';
        const number = '(\\d+(?:[.,]\\d+)?)';

        const patterns = [
            // 100 ot / 100 soluk / 100 soluk kir / 100 soluk kir otu
            // 100 adet ot / 100 tane ot / 100x ot
            new RegExp('(?:^|[^\\d.,])' + number + '\\s*(?:adet\\s+|tane\\s+|x\\s*)?' + item + '\\b', 'i'),

            // "soluk 100 adet" / "ot 100 tane" biçimini de kabul et.
            // Sadece sayıdan sonra adet/tane varsa; "ot 1 TL" gibi fiyat cümlelerini miktar sanma.
            new RegExp('\\b' + item + '\\b\\s*[:\\-]?\\s*' + number + '\\s*(?:adet|tane)\\b', 'i')
        ];

        for (const rx of patterns) {
            const m = t.match(rx);
            if (!m) continue;

            const raw = m[1];
            const amount = countNumber(raw);

            if (amount && amount > 0) return amount;
        }

        return null;
    }

    function parseMagnitudeNumber(rawNumber, suffix) {
        const n = countNumber(rawNumber);
        if (!n || n <= 0) return null;

        const s = normalizeSearchText(suffix || '');

        if (s === 'k') return n * 1000;
        if (s === 'm') return n * 1000000;

        // Suffix yoksa sadece büyük çıplak sayıları miktar kabul et.
        // 500 TL gibi fiyatların yanlışlıkla "500 derece" miktarı sanılmasını istemiyoruz.
        if (n >= 10000) return n;

        return null;
    }

    function degreeFruitAmountFromTitle(title) {
        const t = normalizeSearchText(title);

        // Kabul edilen ürün isimleri:
        // "derece", "derece meyvesi", "meyve"
        const item = '(?:derece\\s+meyvesi|derece|meyve)';
        const number = '(\\d+(?:[.,]\\d+)?)';
        const suffix = '([km]?)';

        const patterns = [
            // 500k derece / 1m derece meyvesi / 2,5m meyve
            new RegExp('(?:^|[^\\d.,])' + number + '\\s*' + suffix + '\\s*(?:adet\\s+|tane\\s+|x\\s*)?' + item + '\\b', 'i'),

            // derece 500k / meyve 1m
            new RegExp('\\b' + item + '\\b\\s*[:\\-]?\\s*' + number + '\\s*' + suffix + '\\b', 'i')
        ];

        for (const rx of patterns) {
            const m = t.match(rx);
            if (!m) continue;

            const absoluteAmount = parseMagnitudeNumber(m[1], m[2]);

            if (absoluteAmount && absoluteAmount > 0) {
                // Arayüz ve piyasa hesabında 1M birimini kullanıyoruz.
                return absoluteAmount / 1000000;
            }
        }

        return null;
    }

    function amountFromTitle(title) {
        const m = title.match(/(?:^|[^\d.,])(\d+(?:[.,]\d+)?)\s*K\s*T+\b/i);
        if (!m) return null;
        const amount = trNumber(m[1]);
        return amount && amount > 0 ? amount : null;
    }

    function realPriceFromCard(card) {
        const nodes = [...card.querySelectorAll('.tabular-nums')];

        for (const el of nodes) {
            const txt = clean(el.textContent);
            const m = txt.match(/(\d{1,3}(?:\.\d{3})*|\d+),(\d{2})\s*TL/i);
            if (!m) continue;

            const n = Number(m[1].replace(/\./g, '') + '.' + m[2]);
            if (Number.isFinite(n) && n > 0) return n;
        }

        // Yedek: kart içindeki 000,00 TL biçimi
        const text = clean(card.textContent);
        const m = text.match(/(\d{1,3}(?:\.\d{3})*|\d+),(\d{2})\s*TL/i);
        if (!m) return null;

        const n = Number(m[1].replace(/\./g, '') + '.' + m[2]);
        return Number.isFinite(n) && n > 0 ? n : null;
    }

    function sellerFromCard(card) {
        const direct = card.querySelector('span.truncate.font-semibold.text-neutral-700');
        if (direct) return clean(direct.textContent);

        const imgs = [...card.querySelectorAll('img[alt]')];
        if (imgs.length >= 2) {
            const alt = clean(imgs[imgs.length - 1].getAttribute('alt'));
            if (alt) return alt;
        }

        return 'Bilinmiyor';
    }

    function timeFromCard(card) {
        const candidates = [...card.querySelectorAll('span')];
        for (const el of candidates) {
            const txt = clean(el.textContent);
            if (/\b(dk|saat|gün|gun|hafta|ay)\s+önce\b/i.test(txt)) return txt;
            if (/^(az önce|şimdi|dün)$/i.test(txt)) return txt;
        }
        return '';
    }

    function ageHoursFromTime(text) {
        const t = clean(text).toLocaleLowerCase('tr-TR');
        if (!t) return null;

        if (t === 'az önce' || t === 'şimdi') return 0;
        if (t === 'dün') return 24;

        let m = t.match(/(\d+)\s*dk\s*önce/);
        if (m) return Number(m[1]) / 60;

        m = t.match(/(\d+)\s*saat\s*önce/);
        if (m) return Number(m[1]);

        m = t.match(/(\d+)\s*(?:gün|gun)\s*önce/);
        if (m) return Number(m[1]) * 24;

        m = t.match(/(\d+)\s*hafta\s*önce/);
        if (m) return Number(m[1]) * 24 * 7;

        m = t.match(/(\d+)\s*ay\s*önce/);
        if (m) return Number(m[1]) * 24 * 30;

        return null;
    }

    function buyerIntentFromTitle(title) {
        const t = normalizeSearchText(title);

        const rules = [
            { label: 'ALINIR', rx: /\balinir\b/ },
            { label: 'LAZIM', rx: /\blazim\b/ },
            { label: 'ARIYORUM', rx: /\bariyorum\b/ },
            { label: 'ARANIYOR', rx: /\baraniyor\b/ },
            { label: 'ALINACAK', rx: /\balinacak\b/ },
            { label: 'ALIYORUM', rx: /\baliyorum\b/ },
            { label: 'SATIN ALIRIM', rx: /\bsatin\s+alirim\b/ },
            { label: 'ALMAK İSTİYORUM', rx: /\balmak\s+istiyorum\b/ },
            { label: 'İHTİYACIM VAR', rx: /\bihtiyacim\s+var\b/ },
            { label: 'ALIM YAPILIR', rx: /\balim\s+yapilir\b/ },
            { label: 'ALIM YAPIYORUM', rx: /\balim\s+yapiyorum\b/ }
        ];

        for (const rule of rules) {
            if (rule.rx.test(t)) return rule.label;
        }

        return null;
    }

    function suspiciousByText(title) {
        const t = title.toLocaleLowerCase('tr-TR');

        const keywords = [
            'teklif',
            'mesaj at',
            'mesaj atin',
            'mesaj atın',
            'alınır',
            'alinir',
            'arıyorum',
            'ariyorum',
            'stok bitti',
            'stokkk',
            'fiyat sor',
            'dm',
            'iletişim',
            'iletisim'
        ];

        if (keywords.some(k => t.includes(k))) return true;

        // "2.7 KT vuran" gibi miktar olmayan hasar ifadeleri
        if (/kt.{0,30}(vuran|vuruyor|hasar|damage|dmg)/i.test(t)) return true;
        if (/(vuran|vuruyor|hasar|damage|dmg).{0,30}kt/i.test(t)) return true;

        return false;
    }

    function parseCard(card) {
        const h3 = card.querySelector('h3');
        if (!h3) return null;

        const title = clean(h3.textContent);
        if (!title) return null;

        const price = realPriceFromCard(card);
        if (!price) return null;

        const time = timeFromCard(card);

        return {
            title,
            price,
            seller: sellerFromCard(card),
            sellerUrl: card.querySelector('a[href*="/user/store/"]')?.href || "",
            time,
            ageHours: ageHoursFromTime(time),
            url: card.href,
            suspiciousText: suspiciousByText(title)
        };
    }

    function phraseMatch(title, phrases) {
        if (!Array.isArray(phrases) || !phrases.length) return true;

        const hay = normalizeSearchText(title);

        return phrases.some(phrase => {
            const p = normalizeSearchText(phrase);
            return p && hay.includes(p);
        });
    }

    function escapeRegExp(value) {
        return String(value).replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    }

    function tokenAmountFromTitle(title, category) {
        const c = normalizeCategory(category);
        if (!c || c.mode !== 'token') return null;

        if (c.keywords.length && !phraseMatch(title, c.keywords)) {
            return null;
        }

        const aliases = c.tokens
            .map(x => normalizeSearchText(x))
            .filter(Boolean)
            .sort((a, b) => b.length - a.length);

        if (!aliases.length) return null;

        const aliasPattern = aliases.map(escapeRegExp).join('|');
        const t = normalizeSearchText(title);

        const patterns = [
            // 40KT, 15 M, 100 ot, 100 adet ot, 100xKT
            new RegExp(
                '(?:^|[^\\d.,])' +
                '(\\d+(?:[.,]\\d+)?)' +
                '\\s*(?:adet\\s+|tane\\s+|x\\s*)?' +
                '(?:' + aliasPattern + ')' +
                '(?![a-z0-9])',
                'i'
            ),

            // "ot 100 adet" gibi ters yazım.
            new RegExp(
                '(?:^|[^a-z0-9])' +
                '(?:' + aliasPattern + ')' +
                '(?![a-z0-9])' +
                '\\s*[:\\-]?\\s*' +
                '(\\d+(?:[.,]\\d+)?)' +
                '\\s*(?:adet|tane)\\b',
                'i'
            )
        ];

        for (const rx of patterns) {
            const m = t.match(rx);
            if (!m) continue;

            const amount = countNumber(m[1]);
            if (amount && amount > 0) return amount;
        }

        return null;
    }

    function magnitudeAmountFromTitle(title, category) {
        const c = normalizeCategory(category);
        if (!c || c.mode !== 'magnitude') return null;

        const t = normalizeSearchText(title);
        const items = c.keywords
            .map(x => normalizeSearchText(x))
            .filter(Boolean)
            .sort((a, b) => b.length - a.length);

        if (!items.length) return null;

        const itemPattern = items.map(escapeRegExp).join('|');
        const number = '(\\d+(?:[.,]\\d+)?)';
        const suffix = '([km])';

        const patterns = [
            new RegExp(
                '(?:^|[^\\d.,])' +
                number + '\\s*' + suffix +
                '\\s*(?:adet\\s+|tane\\s+|x\\s*)?' +
                '(?:' + itemPattern + ')' +
                '(?![a-z0-9])',
                'i'
            ),
            new RegExp(
                '(?:' + itemPattern + ')' +
                '(?![a-z0-9])\\s*[:\\-]?\\s*' +
                number + '\\s*' + suffix + '\\b',
                'i'
            )
        ];

        for (const rx of patterns) {
            const m = t.match(rx);
            if (!m) continue;

            const n = countNumber(m[1]);
            if (!n || n <= 0) continue;

            const s = String(m[2] || '').toLowerCase();

            // Bu modun ana ölçüsü M.
            if (s === 'k') return n / 1000;
            if (s === 'm') return n;
        }

        return null;
    }

    function analyzeCategoryRow(raw, category) {
        const c = normalizeCategory(category);
        if (!c) return null;

        if (c.mode === 'search') {
            if (!phraseMatch(raw.title, c.keywords)) return null;

            return {
                ...raw,
                categoryId: c.id,
                categoryName: c.name
            };
        }

        let amount = null;

        if (c.mode === 'token') {
            amount = tokenAmountFromTitle(raw.title, c);
        } else if (c.mode === 'magnitude') {
            amount = magnitudeAmountFromTitle(raw.title, c);
        }

        if (!amount || amount <= 0) return null;

        const unit = raw.price / amount;
        if (!Number.isFinite(unit) || unit <= 0) return null;

        return {
            ...raw,
            amount,
            unit,
            categoryId: c.id,
            categoryName: c.name
        };
    }

    function cardsFromDocument(doc, categoryBase) {
        let categorySlug = '';

        try {
            const parts = new URL(categoryBase).pathname.split('/').filter(Boolean);
            categorySlug = parts[parts.length - 1] || '';
        } catch {
            return [];
        }

        if (!categorySlug) return [];

        return [...doc.querySelectorAll('a[href*="/product/"]')].filter(a => {
            const href = a.getAttribute('href') || '';
            return href.includes(`/product/${categorySlug}/`);
        });
    }

    let lastAnalysisRequestAt=0;
    async function requestHtml(url) {
        await sleep(Math.max(0,2500-(Date.now()-lastAnalysisRequestAt)));
        if(Date.now()<(window.__niyaziSitePauseUntil||0))throw new Error('Itemci işlem sınırı; bekleme süresi dolunca tekrar deneyebilirsin.');
        lastAnalysisRequestAt=Date.now();
        if (typeof GM_xmlhttpRequest === 'function') {
            return new Promise((resolve, reject) => {
                GM_xmlhttpRequest({
                    method: 'GET',
                    url,
                    headers: {
                        'Accept': 'text/html,application/xhtml+xml'
                    },
                    timeout: 20000,
                    onload: response => {
                        if (response.status >= 200 && response.status < 400) {
                            resolve(response.responseText);
                        } else {
                            reject(new Error(`HTTP ${response.status}`));
                        }
                    },
                    onerror: () => reject(new Error('Itemci sayfasına bağlanılamadı.')),
                    ontimeout: () => reject(new Error('Itemci isteği zaman aşımına uğradı.'))
                });
            });
        }

        return fetch(url, {
            credentials: 'include',
            cache: 'no-store',
            headers: { 'Accept': 'text/html,application/xhtml+xml' }
        }).then(async r => {
            if (!r.ok) throw new Error(`HTTP ${r.status}`);
            return r.text();
        });
    }

    async function fetchPage(page, categoryBase) {
        const url = page === 1 ? categoryBase : `${categoryBase}?page=${page}`;
        const html = await requestHtml(url);
        const doc = new DOMParser().parseFromString(html, 'text/html');
        return { doc, url };
    }

    function makeProgress() {
        document.getElementById('ikt-progress')?.remove();

        const box = document.createElement('div');
        box.id = 'ikt-progress';
        box.style.cssText = `
            position:fixed; right:20px; bottom:145px; z-index:2147483647;
            background:#1b1813; color:#fff; border:1px solid #e3c27a;
            border-radius:10px; padding:13px 16px;
            font:13px Segoe UI,Arial,sans-serif;
            box-shadow:0 6px 24px rgba(0,0,0,.45)
        `;
        box.textContent = 'Hazırlanıyor...';
        document.body.appendChild(box);
        return box;
    }

    function splitRows(rows) {
        const nonTextSuspicious = rows.filter(x => !x.suspiciousText);
        const baseUnits = nonTextSuspicious.map(x => x.unit);
        const med = median(baseUnits);

        const valid = [];
        const suspicious = [];

        for (const x of rows) {
            let outlier = false;

            if (med > 0) {
                // Çok uç/placeholder fiyatları istatistikten ayır.
                outlier = x.unit < med * 0.45 || x.unit > med * 2.20;
            }

            if (x.suspiciousText || outlier) suspicious.push(x);
            else valid.push(x);
        }

        return { valid, suspicious };
    }

    function csvDownload(rows, amountLabel = 'KT', fileBase = 'itemci_kt_analiz') {
        const header = [
            'Satıcı',
            'İlan Başlığı',
            `Miktar ${amountLabel}`,
            `Birim Fiyat TL/${amountLabel}`,
            'Toplam TL',
            'Zaman',
            'İlan Linki'
        ];

        const q = v => '"' + String(v ?? '').replace(/"/g, '""') + '"';

        const lines = [
            header.map(q).join(';'),
            ...rows.map(x => [
                x.seller,
                x.title,
                String(x.amount).replace('.', ','),
                fmt(x.unit),
                fmt(x.price),
                x.time,
                x.url
            ].map(q).join(';'))
        ];

        const blob = new Blob(['\uFEFF' + lines.join('\r\n')], {
            type: 'text/csv;charset=utf-8'
        });

        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `${fileBase}_${new Date().toISOString().slice(0,10)}.csv`;
        document.body.appendChild(a);
        a.click();
        a.remove();
        setTimeout(() => URL.revokeObjectURL(url), 1500);
    }

    function csvDownloadGeneral(rows) {
        const header = ['Satıcı','İlan Başlığı','Fiyat TL','Zaman','İlan Linki'];

        const q = v => '"' + String(v ?? '').replace(/"/g, '""') + '"';

        const lines = [
            header.map(q).join(';'),
            ...rows.map(x => [
                x.seller,
                x.title,
                fmt(x.price),
                x.time,
                x.url
            ].map(q).join(';'))
        ];

        const blob = new Blob(['\uFEFF' + lines.join('\r\n')], {
            type: 'text/csv;charset=utf-8'
        });

        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `itemci_genel_arama_${new Date().toISOString().slice(0,10)}.csv`;
        document.body.appendChild(a);
        a.click();
        a.remove();
        setTimeout(() => URL.revokeObjectURL(url), 1500);
    }

    function generalRowHtml(rows) {
        return rows.map((x, i) => {
            const searchText = `${x.seller} ${x.title} ${x.price} ${x.time}`;

            return `
            <tr
                class="data-row generalRow"
                data-search="${esc(normalizeSearchText(searchText))}"
                data-seller="${esc(normalizeSearchText(x.seller))}"
                data-title="${esc(normalizeSearchText(x.title))}"
                data-price="${x.price}"
                data-age="${x.ageHours === null ? 999999 : x.ageHours}"
            >
                <td class="rank">${i + 1}</td>
                <td class="seller">@${esc(x.seller.replace(/^@/, ''))}</td>
                <td class="listing"><div class="listingTitle">${esc(x.title)}</div></td>
                <td class="num total">${fmt(x.price)} TL</td>
                <td class="timeCell">
                    <span class="timeBadge">${esc(x.time || 'Bilinmiyor')}</span>
                </td>
                <td class="actionCell">
                    <div class="rowActions">
                        <button
                            class="miniBtn openListing"
                            type="button"
                            data-url="${esc(x.url)}"
                            title="İlanı yeni sekmede aç"
                        >↗ GİT</button>

                        <button
                            class="miniBtn copyLink"
                            type="button"
                            data-url="${esc(x.url)}"
                            title="İlan linkini kopyala"
                        >⧉ KOPYALA</button>
                    </div>
                </td>
            </tr>
            `;
        }).join('');
    }

    function buyerRowHtml(rows) {
        return rows.map((x, i) => {
            const searchText = `${x.seller} ${x.title} ${x.price} ${x.time} ${x.buyerIntent || ''}`;

            return `
            <tr
                class="data-row buyerRow"
                data-search="${esc(normalizeSearchText(searchText))}"
                data-seller="${esc(normalizeSearchText(x.seller))}"
                data-title="${esc(normalizeSearchText(x.title))}"
                data-price="${x.price}"
                data-age="${x.ageHours === null ? 999999 : x.ageHours}"
                data-intent="${esc(normalizeSearchText(x.buyerIntent || ''))}"
            >
                <td class="rank">${i + 1}</td>

                <td class="seller">@${esc(x.seller.replace(/^@/, ''))}</td>

                <td class="listing">
                    <div class="listingTitle">${esc(x.title)}</div>
                </td>

                <td class="intentCell">
                    <span class="intentBadge">${esc(x.buyerIntent || 'ALICI')}</span>
                </td>

                <td class="num total">${fmt(x.price)} TL</td>

                <td class="timeCell">
                    <span class="timeBadge">${esc(x.time || 'Bilinmiyor')}</span>
                </td>

                <td class="actionCell">
                    <div class="rowActions">
                        <button
                            class="miniBtn openListing"
                            type="button"
                            data-url="${esc(x.url)}"
                            title="İlanı yeni sekmede aç"
                        >↗ GİT</button>

                        <button
                            class="miniBtn copyLink"
                            type="button"
                            data-url="${esc(x.url)}"
                            title="İlan linkini kopyala"
                        >⧉ KOPYALA</button>
                    </div>
                </td>
            </tr>
            `;
        }).join('');
    }

    function rowHtml(rows, rowType = 'valid', amountLabel = 'KT') {
        return rows.map((x, i) => {
            const searchText = `${x.seller} ${x.title} ${x.amount} ${x.unit} ${x.price} ${x.time}`
                .toLocaleLowerCase('tr-TR');

            const ageSort = x.ageHours === null ? 999999 : x.ageHours;

            return `
            <tr
                class="data-row ${rowType}"
                data-search="${esc(searchText)}"
                data-seller="${esc(x.seller.toLocaleLowerCase('tr-TR'))}"
                data-seller-url="${esc(x.sellerUrl || '')}"
                data-title="${esc(x.title.toLocaleLowerCase('tr-TR'))}"
                data-amount="${x.amount}"
                data-unit="${x.unit}"
                data-price="${x.price}"
                data-age="${ageSort}"
            >
                <td class="rank">${i + 1}</td>

                <td class="seller">
                    @${esc(x.seller.replace(/^@/, ''))}
                </td>

                <td class="listing">
                    <div class="listingTitle">${esc(x.title)}</div>
                </td>

                <td class="num amount">
                    ${Number.isInteger(x.amount) ? x.amount : fmt(x.amount)} ${esc(amountLabel)}
                </td>

                <td class="num unit">
                    ${fmt(x.unit)} TL
                </td>

                <td class="num total">
                    ${fmt(x.price)} TL
                </td>

                <td class="timeCell">
                    <span class="timeBadge">${esc(x.time || 'Bilinmiyor')}</span>
                </td>

                <td class="actionCell">
                    <div class="rowActions">
                        <button
                            class="miniBtn openListing"
                            type="button"
                            data-url="${esc(x.url)}"
                            title="İlanı yeni sekmede aç"
                        >
                            ↗ GİT
                        </button>

                        <button
                            class="miniBtn copyLink"
                            type="button"
                            data-url="${esc(x.url)}"
                            title="İlan linkini kopyala"
                        >
                            ⧉ KOPYALA
                        </button>
                    </div>
                </td>
            </tr>
        `;
        }).join('');
    }

    function buildUnitTableHtml(rows, prefix, amountLabel) {
        return `
        <div class="tableCard">
            <div class="tableWrap">
                <table class="sortableTable ${prefix}Table">
                    <thead>
                        <tr>
                            <th>#</th>
                            <th class="sortable" data-sort="seller" data-default-dir="asc">
                                <span class="thInner">SATICI <span class="sortIcon">↕</span></span>
                            </th>
                            <th class="sortable" data-sort="title" data-default-dir="asc">
                                <span class="thInner">İLAN BAŞLIĞI <span class="sortIcon">↕</span></span>
                            </th>
                            <th class="sortable numHead" data-sort="amount" data-default-dir="asc">
                                <span class="thInner">MİKTAR <span class="sortIcon">↕</span></span>
                            </th>
                            <th class="sortable numHead sortActive" data-sort="unit" data-default-dir="asc" data-current-dir="asc">
                                <span class="thInner">BİRİM FİYAT <span class="sortIcon">↑</span></span>
                            </th>
                            <th class="sortable numHead" data-sort="price" data-default-dir="asc">
                                <span class="thInner">TOPLAM <span class="sortIcon">↕</span></span>
                            </th>
                            <th class="sortable timeHead" data-sort="age" data-default-dir="asc">
                                <span class="thInner">SÜRE <span class="sortIcon">↕</span></span>
                            </th>
                            <th>İŞLEM</th>
                        </tr>
                    </thead>
                    <tbody class="${prefix}Body">
                        ${rows.length
                            ? rowHtml(rows, prefix + 'Valid', amountLabel)
                            : '<tr><td colspan="8" class="empty">Son 36 saatte miktarı okunabilen ilan bulunamadı.</td></tr>'}
                    </tbody>
                </table>
            </div>
        </div>
        `;
    }

    function buildSuspiciousHtml(rows, label, amountLabel) {
        if (!rows.length) return '';

        return `
        <details>
            <summary>ŞÜPHELİ / HESABA KATILMAYAN ${esc(label.toUpperCase())} İLANLARI (${rows.length})</summary>
            <div class="tableWrap" style="max-height:360px">
                <table class="sortableTable">
                    <thead>
                        <tr>
                            <th>#</th>
                            <th class="sortable" data-sort="seller" data-default-dir="asc">
                                <span class="thInner">SATICI <span class="sortIcon">↕</span></span>
                            </th>
                            <th class="sortable" data-sort="title" data-default-dir="asc">
                                <span class="thInner">İLAN <span class="sortIcon">↕</span></span>
                            </th>
                            <th class="sortable numHead" data-sort="amount" data-default-dir="asc">
                                <span class="thInner">MİKTAR <span class="sortIcon">↕</span></span>
                            </th>
                            <th class="sortable numHead" data-sort="unit" data-default-dir="asc">
                                <span class="thInner">BİRİM <span class="sortIcon">↕</span></span>
                            </th>
                            <th class="sortable numHead" data-sort="price" data-default-dir="asc">
                                <span class="thInner">TOPLAM <span class="sortIcon">↕</span></span>
                            </th>
                            <th class="sortable timeHead" data-sort="age" data-default-dir="asc">
                                <span class="thInner">SÜRE <span class="sortIcon">↕</span></span>
                            </th>
                            <th>İŞLEM</th>
                        </tr>
                    </thead>
                    <tbody>${rowHtml(rows, 'suspicious', amountLabel)}</tbody>
                </table>
            </div>
        </details>
        `;
    }

    function openCategoryManager(serverId, onDone = null) {
        document.getElementById('itemci-category-manager')?.remove();

        const servers = loadServers();
        const serverIndex = servers.findIndex(x => x.id === serverId);

        if (serverIndex < 0) {
            alert('Server bulunamadı.');
            return;
        }

        const server = servers[serverIndex];
        server.categories = categoriesForServer(server);

        const overlay = document.createElement('div');
        overlay.id = 'itemci-category-manager';

        const render = () => {
            const categoryCards = server.categories.length
                ? server.categories.map((c, i) => `
                    <div class="catCard" data-cat-id="${esc(c.id)}">
                        <div class="catInfo">
                            <div class="catName">${esc(c.name)}</div>
                            <div class="catSummary">${esc(categorySummary(c))}</div>
                        </div>

                        <div class="catActions">
                            <button class="editCat" data-index="${i}">DÜZENLE</button>
                            <button class="deleteCat" data-index="${i}">SİL</button>
                        </div>
                    </div>
                `).join('')
                : `
                    <div class="emptyCats">
                        Bu serverde henüz ürün kategorisi yok.<br>
                        “+ KATEGORİ EKLE” ile başlayabilirsin.
                    </div>
                `;

            overlay.innerHTML = `
                <style>
                #itemci-category-manager{
                    position:fixed; inset:0; z-index:2147483647;
                    display:flex; align-items:center; justify-content:center;
                    background:rgba(7,9,12,.78); backdrop-filter:blur(7px);
                    font-family:Inter,Segoe UI,Arial,sans-serif;
                }
                #itemci-category-manager *{box-sizing:border-box}
                #itemci-category-manager .box{
                    width:min(720px,calc(100vw - 28px));
                    max-height:86vh;
                    overflow:hidden;
                    background:#171614;
                    border:1px solid #3d3a35;
                    border-radius:14px;
                    color:#ede9e3;
                    box-shadow:0 24px 70px rgba(0,0,0,.58);
                }
                #itemci-category-manager .head{
                    padding:18px 20px 14px;
                    border-bottom:1px solid #302d28;
                }
                #itemci-category-manager .title{
                    color:#e3c27a;
                    font-size:18px;
                    font-weight:900;
                }
                #itemci-category-manager .sub{
                    margin-top:6px;
                    color:#999691;
                    font-size:11px;
                }
                #itemci-category-manager .body{
                    padding:14px 16px;
                    overflow:auto;
                    max-height:62vh;
                }
                #itemci-category-manager .catCard{
                    display:flex;
                    align-items:center;
                    justify-content:space-between;
                    gap:12px;
                    padding:12px;
                    margin-bottom:8px;
                    border:1px solid #37342f;
                    border-radius:9px;
                    background:#1c1a17;
                }
                #itemci-category-manager .catInfo{min-width:0}
                #itemci-category-manager .catName{
                    color:#ede9e3;
                    font-size:13px;
                    font-weight:850;
                }
                #itemci-category-manager .catSummary{
                    margin-top:4px;
                    color:#8c8984;
                    font-size:10px;
                    line-height:1.4;
                }
                #itemci-category-manager .catActions{
                    display:flex; gap:6px; flex:0 0 auto;
                }
                #itemci-category-manager button{
                    height:34px; padding:0 11px;
                    border:1px solid #44413c;
                    border-radius:8px;
                    background:#23211d;
                    color:#ede9e3;
                    font-size:10px;
                    font-weight:900;
                    cursor:pointer;
                }
                #itemci-category-manager .editCat{color:#8fc7ff}
                #itemci-category-manager .deleteCat{color:#ff9999}
                #itemci-category-manager .footer{
                    display:flex;
                    gap:8px;
                    padding:13px 16px 16px;
                    border-top:1px solid #302d28;
                }
                #itemci-category-manager .addCat{
                    flex:1;
                    color:#181510;
                    border-color:#e3c27a;
                    background:#e3c27a;
                }
                #itemci-category-manager .done{min-width:120px}
                #itemci-category-manager .emptyCats{
                    padding:28px 16px;
                    text-align:center;
                    color:#83807b;
                    font-size:11px;
                    line-height:1.6;
                }
                </style>

                <div class="box">
                    <div class="head">
                        <div class="title">${esc(server.name)} · KATEGORİLER</div>
                        <div class="sub">
                            Bu serverde hangi ürünleri takip edeceğini burada belirliyorsun.
                        </div>
                    </div>

                    <div class="body">
                        ${categoryCards}
                    </div>

                    <div class="footer">
                        <button class="addCat">+ KATEGORİ EKLE</button>
                        <button class="done">TAMAM</button>
                    </div>
                </div>
            `;

            overlay.querySelector('.addCat').onclick = () => {
                const category = promptCategory();
                if (!category) return;

                server.categories.push(category);
                saveServers(servers);
                render();
            };

            overlay.querySelectorAll('.editCat').forEach(btn => {
                btn.onclick = () => {
                    const index = Number(btn.dataset.index);
                    const current = server.categories[index];
                    if (!current) return;

                    const edited = promptCategory(current);
                    if (!edited) return;

                    server.categories[index] = edited;
                    saveServers(servers);
                    render();
                };
            });

            overlay.querySelectorAll('.deleteCat').forEach(btn => {
                btn.onclick = () => {
                    const index = Number(btn.dataset.index);
                    const current = server.categories[index];
                    if (!current) return;

                    if (!confirm(`"${current.name}" kategorisini silmek istiyor musun?`)) {
                        return;
                    }

                    server.categories.splice(index, 1);
                    saveServers(servers);
                    render();
                };
            });

            overlay.querySelector('.done').onclick = () => {
                saveServers(servers);
                overlay.remove();
                if (typeof onDone === 'function') onDone(server);
            };
        };

        document.body.appendChild(overlay);
        render();
    }

    function categoryResult(allRows, category) {
        const recentRows = allRows.filter(x =>
            x.ageHours !== null && x.ageHours <= 36
        );

        const rows = recentRows
            .map(x => analyzeCategoryRow(x, category))
            .filter(Boolean);

        if (category.mode === 'search') {
            rows.sort((a, b) => a.price - b.price);

            const prices = rows.map(x => x.price);
            return {
                category,
                rows,
                valid: rows,
                suspicious: [],
                cheapest: prices.length ? Math.min(...prices) : 0,
                median: median(prices),
                average: prices.length
                    ? prices.reduce((a,b) => a+b, 0) / prices.length
                    : 0,
                totalAmount: 0
            };
        }

        rows.sort((a, b) => a.unit - b.unit);
        const split = splitRows(rows);
        const units = split.valid.map(x => x.unit);

        return {
            category,
            rows,
            valid: split.valid,
            suspicious: split.suspicious,
            cheapest: units.length ? Math.min(...units) : 0,
            median: median(units),
            average: units.length
                ? units.reduce((a,b) => a+b, 0) / units.length
                : 0,
            totalAmount: split.valid.reduce((sum, x) => sum + x.amount, 0)
        };
    }

    function categorySearchRowHtml(rows) {
        return rows.map((x, i) => {
            const searchText = `${x.seller} ${x.title} ${x.price} ${x.time}`;

            return `
            <tr
                class="data-row categorySearchRow"
                data-search="${esc(normalizeSearchText(searchText))}"
                data-seller="${esc(normalizeSearchText(x.seller))}"
                data-title="${esc(normalizeSearchText(x.title))}"
                data-price="${x.price}"
                data-age="${x.ageHours === null ? 999999 : x.ageHours}"
            >
                <td class="rank">${i + 1}</td>
                <td class="seller">@${esc(x.seller.replace(/^@/, ''))}</td>
                <td class="listing"><div class="listingTitle">${esc(x.title)}</div></td>
                <td class="num total">${fmt(x.price)} TL</td>
                <td class="timeCell">
                    <span class="timeBadge">${esc(x.time || 'Bilinmiyor')}</span>
                </td>
                <td class="actionCell">
                    <div class="rowActions">
                        <button class="miniBtn openListing" type="button" data-url="${esc(x.url)}">↗ GİT</button>
                        <button class="miniBtn copyLink" type="button" data-url="${esc(x.url)}">⧉ KOPYALA</button>
                    </div>
                </td>
            </tr>
            `;
        }).join('');
    }

    function showReport(allRows, scannedPages, totalCards, activeServer) {
        document.getElementById('itemci-v17')?.remove();

        const recentAll = allRows.filter(x =>
            x.ageHours !== null && x.ageHours <= 36
        );

        const olderOrUnknownCount = allRows.length - recentAll.length;

        const buyerRows = allRows
            .filter(x => x.ageHours !== null && x.ageHours <= 24)
            .map(x => ({
                ...x,
                buyerIntent: buyerIntentFromTitle(x.title)
            }))
            .filter(x => x.buyerIntent)
            .sort((a, b) => {
                const aa = a.ageHours ?? 999999;
                const bb = b.ageHours ?? 999999;
                return aa - bb;
            });

        const generalRows = [...recentAll].sort((a, b) => {
            const aa = a.ageHours ?? 999999;
            const bb = b.ageHours ?? 999999;
            return aa - bb;
        });

        const categories = categoriesForServer(activeServer);
        const categoryResults = categories.map(c => categoryResult(allRows, c));

        const updatedAt = new Date().toLocaleString('tr-TR', {
            day: '2-digit',
            month: '2-digit',
            year: 'numeric',
            hour: '2-digit',
            minute: '2-digit'
        });

        const safePrefix = id =>
            'cat_' + String(id).replace(/[^a-zA-Z0-9_-]/g, '_');

        const categoryTabsHtml = categoryResults.map((r, i) => `
            <button
                class="tabBtn ${i === 0 ? 'active' : ''}"
                data-view="${safePrefix(r.category.id)}View"
            >
                ${esc(r.category.name.toUpperCase())}
            </button>
        `).join('');

        const categorySectionsHtml = categoryResults.map((r, i) => {
            const c = r.category;
            const prefix = safePrefix(c.id);

            if (c.mode === 'search') {
                return `
                <section id="${prefix}View" class="view ${i === 0 ? 'active' : ''}">
                    <div class="stats">
                        <div class="stat"><span>EN UCUZ İLAN</span><strong class="green">${fmt(r.cheapest)} TL</strong></div>
                        <div class="stat"><span>MEDYAN FİYAT</span><strong>${fmt(r.median)} TL</strong></div>
                        <div class="stat"><span>ORTALAMA FİYAT</span><strong>${fmt(r.average)} TL</strong></div>
                        <div class="stat"><span>İLAN SAYISI</span><strong>${r.valid.length}</strong></div>
                        <div class="stat"><span>SON 36 SAAT</span><strong>${r.valid.length}</strong></div>
                    </div>

                    <div class="workspace">
                        <div class="modeHint">
                            <b>${esc(c.name)}</b> · Fiyat listesi modu ·
                            Eşleşmeler: <b>${esc(c.keywords.join(', '))}</b>
                        </div>

                        <div class="toolbar">
                            <div class="searchWrap">
                                <input
                                    class="search dynamicSearch"
                                    data-body=".${prefix}Body"
                                    type="text"
                                    autocomplete="off"
                                    placeholder="${esc(c.name)} ilanlarında ara..."
                                >
                                <span class="searchIcon">⌕</span>
                            </div>

                            <div class="filterInfo">
                                Gösterilen: <b class="${prefix}Shown">${r.valid.length}</b> / ${r.valid.length}
                                · Son tarama: ${esc(updatedAt)}
                            </div>
                        </div>

                        <div class="tableCard">
                            <div class="tableWrap">
                                <table class="sortableTable">
                                    <thead>
                                        <tr>
                                            <th>#</th>
                                            <th class="sortable" data-sort="seller" data-default-dir="asc"><span class="thInner">SATICI <span class="sortIcon">↕</span></span></th>
                                            <th class="sortable" data-sort="title" data-default-dir="asc"><span class="thInner">İLAN BAŞLIĞI <span class="sortIcon">↕</span></span></th>
                                            <th class="sortable numHead" data-sort="price" data-default-dir="asc"><span class="thInner">FİYAT <span class="sortIcon">↕</span></span></th>
                                            <th class="sortable timeHead" data-sort="age" data-default-dir="asc"><span class="thInner">SÜRE <span class="sortIcon">↕</span></span></th>
                                            <th>İŞLEM</th>
                                        </tr>
                                    </thead>
                                    <tbody class="${prefix}Body">
                                        ${r.valid.length
                                            ? categorySearchRowHtml(r.valid)
                                            : '<tr><td colspan="6" class="empty">Son 36 saatte eşleşen ilan bulunamadı.</td></tr>'}
                                    </tbody>
                                </table>
                            </div>
                        </div>

                        <div class="footnote">
                            <span>Bu kategoride birim fiyat hesabı yapılmaz; ilanların toplam fiyatı karşılaştırılır.</span>
                            <button class="categoryCsv" data-category-id="${esc(c.id)}">CSV</button>
                        </div>
                    </div>
                </section>
                `;
            }

            return `
            <section id="${prefix}View" class="view ${i === 0 ? 'active' : ''}">
                <div class="stats">
                    <div class="stat"><span>EN UCUZ</span><strong class="green">${fmt(r.cheapest)} TL / ${esc(c.unitLabel)}</strong></div>
                    <div class="stat"><span>MEDYAN</span><strong>${fmt(r.median)} TL / ${esc(c.unitLabel)}</strong></div>
                    <div class="stat"><span>ORTALAMA</span><strong>${fmt(r.average)} TL / ${esc(c.unitLabel)}</strong></div>
                    <div class="stat"><span>TOPLAM MİKTAR</span><strong>${fmt(r.totalAmount, 2)} ${esc(c.unitLabel)}</strong></div>
                    <div class="stat"><span>GEÇERLİ İLAN</span><strong>${r.valid.length}</strong></div>
                </div>

                <div class="workspace">
                    <div class="modeHint">
                        <b>${esc(c.name)}</b> · ${esc(categorySummary(c))}
                    </div>

                    <div class="toolbar">
                        <div class="searchWrap">
                            <input
                                class="search dynamicSearch"
                                data-body=".${prefix}Body"
                                type="text"
                                autocomplete="off"
                                placeholder="${esc(c.name)} ilanlarında ara..."
                            >
                            <span class="searchIcon">⌕</span>
                        </div>

                        <div class="filterInfo">
                            Gösterilen: <b class="${prefix}Shown">${r.valid.length}</b> / ${r.valid.length}
                            · Son 36 saat
                        </div>
                    </div>

                    ${buildUnitTableHtml(r.valid, prefix, c.unitLabel)}

                    <div class="footnote">
                        <span>${esc(categorySummary(c))}</span>
                        <button class="categoryCsv" data-category-id="${esc(c.id)}">CSV</button>
                    </div>

                    ${buildSuspiciousHtml(r.suspicious, c.name, c.unitLabel)}
                </div>
            </section>
            `;
        }).join('');

        const initialExtraActive = categoryResults.length === 0;

        const servers = loadServers();
        const serverOptions = servers.map(s => `
            <option value="${esc(s.id)}" ${s.id === activeServer.id ? 'selected' : ''}>
                ${esc(s.name)} · ${categoriesForServer(s).length} kategori
            </option>
        `).join('');

        const root = document.createElement('div');
        root.id = 'itemci-v17';

        root.innerHTML = `
<style>
#itemci-v17{
    --bg:#141311;--panel:#1c1a17;--panel3:#23211d;--line:#302d28;--line2:#3a362f;
    --text:#ede9e3;--muted:#a39c91;--orange:#e3c27a;--green:#7cc79a;
    position:fixed;inset:0;z-index:2147483647;overflow:auto;background:var(--bg);
    color:var(--text);font-family:"Segoe UI Variable","Segoe UI",Arial,sans-serif
}
#itemci-v17 *{box-sizing:border-box}
#itemci-v17 .topbar{position:sticky;top:0;z-index:30;padding:14px 18px 11px;background:rgba(20,19,17,.97);border-bottom:1px solid var(--line)}
#itemci-v17 .toprow{display:flex;align-items:center;justify-content:space-between;gap:16px}
#itemci-v17 .title{color:var(--orange);font-size:20px;font-weight:900}
#itemci-v17 .sub{margin-top:5px;color:var(--muted);font-size:12px}
#itemci-v17 .actions{display:flex;gap:6px;align-items:center}
#itemci-v17 button{height:34px;border:1px solid var(--line2);background:var(--panel3);color:var(--text);border-radius:10px;padding:0 11px;font-size:12px;font-weight:850;cursor:pointer}
#itemci-v17 button:hover{background:#2a2722;border-color:#4a443a}
#itemci-v17 .close{width:34px;padding:0;font-size:20px}
#itemci-v17 .rescan{color:#e3c27a;border-color:rgba(211,190,143,.38);background:rgba(211,190,143,.07)}
#itemci-v17 .serverbar{display:flex;align-items:center;gap:7px;margin-top:11px;padding-top:10px;border-top:1px solid var(--line);flex-wrap:wrap}
#itemci-v17 .serverLabel{color:#a39c91;font-size:11px;font-weight:900;letter-spacing:.06em}
#itemci-v17 .serverSelect{min-width:260px;height:34px;padding:0 32px 0 10px;border:1px solid var(--line2);border-radius:10px;outline:none;background:var(--panel3);color:var(--text);font-size:11px;font-weight:750}
#itemci-v17 .scanServer{color:#171614;border-color:var(--orange);background:var(--orange)}
#itemci-v17 .addServer{color:#7bf0b0;border-color:rgba(52,225,132,.35);background:rgba(52,225,132,.07)}
#itemci-v17 .categoriesBtn{color:#8fc7ff;border-color:rgba(80,160,255,.30);background:rgba(80,160,255,.06)}
#itemci-v17 .removeServer{color:#ff9696;border-color:rgba(255,107,107,.25);background:rgba(255,107,107,.05)}
#itemci-v17 .tabs{display:flex;gap:7px;margin-top:11px;flex-wrap:wrap}
#itemci-v17 .tabBtn{height:32px;padding:0 14px;color:#a7a49f;background:#1c1a17}
#itemci-v17 .tabBtn.active{color:#16130e;background:var(--orange);border-color:var(--orange)}
#itemci-v17 .view{display:none}
#itemci-v17 .view.active{display:block}
#itemci-v17 .stats{display:grid;grid-template-columns:repeat(5,minmax(0,1fr));gap:8px;padding:11px 14px 0}
#itemci-v17 .stat{min-height:61px;padding:10px 12px;background:linear-gradient(180deg,#1c1a17,#1c1a17);border:1px solid var(--line);border-radius:9px}
#itemci-v17 .stat span{display:block;color:#a39c91;font-size:11px;font-weight:750;margin-bottom:5px}
#itemci-v17 .stat strong{font-size:18px;white-space:nowrap}
#itemci-v17 .green{color:var(--green)!important}
#itemci-v17 .workspace{padding:10px 14px 18px}
#itemci-v17 .toolbar{display:flex;align-items:center;justify-content:space-between;gap:10px;margin-bottom:9px}
#itemci-v17 .searchWrap{position:relative;width:min(520px,100%)}
#itemci-v17 .search{width:100%;height:36px;padding:0 38px 0 12px;border:1px solid var(--line);border-radius:8px;outline:none;background:#1c1a17;color:#fff;font-size:12px}
#itemci-v17 .search:focus{border-color:rgba(211,190,143,.7);box-shadow:0 0 0 3px rgba(211,190,143,.08)}
#itemci-v17 .search::placeholder{color:#74716c}
#itemci-v17 .searchIcon{position:absolute;right:12px;top:50%;transform:translateY(-50%);color:#7f7c77}
#itemci-v17 .filterInfo{color:#8a8782;font-size:12px;text-align:right}
#itemci-v17 .filterInfo b{color:#e7e4df}
#itemci-v17 .tableCard{overflow:hidden;border:1px solid var(--line);border-radius:9px;background:#181510}
#itemci-v17 .tableWrap{max-height:calc(100vh - 245px);overflow:auto}
#itemci-v17 table{width:100%;min-width:1000px;table-layout:fixed;border-collapse:collapse}
#itemci-v17 thead{position:sticky;top:0;z-index:5}
#itemci-v17 th{padding:9px 10px;background:#23201b;border-bottom:1px solid #393631;color:var(--orange);text-align:left;font-size:11px;font-weight:900;letter-spacing:.05em}
#itemci-v17 th.sortable{cursor:pointer;user-select:none}
#itemci-v17 th.sortable:hover{background:#23211d;color:#ffc75f}
#itemci-v17 .thInner{display:flex;align-items:center;gap:6px}
#itemci-v17 .numHead .thInner{justify-content:flex-end}
#itemci-v17 .timeHead .thInner{justify-content:center}
#itemci-v17 .sortIcon{color:#73706b;font-size:12px}
#itemci-v17 th.sortActive{color:#ffd166}
#itemci-v17 th.sortActive .sortIcon{color:var(--green)}
#itemci-v17 td{padding:8px 10px;border-top:1px solid #23211d;font-size:11px;vertical-align:middle}
#itemci-v17 tbody tr:nth-child(even){background:#1c1a17}
#itemci-v17 tbody tr:hover{background:#25221d}
#itemci-v17 .rank{width:42px;color:#84817c}
#itemci-v17 .seller{color:var(--orange);font-weight:800;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
#itemci-v17 .listing{overflow:hidden}
#itemci-v17 .listingTitle{color:#f6f3ee;font-weight:650;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
#itemci-v17 .num{text-align:right;white-space:nowrap;font-variant-numeric:tabular-nums}
#itemci-v17 .unit{color:var(--green);font-weight:900}
#itemci-v17 .timeCell{text-align:center;white-space:nowrap}
#itemci-v17 .timeBadge{display:inline-flex;min-width:76px;height:24px;align-items:center;justify-content:center;padding:0 8px;border:1px solid #393631;border-radius:6px;background:#1c1a17;color:#aba8a3;font-size:11px;font-weight:800}
#itemci-v17 .intentCell{text-align:center;white-space:nowrap}
#itemci-v17 .intentBadge{display:inline-flex;align-items:center;justify-content:center;min-width:84px;height:24px;padding:0 8px;border:1px solid rgba(211,190,143,.32);border-radius:6px;background:rgba(211,190,143,.07);color:#e3c27a;font-size:11px;font-weight:900}
#itemci-v17 .actionCell{text-align:right;white-space:nowrap}
#itemci-v17 .rowActions{display:flex;justify-content:flex-end;gap:5px}
#itemci-v17 .miniBtn{height:26px;padding:0 8px;border-radius:6px;font-size:11px;font-weight:900}
#itemci-v17 .openListing{color:#e3c27a;border-color:rgba(211,190,143,.4);background:rgba(211,190,143,.08)}
#itemci-v17 .copyLink{color:#e1ded9;background:#23211d}
#itemci-v17 .copyLink.copied{color:#46e591;border-color:rgba(52,225,132,.55);background:rgba(52,225,132,.08)}
#itemci-v17 details{margin-top:9px;overflow:hidden;border:1px solid #554421;border-radius:9px}
#itemci-v17 summary{padding:10px 12px;color:#e9b64f;background:#1f1c17;font-size:12px;font-weight:800;cursor:pointer}
#itemci-v17 .footnote{display:flex;justify-content:space-between;align-items:center;gap:12px;margin-top:8px;color:#7b7873;font-size:11px}
#itemci-v17 .hiddenRow{display:none!important}
#itemci-v17 .empty{padding:36px!important;text-align:center!important;color:#807d78!important}
#itemci-v17 .modeHint{margin-bottom:9px;padding:9px 11px;border:1px solid rgba(211,190,143,.16);border-radius:8px;background:rgba(211,190,143,.04);color:#a6a39e;font-size:12px;line-height:1.5}
#itemci-v17 .toast{position:fixed;right:20px;bottom:20px;z-index:2147483647;opacity:0;transform:translateY(8px);padding:10px 13px;border:1px solid rgba(52,225,132,.42);border-radius:8px;background:#1e1b16;color:#7af0ae;font-size:11px;font-weight:800;pointer-events:none;transition:.18s ease}
#itemci-v17 .toast.show{opacity:1;transform:translateY(0)}
@media(max-width:900px){#itemci-v17 .stats{grid-template-columns:repeat(2,minmax(0,1fr))}#itemci-v17 .toolbar{align-items:stretch;flex-direction:column}#itemci-v17 .filterInfo{text-align:left}}

#itemci-v17 .stat{border-radius:13px;padding:15px;background:#211e19;min-height:90px}#itemci-v17 .stat strong{font-size:23px}#itemci-v17 .tableCard{border-radius:13px}#itemci-v17 td{padding-top:14px;padding-bottom:14px;font-size:12px}#itemci-v17 .scanServer{color:#1e170b;background:#7cc79a;border-color:#7cc79a}#itemci-v17 .topbar{background:#1c1a17}#itemci-v17 .lowest-unit{background:#2f2c27}#itemci-v17 .lowest-unit .unit:after{content:" EN DÜŞÜK";font-size:9px;display:block;color:#7cc79a}#itemci-v17 .own-label{display:inline-block;margin-left:8px;border:1px solid #665b43;border-radius:5px;padding:2px 5px;color:#e3c27a;font-size:10px}#itemci-v17 .own-listing{box-shadow:inset 3px 0 #e3c27a}
@media(max-width:700px){#itemci-v17 .toprow{flex-wrap:wrap}#itemci-v17 .stat strong{font-size:18px}#itemci-v17 .stats{gap:6px}#itemci-v17 .topbar{position:static}}

#itemci-v17{--bg:#141311;--panel:#1c1a17;--panel3:#23211d;--line:#302d28;--orange:#e3c27a;--green:#7cc79a;font-size:13px}
#itemci-v17 .topbar{padding:24px 28px 14px;background:#141311}
#itemci-v17 .title{font-size:28px;line-height:1.3;color:#f6f3ee;font-weight:750}
#itemci-v17 .sub{font-size:12px;line-height:1.7;margin-top:8px}
#itemci-v17 .serverbar{padding-top:15px;padding-bottom:12px;gap:12px}
#itemci-v17 .tabs{padding-top:12px;gap:10px}#itemci-v17 .tabBtn{height:40px;font-size:12px;padding:0 18px;border-radius:8px}
#itemci-v17 button{height:40px;font-size:12px;font-weight:600;border-radius:8px}
#itemci-v17 .serverSelect{height:42px;min-width:240px;font-size:13px;border-radius:8px}
#itemci-v17 .stats{gap:14px;padding:18px 28px 0}#itemci-v17 .stat{min-height:106px;padding:20px;border-radius:10px;background:#1c1a17}
#itemci-v17 .stat span{font-size:12px;letter-spacing:.02em;font-weight:500;color:#a39c91;margin-bottom:12px}
#itemci-v17 .stat strong{font-size:25px;font-weight:700}#itemci-v17 .workspace{padding:18px 28px 24px}
#itemci-v17 .toolbar{margin:0 0 16px;gap:16px}#itemci-v17 .search{height:44px;font-size:13px;background:#23211d;border-color:#302d28;padding-left:16px}
#itemci-v17 .tableCard{border-radius:10px;background:#1c1a17}#itemci-v17 th{padding:15px 14px;font-size:12px;font-weight:600;color:#a39c91;background:#24211c}
#itemci-v17 td{height:64px;padding:16px 14px;font-size:13px}#itemci-v17 .listingTitle{font-size:13px;font-weight:600}#itemci-v17 .seller{font-weight:600}
#itemci-v17 .miniBtn{height:32px;font-size:11px;padding:0 12px}#itemci-v17 .timeBadge{font-size:11px;height:30px;font-weight:500}
#itemci-v17 .addServer,#itemci-v17 .categoriesBtn{color:#e3c27a;border-color:#65563d;background:#23211d}
#itemci-v17 .scanServer{color:#24211c;background:#7cc79a;border-color:#7cc79a}#itemci-v17 .footnote{font-size:11px;margin-top:14px}
#itemci-v17 .tableWrap{max-height:calc(100vh - 410px);min-height:170px}
@media(max-width:950px){#itemci-v17 .stats{padding:14px 16px 0}#itemci-v17 .topbar{padding:18px 16px 12px}#itemci-v17 .workspace{padding:14px 16px}#itemci-v17 .title{font-size:24px}#itemci-v17 .stat strong{font-size:21px}#itemci-v17 .serverbar{flex-wrap:wrap}}
</style>

<div class="topbar">
    <div class="toprow">
        <div>
            <div class="title">Piyasa Analizi</div>
            <div class="sub">
                ${esc(activeServer.name)} · ${categories.length} özel kategori ·
                ${scannedPages} sayfa · ${totalCards} kart ·
                ${recentAll.length} son 36 saat ilan · ${buyerRows.length} son 24 saat alıcı ilanı
            </div>
        </div>

        <div class="actions">
            <button class="rescan">YENİDEN TARA</button>
            <button class="close">×</button>
        </div>
    </div>

    <div class="serverbar">
        <span class="serverLabel">SERVER</span>
        <select class="serverSelect">${serverOptions}</select>
        <button class="scanServer">SEÇİLİ SERVERİ TARA</button>
        <button class="addServer">+ SERVER EKLE</button>
        <button class="categoriesBtn">KATEGORİLER</button>
        <button class="removeServer">SERVER SİL</button>
    </div>

    <div class="tabs">
        ${categoryTabsHtml}
        <button class="tabBtn ${initialExtraActive ? 'active' : ''}" data-view="buyerView">ALICI ARAYANLAR</button>
        <button class="tabBtn" data-view="generalView">GENEL ARAMA</button>
    </div>
</div>

${categorySectionsHtml}

<section id="buyerView" class="view ${initialExtraActive ? 'active' : ''}">
    <div class="stats">
        <div class="stat"><span>SON 24 SAAT</span><strong>${buyerRows.length}</strong></div>
        <div class="stat"><span>EN YENİ İLAN</span><strong class="green">${buyerRows.length ? esc(buyerRows[0].time || 'Şimdi') : '—'}</strong></div>
        <div class="stat"><span>“ALINIR”</span><strong>${buyerRows.filter(x => x.buyerIntent === 'ALINIR').length}</strong></div>
        <div class="stat"><span>“LAZIM”</span><strong>${buyerRows.filter(x => x.buyerIntent === 'LAZIM').length}</strong></div>
        <div class="stat"><span>DİĞER</span><strong>${buyerRows.filter(x => !['ALINIR','LAZIM'].includes(x.buyerIntent)).length}</strong></div>
    </div>

    <div class="workspace">
        <div class="modeHint">
            Bu bölüm son 24 saatte item arayan kişileri toplar:
            <b>alınır, lazım, arıyorum, aranıyor, alınacak, alıyorum</b> vb.
        </div>

        <div class="toolbar">
            <div class="searchWrap">
                <input class="search buyerSearch" type="text" autocomplete="off" placeholder="Aranan itemi veya kullanıcıyı yaz...">
                <span class="searchIcon">⌕</span>
            </div>
            <div class="filterInfo">Gösterilen: <b class="buyerShown">${buyerRows.length}</b> / ${buyerRows.length}</div>
        </div>

        <div class="tableCard">
            <div class="tableWrap">
                <table class="sortableTable buyerTable">
                    <thead>
                        <tr>
                            <th>#</th>
                            <th class="sortable" data-sort="seller" data-default-dir="asc"><span class="thInner">KULLANICI <span class="sortIcon">↕</span></span></th>
                            <th class="sortable" data-sort="title" data-default-dir="asc"><span class="thInner">ARADIĞI / İLAN <span class="sortIcon">↕</span></span></th>
                            <th class="sortable" data-sort="intent" data-default-dir="asc"><span class="thInner">EŞLEŞME <span class="sortIcon">↕</span></span></th>
                            <th class="sortable numHead" data-sort="price" data-default-dir="asc"><span class="thInner">İLAN FİYATI <span class="sortIcon">↕</span></span></th>
                            <th class="sortable timeHead sortActive" data-sort="age" data-default-dir="asc" data-current-dir="asc"><span class="thInner">SÜRE <span class="sortIcon">↑</span></span></th>
                            <th>İŞLEM</th>
                        </tr>
                    </thead>
                    <tbody class="buyerBody">
                        ${buyerRows.length
                            ? buyerRowHtml(buyerRows)
                            : '<tr><td colspan="7" class="empty">Son 24 saatte alıcı ilanı bulunamadı.</td></tr>'}
                    </tbody>
                </table>
            </div>
        </div>

        <div class="footnote">
            <span>Alınır ilanındaki fiyat temsili olabilir; amaç alıcıyı hızlı bulmaktır.</span>
            <button class="buyerCsv">GÖRÜNENLERİ CSV</button>
        </div>
    </div>
</section>

<section id="generalView" class="view">
    <div class="stats generalStats">
        <div class="stat"><span>EN UCUZ İLAN</span><strong class="generalMin green">—</strong></div>
        <div class="stat"><span>MEDYAN FİYAT</span><strong class="generalMedian">—</strong></div>
        <div class="stat"><span>ORTALAMA FİYAT</span><strong class="generalAverage">—</strong></div>
        <div class="stat"><span>EŞLEŞEN İLAN</span><strong class="generalCount">${generalRows.length}</strong></div>
        <div class="stat"><span>SON 36 SAAT</span><strong>${recentAll.length}</strong></div>
    </div>

    <div class="workspace">
        <div class="modeHint">
            Genel aramada istediğin şeyi ara. Özel ürünler için servere ait kategorileri kullan.
        </div>

        <div class="toolbar">
            <div class="searchWrap">
                <input class="search generalSearch" type="text" autocomplete="off" placeholder="Ne arıyorsun?">
                <span class="searchIcon">⌕</span>
            </div>
            <div class="filterInfo">Gösterilen: <b class="generalShown">${generalRows.length}</b> / ${recentAll.length}</div>
        </div>

        <div class="tableCard">
            <div class="tableWrap">
                <table class="sortableTable generalTable">
                    <thead>
                        <tr>
                            <th>#</th>
                            <th class="sortable" data-sort="seller" data-default-dir="asc"><span class="thInner">SATICI <span class="sortIcon">↕</span></span></th>
                            <th class="sortable" data-sort="title" data-default-dir="asc"><span class="thInner">İLAN BAŞLIĞI <span class="sortIcon">↕</span></span></th>
                            <th class="sortable numHead" data-sort="price" data-default-dir="asc"><span class="thInner">FİYAT <span class="sortIcon">↕</span></span></th>
                            <th class="sortable timeHead sortActive" data-sort="age" data-default-dir="asc" data-current-dir="asc"><span class="thInner">SÜRE <span class="sortIcon">↑</span></span></th>
                            <th>İŞLEM</th>
                        </tr>
                    </thead>
                    <tbody class="generalBody">
                        ${generalRows.length
                            ? generalRowHtml(generalRows)
                            : '<tr><td colspan="6" class="empty">Son 36 saatte ilan bulunamadı.</td></tr>'}
                    </tbody>
                </table>
            </div>
        </div>

        <div class="footnote">
            <span>${olderOrUnknownCount} ilan 36 saat dışında veya zamanı okunamadığı için genel aramaya alınmadı.</span>
            <button class="generalCsv">GÖRÜNENLERİ CSV</button>
        </div>
    </div>
</section>

<div class="toast">Link kopyalandı</div>
        `;

        document.body.appendChild(root);
        for(const body of root.querySelectorAll('tbody')) {
            const rows=[...body.querySelectorAll('tr[data-unit]')];
            const units=rows.map(r=>Number(r.dataset.unit)).filter(n=>Number.isFinite(n)&&n>0);
            const lowest=units.length ? Math.min(...units) : null;
            rows.forEach(r=>r.classList.toggle('lowest-unit',lowest!==null&&Number(r.dataset.unit)===lowest));
        }
        // Only a verified native own-profile URL can mark ownership.
        const own=window.__niyaziVerifiedOwnStoreUrl;
        if(own) for(const row of root.querySelectorAll('tbody tr')) {
            const href=row.dataset.sellerUrl;
            if(href&&new URL(href,location.href).href.replace(/\/$/,'')===own.replace(/\/$/,'')) {
                row.classList.add('own-listing');const badge=document.createElement('span');badge.className='own-label';badge.textContent='Senin İlanın';row.querySelector('.listingTitle')?.append(badge);
            }
        }

        const serverSelect = root.querySelector('.serverSelect');

        root.querySelector('.scanServer').onclick = () => {
            const selected = loadServers().find(x => x.id === serverSelect.value);
            if (!selected) return;

            setActiveServer(selected.id);
            root.remove();
            runAnalysis(selected);
        };

        root.querySelector('.addServer').onclick = () => {
            const name = prompt('Serverin adını yaz.');
            if (!name || !name.trim()) return;

            const categoryInput = prompt(
                'Itemci serverin ANA kategori linkini veya Item&Yang kategori linkini yapıştır.'
            );
            if (!categoryInput) return;

            const normalizedUrl = normalizeServerCategoryUrl(categoryInput);
            if (!normalizedUrl) {
                alert('Geçerli bir Itemci kategori linki değil.');
                return;
            }

            const currentServers = loadServers();
            const existing = currentServers.find(
                x => x.url.toLowerCase() === normalizedUrl.toLowerCase()
            );

            if (existing) {
                alert(`Bu server zaten kayıtlı: ${existing.name}`);
                return;
            }

            const newServer = {
                id: serverIdFromName(name.trim()),
                name: name.trim(),
                url: normalizedUrl,
                categories: []
            };

            currentServers.push(newServer);
            saveServers(currentServers);
            setActiveServer(newServer.id);

            root.remove();

            openCategoryManager(newServer.id, updatedServer => {
                runAnalysis(updatedServer);
            });
        };

        root.querySelector('.categoriesBtn').onclick = () => {
            const selected = loadServers().find(x => x.id === serverSelect.value);
            if (!selected) return;

            root.remove();

            openCategoryManager(selected.id, updatedServer => {
                setActiveServer(updatedServer.id);
                runAnalysis(updatedServer);
            });
        };

        root.querySelector('.removeServer').onclick = () => {
            const selected = loadServers().find(x => x.id === serverSelect.value);
            if (!selected) return;

            if (selected.id === DEFAULT_SERVER.id) {
                alert('Varsayılan MSP2 Global - Yeni serveri silinemez.');
                return;
            }

            if (!confirm(`"${selected.name}" serverini silmek istiyor musun?`)) return;

            const remaining = loadServers().filter(x => x.id !== selected.id);
            saveServers(remaining);
            setActiveServer(DEFAULT_SERVER.id);

            root.remove();
            runAnalysis(loadServers().find(x => x.id === DEFAULT_SERVER.id) || DEFAULT_SERVER);
        };

        function applyDynamicSearch(input) {
            const bodySelector = input.dataset.body;
            if (!bodySelector) return;

            const rows = [...root.querySelectorAll(`${bodySelector} .data-row`)];
            const q = input.value;
            let shown = 0;

            rows.forEach(row => {
                const hit = queryMatches(row.dataset.search || '', q);
                row.classList.toggle('hiddenRow', !hit);
                if (hit) shown++;
            });

            const body = root.querySelector(bodySelector);
            if (!body) return;

            const section = body.closest('.view');
            const shownNode = section?.querySelector('.filterInfo b');
            if (shownNode) shownNode.textContent = String(shown);
        }

        root.querySelectorAll('.dynamicSearch').forEach(input => {
            input.addEventListener('input', () => applyDynamicSearch(input));
        });

        const buyerSearch = root.querySelector('.buyerSearch');
        const generalSearch = root.querySelector('.generalSearch');

        const buyerShown = root.querySelector('.buyerShown');
        const generalShown = root.querySelector('.generalShown');

        function applyBuyerSearch() {
            const rows = [...root.querySelectorAll('.buyerBody .buyerRow')];
            let shown = 0;

            rows.forEach(row => {
                const hit = queryMatches(row.dataset.search || '', buyerSearch.value);
                row.classList.toggle('hiddenRow', !hit);
                if (hit) shown++;
            });

            buyerShown.textContent = String(shown);
        }

        function updateGeneralStats() {
            const visibleRows = [...root.querySelectorAll('.generalBody .generalRow')]
                .filter(row => !row.classList.contains('hiddenRow'));

            const prices = visibleRows
                .map(row => Number(row.dataset.price))
                .filter(Number.isFinite);

            const min = prices.length ? Math.min(...prices) : null;
            const medValue = prices.length ? median(prices) : null;
            const avgValue = prices.length
                ? prices.reduce((a,b) => a+b, 0) / prices.length
                : null;

            root.querySelector('.generalMin').textContent = min === null ? '—' : `${fmt(min)} TL`;
            root.querySelector('.generalMedian').textContent = medValue === null ? '—' : `${fmt(medValue)} TL`;
            root.querySelector('.generalAverage').textContent = avgValue === null ? '—' : `${fmt(avgValue)} TL`;
            root.querySelector('.generalCount').textContent = String(visibleRows.length);
        }

        function applyGeneralSearch() {
            const rows = [...root.querySelectorAll('.generalBody .generalRow')];
            let shown = 0;

            rows.forEach(row => {
                const hit = queryMatches(row.dataset.search || '', generalSearch.value);
                row.classList.toggle('hiddenRow', !hit);
                if (hit) shown++;
            });

            generalShown.textContent = String(shown);
            updateGeneralStats();
        }

        buyerSearch.addEventListener('input', applyBuyerSearch);
        generalSearch.addEventListener('input', applyGeneralSearch);

        function sortTable(table, key, dir) {
            const tbody = table.querySelector('tbody');
            if (!tbody) return;

            const rows = [...tbody.querySelectorAll('.data-row')];
            const numericKeys = new Set(['amount', 'unit', 'price', 'age']);

            rows.sort((a, b) => {
                let av = a.dataset[key] ?? '';
                let bv = b.dataset[key] ?? '';
                let result = 0;

                if (numericKeys.has(key)) {
                    av = Number(av);
                    bv = Number(bv);

                    if (!Number.isFinite(av)) av = Number.POSITIVE_INFINITY;
                    if (!Number.isFinite(bv)) bv = Number.POSITIVE_INFINITY;

                    result = av - bv;
                } else {
                    result = String(av).localeCompare(String(bv), 'tr', {
                        sensitivity: 'base',
                        numeric: true
                    });
                }

                return dir === 'asc' ? result : -result;
            });

            rows.forEach((row, index) => {
                tbody.appendChild(row);
                const rank = row.querySelector('.rank');
                if (rank) rank.textContent = String(index + 1);
            });
        }

        function setSortHeaderState(table, activeHeader, dir) {
            table.querySelectorAll('th.sortable').forEach(th => {
                th.classList.remove('sortActive');
                th.removeAttribute('data-current-dir');

                const icon = th.querySelector('.sortIcon');
                if (icon) icon.textContent = '↕';
            });

            activeHeader.classList.add('sortActive');
            activeHeader.dataset.currentDir = dir;

            const icon = activeHeader.querySelector('.sortIcon');
            if (icon) icon.textContent = dir === 'asc' ? '↑' : '↓';
        }

        root.querySelectorAll('.sortableTable').forEach(table => {
            table.querySelectorAll('th.sortable').forEach(th => {
                th.addEventListener('click', () => {
                    const key = th.dataset.sort;
                    const currentDir = th.dataset.currentDir;

                    const nextDir =
                        currentDir === 'asc' ? 'desc' :
                        currentDir === 'desc' ? 'asc' :
                        th.dataset.defaultDir || 'asc';

                    sortTable(table, key, nextDir);
                    setSortHeaderState(table, th, nextDir);
                });
            });
        });

        root.querySelectorAll('.tabBtn').forEach(btn => {
            btn.addEventListener('click', () => {
                root.querySelectorAll('.tabBtn').forEach(x => x.classList.remove('active'));
                root.querySelectorAll('.view').forEach(x => x.classList.remove('active'));

                btn.classList.add('active');
                root.querySelector('#' + btn.dataset.view)?.classList.add('active');
            });
        });

        const toast = root.querySelector('.toast');
        let toastTimer = null;

        function showToast(text) {
            toast.textContent = text;
            toast.classList.add('show');
            clearTimeout(toastTimer);
            toastTimer = setTimeout(() => toast.classList.remove('show'), 1400);
        }

        root.addEventListener('click', async e => {
            const openBtn = e.target.closest('.openListing');

            if (openBtn) {
                const url = openBtn.dataset.url;
                if (url) window.open(url, '_blank', 'noopener');
                return;
            }

            const copyBtn = e.target.closest('.copyLink');

            if (copyBtn) {
                const url = copyBtn.dataset.url;
                if (!url) return;

                try {
                    await navigator.clipboard.writeText(url);
                } catch {
                    const ta = document.createElement('textarea');
                    ta.value = url;
                    ta.style.position = 'fixed';
                    ta.style.opacity = '0';
                    document.body.appendChild(ta);
                    ta.select();
                    document.execCommand('copy');
                    ta.remove();
                }

                const oldText = copyBtn.textContent;
                copyBtn.textContent = '✓ KOPYALANDI';
                copyBtn.classList.add('copied');
                showToast('İlan linki kopyalandı');

                setTimeout(() => {
                    copyBtn.textContent = oldText;
                    copyBtn.classList.remove('copied');
                }, 1400);
            }
        });

        root.querySelector('.close').onclick = () => root.remove();

        root.querySelector('.rescan').onclick = () => {
            root.remove();
            runAnalysis(activeServer);
        };

        const resultMap = new Map(
            categoryResults.map(r => [r.category.id, r])
        );

        root.querySelectorAll('.categoryCsv').forEach(btn => {
            btn.onclick = () => {
                const result = resultMap.get(btn.dataset.categoryId);
                if (!result) return;

                if (result.category.mode === 'search') {
                    csvDownloadGeneral(result.valid);
                } else {
                    csvDownload(
                        result.valid,
                        result.category.unitLabel,
                        `itemci_${normalizeSearchText(result.category.name).replace(/\s+/g, '_')}`
                    );
                }
            };
        });

        root.querySelector('.buyerCsv').onclick = () => {
            const visibleRows = [...root.querySelectorAll('.buyerBody .buyerRow')]
                .filter(row => !row.classList.contains('hiddenRow'));

            const urls = new Set(
                visibleRows.map(row => row.querySelector('.openListing')?.dataset.url).filter(Boolean)
            );

            csvDownloadGeneral(buyerRows.filter(x => urls.has(x.url)));
        };

        root.querySelector('.generalCsv').onclick = () => {
            const visibleRows = [...root.querySelectorAll('.generalBody .generalRow')]
                .filter(row => !row.classList.contains('hiddenRow'));

            const urls = new Set(
                visibleRows.map(row => row.querySelector('.openListing')?.dataset.url).filter(Boolean)
            );

            csvDownloadGeneral(generalRows.filter(x => urls.has(x.url)));
        };

        applyGeneralSearch();
    }

    function openServerMenu() {
        document.getElementById('itemci-server-menu')?.remove();

        const servers = loadServers();
        const activeServer = getActiveServer();

        const options = servers.map(s => `
            <option value="${esc(s.id)}" ${s.id === activeServer.id ? 'selected' : ''}>
                ${esc(s.name)} · ${categoriesForServer(s).length} kategori
            </option>
        `).join('');

        const overlay = document.createElement('div');
        overlay.id = 'itemci-server-menu';

        overlay.innerHTML = `
            <style>
            #itemci-server-menu{
                position:fixed;inset:0;z-index:2147483647;display:flex;align-items:center;justify-content:center;
                background:rgba(7,9,12,.72);backdrop-filter:blur(6px);font-family:Inter,Segoe UI,Arial,sans-serif
            }
            #itemci-server-menu *{box-sizing:border-box}
            #itemci-server-menu .box{width:min(590px,calc(100vw - 30px));background:#171614;border:1px solid #3a3732;border-radius:14px;box-shadow:0 20px 60px rgba(0,0,0,.55);color:#ede9e3;overflow:hidden}
            #itemci-server-menu .head{padding:18px 20px 14px;border-bottom:1px solid #2f2c27}
            #itemci-server-menu .title{color:#e3c27a;font-size:18px;font-weight:900}
            #itemci-server-menu .desc{margin-top:6px;color:#95928d;font-size:11px;line-height:1.45}
            #itemci-server-menu .body{padding:18px 20px 20px}
            #itemci-server-menu label{display:block;margin-bottom:7px;color:#a29f9a;font-size:10px;font-weight:850;letter-spacing:.05em}
            #itemci-server-menu select{width:100%;height:42px;padding:0 12px;border:1px solid #403d38;border-radius:9px;outline:none;background:#221f1a;color:#fff;font-size:13px;font-weight:750}
            #itemci-server-menu .buttons{display:grid;grid-template-columns:1fr 1fr;gap:8px;margin-top:12px}
            #itemci-server-menu button{height:38px;border:1px solid #3f3c37;border-radius:8px;background:#23211d;color:#ede9e3;font-size:10px;font-weight:900;cursor:pointer}
            #itemci-server-menu .scan{grid-column:1/-1;height:44px;border-color:#e3c27a;background:#e3c27a;color:#171614;font-size:12px}
            #itemci-server-menu .add{color:#66e8a2;border-color:rgba(52,225,132,.34);background:rgba(52,225,132,.06)}
            #itemci-server-menu .cats{color:#8fc7ff;border-color:rgba(80,160,255,.30);background:rgba(80,160,255,.06)}
            #itemci-server-menu .remove{color:#ff9696;border-color:rgba(255,107,107,.26);background:rgba(255,107,107,.05)}
            #itemci-server-menu .cancel{grid-column:1/-1;color:#a9a6a1;background:#1c1a17}
            #itemci-server-menu .note{margin-top:12px;color:#76736e;font-size:9px;line-height:1.55}
            </style>

            <div class="box">
                <div class="head">
                    <div class="title">SERVER SEÇ</div>
                    <div class="desc">
                        Her serverin ürün kategorileri birbirinden bağımsızdır.
                    </div>
                </div>

                <div class="body">
                    <label>KAYITLI SERVERLER</label>
                    <select class="serverSelect">${options}</select>

                    <div class="buttons">
                        <button class="scan">SEÇİLİ SERVERİ TARA</button>
                        <button class="add">+ SERVER EKLE</button>
                        <button class="cats">KATEGORİLERİ AYARLA</button>
                        <button class="remove">SERVER SİL</button>
                        <button class="cancel">VAZGEÇ</button>
                    </div>

                    <div class="note">
                        Örnek: Bir serverde “Yang = 15M / 20M Yang”, başka serverde “Yang = 40KT” olabilir.
                        Aynı servere ayrıca Beceri Kitabı, Pet, Simya veya istediğin başka ürünleri kategori olarak ekleyebilirsin.
                    </div>
                </div>
            </div>
        `;

        document.body.appendChild(overlay);

        const select = overlay.querySelector('.serverSelect');

        overlay.querySelector('.scan').onclick = () => {
            const selected = loadServers().find(x => x.id === select.value);
            if (!selected) return;

            setActiveServer(selected.id);
            overlay.remove();
            runAnalysis(selected);
        };

        overlay.querySelector('.add').onclick = () => {
            const name = prompt('Serverin listede görünecek adını yaz.');
            if (!name || !name.trim()) return;

            const categoryInput = prompt(
                'Itemci serverin ANA kategori linkini veya Item&Yang kategori linkini yapıştır.'
            );
            if (!categoryInput) return;

            const normalizedUrl = normalizeServerCategoryUrl(categoryInput);

            if (!normalizedUrl) {
                alert('Bu link geçerli bir Itemci kategori linki gibi görünmüyor.');
                return;
            }

            const currentServers = loadServers();

            const existing = currentServers.find(
                x => x.url.toLowerCase() === normalizedUrl.toLowerCase()
            );

            if (existing) {
                alert(`Bu server zaten kayıtlı: ${existing.name}`);
                return;
            }

            const newServer = {
                id: serverIdFromName(name.trim()),
                name: name.trim(),
                url: normalizedUrl,
                categories: []
            };

            currentServers.push(newServer);
            saveServers(currentServers);
            setActiveServer(newServer.id);

            overlay.remove();

            openCategoryManager(newServer.id, updatedServer => {
                openServerMenu();
            });
        };

        overlay.querySelector('.cats').onclick = () => {
            const selected = loadServers().find(x => x.id === select.value);
            if (!selected) return;

            overlay.remove();

            openCategoryManager(selected.id, () => {
                openServerMenu();
            });
        };

        overlay.querySelector('.remove').onclick = () => {
            const selected = loadServers().find(x => x.id === select.value);
            if (!selected) return;

            if (selected.id === DEFAULT_SERVER.id) {
                alert('Varsayılan MSP2 Global - Yeni serveri silinemez.');
                return;
            }

            if (!confirm(`"${selected.name}" serverini listeden silmek istiyor musun?`)) return;

            const remaining = loadServers().filter(x => x.id !== selected.id);
            saveServers(remaining);
            setActiveServer(DEFAULT_SERVER.id);

            overlay.remove();
            openServerMenu();
        };

        overlay.querySelector('.cancel').onclick = () => overlay.remove();

        overlay.addEventListener('click', e => {
            if (e.target === overlay) overlay.remove();
        });
    }

    async function runAnalysis(serverOverride = null) {
        const validOverride =
            serverOverride &&
            typeof serverOverride === 'object' &&
            typeof serverOverride.name === 'string' &&
            typeof serverOverride.url === 'string'
                ? serverOverride
                : null;

        const activeServer = validOverride || getActiveServer();

        const progress = makeProgress();
        const allByUrl = new Map();

        let scannedPages = 0;
        let totalCards = 0;
        let consecutiveNoNew = 0;

        try {
            progress.textContent = `${activeServer.name}: Item&Yang kategorisi bulunuyor...`;

            const categoryBase = await resolveItemYangCategory(activeServer.url);

            // Gerçek Item&Yang linkini bulduysak server kaydını güncelle.
            if (categoryBase !== activeServer.url) {
                const servers = loadServers();
                const idx = servers.findIndex(x => x.id === activeServer.id);

                if (idx >= 0) {
                    servers[idx] = { ...servers[idx], url: categoryBase, categories: categoriesForServer(servers[idx]) };
                    saveServers(servers);
                    activeServer.url = categoryBase;
                }
            }

            for (let page = 1; page <= MAX_PAGES; page++) {
                progress.textContent =
                    `${activeServer.name} taranıyor... Sayfa ${page} • Okunan ilan: ${allByUrl.size}`;

                let data;
                try {
                    data = await fetchPage(page, categoryBase);
                } catch (err) {
                    // İlk sayfa açılmazsa gerçek hata, sonraki sayfalarda taramayı durdur.
                    if (page === 1) throw err;
                    break;
                }

                const cards = cardsFromDocument(data.doc, categoryBase);
                scannedPages++;

                if (!cards.length) break;

                totalCards += cards.length;

                let newUrlsThisPage = 0;

                for (const card of cards) {
                    if (!card.href) continue;

                    const absoluteUrl = new URL(card.getAttribute('href'), data.url).href;

                    // DOMParser'da .href bazen base'i bilmediği için kendimiz kesinleştiriyoruz.
                    card.href = absoluteUrl;

                    if (!allByUrl.has(absoluteUrl)) {
                        newUrlsThisPage++;
                    }

                    const row = parseCard(card);
                    if (row) allByUrl.set(absoluteUrl, row);
                }

                if (newUrlsThisPage === 0) consecutiveNoNew++;
                else consecutiveNoNew = 0;

                // Aynı sayfa tekrar dönmeye başladıysa pagination bitmiştir.
                if (consecutiveNoNew >= 1 && page > 1) break;

                await sleep(PAGE_DELAY);
            }

            progress.textContent = 'Rapor hazırlanıyor...';
            await sleep(150);

            showReport(
                [...allByUrl.values()],
                scannedPages,
                totalCards,
                activeServer
            );
        } catch (err) {
            console.error('Itemci V17 hata:', err);
            alert(
                'Analiz sırasında hata oldu:\n\n' +
                err.message +
                '\n\nServer: ' + activeServer.name +
                '\nKayıtlı link: ' + activeServer.url
            );
        } finally {
            progress.remove();
        }
    }

    function addButton() {
        if (!document.body) return;

        let b = document.getElementById('itemci-v17-btn');

        if (!b) {
            b = document.createElement('button');
            b.id = 'itemci-v17-btn';
            b.type = 'button';
            b.textContent = '📊 ITEMCI PİYASA ANALİZİ';

            b.style.cssText = `
                position:fixed;
                right:20px;
                bottom:80px;
                z-index:2147483647;
                background:#1e1b16;
                color:#e3c27a;
                border:2px solid #e3c27a;
                padding:14px 18px;
                border-radius:10px;
                font-size:14px;
                font-weight:900;
                cursor:pointer;
                box-shadow:0 5px 20px rgba(0,0,0,.45);
                transition:.15s ease;
            `;

            b.addEventListener('mouseenter', () => {
                b.style.transform = 'translateY(-2px)';
            });

            b.addEventListener('mouseleave', () => {
                b.style.transform = 'translateY(0)';
            });

            b.onclick = () => openServerMenu();
            document.body.appendChild(b);
        }

        b.style.display = 'block';
        b.style.visibility = 'visible';
        b.style.opacity = '1';
    }

    function startPersistentLauncher() {
        // Alt iframe içinde ikinci buton üretme.
        if (window.top !== window.self) return;

        const ensure = () => {
            if (document.body && !document.getElementById('itemci-v17-btn')) {
                addButton();
            }
        };

        addButton();

        const observer = new MutationObserver(() => {
            ensure();
        });

        observer.observe(document.documentElement, {
            childList: true,
            subtree: true
        });

        window.addEventListener('popstate', () => {
            setTimeout(ensure, 100);
            setTimeout(ensure, 700);
        });

        window.addEventListener('hashchange', () => {
            setTimeout(ensure, 100);
        });

        // Mesajlar ekranı DOM'u yenilese bile buton geri gelsin.
        setInterval(ensure, 1000);
    }
    function desktopServerList() {
        return loadServers().map(s => ({
            id: s.id,
            name: s.name,
            url: s.url,
            categoryCount: categoriesForServer(s).length,
            isDefault: s.id === DEFAULT_SERVER.id
        }));
    }

    function desktopAddServer() {
        const name = prompt(
            'Serverin listede görünecek adını yaz.\n\nÖrnek: Azyrah2'
        );

        if (!name || !name.trim()) return;

        const categoryInput = prompt(
            'Itemci serverin ANA kategori linkini veya Item&Yang kategori linkini yapıştır.'
        );

        if (!categoryInput) return;

        const normalizedUrl = normalizeServerCategoryUrl(categoryInput);

        if (!normalizedUrl) {
            alert('Bu link geçerli bir Itemci kategori linki gibi görünmüyor.');
            return;
        }

        const currentServers = loadServers();

        const existing = currentServers.find(
            x => x.url.toLowerCase() === normalizedUrl.toLowerCase()
        );

        if (existing) {
            alert(`Bu server zaten kayıtlı: ${existing.name}`);
            return;
        }

        const newServer = {
            id: serverIdFromName(name.trim()),
            name: name.trim(),
            url: normalizedUrl,
            categories: []
        };

        currentServers.push(newServer);
        saveServers(currentServers);
        setActiveServer(newServer.id);

        openCategoryManager(newServer.id, () => {
            notifyDesktopServers();
        });
    }

    function desktopEditServer(serverId) {
        const server = loadServers().find(x => x.id === serverId);

        if (!server) {
            alert('Server bulunamadı.');
            return;
        }

        openCategoryManager(server.id, () => {
            notifyDesktopServers();
        });
    }

    function desktopDeleteServer(serverId) {
        if (serverId === DEFAULT_SERVER.id) {
            alert('MSP2 varsayılan server olduğu için silinemez.');
            return false;
        }

        const currentServers = loadServers();
        const server = currentServers.find(x => x.id === serverId);

        if (!server) return false;

        const remaining = currentServers.filter(x => x.id !== serverId);

        saveServers(remaining);
        setActiveServer(DEFAULT_SERVER.id);
        notifyDesktopServers(remaining);

        document.getElementById('itemci-server-menu')?.remove();
        document.getElementById('itemci-category-manager')?.remove();
        document.getElementById('itemci-v17')?.remove();

        return true;
    }

    async function desktopRunServer(serverId) {
        const server = loadServers().find(x => x.id === serverId);

        if (!server) {
            alert('Server bulunamadı.');
            return false;
        }

        setActiveServer(server.id);
        await runAnalysis(server);
        return true;
    }

    // Desktop uygulamasında sağ altta ayrı launcher üretmiyoruz.
    // Sol menü doğrudan serverleri ve işlemleri kontrol eder.
    window.__NiyazisugarGetServersJson = () =>
        JSON.stringify(desktopServerList());

    window.__NiyazisugarAddServer = desktopAddServer;
    window.__NiyazisugarEditServerById = desktopEditServer;
    window.__NiyazisugarDeleteServerById = desktopDeleteServer;
    window.__NiyazisugarRunServerById = desktopRunServer;

    window.__NiyazisugarOpenMarket = openServerMenu;
    function liveListingRow(row, server) {
        const match = categoriesForServer(server)
            .map(category => ({ category, parsed: analyzeCategoryRow(row, category) }))
            .find(result => result.parsed && Number.isFinite(result.parsed.unit) && result.parsed.amount > 0);
        return { ...row,
            quantity: match?.parsed.amount ?? null,
            unitPrice: match?.parsed.unit ?? null,
            unitLabel: match?.category.unitLabel || '',
            categoryName: match?.category.name || ''
        };
    }

    window.__NiyazisugarLiveListings = async serverId => {
        const server = loadServers().find(s => s.id === serverId) || getActiveServer();
        const base = await resolveItemYangCategory(server.url);
        const rows = new Map();
        for (let page = 1; page <= 2; page++) {
            const data = await fetchPage(page, base);
            const cards = cardsFromDocument(data.doc, base);
            for (const card of cards) {
                card.href = new URL(card.getAttribute('href'), data.url).href;
                const row = parseCard(card);
                if (row) rows.set(row.url, liveListingRow(row, server));
            }
            if (!cards.length) break;
            await sleep(PAGE_DELAY);
        }
        return [...rows.values()].sort((a,b) => (a.ageHours ?? 99999) - (b.ageHours ?? 99999)).slice(0, 6);
    };
    window.__NiyazisugarServerSalesScan = async (serverId, known, cursor, pageCursor = 1, measurements = [], attempts = {}, priceRepairs = [], sellerSnapshots = [], recordedUrls = []) => {
        const server = loadServers().find(s => s.id === serverId);
        if (!server) throw new Error('Sunucu bulunamadı.');
        const deadline = Date.now() + 30000;
        let rateLimited = false, failed = 0, checked = 0, complete = false;
        let lastRequestAt=0;
        const read = async url => {
            await sleep(Math.max(0,2500-(Date.now()-lastRequestAt)));
            if(Date.now()<(window.__niyaziSitePauseUntil||0)){rateLimited=true;throw new Error("Itemci işlem sınırı");}
            lastRequestAt=Date.now();
            const controller = new AbortController();
            const timer = setTimeout(() => controller.abort(), Math.min(8000, Math.max(1, deadline-Date.now())));
            try {
                const response = await fetch(url, {credentials:'include',cache:'no-store',signal:controller.signal});
                if (response.status === 429) {rateLimited=true;throw new Error('Site işlem sınırı; takip beklemeye alındı.');}
                if (!response.ok) throw new Error('HTTP '+response.status);
                if (response.url && new URL(response.url).pathname.replace(/\/$/,'') !== new URL(url).pathname.replace(/\/$/,'')) throw new Error('Sayfa başka bir adrese yönlendirildi.');
                return await response.text();
            } finally {clearTimeout(timer);}
        };
        let base=normalizeServerCategoryUrl(server.url);
        if (!base) throw new Error('Sunucu kategori bağlantısı geçersiz.');
        // Resolve from one bounded request, preserving the configured category.
        if (!/-itemyang$/.test(new URL(base).pathname)) {
            const html=await read(base);
            base=discoverItemYangUrl(new DOMParser().parseFromString(html,'text/html'),base);
            if(!base)throw new Error('ItemYang kategorisi bulunamadı.');
        }
        const active = new Map();
        let nextPage=1,pagesRead=0;
        // Live monitoring deliberately covers only the latest three pages.
        const pages=[1,2,3];
        for (const page of pages) {
            if(Date.now()>=deadline||rateLimited)break;
            try {
                const url=page===1?base:`${base}?page=${page}`;
                const doc=new DOMParser().parseFromString(await read(url),'text/html');
                const cards=cardsFromDocument(doc,base);
                pagesRead++;
                if(!cards.length){
                    const body=normalizeSearchText(doc.body.textContent);
                    if(/ilan bulunamadi|urun bulunamadi|sonuc bulunamadi|ilan yok/.test(body)){complete=true;nextPage=1;break;}
                    failed++;break;
                }
                for(const card of cards){card.href=new URL(card.getAttribute('href'),url).href;const row=parseCard(card);if(row)active.set(row.url,row);}
                nextPage=page>=MAX_PAGES?1:page+1;
                const hasNext=[...doc.querySelectorAll('a[href]')].some(a=>{try{const u=new URL(a.getAttribute('href'),url);return u.pathname===new URL(base).pathname&&Number(u.searchParams.get('page'))>page;}catch{return false;}});
                if(!hasNext||page===3){complete=true;nextPage=1;break;}
            } catch {failed++;break;}
            await sleep(250);
        }
        // A sold listing may still be present in a category. Check known URLs
        // directly even when pagination is incomplete; disappearance is never proof.
        const recorded=new Set(recordedUrls||[]);
        const trackingRows=[...active.values()].filter(row=>!recorded.has(row.url));
        known=(known||[]).filter(row=>!recorded.has(row.url));
        const snapshots=new Map((sellerSnapshots||[]).map(s=>[String(s.seller||'').toLocaleLowerCase('tr-TR'),s]));
        const stores=new Map();
        for(const row of [...trackingRows,...(known||[])]){
            const seller=String(row.seller||'').trim();if(!seller||['—','Bilinmiyor','Bilinmeyen satıcı'].includes(seller))continue;
            const url=row.sellerUrl||'https://itemci.com/user/store/'+encodeURIComponent(seller);
            try{const u=new URL(url);if(u.origin==='https://itemci.com'&&u.pathname.startsWith('/user/store/'))stores.set(seller.toLocaleLowerCase('tr-TR'),{seller,url:u.origin+u.pathname});}catch{}
        }
        const storeQueue=[...stores.entries()].sort(([a],[b])=>(Date.parse(snapshots.get(a)?.lastAttemptUtc||snapshots.get(a)?.observedAtUtc||'')||0)-(Date.parse(snapshots.get(b)?.lastAttemptUtc||snapshots.get(b)?.observedAtUtc||'')||0));
        const initial=storeQueue.some(([key])=>!snapshots.get(key)?.sellerId);
        const sellerResults=[],sellerAttempts=[],suspects=[];
        const knownUrls=new Set([...(known||[]),...trackingRows].map(r=>r.url));
        let storeRequests=0;
        for(const [key,store] of storeQueue.slice(0,initial?2:1)){
            if(Date.now()>=deadline||rateLimited)break;
            storeRequests++;sellerAttempts.push(store.seller);
            try{
                const snapshot=window.__NiyaziSellerSnapshot(await read(store.url),store.url);
                if(!snapshot){failed++;continue;}
                sellerResults.push(snapshot);
                const before=snapshots.get(key);
                if(before?.sellerId===snapshot.sellerId&&snapshot.complete&&before.complete&&snapshot.salesCount>before.salesCount){
                    const present=new Set(snapshot.products.map(r=>r.url));
                    suspects.push(...before.products.filter(r=>knownUrls.has(r.url)&&!present.has(r.url)));
                }
            }catch{failed++;}
        }
        let queue=window.__NiyaziSelectWatchQueue(trackingRows,known,measurements,complete,attempts);
        const repair=(priceRepairs||[]).find(row=>{try{const u=new URL(row.url);return u.origin==='https://itemci.com'&&u.pathname.startsWith('/product/');}catch{return false;}});
        queue=window.__NiyaziPlanWatchDetails(queue,suspects,repair?[repair]:[],Math.max(0,3-storeRequests),cursor,attempts);
        const sold=[],observations=[],attemptedUrls=[];let attempted=0;
        for(let i=0;i<Math.min(3,queue.length)&&Date.now()<deadline&&!rateLimited;i++){
            const row=queue[i];attempted++;attemptedUrls.push(row.url);
            try{
                const url=new URL(row.url);
                if(url.origin!=='https://itemci.com'||!url.pathname.startsWith('/product/'))continue;
                const html=await read(url.href);checked++;
                const observation=window.__NiyaziPublicProductState(html,url.href);
                if(observation)observations.push(observation);
                if(window.__NiyaziConfirmedPublicSale(html,url.href))sold.push(row);
            }catch{failed++;}
            await sleep(250);
        }
        return {rows:[...active.values()],sold,observations,attemptedUrls,sellerResults,sellerAttempts,complete,nextCursor:(Number(cursor)||0)+1,nextPage,pagesRead,checked,failed,rateLimited};
    };
    window.__NiyazisugarSaveQuickServer = (name, url) => {
        const normalized = normalizeServerCategoryUrl(url);
        if (!name.trim() || !normalized) throw new Error('Sunucu bilgisi geçersiz.');
        const servers = loadServers();
        const existing = servers.find(s => normalizeSearchText(s.name) === normalizeSearchText(name));
        if (existing) existing.url = normalized;
        else servers.push({id:serverIdFromName(name),name:name.trim(),url:normalized,categories:defaultCategoriesClone()});
        saveServers(servers);
        return true;
    };
    window.__NiyazisugarRunMarket = runAnalysis;
    window.__NiyazisugarMarketVersion = 'V17.1-Desktop';

})();
