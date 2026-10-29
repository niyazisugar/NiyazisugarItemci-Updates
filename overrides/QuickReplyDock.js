// Read-only structural diagnostics. No text, values, URLs, IDs or credentials.
window.__niyaziMessageLayout = () => {
 const skip=new Set(['SCRIPT','STYLE','NOSCRIPT','SVG','PATH','IMG']);let count=0;
 function visit(el){
  if(skip.has(el.tagName)||count++>=3000)return null;
  const r=el.getBoundingClientRect();
  const placeholder=el.getAttribute('placeholder')||'';
  const kind=/mesaj/i.test(placeholder)?'message-input':/kullanıcı|kullanici|kişi|kisi/i.test(placeholder)?'person-search':/ara/i.test(placeholder)?'search':'';
  const text=el.children.length===0?(el.textContent||'').trim():'';
  const marker=/^Mesajlarınızı Görüntüleyin$/.test(text)?'empty-conversation':/^Mesajlar$/.test(text)?'messages-heading':/^Gönder$/.test(text)?'send':/^\d{1,2}:\d{2}$/.test(text)?'time':'';
  const classes=[...el.classList].filter(x=>x.length<100&&!/[a-f0-9]{24,}|@|https?:/i.test(x)).slice(0,35);
  return {tag:el.tagName,classes,role:el.getAttribute('role')||'',type:['text','submit','button','password'].includes(el.getAttribute('type'))?el.getAttribute('type'):'',kind,marker,editable:el.getAttribute('contenteditable')==='true',rect:[r.x,r.y,r.width,r.height].map(Math.round),children:[...el.children].map(visit).filter(Boolean)};
 }
 return {format:1,page:'itemci-messages',viewport:[innerWidth,innerHeight],tree:visit(document.body),truncated:count>=3000};
};
(() => { 'use strict';
if(window.__niyaziQuickDockV24Installed){window.__niyaziQuickDockV24Refresh?.();return;}
window.__niyaziQuickDockV24Installed=true;
    const REPLIES = [
        {
            id:"ch1",
            short:"CH1 Depo",
            text:"CH1 depocudayım, bekliyorum.",
            key:"Alt+1",
            tone:"blue"
        },
        {
            id:"pazar",
            short:"Pazara At",
            text:"Pazara item at, çekeyim.",
            key:"Alt+2",
            tone:"blue"
        },
        {
            id:"onay",
            short:"Onay + Değerlendirme",
            text:"Onay yolladım, hayırlı olsun. Başka bir şey lazım olursa yazabilirsin. Bir de değerlendirme atarsan iyi olur kankam.",
            key:"Alt+3",
            tone:"gold"
        }
    ];

const key='niyazi_quick_replies_custom_v1';
try { const saved=JSON.parse(localStorage.getItem(key)); if(Array.isArray(saved)&&saved.every(x=>typeof x.short==='string'&&typeof x.text==='string')) REPLIES.splice(0,REPLIES.length,...saved); }catch{}
function isMessagesPage(){return /\/(panel\/mesaj|messages?|chat)/i.test(location.pathname);}
    const norm = v => String(v || "")
        .toLocaleLowerCase("tr-TR")
        .normalize("NFD")
        .replace(/[\u0300-\u036f]/g,"")
        .replace(/ı/g,"i")
        .replace(/\s+/g," ")
        .trim();

    const visible = el => {
        if (!el || !el.isConnected) return false;
        const r = el.getBoundingClientRect();
        const s = getComputedStyle(el);

        return (
            r.width > 0 &&
            r.height > 0 &&
            s.display !== "none" &&
            s.visibility !== "hidden"
        );
    };

    function findMessageInput() {
        const candidates = [
            ...document.querySelectorAll("textarea"),
            ...document.querySelectorAll('[contenteditable="true"]'),
            ...document.querySelectorAll('input[type="text"]')
        ].filter(visible);

        const scored = candidates.map(el => {
            const placeholder = norm(el.getAttribute("placeholder"));
            const aria = norm(el.getAttribute("aria-label"));
            const cls = norm(el.className || "");
            const parent = norm(el.parentElement?.textContent || "");
            const r = el.getBoundingClientRect();

            let score = 0;

            if (el.tagName === "TEXTAREA") score += 12;
            if (el.getAttribute("contenteditable") === "true") score += 10;
            if (placeholder.includes("mesaj")) score += 25;
            if (placeholder.includes("yaz")) score += 9;
            if (aria.includes("mesaj")) score += 15;
            if (cls.includes("message")) score += 10;
            if (cls.includes("chat")) score += 10;
            if (parent.includes("gonder") || parent.includes("gönder")) score += 6;

            if (r.left > window.innerWidth * .30) score += 7;
            if (r.top > window.innerHeight * .38) score += 5;

            if (placeholder.includes("kullanici") || placeholder.includes("kullanıcı")) score -= 40;
            if (placeholder.includes("ara")) score -= 20;

            return {el,score};
        }).sort((a,b) => b.score-a.score);

        return scored[0]?.score >= 12
            ? scored[0].el
            : null;
    }

    function setValue(target,value) {
        if (!target) return false;

        target.focus();

        if (target.getAttribute("contenteditable") === "true") {
            target.innerHTML = "";
            target.textContent = value;

            target.dispatchEvent(new InputEvent("input",{
                bubbles:true,
                inputType:value ? "insertText" : "deleteContentBackward",
                data:value || null
            }));

            target.dispatchEvent(new Event("change",{bubbles:true}));
            return true;
        }

        const proto = target.tagName === "TEXTAREA"
            ? HTMLTextAreaElement.prototype
            : HTMLInputElement.prototype;

        const setter = Object.getOwnPropertyDescriptor(proto,"value")?.set;

        if (setter) setter.call(target,value);
        else target.value = value;

        target.dispatchEvent(new Event("input",{bubbles:true}));
        target.dispatchEvent(new Event("change",{bubbles:true}));

        return true;
    }


let strip=null,frame=0;
function render(){
 if(strip) strip.replaceChildren(); else {strip=document.createElement('div');strip.id='niyazi-quick-replies';strip.style.cssText='box-sizing:border-box;display:flex;flex-wrap:wrap;align-items:center;gap:8px;padding:10px 12px;margin:0 0 8px;background:#1c1a17;border:1px solid #65563d;border-radius:10px;font:12px "Segoe UI",sans-serif;';}
 const label=document.createElement('span');label.textContent='⚡ Hızlı Cevaplar';label.style.color='#e3c27a';strip.append(label);
 REPLIES.forEach(r=>{const b=document.createElement('button');b.type='button';b.textContent=r.short;b.title=r.text;b.style.cssText='padding:9px 13px;border:1px solid #665b43;border-radius:8px;background:#25251f;color:#e3c27a;font:inherit;cursor:pointer;';b.onclick=()=>{const target=findMessageInput();if(target)setValue(target,r.text);};strip.append(b);});
 const gear=document.createElement('button');gear.type='button';gear.textContent='⚙';gear.title='Hızlı cevapları düzenle';gear.setAttribute('aria-label',gear.title);gear.style.cssText='margin-left:auto;border:0;background:transparent;color:#e3c27a;font-size:18px;cursor:pointer';
 gear.onclick=()=>{const index=prompt('Düzenlemek istediğin cevap numarası:\n'+REPLIES.map((r,i)=>(i+1)+'. '+r.short).join('\n')+'\nYeni cevap için '+(REPLIES.length+1));if(index===null)return;const n=Number(index)-1;if(!Number.isInteger(n)||n<0||n>REPLIES.length)return;const old=REPLIES[n];const short=prompt('Buton adı',old?.short||'');if(!short?.trim())return;const text=prompt('Cevap metni',old?.text||'');if(text===null)return;REPLIES[n]={id:old?.id||('custom'+Date.now()),short:short.trim(),text,key:old?.key||''};try{localStorage.setItem(key,JSON.stringify(REPLIES));}catch{alert('Cevaplar kaydedilemedi.');}render();};strip.append(gear);
}
function refresh(){if(frame)return;frame=requestAnimationFrame(()=>{frame=0;if(!isMessagesPage()){strip?.remove();return;}const target=findMessageInput();if(!target){strip?.remove();return;}if(!strip)render();
 // Insert above the smallest block containing the input, so flex send rows stay intact.
 let host=target; const parent=target.parentElement;
 if(parent && getComputedStyle(parent).display.includes('flex')) host=parent;
 if(host.parentElement && (strip.parentElement!==host.parentElement || strip.nextElementSibling!==host))host.before(strip);
 });}
window.__niyaziQuickDockV24Refresh=refresh;
new MutationObserver(records=>{if(records.some(r=>!strip?.contains(r.target)))refresh();}).observe(document.documentElement,{childList:true,subtree:true});
window.addEventListener('resize',refresh);window.addEventListener('popstate',refresh);
document.addEventListener('keydown',e=>{if(!isMessagesPage()||!e.altKey||e.ctrlKey||e.metaKey)return;const r=REPLIES[Number(e.key)-1];if(!r)return;const target=findMessageInput();if(target){e.preventDefault();setValue(target,r.text);}});
refresh();})();
