// Custom presentation/connection code; the meter engine in vendor/ is AGPL-3.0.
import HitErrorMeter from './vendor/hitErrorMeter.js';
import {summarizePlay,countTimings,buildDistribution} from './stats-model.js?v=1.3.0';
const meter = new HitErrorMeter();
const root = document.querySelector('.main');
const container = document.querySelector('.hitErrorMeterContainer');
const arrow = document.querySelector('.movingAverageArrow');
const panel=document.querySelector('.performance');
const demo=new URLSearchParams(location.search).get('demo')==='1';
let layout = {x:425,y:164,width:80,size:120,statsEnabled:true,statsX:660,statsY:86,statsSize:110,graphEnabled:true,playerEnabled:true,preview:false,enabled:false};
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
  document.querySelector('.distribution').style.display=layout.graphEnabled===false?'none':'';
  document.querySelector('.player-profile').style.display=layout.playerEnabled===false?'none':'';
}
function updateGraph(errors){
  const d=buildDistribution(errors,41,150);
  const width=155/d.bins.length;
  document.querySelector('#graph-count').textContent=d.total.toLocaleString('en-US')+' HITS';
  document.querySelector('#timing-graph').innerHTML='<line x1="77.5" x2="77.5" y1="0" y2="60" stroke="#ffffff40" stroke-width=".6"/>'+d.bins.map((n,i)=>{const h=n?Math.max(1,n/d.max*57):0;return `<rect x="${(i*width+.3).toFixed(2)}" y="${(60-h).toFixed(2)}" width="${(width-.6).toFixed(2)}" height="${h.toFixed(2)}" fill="${i<20?'#7bccc4':i>20?'#db9ab3':'#e8edf4'}" opacity=".88"/>`;}).join('');
}
let lastAvatar='';
function showPlayer(p){
  const name=p.name||'PLAYER';
  document.querySelector('#player-name').textContent=name;
  document.querySelector('#player-initials').textContent=name.slice(0,2).toUpperCase();
  document.querySelector('#player-country').textContent=p.countryName||p.countryCode||'로그인 정보 대기';
  for(const keys of [4,7]){const value=p[`countryRank${keys}K`];const rank=Number.isInteger(value)&&value>0?'#'+value.toLocaleString('en-US'):'—';const label=document.querySelector(`#player-rank${keys}`);label.textContent=`${rank} (${keys} Keys)`;label.title=(p.countryName||'')+' · '+(p.updatedAt?'osu! 공식 프로필 조회: '+p.updatedAt:(p.status||''));}
  const country=/^[A-Z]{2}$/.test(p.countryCode||'')?p.countryCode:'';
  const flagUrl=country?`https://osu.ppy.sh/images/flags/${country}.png`:'';
  for(const flag of document.querySelectorAll('.player-flag')){if(flagUrl){if(flag.getAttribute('src')!==flagUrl)flag.src=flagUrl;flag.alt=p.countryName||country;flag.title=p.countryName||country;flag.onerror=()=>flag.removeAttribute('src');}else flag.removeAttribute('src');}
  const avatar=document.querySelector('#player-avatar');
  if(p.avatarUrl!==lastAvatar){lastAvatar=p.avatarUrl||'';if(lastAvatar&&/^https:\/\/a\.ppy\.sh\//.test(lastAvatar)){avatar.src=lastAvatar;avatar.onerror=()=>avatar.removeAttribute('src')}else avatar.removeAttribute('src');}
}
async function loadPlayer(){if(demo)return;try{showPlayer(await(await fetch('player.json?t='+Date.now(),{cache:'no-store'})).json())}catch{}}
let active = false, history = [], average = 0, lastTime = 0, lastFingerprint = '', received = false;
let settings = null;
function clearTicks(){meter.removeAllHitErrorTicks();history=[];average=0;arrow.style.left='0%'}
function position(){
  const scale = innerHeight / 480;
  const x = (innerWidth - innerHeight * 854 / 480) / 2 + layout.x * scale;
  root.style.left = `${x}px`; root.style.top = `${layout.y * scale}px`;
  root.style.transform = `scale(${layout.width * scale / 284 * (layout.size ?? 100) / 100})`;
  panel.style.left=`${(innerWidth-innerHeight*854/480)/2+(layout.statsX??660)*scale}px`;
  panel.style.top=`${(layout.statsY??86)*scale}px`;
  panel.style.transform=`scale(${innerHeight/1080*(layout.statsSize??110)/100})`;
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
setInterval(loadPlayer,5000);loadPlayer();
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
  if(h.length!==history.length || (h.length&&h[h.length-1]!==history[history.length-1])){timingTotals=countTimings(h);updateStats();updateGraph(h);}
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
  updateGraph(Array.from({length:140},(_,i)=>Math.sin(i*2.9)*12+Math.cos(i*.73)*18));
  showPlayer({name:'PLAYER',countryName:'South Korea',countryCode:'KR',countryRank4K:null,status:'예시 프로필'});
  setInterval(()=>[-32,-14,8,42].forEach(v=>meter.addTick(v)),1300);
}
