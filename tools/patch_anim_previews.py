import re, pathlib, sys

p = pathlib.Path('/data/aura/prototype/template.html')
h = p.read_text(encoding='utf-8')
if 'anim-grid' in h:
    print('already patched'); sys.exit(0)

rows = re.compile(r'<div class="row"><div class="lbl">Сворачивание</div>.*?data-key="windows\.open">.*?</div></div>\n', re.S)
assert rows.search(h), 'anim rows not found'

def card(v, title, sub):
    return (f'<button class="acard" data-v="{v}" aria-pressed="false"><div class="stage"><div class="mw"></div><i class="dock"></i></div>'
            f'<b>{title}</b><small>{sub}</small></button>')

min_cards = ''.join([
    card('genie', 'Джинн', 'Перетекает в иконку, как в macOS'),
    card('shrink', 'Сжатие', 'Уменьшается и летит в панель'),
    card('fade', 'Затухание', 'Мягко растворяется'),
    card('none', 'Без анимации', 'Мгновенно, ноль нагрузки'),
])
open_cards = ''.join([
    card('scale', 'Масштаб', 'Вырастает из центра'),
    card('slide', 'Снизу', 'Выезжает снизу вверх'),
    card('fade', 'Затухание', 'Плавно проявляется'),
    card('none', 'Без анимации', 'Мгновенно'),
])
grid_html = (
    '            <div class="group-t">Сворачивание — наведи и выбери</div>\n'
    f'            <div class="anim-grid" data-key="windows.minimize" data-kind="min">{min_cards}</div>\n'
    '            <div class="group-t">Открытие и закрытие</div>\n'
    f'            <div class="anim-grid" data-key="windows.open" data-kind="open">{open_cards}</div>\n'
)
h = rows.sub(lambda m: grid_html, h, count=1)

css = '''.anim-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(150px,1fr));gap:12px;margin:4px 0 18px}
.acard{display:flex;flex-direction:column;gap:4px;padding:8px 8px 12px;border-radius:14px;border:1px solid var(--border);background:var(--tile);color:var(--text);text-align:left;cursor:pointer;transition:border-color .2s,transform .2s,box-shadow .2s}
.acard:hover{transform:translateY(-2px);border-color:rgba(var(--accent-rgb),.5)}
.acard[aria-pressed=true]{border-color:var(--accent);box-shadow:0 0 0 3px rgba(var(--accent-rgb),.22)}
.acard b{font-size:13px;margin-top:6px;padding:0 4px}.acard small{color:var(--muted);font-size:11.5px;line-height:1.35;padding:0 4px}
.stage{position:relative;height:96px;border-radius:9px;overflow:hidden;background:radial-gradient(120% 90% at 15% 0%,rgba(var(--accent-rgb),.45),transparent 60%),radial-gradient(90% 90% at 95% 100%,rgba(110,80,255,.35),transparent 60%),#0c0c13}
.stage .mw{position:absolute;left:calc(50% - 35px);top:14px;width:70px;height:46px;border-radius:6px;background:var(--surface-solid);border:1px solid var(--border);box-shadow:0 6px 16px rgba(0,0,0,.45);will-change:transform,opacity}
.stage .mw::before{content:"";position:absolute;left:0;right:0;top:0;height:9px;border-radius:6px 6px 0 0;background:rgba(var(--accent-rgb),.35)}
.stage .mw::after{content:"";position:absolute;left:8px;right:22px;top:16px;height:4px;border-radius:2px;background:rgba(255,255,255,.2);box-shadow:0 8px 0 rgba(255,255,255,.13),12px 16px 0 rgba(255,255,255,.09)}
.stage .dock{position:absolute;left:50%;bottom:5px;transform:translateX(-50%);width:62px;height:14px;border-radius:7px;background:rgba(255,255,255,.12);border:1px solid var(--border)}
.stage .dock::after{content:"";position:absolute;left:50%;top:3px;width:8px;height:6px;margin-left:-4px;border-radius:2px;background:var(--accent);box-shadow:-14px 0 0 rgba(255,255,255,.35),14px 0 0 rgba(255,255,255,.35)}
'''
anchor = '@media (prefers-reduced-motion:reduce)'
assert anchor in h
h = h.replace(anchor, css + anchor, 1)

sync_anchor = 'function syncControls(){'
assert sync_anchor in h
h = h.replace(sync_anchor, sync_anchor + '\n  $$(".anim-grid").forEach(g=>{const v=get(g.dataset.key);$$(".acard",g).forEach(b=>b.setAttribute("aria-pressed",String(b.dataset.v===v)))});', 1)

js = r'''
/* ---- живые превью анимаций в хабе ---- */
const PREV_DY=46;
function miniMin(kind){
  if(kind==="genie")return [{offset:0,clipPath:"polygon(0% 0%,100% 0%,100% 100%,0% 100%)",transform:"none",opacity:1},
    {offset:.45,clipPath:"polygon(0% 0%,100% 0%,74% 100%,26% 100%)",transform:`translateY(${PREV_DY*.12}px) scaleY(.92)`,opacity:1},
    {offset:1,clipPath:"polygon(44% 0%,56% 0%,56% 100%,44% 100%)",transform:`translateY(${PREV_DY}px) scaleY(.04)`,opacity:.15}];
  if(kind==="shrink")return [{transform:"none",opacity:1},{transform:`translateY(${PREV_DY}px) scale(.12)`,opacity:0}];
  if(kind==="fade")return [{transform:"none",opacity:1},{transform:"scale(.96)",opacity:0}];
  return null;
}
const pause=ms=>new Promise(r=>setTimeout(r,ms));
function play(el,frames,opts){el.getAnimations().forEach(a=>a.cancel());return el.animate(frames,{fill:"forwards",...opts}).finished.catch(()=>{})}
async function loopCard(card,kind){
  const mw=$(".mw",card),v=card.dataset.v,panel=card.closest(".panel");
  for(;;){
    if(panel.hidden||document.hidden||reduceMotion){await pause(500);continue}
    const d=Math.max(160,state.windows.durationMs);
    if(kind==="min"){
      const f=miniMin(v);
      await pause(750);
      if(f)await play(mw,f,{duration:d,easing:"cubic-bezier(.5,0,.75,0)"});else{mw.getAnimations().forEach(a=>a.cancel());mw.style.opacity="0"}
      await pause(450);
      if(f)await play(mw,f,{duration:d,easing:"cubic-bezier(.3,0,.2,1)",direction:"reverse"});else mw.style.opacity="";
    }else{
      const f=openFrames(v);
      if(f)await play(mw,f,{duration:d,easing:"cubic-bezier(.2,.8,.2,1)"});else mw.style.opacity="";
      await pause(1000);
      if(f)await play(mw,f,{duration:d,easing:"cubic-bezier(.2,.8,.2,1)",direction:"reverse"});else mw.style.opacity="0";
      await pause(380);
    }
  }
}
$$(".anim-grid").forEach(g=>{
  g.addEventListener("click",e=>{const b=e.target.closest(".acard");if(!b)return;set(g.dataset.key,b.dataset.v);state.id="custom";apply()});
  $$(".acard",g).forEach(c=>loopCard(c,g.dataset.kind));
});
'''
try_anchor = re.search(r'\$\("#tryAnim"\)\.onclick=.*\n', h)
assert try_anchor, 'tryAnim not found'
h = h[:try_anchor.end()] + js + h[try_anchor.end():]

p.write_text(h, encoding='utf-8')
print('patched ok', len(h))
