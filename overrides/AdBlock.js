(() => {
    "use strict";

    const VERSION = "275";
    const FLAG = `__niyazisugarAdBlockInstalledV${VERSION}`;
    const REFRESH = `__niyazisugarAdBlockRefreshV${VERSION}`;

    if (window[FLAG]) {
        try { window[REFRESH]?.(); } catch { }
        return;
    }

    window[FLAG] = true;

    const path = String(location.pathname || "").toLocaleLowerCase("tr-TR");

    if (
        path.startsWith("/user/login") ||
        path.startsWith("/user/register") ||
        path.includes("forgot") ||
        path.includes("password") ||
        path.includes("oauth") ||
        path.includes("auth")
    ) {
        return;
    }

    const norm = value => String(value || "")
        .toLocaleLowerCase("tr-TR")
        .replace(/\s+/g, " ")
        .trim();

    const style = document.createElement("style");
    style.id = `niyazisugar-adblock-style-v${VERSION}`;
    style.textContent = `
        iframe[src*="doubleclick.net" i],
        iframe[src*="googlesyndication.com" i],
        iframe[src*="googletagservices.com" i],
        iframe[src*="googleadservices.com" i],
        iframe[src*="adservice.google." i],
        iframe[src*="adnxs.com" i],
        iframe[src*="criteo." i],
        iframe[src*="taboola.com" i],
        iframe[src*="outbrain.com" i],
        iframe[src*="adform.net" i],
        iframe[src*="pubmatic.com" i],
        iframe[src*="rubiconproject.com" i],
        iframe[src*="openx.net" i],
        iframe[src*="adsrvr.org" i],
        iframe[src*="casalemedia.com" i],
        iframe[src*="smartadserver.com" i],
        iframe[src*="amazon-adsystem.com" i],
        [data-ad-slot],
        [data-ad-client],
        [data-google-query-id],
        ins.adsbygoogle,
        .adsbygoogle,
        [id^="google_ads" i],
        [id*="google_ads" i],
        [class~="advertisement" i],
        [class~="ad-container" i],
        [class~="ad-wrapper" i],
        [class~="ad-banner" i],
        [id~="advertisement" i],
        [id~="ad-container" i],
        [id~="ad-wrapper" i],
        [id~="ad-banner" i] {
            display: none !important;
            visibility: hidden !important;
            height: 0 !important;
            min-height: 0 !important;
            max-height: 0 !important;
            margin: 0 !important;
            padding: 0 !important;
            border: 0 !important;
            overflow: hidden !important;
            pointer-events: none !important;
        }
    `;

    (document.head || document.documentElement).appendChild(style);

    const blockedUrlFragments = [
        "doubleclick.net",
        "googlesyndication.com",
        "googletagservices.com",
        "googleadservices.com",
        "adservice.google.",
        "adnxs.com",
        "criteo.com",
        "criteo.net",
        "taboola.com",
        "outbrain.com",
        "adform.net",
        "pubmatic.com",
        "rubiconproject.com",
        "openx.net",
        "adsrvr.org",
        "casalemedia.com",
        "smartadserver.com",
        "amazon-adsystem.com",
        "scorecardresearch.com",
        "/pagead/",
        "/gampad/",
        "/adserver/"
    ];

    function looksLikeAdUrl(value) {
        const url = norm(value);
        if (!url)
            return false;

        return blockedUrlFragments.some(fragment => url.includes(fragment));
    }

    function isProtected(el) {
        if (!el || !el.matches)
            return true;

        if (
            el === document.body ||
            el === document.documentElement ||
            el.matches("html,body,main,header,nav,footer,form,#root,#__next,[role='main'],[role='navigation']")
        ) {
            return true;
        }

        if (
            el.querySelector?.(
                "form,input[type='password'],input[type='email'],button[type='submit']"
            )
        ) {
            return true;
        }

        return false;
    }

    function safeHide(el) {
        if (!el || !el.isConnected || !el.style || isProtected(el))
            return false;

        const r = el.getBoundingClientRect();

        if (
            r.width > window.innerWidth * 0.97 &&
            r.height > window.innerHeight * 0.48
        ) {
            return false;
        }

        el.style.setProperty("display", "none", "important");
        el.style.setProperty("visibility", "hidden", "important");
        el.style.setProperty("pointer-events", "none", "important");
        el.dataset.niyazisugarAdBlocked = "1";
        return true;
    }

    function candidateContainer(el, options = {}) {
        const maxHeight = options.maxHeight ?? 620;
        const maxWidth = options.maxWidth ?? Math.max(1250, window.innerWidth * 0.96);
        const minWidth = options.minWidth ?? 90;
        const requireOverlay = options.requireOverlay ?? false;
        const requireImage = options.requireImage ?? false;

        let cur = el;

        for (
            let i = 0;
            i < 5 && cur && cur !== document.body && cur !== document.documentElement;
            i++, cur = cur.parentElement
        ) {
            if (isProtected(cur))
                continue;

            const r = cur.getBoundingClientRect();
            const s = getComputedStyle(cur);

            if (
                r.width < minWidth ||
                r.height < 16 ||
                r.height > maxHeight ||
                r.width > maxWidth
            ) {
                continue;
            }

            if (requireImage && !cur.querySelector?.("img,picture,video,iframe"))
                continue;

            const overlay =
                s.position === "fixed" ||
                s.position === "absolute" ||
                s.position === "sticky";

            if (requireOverlay && !overlay)
                continue;

            return cur;
        }

        return null;
    }

    function hideKnownNetworkFrames() {
        for (const el of document.querySelectorAll("iframe,embed,object")) {
            const url =
                el.getAttribute("src") ||
                el.getAttribute("data") ||
                "";

            if (looksLikeAdUrl(url))
                safeHide(el);
        }
    }

    function hideItemciImageAds() {
        for (const link of document.querySelectorAll("a[href]")) {
            const href = norm(link.getAttribute("href"));
            const text = norm(link.innerText || link.textContent || "");
            const image = link.querySelector("img,picture,video");

            if (!image)
                continue;

            const r = link.getBoundingClientRect();
            if (r.width < 220 || r.height < 35 || r.height > 620)
                continue;

            const imageMeta = norm([
                image.getAttribute?.("src"),
                image.getAttribute?.("alt"),
                image.getAttribute?.("title"),
                image.getAttribute?.("class"),
                link.getAttribute("aria-label"),
                link.getAttribute("title")
            ].join(" "));

            const adSignal =
                href.includes("/reklam") ||
                href.includes("/advert") ||
                href.includes("/campaign") ||
                href.includes("/kampanya") ||
                imageMeta.includes("reklam") ||
                imageMeta.includes("advert") ||
                imageMeta.includes("banner") ||
                imageMeta.includes("campaign") ||
                imageMeta.includes("kampanya") ||
                text.includes("görsele tıkla") ||
                text.includes("gorsele tikla") ||
                text.includes("kampanyaya git");

            if (!adSignal)
                continue;

            if (link.closest("header,nav,footer"))
                continue;

            const box = candidateContainer(link, {
                maxHeight: 650,
                minWidth: 180,
                requireImage: true
            });

            safeHide(box || link);
        }
    }

    function hideTextAndOverlayAds() {
        const nodes = document.querySelectorAll(
            "div,section,aside,a,span,p,button"
        );

        for (const el of nodes) {
            if (el.dataset?.niyazisugarAdBlocked === "1")
                continue;

            if (isProtected(el))
                continue;

            if (el.children.length > 8)
                continue;

            const text = norm(el.innerText || el.textContent || "");
            if (!text || text.length > 220)
                continue;

            if (
                text === "reklam ver" ||
                (text === "reklam" && el.closest("header,nav,footer"))
            ) {
                continue;
            }

            if (
                text.includes("görsele tıkla") ||
                text.includes("gorsele tikla") ||
                text.includes("kampanyaya git") ||
                text.includes("sponsorlu içerik") ||
                text.includes("sponsorlu icerik") ||
                text === "sponsorlu" ||
                text === "advertisement"
            ) {
                const box = candidateContainer(el, {
                    maxHeight: 650,
                    requireImage: false
                });
                safeHide(box || el);
                continue;
            }

            if (
                text.includes("büyük çekiliş başladı") ||
                text.includes("buyuk cekilis basladi")
            ) {
                const box = candidateContainer(el, {
                    maxHeight: 160,
                    minWidth: 240
                });
                safeHide(box || el);
                continue;
            }

            if (text === "reklam") {
                const box = candidateContainer(el, {
                    maxHeight: 420,
                    requireImage: true
                });

                if (box)
                    safeHide(box);
            }
        }
    }

    function hideAttributeAds() {
        const selectors = [
            "[id*='advert' i]",
            "[class*='advert' i]",
            "[id*='sponsor' i]",
            "[class*='sponsor' i]",
            "[id*='campaign' i]",
            "[class*='campaign' i]",
            "[id*='kampanya' i]",
            "[class*='kampanya' i]",
            "[id*='banner-ad' i]",
            "[class*='banner-ad' i]",
            "[aria-label*='reklam' i]",
            "[aria-label*='advert' i]"
        ];

        for (const el of document.querySelectorAll(selectors.join(","))) {
            if (isProtected(el))
                continue;

            const r = el.getBoundingClientRect();
            if (r.height <= 650 && r.width <= Math.max(1400, window.innerWidth * 0.98))
                safeHide(el);
        }
    }

    function hideBackgroundAds() {
        for (const el of document.querySelectorAll("div,section,aside,a")) {
            if (isProtected(el))
                continue;

            const r = el.getBoundingClientRect();
            if (r.width < 220 || r.height < 40 || r.height > 650)
                continue;

            const bg = norm(getComputedStyle(el).backgroundImage);
            if (!bg || bg === "none")
                continue;

            if (
                looksLikeAdUrl(bg) ||
                bg.includes("reklam") ||
                bg.includes("advert") ||
                bg.includes("banner") ||
                bg.includes("campaign") ||
                bg.includes("kampanya")
            ) {
                safeHide(el);
            }
        }
    }

    function scan() {
        try { hideKnownNetworkFrames(); } catch { }
        try { hideItemciImageAds(); } catch { }
        try { hideTextAndOverlayAds(); } catch { }
        try { hideAttributeAds(); } catch { }
        try { hideBackgroundAds(); } catch { }
    }

    window[REFRESH] = scan;

    let scheduled = false;
    const scheduleScan = () => {
        if (scheduled)
            return;

        scheduled = true;
        requestAnimationFrame(() => {
            scheduled = false;
            scan();
        });
    };

    const observer = new MutationObserver(scheduleScan);
    observer.observe(document.documentElement, {
        childList: true,
        subtree: true,
        attributes: true,
        attributeFilter: ["src", "href", "class", "id", "style", "aria-label"]
    });

    scan();
    setTimeout(scan, 250);
    setTimeout(scan, 900);
    setTimeout(scan, 2200);
})();
