(() => {
 'use strict';
 if(window.__niyaziFogMessages)return;
 window.__niyaziFogMessages=true;
 const css=`
 html.niyazi-fog {color-scheme:light!important;background:#eceef2!important}
 .niyazi-fog-root{position:fixed!important;inset:14px!important;z-index:30!important;display:flex!important;gap:14px!important;background:#eceef2!important;padding:0!important;margin:0!important;max-width:none!important;width:auto!important;height:auto!important;min-height:0!important;font:13px 'Segoe UI',Arial,sans-serif!important;color:#242832!important}
 .niyazi-fog-root *{box-sizing:border-box;color:#252932!important;border-color:#e3e7ed!important}
 .niyazi-fog-root [class*=bg-neutral],.niyazi-fog-root [class*="dark:bg"]{background-color:transparent!important}
 .niyazi-fog-root .niyazi-fog-list{background:white!important}
 .niyazi-fog-root .niyazi-fog-chat{background:#f9fafb!important}
 .niyazi-fog-list{flex:0 0 300px!important;min-width:240px!important;max-width:34%!important;overflow:auto!important;background:white!important;border:1px solid #dce0e7!important;border-radius:22px!important;padding:14px!important;height:100%!important}
 .niyazi-fog-chat{flex:1 1 0!important;min-width:0!important;height:100%!important;overflow:auto!important;background:#f9fafb!important;border:1px solid #dce0e7!important;border-radius:22px!important;color:#252932!important}
 .niyazi-fog-list :is(h1,h2,h3){font-size:16px!important;margin:4px 6px 18px!important}
 .niyazi-fog-list input{width:100%!important;margin-bottom:10px!important}
 .niyazi-fog-list :is(a,[role=button],button){display:block!important;width:100%!important;text-align:left!important;padding:14px!important;border:0!important;background:#f0f6f5!important;margin:4px 0!important}
 .niyazi-fog-chat>h3{font-size:14px!important;padding:20px 22px!important;margin:0!important;border-bottom:1px solid #e9edf1!important;background:white!important}
 .niyazi-fog-chat form{display:flex!important;flex-wrap:wrap!important;gap:8px!important;align-items:center!important;padding:10px 12px!important;flex-shrink:0!important}
 .niyazi-fog-chat form textarea{flex:1 1 160px!important;min-width:0!important;resize:none!important;height:44px!important;min-height:44px!important}
 .niyazi-fog-chat form button[type=submit]{min-width:44px!important;min-height:44px!important;border:0!important;padding:10px 14px!important}
 .niyazi-fog-list input,.niyazi-fog-chat textarea,.niyazi-fog-chat input[type=text]{background:#f2f3f6!important;color:#252932!important;border:0!important;border-radius:14px!important;box-shadow:none!important;font:13px 'Segoe UI',sans-serif!important;padding:12px!important}
 .niyazi-fog-list :is(a,[role=button],button){border-radius:16px!important;color:#252932!important}
 .niyazi-fog-list :is([aria-selected=true],[data-state=active]){background:#edf5f4!important}
 .niyazi-fog-chat :is(form,[class*=border-t]){background:white!important;border-color:#e9edf1!important}
 .niyazi-fog-chat :is([class*=bg-orange-500],[class*=bg-blue-500],[class*=bg-primary]){background:#20242c!important;color:white!important;border-radius:18px!important}
 .niyazi-fog-chat button[type=submit]{background:#0e7875!important;color:white!important;border-radius:14px!important}
 .niyazi-fog-root #niyazi-quick-replies{background:white!important;border:0!important;border-radius:0!important;padding:6px 10px!important;gap:6px!important;margin:0!important}
 .niyazi-fog-root #niyazi-quick-replies>span{display:none}
 .niyazi-fog-root #niyazi-quick-replies button{background:white!important;border:1px solid #dce0e7!important;border-radius:20px!important;color:#505969!important;padding:8px 12px!important;font-size:12px!important}
 #niyazi-fog-toggle{position:fixed;right:26px;top:18px;z-index:45;border:1px solid #dce0e7;border-radius:12px;background:white;color:#505969;padding:6px 10px;font:11px 'Segoe UI',sans-serif;cursor:pointer}
 @media(max-width:760px){.niyazi-fog-root{inset:6px!important;gap:6px!important}.niyazi-fog-list{flex-basis:220px!important;min-width:160px!important;padding:8px!important}.niyazi-fog-chat{border-radius:16px!important}}
 `;
 let marked=[],frame=0,disabled=false;
 const style=document.createElement('style');style.id='niyazi-fog-style';style.textContent=css;document.head.append(style);
 const toggle=document.createElement('button');toggle.type='button';toggle.id='niyazi-fog-toggle';toggle.textContent='Itemci görünümü';toggle.title='Mesaj ekranının görünümünü değiştir';
 function clear(){for(const [el,cls]of marked)el.classList.remove(cls);marked=[];document.documentElement.classList.remove('niyazi-fog');toggle.remove();}
 function apply(){frame=0;if(disabled||!/^\/panel\/mesajlar(?:\/|$)/.test(location.pathname)){clear();return;}
  const input=[...document.querySelectorAll('textarea,[contenteditable=true]')].find(e=>e.getBoundingClientRect().width>0);
  const searches=[...document.querySelectorAll('input')].filter(e=>/ara/i.test(e.placeholder||'')&&e.getBoundingClientRect().width>0);
  const search=searches.find(e=>/kullanıcı|kullanici|kişi|kisi/i.test(e.placeholder||''))||searches[0];
  // Itemci has no composer until a conversation is selected. The empty
  // conversation placeholder is equally valid as a right-pane anchor.
  const empty=[...document.querySelectorAll('h1,h2,h3,p,div,span')].find(e=>/^mesajlarınızı görüntüleyin$/i.test(e.textContent.trim())&&e.children.length<2&&e.getBoundingClientRect().width>0);
  const anchor=input||empty;
  if(!anchor||!search){clear();return;}
  let root=anchor.parentElement;while(root&&!root.contains(search))root=root.parentElement;
  if(!root||root===document.body||root===document.documentElement){clear();return;}
  const branch=el=>{while(el.parentElement!==root)el=el.parentElement;return el;};
  const list=branch(search),chat=branch(anchor);if(list===chat){clear();return;}
  if(marked[0]?.[0]===root&&marked[1]?.[0]===list&&marked[2]?.[0]===chat)return;
  clear();marked=[[root,'niyazi-fog-root'],[list,'niyazi-fog-list'],[chat,'niyazi-fog-chat']];for(const[el,cls]of marked)el.classList.add(cls);
  document.documentElement.classList.add('niyazi-fog');document.body.append(toggle);
 }
 function schedule(){if(!frame)frame=requestAnimationFrame(apply);}
 toggle.onclick=()=>{disabled=true;clear();};
 new MutationObserver(records=>{if(records.some(r=>r.target!==style&&r.target!==toggle))schedule();}).observe(document.body,{childList:true,subtree:true});
 window.addEventListener('popstate',schedule);window.addEventListener('resize',schedule);schedule();
})();

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
