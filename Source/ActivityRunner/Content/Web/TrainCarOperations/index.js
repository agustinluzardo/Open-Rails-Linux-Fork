'use strict';
const root=document.getElementById('cars'), connection=document.getElementById('connection');
let busy=false;
const labels={
 handbrake:'Freno de mano', power:'Potencia', mu:'MU', battery:'Batería', ets:'Cable ETS',
 hose:'Manguera freno', 'front-angle':'Grifo delantero', 'rear-angle':'Grifo trasero',
 bleed:'Purgador', uncouple:'Desacoplar detrás'
};
function button(index,action,enabled,on=false,warn=false){
 const b=document.createElement('button'); b.textContent=labels[action]; b.disabled=!enabled;
 if(on)b.classList.add('on'); if(warn)b.classList.add('warn');
 b.addEventListener('click',()=>send(index,action)); return b;
}
function render(cars){
 root.replaceChildren();
 for(const car of cars){
  const el=document.createElement('section'); el.className='car';
  const id=document.createElement('div'); id.className='identity';
  const strong=document.createElement('strong'); strong.textContent=car.CarId||('Vehículo '+(car.Index+1));
  const type=document.createElement('span'); type.textContent='#'+(car.Index+1)+' · '+(car.Kind||'Vehículo');
  id.append(strong,type);
  const a=document.createElement('div'); a.className='actions';
  a.append(
   button(car.Index,'handbrake',car.HandbrakeAvailable,car.HandbrakeOn),
   button(car.Index,'power',car.PowerAvailable,car.PowerOn),
   button(car.Index,'mu',car.MuAvailable,car.MuConnected),
   button(car.Index,'battery',car.BatteryAvailable,car.BatteryOn),
   button(car.Index,'ets',car.EtsAvailable,car.EtsConnected),
   button(car.Index,'hose',true,car.FrontBrakeHoseConnected),
   button(car.Index,'front-angle',true,car.FrontAngleCockOpen),
   button(car.Index,'rear-angle',true,car.RearAngleCockOpen),
   button(car.Index,'bleed',car.BleedAvailable,car.BleedOpen),
   button(car.Index,'uncouple',car.CanUncoupleAfter,false,true)
  );
  el.append(id,a); root.appendChild(el);
 }
}
async function load(){
 if(busy)return;
 try{
  const r=await fetch('/API/TRAINCAROPERATIONS',{cache:'no-store'}); if(!r.ok)throw new Error();
  render(await r.json()); connection.textContent='Conectado';
 }catch{connection.textContent='Sin conexión'}
}
async function send(index,action){
 if(busy)return; busy=true;
 try{
  const r=await fetch('/API/TRAINCAROPERATIONS',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({Index:index,Action:action})});
  if(!r.ok)throw new Error(); render(await r.json()); connection.textContent='Conectado';
 }catch{connection.textContent='Error al ejecutar'} finally{busy=false}
}
load(); setInterval(load,600);
