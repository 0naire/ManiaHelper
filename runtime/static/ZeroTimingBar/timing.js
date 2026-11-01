// Custom presentation/connection code; the meter engine in vendor/ is AGPL-3.0.
import HitErrorMeter from './vendor/hitErrorMeter.js';
import {summarizePlay,countTimings} from './stats-model.js';
const meter = new HitErrorMeter();
const root = document.querySelector('.main');
const container = document.querySelector('.hitErrorMeterContainer');
const arrow = document.querySelector('.movingAverageArrow');
const panel=document.querySelector('.performance');
const demo=new URLSearchParams(location.search).get('demo')==='1';
let layout = {x:425,y:164,width:80,size:120,statsEnabled:true,statsX:270,statsY:115,statsSize:90,preview:false,enabled:false};
let currentPlay={},timingTotals={early:0,late:0};
function updateStats(){
  const s=summarizePlay(currentPlay,timingTotals);
  document.querySelector('#stats-accuracy').textContent=s.accuracy.toFixed(2);
  document.querySelector('#stats-ur').textContent=s.ur.toFixed(2);
  for(let i=0;i<6;i++)document.querySelector(`#stats-${i}`).textContent=s.counts[i].toLocaleString('en-US');
  for(const key of ['early','late','total'])document.querySelector(`#stats-${key}`).textContent=s[key].toLocaleString('en-US');
  document.querySelector('#stats-ratio').textContent=s.ratio;
}
function visibility(){
  const visible=demo||!!layout.enabled&&(active||layout.preview);
  container.style.opacity=visible?'1':'0';
  panel.style.opacity=visible&&layout.statsEnabled!==false?'1':'0';
}
let active = false, history = [], average = 0, lastTime = 0, lastFingerprint = '', received = false;
let settings = null;
function clearTicks(){meter.removeAllHitErrorTicks();history=[];average=0;arrow.style.left='0%'}
function position(){
  const scale = innerHeight / 480;
  const x = (innerWidth - innerHeight * 854 / 480) / 2 + layout.x * scale;
  root.style.left = `${x}px`; root.style.top = `${layout.y * scale}px`;
  root.style.transform = `scale(${layout.width * scale / 284 * (layout.size ?? 100) / 100})`;
  panel.style.left=`${(innerWidth-innerHeight*854/480)/2+(layout.statsX??270)*scale}px`;
  panel.style.top=`${(layout.statsY??115)*scale}px`;
  panel.style.transform=`scale(${innerHeight/1080*(layout.statsSize??90)/100})`;
}
async function loadLayout(){
  try {
    const next = await (await fetch(`layout.json?t=${Date.now()}`, {cache:'no-store'})).json();
    layout = next; position();
    visibility();
    if(layout.preview && !active && settings) meter.prepareHitErrorMeter('stable','mania',false,8,4,'',1);
  }catch(e){console.warn('Layout unavailable',e)}
}
function connect(endpoint,onData){
  const ws = new WebSocket(`ws://${location.host}${endpoint}`);
  ws.onmessage = e => {try{const d=JSON.parse(e.data);if(!d.error)onData(d)}catch(err){console.warn(err)}};
  ws.onclose = () => {if(endpoint==='/websocket/v2'){active=false;visibility()}setTimeout(()=>connect(endpoint,onData),1500)};
  ws.onerror=()=>ws.close();
}
settings = await (await fetch('meter-options.json')).json();
meter.applyUserSettings(settings);
await loadLayout();
window.addEventListener('resize',position);
setInterval(loadLayout,750);
if(!demo)connect('/websocket/v2',d=>{
  currentPlay=d.play||{};updateStats();
  const nextActive = d.state?.name === 'play' && d.settings?.mode?.name === 'mania';
  const time = d.beatmap?.time?.live ?? 0;
  const fingerprint = JSON.stringify([d.client,d.settings?.mode?.name,d.beatmap?.isConvert,d.beatmap?.stats?.od?.original,d.beatmap?.stats?.cs?.original,d.play?.mods?.name,d.play?.mods?.rate]);
  if(nextActive && (!active || time < lastTime - 1000)) clearTicks();
  active = nextActive; lastTime=time;
  if(fingerprint!==lastFingerprint){
    lastFingerprint=fingerprint;
    meter.prepareHitErrorMeter(d.client,d.settings.mode.name,d.beatmap.isConvert,d.beatmap.stats.od.original,d.beatmap.stats.cs.original,d.play.mods.name,d.play.mods.rate);
  }
  visibility();
});
if(!demo)connect('/websocket/v2/precise',d=>{
  if(!Array.isArray(d.hitErrors))return;
  const h=d.hitErrors;
  // Count the complete timing data, including when joining a play in progress.
  if(h.length!==history.length || (h.length&&h[h.length-1]!==history[history.length-1])){timingTotals=countTimings(h);updateStats();}
  if(!active){history=h.slice();received=false;return}
  let start=history.length;
  if(!received){start=Math.max(0,h.length-40);received=true}
  else if(h.length < history.length || (h.length && history.length && h[Math.min(h.length,history.length)-1]!==history[Math.min(h.length,history.length)-1])){
    clearTicks();start=Math.max(0,h.length-40);
  }
  for(let i=start;i<h.length;i++){
    const value=h[i]; if(typeof value!=='number'||!Number.isFinite(value))continue;
    meter.addTick(value);
    const ticks=document.querySelectorAll('.tick');
    // Only recent marks remain visible; dense patterns must not make a solid bar.
    for(let j=0;j<ticks.length-12;j++)ticks[j].remove();
    average=average*.9+value*.1;
    arrow.style.left=`${meter.getRelativeHitErrorPosition(average)*100}%`;
  }
  history=h.slice();
});
if(demo){
  document.body.style.background='#10121b';
  currentPlay={accuracy:98.68,unstableRate:45.2,hits:{geki:2000,'300':510,katu:65,'100':12,'50':3,'0':2}};
  timingTotals={early:108,late:99};updateStats();visibility();
  meter.prepareHitErrorMeter('stable','mania',false,8,4,'',1);
  setInterval(()=>[-32,-14,8,42].forEach(v=>meter.addTick(v)),1300);
}
