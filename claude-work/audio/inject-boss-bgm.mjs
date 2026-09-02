// 보스 BGM 블록 주입기 — node claude-work/audio/inject-boss-bgm.mjs <대상.html> [출력.html]
// · <script id="tcBossBgmJs"> 로 감싸 보스 죽음 시네마틱 스크립트(tcBossDeathFxJs) 바로 뒤에 넣는다.
//   infSpawnBoss / infBossDefeated 는 tcInfiniteModeScript 에, 등장·죽음 연출 래퍼는 tcBossFxJs /
//   tcBossDeathFxJs 에 있으므로 그 뒤에 와야 최종(가장 바깥) 래퍼가 된다.
// · 이미 들어가 있으면 BOSS-BGM-BLOCK 마커 사이만 교체한다
// · 대상 파일의 줄바꿈(CRLF/LF)을 따라간다
import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';

const here=path.dirname(fileURLToPath(import.meta.url));
const [,,target,outArg]=process.argv;
if(!target){console.error('usage: node inject-boss-bgm.mjs <target.html> [out.html]');process.exit(1);}
const out=outArg||target;
const BEGIN='/* BOSS-BGM-BLOCK:BEGIN */',END='/* BOSS-BGM-BLOCK:END */';
const ANCHORS=['<script id="tcBossDeathFxJs">','<script id="tcBossFxJs">'];

let src=fs.readFileSync(target,'utf8');
const nl=src.includes('\r\n')?'\r\n':'\n';
let block=fs.readFileSync(path.join(here,'boss_bgm_block.js'),'utf8').replace(/\r\n/g,'\n').trimEnd();
block=block.replace(/\n/g,nl);

const b=src.indexOf(BEGIN),e=src.indexOf(END);
if(b>=0&&e>b){
  src=src.slice(0,b)+block+src.slice(e+END.length);
  console.log('[boss-bgm] replaced existing block');
}else{
  let at=-1;
  for(const a of ANCHORS){
    const i=src.indexOf(a);if(i<0)continue;
    const close=src.indexOf('</script>',i);if(close<0)continue;
    at=close+'</script>'.length;
    console.log('[boss-bgm] anchor: after '+a);
    break;
  }
  if(at<0){console.error('anchor not found: '+ANCHORS.join(' | '));process.exit(2);}
  const tag=nl+'<script id="tcBossBgmJs">'+nl+block+nl+'</script>';
  src=src.slice(0,at)+tag+src.slice(at);
  console.log('[boss-bgm] inserted <script id="tcBossBgmJs">');
}
const mp3=path.join(path.dirname(path.resolve(out)),'assets/audio/bgm/boss-blood-ascendant.mp3');
if(!fs.existsSync(mp3))console.warn('[boss-bgm] warn: '+mp3+' 이 없다 — 곡 파일을 HTML 옆 assets/audio/bgm/ 에 두어야 한다');
fs.writeFileSync(out,src);
console.log('[boss-bgm] wrote '+out+' ('+src.length+' chars, '+(nl==='\r\n'?'CRLF':'LF')+')');
