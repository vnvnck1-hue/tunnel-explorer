// 페이크 로딩 블록 주입기 — node claude-work/loading/inject-fake-loading.mjs <대상.html> [출력.html]
// · <script id="tcFakeLoadJs"> 로 감싸 </body> 직전(모든 스크립트 뒤)에 넣는다.
//   infStartRun 은 tcHudV77 등에서 이미 여러 번 래핑되어 있고 startCrewMission 도 래핑되어 있으므로
//   맨 뒤에 와야 최종(가장 바깥) 래퍼가 되어 원본 + 모든 훅이 끝난 뒤 로딩 화면이 뜬다.
// · 이미 들어가 있으면 FAKE-LOADING-BLOCK 마커 사이만 교체한다
// · 대상 파일의 줄바꿈(CRLF/LF)을 따라간다
import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';

const here=path.dirname(fileURLToPath(import.meta.url));
const [,,target,outArg]=process.argv;
if(!target){console.error('usage: node inject-fake-loading.mjs <target.html> [out.html]');process.exit(1);}
const out=outArg||target;
const BEGIN='/* FAKE-LOADING-BLOCK:BEGIN */',END='/* FAKE-LOADING-BLOCK:END */';

let src=fs.readFileSync(target,'utf8');
const nl=src.includes('\r\n')?'\r\n':'\n';
let block=fs.readFileSync(path.join(here,'fake_loading_block.js'),'utf8').replace(/\r\n/g,'\n').trimEnd();
block=block.replace(/\n/g,nl);

const b=src.indexOf(BEGIN),e=src.indexOf(END);
if(b>=0&&e>b){
  src=src.slice(0,b)+block+src.slice(e+END.length);
  console.log('[fake-loading] replaced existing block');
}else{
  const at=src.lastIndexOf('</body>');
  if(at<0){console.error('</body> not found');process.exit(2);}
  const tag='<script id="tcFakeLoadJs">'+nl+block+nl+'</script>'+nl;
  src=src.slice(0,at)+tag+src.slice(at);
  console.log('[fake-loading] inserted <script id="tcFakeLoadJs"> before </body>');
}
fs.writeFileSync(out,src);
console.log('[fake-loading] wrote '+out+' ('+src.length+' chars)');
