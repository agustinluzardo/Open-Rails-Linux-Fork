'use strict';
const root=document.getElementById('controls'), connection=document.getElementById('connection');
async function send(command,eventName){
 try{
  const r=await fetch('/API/SWITCHPANEL',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({Command:command,Event:eventName})});
  if(!r.ok)throw new Error(); render(await r.json()); connection.textContent='Conectado';
 }catch{connection.textContent='Error al ejecutar'}
}
function render(items){
 root.replaceChildren(); const grid=document.createElement('div'); grid.className='grid';
 for(const item of items){
  const card=document.createElement('div'); card.className='control';
  const b=document.createElement('button'); b.textContent=item.Label;
  const state=document.createElement('div'); state.className='state'; state.textContent=item.State||'';
  if(item.Momentary){
   const down=()=>send(item.Command,'pressed'), up=()=>send(item.Command,'released');
   b.addEventListener('pointerdown',e=>{e.preventDefault();down()});
   b.addEventListener('pointerup',up); b.addEventListener('pointercancel',up); b.addEventListener('pointerleave',e=>{if(e.buttons)up()});
  }else b.addEventListener('click',()=>send(item.Command,'pressed'));
  card.append(b,state); grid.appendChild(card);
 }
 root.appendChild(grid);
}
async function load(){
 try{const r=await fetch('/API/SWITCHPANEL',{cache:'no-store'});if(!r.ok)throw new Error();render(await r.json());connection.textContent='Conectado'}
 catch{connection.textContent='Sin conexión'}
}
load();setInterval(load,700);
