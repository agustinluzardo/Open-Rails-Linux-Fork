'use strict';
const nameEl=document.getElementById('activityName');
const briefingEl=document.getElementById('briefing');
const eventsEl=document.getElementById('events');
const connectionEl=document.getElementById('connection');
const beepEl=document.getElementById('beep');
let lastSequence=0, initialized=false;
beepEl.checked=localStorage.getItem('riel/activityevents/beep')==='true';
beepEl.addEventListener('change',()=>localStorage.setItem('riel/activityevents/beep',String(beepEl.checked)));

function beep(){
  if(!beepEl.checked) return;
  try {
    const ctx=new (window.AudioContext||window.webkitAudioContext)();
    const osc=ctx.createOscillator(), gain=ctx.createGain();
    osc.frequency.value=880; gain.gain.value=.045; osc.connect(gain); gain.connect(ctx.destination);
    osc.start(); osc.stop(ctx.currentTime+.12); osc.onended=()=>ctx.close();
  } catch {}
}
function render(data){
  nameEl.textContent=data.Name||'Sin actividad';
  const intro=[data.Description,data.Briefing].filter(Boolean).join('\n\n');
  briefingEl.textContent=intro; briefingEl.classList.toggle('hidden',!intro);
  const events=data.Events||[];
  eventsEl.replaceChildren();
  for(const ev of events){
    const article=document.createElement('article'); article.className='event';
    const time=document.createElement('time'); time.textContent=new Date(ev.TimestampUtc).toLocaleTimeString();
    const h=document.createElement('h2'); h.textContent=ev.Header||'Evento';
    const text=document.createElement('div'); text.textContent=ev.Text||'';
    article.append(time,h,text); eventsEl.appendChild(article);
  }
  const newest=events.length?events[events.length-1].Sequence:0;
  if(initialized && newest>lastSequence) beep();
  lastSequence=newest; initialized=true;
}
async function update(){
  try{
    const response=await fetch('/API/ACTIVITYEVENTS',{cache:'no-store'});
    if(!response.ok) throw new Error(response.statusText);
    render(await response.json());
    connectionEl.textContent='Conectado';
  }catch(e){ connectionEl.textContent='Sin conexión'; }
}
update(); setInterval(update,750);
