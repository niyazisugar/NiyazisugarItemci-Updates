(() => {
'use strict';
if(window.__niyaziOwnMessages)return;window.__niyaziOwnMessages=true;
let host,ui,frame=0,last='',disabled=false,selection='',pending=null,timer=0,switching='';
const drafts=new Map();
const clean=e=>(e?.textContent||'').trim();
function source(){
 const search=[...document.querySelectorAll('input')].find(e=>/kullanıcı ara|kullanici ara|kişi.*ara/i.test(e.placeholder||''));if(!search)return null;
 let root=search.parentElement;while(root&&!(root.classList.contains('overflow-hidden')&&root.children.length===2&&root.querySelector('h2')))root=root.parentElement;
 if(!root)return null;let list=[...root.children].find(e=>e.contains(search)),chat=[...root.children].find(e=>e!==list);
 const rows=[...list.querySelectorAll('button')].filter(e=>e.querySelector('h4'));
 const stream=[...chat.children].find(e=>e.classList.contains('overflow-y-auto'));
 const messages=stream?[...stream.children].filter(e=>e.classList.contains('flex-col')).map(e=>{
  const bubble=e.querySelector('.whitespace-pre-wrap');if(!bubble)return null;
  const copy=bubble.cloneNode(true);const meta=copy.querySelector('.mt-1');meta?.remove();
  return {text:clean(copy),meta:clean(bubble.querySelector('.mt-1')),own:e.classList.contains('ml-auto')||e.classList.contains('items-end')};
 }).filter(Boolean):[];
 const input=chat.querySelector('input[placeholder],textarea[placeholder]');const form=input?.closest('form');
 const send=form?[...form.children].find(e=>e.tagName==='BUTTON')||form.querySelector('button[type=submit]'):null;
 const current=rows.find(e=>[...e.classList].some(c=>/^bg-orange-50/.test(c)));
 return {root,list,chat,search,rows,messages,input,send,current,name:clean(chat.querySelector('h3'))};
}
function node(tag,cls,text){const e=document.createElement(tag);e.className=cls||'';if(text!==undefined)e.textContent=text;return e;}
function setInput(el,value){const proto=el.tagName==='TEXTAREA'?HTMLTextAreaElement.prototype:HTMLInputElement.prototype;Object.getOwnPropertyDescriptor(proto,'value').set.call(el,value);el.dispatchEvent(new Event('input',{bubbles:true}));el.dispatchEvent(new Event('change',{bubbles:true}));}
function mount(){
 host=document.createElement('div');host.id='niyazi-message-workspace';host.style.cssText='position:fixed!important;inset:0!important;z-index:2147483647!important;display:block!important;background:#eceef2!important;';
 ui=host.attachShadow({mode:'open'});
 ui.innerHTML=`<style>
 :host{all:initial;font:13px 'Segoe UI',Arial,sans-serif;color:#272c35}*{box-sizing:border-box}button,input,textarea{font:inherit}button{cursor:pointer}button:disabled{cursor:default;opacity:.5}
 .layout{height:100dvh;display:grid;grid-template-columns:300px minmax(0,1fr);gap:14px;padding:16px;background:#eceef2}.card{border:1px solid #dce0e7;border-radius:22px;background:white;overflow:hidden;min-height:0;display:flex;flex-direction:column}.lefthead{padding:20px 18px 10px}.title{display:flex;align-items:center;justify-content:space-between;margin-bottom:16px;font-size:16px;font-weight:600}.muted{font-size:12px;color:#687384;font-weight:400}.search{width:100%;border:0;background:#f3f4f6;border-radius:14px;padding:12px;outline-color:#107b77}.threads{overflow:auto;padding:0 14px 14px;flex:1}.thread{display:flex;align-items:center;gap:12px;width:100%;border:0;background:white;text-align:left;padding:12px 10px;border-radius:16px;margin:2px 0}.thread:hover,.thread.active{background:#edf5f4}.avatar{display:grid;place-items:center;flex-shrink:0;width:44px;height:44px;border-radius:14px;background:#d4eae8;color:#087f79;font-weight:600}.threadbody{min-width:0;flex:1}.threadtop{display:flex;gap:6px;justify-content:space-between}.name{font-weight:600;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}.preview{color:#657080;font-size:12px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;margin-top:4px}.time{font-size:11px;color:#6c7684;white-space:nowrap}.header{height:66px;flex-shrink:0;padding:12px 18px;display:flex;align-items:center;gap:12px;border-bottom:1px solid #edf0f3}.header .info{flex:1;min-width:0}.quiet{border:0;border-radius:12px;background:#f3f4f6;color:#505a68;padding:9px 12px;font-size:12px}.history{flex:1;min-height:0;overflow:auto;background:#f9fafb;padding:24px;display:flex;flex-direction:column;gap:12px}.bubble{align-self:flex-start;max-width:78%;border:1px solid #e1e5eb;border-radius:18px;padding:12px 14px;background:white;white-space:pre-wrap;overflow-wrap:anywhere;line-height:1.5}.bubble.own{align-self:flex-end;background:#20242c;border-color:#20242c;color:#fff}.meta{font-size:11px;color:#76808d;margin-top:5px}.own .meta{color:#bec5ce}.empty{margin:auto;color:#687384;text-align:center;line-height:1.7}.footer{padding:12px;border-top:1px solid #edf0f3;background:white}.replies{display:flex;gap:6px;flex-wrap:wrap;margin-bottom:8px}.chip{border:1px solid #dce0e7;background:white;border-radius:20px;padding:7px 11px;color:#505b69;font-size:12px}.compose{display:flex;align-items:flex-end;gap:8px}.compose textarea{flex:1;min-width:0;height:44px;max-height:110px;border:0;border-radius:14px;background:#f2f3f6;padding:12px;resize:vertical;outline-color:#107b77}.send{background:#107b77;color:white;border:0;border-radius:14px;height:44px;padding:0 18px}.status{font-size:12px;min-height:18px;color:#647184;padding:5px 2px 0}.tools{display:flex;gap:6px;flex-wrap:wrap;padding:10px 18px;border-top:1px solid #edf0f3}.status.error{color:#a53e35}
 @media(max-width:760px){.layout{grid-template-columns:210px minmax(0,1fr);padding:6px;gap:6px}.history{padding:12px}.header{padding:10px}.bubble{max-width:95%}.thread{gap:6px;padding:9px 4px}.avatar{width:34px;height:34px}.lefthead{padding:14px 10px}.threads{padding:0 6px}.time{display:none}}@media(max-width:480px){.layout{grid-template-columns:145px minmax(0,1fr)}.avatar{display:none}.send{padding:0 10px}.quiet{font-size:10px;padding:7px}.footer{padding:8px}}
 </style><div class="layout"><aside class="card"><div class="lefthead"><div class="title">Mesajlar<span class="muted" id="count"></span></div><input class="search" id="search" placeholder="Kişi veya ilan ara…" aria-label="Konuşmalarda ara"></div><div class="threads" id="threads"></div><div class="tools"><button class="quiet" id="original">Itemci'de aç</button></div></aside><section class="card"><header class="header"><div class="avatar" id="avatar">N</div><div class="info"><div class="name" id="name">Mesajlar</div><div class="muted" id="subtitle">Bir konuşma seç</div></div></header><div class="history" id="history"></div><footer class="footer"><div class="replies" id="replies"></div><div class="compose"><textarea id="draft" aria-label="Mesaj yaz" placeholder="Mesaj yaz…" disabled></textarea><button class="send" id="send" disabled>Gönder</button></div><div id="status" class="status" role="status"></div></footer></section></div>`;
 document.documentElement.append(host);
 ui.querySelector('#original').onclick=()=>{disabled=true;host.remove();clearTimeout(timer);};
 ui.querySelector('#search').oninput=()=>{last='';schedule();};
 ui.querySelector('#threads').onscroll=e=>{const el=e.currentTarget;if(el.scrollHeight-el.scrollTop-el.clientHeight>60)return;const list=source()?.list.querySelector('.overflow-y-auto');if(list){list.scrollTop=list.scrollHeight;list.dispatchEvent(new Event('scroll',{bubbles:true}));}};
 ui.querySelector('#draft').oninput=()=>drafts.set(selection,ui.querySelector('#draft').value);
 ui.querySelector('#send').onclick=sendMessage;
 ui.querySelector('#draft').onkeydown=e=>{if(e.key==='Enter'&&!e.shiftKey){e.preventDefault();sendMessage();}};
 const wrap=ui.querySelector('#replies');let replies=[];try{replies=JSON.parse(localStorage.getItem('niyazi_quick_replies_custom_v1')||'null')||[];}catch{}
 if(!replies.length)replies=[{short:'CH1 Depo',text:'CH1 depocudayım, bekliyorum.'},{short:'Pazara At',text:'Pazara item at, çekeyim.'},{short:'Onay + Değerlendirme',text:'Onay yolladım, hayırlı olsun. Başka bir şey lazım olursa yazabilirsin. Bir de değerlendirme atarsan iyi olur kankam.'}];
 for(const r of replies){if(typeof r.short!=='string'||typeof r.text!=='string')continue;const b=node('button','chip',r.short);b.type='button';b.onclick=()=>{if(ui.querySelector('#draft').disabled)return;ui.querySelector('#draft').value=r.text;drafts.set(selection,r.text);ui.querySelector('#draft').focus();};wrap.append(b);}
 const gear=node('button','chip','Düzenle');gear.onclick=()=>{const old=JSON.stringify(replies,null,2);const raw=prompt('Hızlı cevaplar (short: buton adı, text: cevap metni)',old);if(raw===null)return;try{const saved=JSON.parse(raw);if(!Array.isArray(saved)||!saved.every(r=>typeof r.short==='string'&&typeof r.text==='string'))throw Error();localStorage.setItem('niyazi_quick_replies_custom_v1',JSON.stringify(saved));host.remove();host=null;last='';schedule();}catch{alert('Cevaplar kaydedilemedi. Geçerli bir liste gir.');}};wrap.append(gear);
}
function status(text,error=false){if(!ui)return;const e=ui.querySelector('#status');e.textContent=text;e.classList.toggle('error',error);}
function update(){frame=0;if(disabled)return;if(!/^\/panel\/mesajlar(?:\/|$)/.test(location.pathname)){host?.remove();host=null;last='';return;}
 const s=source();if(!s){host?.remove();host=null;last='';return;}if(!host?.isConnected)mount();
 if(switching&&s.name!==switching){ui.querySelector('#draft').disabled=true;ui.querySelector('#send').disabled=true;return;}if(switching){switching='';status('');}
 const name=s.name||clean(s.current?.querySelector('h4'));const key=name||'';
 const changed=selection!==key;if(changed){if(selection)drafts.set(selection,ui.querySelector('#draft').value);selection=key;ui.querySelector('#draft').value=drafts.get(key)||'';}
 if(pending&&pending.key===key&&s.messages.filter(m=>m.own&&m.text===pending.text).length>pending.count){clearTimeout(timer);pending=null;drafts.set(key,'');ui.querySelector('#draft').value='';status('Mesaj Itemci konuşmasında göründü.');}
 const query=ui.querySelector('#search').value.trim().toLocaleLowerCase('tr-TR');
 const signature=JSON.stringify([key,query,s.rows.map(e=>[clean(e),e===s.current]),s.messages,!!s.input,s.send?.disabled,pending?.key]);
 if(last===signature)return;last=signature;
 const threads=ui.querySelector('#threads');threads.replaceChildren();
 for(const row of s.rows){const title=clean(row.querySelector('h4')),preview=clean(row.querySelector('p'));if(query&&!`${title} ${preview}`.toLocaleLowerCase('tr-TR').includes(query))continue;
  const b=node('button','thread'+(row===s.current?' active':''));b.type='button';b.append(node('span','avatar',title.slice(0,2).toLocaleUpperCase('tr-TR')));const body=node('div','threadbody');const top=node('div','threadtop');top.append(node('span','name',title),node('span','time',clean(row.querySelector('h4')?.nextElementSibling)));body.append(top,node('div','preview',preview));b.append(body);
  b.onclick=()=>{if(pending){status('Gönderim sonucu bekleniyor; konuşmayı değiştirmeden önce kontrol et.',true);return;}ui.querySelector('#draft').disabled=true;ui.querySelector('#send').disabled=true;status('Konuşma açılıyor…');switching=title;row.click();last='';schedule();};threads.append(b);
 }
 ui.querySelector('#count').textContent=s.rows.length+' konuşma';ui.querySelector('#name').textContent=name||'Mesajlar';ui.querySelector('#avatar').textContent=name?name.slice(0,2).toLocaleUpperCase('tr-TR'):'N';ui.querySelector('#subtitle').textContent=s.input?'Itemci konuşması':'Bir konuşma seç';
 const history=ui.querySelector('#history');const atBottom=history.scrollHeight-history.scrollTop-history.clientHeight<80;history.replaceChildren();
 if(!s.messages.length)history.append(node('div','empty',s.input?'Bu konuşmada görüntülenebilir mesaj yok.':'Mesajlarını görüntülemek için soldan bir konuşma seç.'));
 for(const m of s.messages){const bubble=node('div','bubble'+(m.own?' own':''));bubble.append(node('div','',m.text));if(m.meta)bubble.append(node('div','meta',m.meta));history.append(bubble);}
 if(changed||atBottom)history.scrollTop=history.scrollHeight;
 ui.querySelector('#draft').disabled=!s.input||!!pending;ui.querySelector('#send').disabled=!s.input||!s.send||s.send.disabled||!!pending;
 if(changed&&!pending)status('');
}
function sendMessage(){
 const s=source(),text=ui?.querySelector('#draft').value.trim();if(!s?.input||!s.send||s.send.disabled||pending||!text)return;
 if(Date.now()<(window.__niyaziSitePauseUntil||0)){status('Itemci işlem sınırı nedeniyle bekleniyor.',true);return;}
 pending={key:selection,text,count:s.messages.filter(m=>m.own&&m.text===text).length};status('Gönderiliyor…');ui.querySelector('#send').disabled=true;ui.querySelector('#draft').disabled=true;
 setInput(s.input,text);
 // Let React apply the controlled input before clicking the real send button.
 requestAnimationFrame(()=>{const current=source();if(!current||current.name!==s.name||!current.send||current.send.disabled){pending=null;status('Gönderilmedi. Konuşmayı ve mesaj alanını kontrol et.',true);last='';schedule();return;}
 current.send.click();timer=setTimeout(()=>{if(!pending)return;status('Gönderim doğrulanamadı. Tekrar göndermeden önce Itemci’de kontrol et.',true);},15000);schedule();});
}
function schedule(){if(!frame)frame=requestAnimationFrame(update);}
new MutationObserver(schedule).observe(document.body,{childList:true,subtree:true,characterData:true,attributes:true,attributeFilter:['class','disabled']});
window.addEventListener('popstate',schedule);window.addEventListener('resize',schedule);schedule();
})();

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
