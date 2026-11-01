// Judgement mapping follows tosu's osu!mania v2 API: geki/300/katu/100/50/0.
const count = value => Number.isFinite(+value) ? Math.max(0, Math.trunc(+value)) : 0;
export function summarizePlay(play = {}, timing = {}) {
  const h = play.hits || {};
  const counts = [h.geki, h['300'], h.katu, h['100'], h['50'], h['0']].map(count);
  const total = counts.reduce((a,b)=>a+b,0);
  const accuracy = Number.isFinite(+play.accuracy) ? Math.min(100,Math.max(0,+play.accuracy)) : 100;
  const ur = Number.isFinite(+play.unstableRate) ? Math.max(0,+play.unstableRate) : 0;
  const ratio = counts[1] ? (counts[0]/counts[1]).toFixed(2) : counts[0] ? '∞' : '—';
  return {counts,total,accuracy,ur,ratio,early:count(timing.early),late:count(timing.late)};
}
export function countTimings(errors = []) {
  let early=0,late=0;
  for(const v of errors)if(typeof v==='number'&&Number.isFinite(v)){if(v<0)early++;else if(v>0)late++;}
  return {early,late};
}
export function buildDistribution(errors=[],binCount=41,limit=150){
  binCount=Math.max(3,Math.min(81,Math.trunc(binCount)))|1;
  limit=Number.isFinite(limit)&&limit>0?limit:150;
  const bins=Array(binCount).fill(0);let total=0;
  for(const v of errors)if(typeof v==='number'&&Number.isFinite(v)){
    const index=Math.round((Math.max(-limit,Math.min(limit,v))/limit+1)*(binCount-1)/2);
    bins[index]++;total++;
  }
  return {bins,total,limit,max:Math.max(1,...bins)};
}
