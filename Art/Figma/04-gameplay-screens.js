// Append to 02-common.js; screens 4—7.
const dossier=frame('04 · 잔영 기록 / 리안',100,1280);
await header(dossier,'AFTERGLOW ARCHIVE','잔영 기록','동료 5명  ·  기억은 유대로 남는다');
art(dossier,1,64,182,552,736);
gradient(dossier,64,745,552,173,false,0,1);
await txt(dossier,'Rarity','★★★★★  /  광휘',670,185,630,22,'radiance');
await txt(dossier,'Character name','리안',670,235,650,72,'ivory',true);
await txt(dossier,'Character role','등대의 파수꾼  ·  수호자',673,343,710,28,'muted');
rule(dossier,670,411,795);
await txt(dossier,'Character quote','“…살아 있으면,\n그걸로 됐어.”',670,447,790,38,'ivory',true);
await txt(dossier,'Profile','13년간 홀로 요새를 지킨 첫 동료.\n부서진 등대 렌즈로 만든 방패에는\n아직 따뜻한 빛이 남아 있다.',670,592,790,26,'muted');
await stat(dossier,'유대','첫 불씨',670,744,230,'amber');await stat(dossier,'전투 역할','맞불 · 호위',991,744,360);
await button(dossier,'리안의 기억',670,851,795,0);
const list=stack(dossier,'Companion index',1550,185,305,732,'VERTICAL',22);
for(const [name,role,col] of [['리안','광휘 · 수호자','radiance'],['세라','화염 · 돌격수','fire'],['미나토','물결 · 치유사','water'],['노아','그림자 · 조율사','shadow'],['하이델','서리 · 사수','frost']]){const c=stack(list,'Entry / '+name,0,0,305,120,'VERTICAL',4);c.paddingLeft=24;c.paddingTop=15;c.fills=[paint(name==='리안'?'navy':'panel')];c.strokes=[paint(name==='리안'?'amber':'line')];await txt(c,'Name',name,0,0,250,29,col);await txt(c,'Role',role,0,0,250,20,'muted');}
await footer(dossier,'잔영 기록');

const stage=frame('05 · 출격 / 꺼진 등대',2100,1280);
await header(stage,'EXPEDITION / CHAPTER 01','꺼진 등대','빛을 잇는 여정  ·  1 / 5');
await txt(stage,'Map title','얼어붙은 시가지에서,\n돌아갈 곳을 찾다.',96,197,1120,43,'ivory',true);
await txt(stage,'Map subtitle','길 위에 남은 작은 빛을 따라가세요.',96,351,1120,24,'muted');
const pts=[[174,630],[412,513],[672,594],[920,470],[1195,555]];
for(let i=0;i<4;i++){const [x,y]=pts[i],[x2,y2]=pts[i+1];const l=track(figma.createLine());stage.appendChild(l);l.x=x;l.y=y;l.resize(Math.sqrt((x2-x)**2+(y2-y)**2),0);l.rotation=-Math.atan2(y2-y,x2-x)*180/Math.PI;l.strokes=[paint(i===0?'amber':'line')];l.strokeWeight=2;}
for(let i=0;i<5;i++){const [x,y]=pts[i];ellipse(stage,'Stage '+(i+1),x-30,y-30,60,60,i===0?'amber':'panel',i<2?'amber':'line');await txt(stage,'Stage number','0'+(i+1),x-18,y-23,70,24,i===0?'night':'ivory');await txt(stage,'Stage label',['마지막 불씨','등대로 가는 길','네 사람의 식탁','흔들리는 방벽','돌아올 곳'][i],x-90,y+48,190,20,i<2?'ivory':'muted');}
rule(stage,96,829,1160);await txt(stage,'Current objective','현재 목표   ·   리안과 함께 등대 요새에 도착하기',96,857,1170,25,'ivory');
rect(stage,'Expedition details',1350,187,506,736,'panel','line');
const ds=stack(stage,'Stage details text',1385,226,428,330,'VERTICAL',18);await txt(ds,'Type','MAIN STORY  ·  1-1',0,0,428,18,'amber');await txt(ds,'Name','등대로 가는 길',0,0,428,37,'ivory',true);const stageDescription=await txt(ds,'Description','안개 너머에서\n옛 등대의 윤곽이 드러난다.',0,0,428,24,'muted');stageDescription.resize(428,84);await txt(ds,'Stages','관측 → 연결 → 실행 → 정리',0,0,428,21,'ivory');
await stat(stage,'출격 동료','리안 · 세라 · 미나토',1385,610,430);
await stat(stage,'첫 발견','기억 조각 × 1',1385,707,430,'amber');
await button(stage,'출격하기  →',1385,816,435,0);await footer(stage,'출격');

const battle=frame('06 · 연광 전투 / 연결 단계',4100,1280);
await txt(battle,'Battle location','1-1  ·  등대로 가는 길',64,40,700,28,'ivory',true);
await txt(battle,'Round','ROUND 02 / 04',64,91,500,18,'muted');
const phase=stack(battle,'Phase flow',688,50,980,55,'HORIZONTAL',30);for(const [i,label] of ['관측','연결','실행','정리'].entries())await txt(phase,'Phase / '+label,(i+1)+'  '+label,0,0,205,25,i===1?'amber':'muted');
await txt(battle,'Pause','Ⅱ',1810,43,50,35,'ivory');rule(battle,64,142,1792);
await txt(battle,'Tactical hint','슬롯에서 적의 예고선으로 빛실을 이어 맞불을 놓으세요.',64,173,1320,23,'ivory');
await txt(battle,'Mode','수동  ·  시간 제한 없음',1454,173,400,21,'amber');
rect(battle,'Battlefield',64,230,1792,470,'navy');
for(let i=0;i<9;i++){const e=ellipse(battle,'Dust',180+i*186,300+(i%3)*71,4,4,'muted');e.opacity=.25;}
art(battle,1,92,257,255,340);gradient(battle,92,493,255,104,false,0,1);
art(battle,2,384,284,216,288);gradient(battle,384,492,216,80,false,0,1);
art(battle,3,632,304,195,260);gradient(battle,632,480,195,84,false,0,1);
const monster=track(figma.createNodeFromSvg('<svg width="320" height="390" viewBox="0 0 320 390" xmlns="http://www.w3.org/2000/svg"><path d="M160 15 L206 72 L191 122 L235 160 L218 237 L279 367 L210 325 L167 248 L107 347 L62 371 L105 224 L81 148 L129 111 L116 63 Z" fill="#101018" stroke="#75607c" stroke-width="2"/><path d="M90 150 L36 191 L12 295 L55 237 L120 205 M212 157 L265 180 L310 300 L258 242 L196 211" fill="#171422" stroke="#75607c" stroke-width="2"/><path d="M136 60 L187 63 L174 103 L144 110 Z" fill="#887f8b"/><path d="M136 75 L150 80 M169 80 L184 75" stroke="#211b28" stroke-width="5"/><path d="M160 127 L184 169 L159 201 L137 168 Z" fill="#bd7766"/><path d="M160 143 L171 168 L158 184 L148 168 Z" fill="#f2a541"/><path d="M155 5 L163 25 M190 50 L220 39 M113 47 L89 31 M237 247 L268 279 M92 277 L56 309" stroke="#a08aad" stroke-width="3"/></svg>'));battle.appendChild(monster);monster.name='분노 개체 / editable concept silhouette';monster.x=1396;monster.y=277;created.push(...monster.findAll(()=>true).map(n=>n.id));
await txt(battle,'Enemy name','분노 개체',1462,245,340,23,'ivory');
rect(battle,'Enemy HP track',1395,674,360,8,'line');rect(battle,'Enemy HP value',1395,674,270,8,'fire');
await txt(battle,'Enemy HP label','생명  720 / 960',1450,626,380,19,'ivory');
function beam(x,y,x2,y2,col,weight=3){const l=track(figma.createLine());battle.appendChild(l);l.name='Light thread / '+col;l.x=x;l.y=y;l.resize(Math.sqrt((x2-x)**2+(y2-y)**2),0);l.rotation=-Math.atan2(y2-y,x2-x)*180/Math.PI;l.strokes=[paint(col)];l.strokeWeight=weight;return l;}
beam(1500,435,280,598,'shadow',3);beam(270,593,1030,489,'amber',4);ellipse(battle,'Clash junction',1008,466,44,44,'night','amber');diamond(battle,1031,478,14,'amber');
rect(battle,'Intent readout',1030,300,338,117,'night','shadow');await txt(battle,'Intent label','예고선  ·  난타',1052,315,294,20,'shadow');await txt(battle,'Intent stats','2타점 × 파워 10  ·  속도 4',1052,358,294,21,'ivory');
rect(battle,'Clash forecast',839,558,478,112,'night','amber');await txt(battle,'Clash status','접전  ·  점화 시 우세',865,572,420,24,'amber');await txt(battle,'Clash formula','파워 9 → 11.25  /  적 파워 10',865,617,420,20,'ivory');
await txt(battle,'Rift label','균열  42 / 60',1418,711,365,19,'frost');
const party=[['리안','방벽 돌파','호위','radiance'],['세라','화염 베기','기본 공격','fire'],['미나토','치유 등불','지원','water']];
for(let i=0;i<3;i++){const x=64+i*443;rect(battle,'Party card / '+party[i][0],x,750,415,269,'panel','line');await txt(battle,'Character',party[i][0],x+24,769,367,28,party[i][3]);await txt(battle,'Actions','행동 슬롯  2 / 2',x+24,816,367,18,'muted');for(let j=0;j<2;j++){rect(battle,'Action slot',x+24+j*190,864,177,123,'night',i===0&&j===0?'amber':'line');await txt(battle,'Slot label',party[i][j+1],x+39+j*190,880,155,21,'ivory');await txt(battle,'Slot cost',j===0?'불씨 1':'불씨 0',x+39+j*190,939,155,18,'amber');}}
await txt(battle,'Ember','불씨  ◆ ◆ ◆ ◆ ◇ ◇',1440,760,420,23,'amber');await txt(battle,'Lantern gauge','등불   68 / 100',1440,814,390,21,'ivory');await txt(battle,'Gloom','어둠도  1 / 3',1440,853,390,20,'shadow');
await button(battle,'연결 확정  →',1440,915,414,0);await txt(battle,'AI suggestion','AI 제안     ·     이전 연결',1440,1006,414,19,'muted');

const board=frame('07 · 캐릭터 원화 / 7인 디자인 보드',100,2460);
await header(board,'CHARACTER DESIGN / SOURCE OF TRUTH','일곱 개의 빛, 일곱 개의 기억','원화 + 설정집  ·  제작 기준 v0.2');
const characters=[['카이','등불지기','168 cm','amber','낡은 황동 등불'],['리안','광휘 · 수호자','172 cm','radiance','등대 렌즈 방패'],['세라','화염 · 돌격수','163 cm','fire','대검 · 손목 리본'],['미나토','물결 · 치유사','158 cm','water','이름이 적힌 의무복'],['노아','그림자 · 조율사','176 cm','shadow','깨진 철종'],['하이델','서리 · 사수','152 cm','frost','고글 · 개조 석궁'],['오르페','철종 교단 수장','188 cm','muted','종 지팡이 · 꺼진 등불']];
for(let i=0;i<7;i++){const x=64+i*256;rect(board,'Character panel / '+characters[i][0],x,188,241,690,'panel','line');const a=art(board,i,x+9,207,223,475);a.fills=[{type:'IMAGE',imageHash:hashes[i],scaleMode:'FIT'}];rule(board,x+17,704,207,characters[i][3]);await txt(board,'Character name',characters[i][0],x+18,724,207,30,'ivory',true);await txt(board,'Role',characters[i][1],x+18,778,207,18,characters[i][3]);await txt(board,'Height',characters[i][2],x+18,825,207,18,'muted');await txt(board,'Identity detail',characters[i][4],x+7,902,234,19,'ivory');}
rule(board,64,973,1792);await txt(board,'Art notes','차가운 남색 · 재회색 · 탁한 청록  /  등불의 앰버를 중심으로  /  실루엣과 소품으로 인물을 구분',64,992,1700,20,'muted');
return {createdNodeIds:created,mutatedNodeIds:mutated,screens,fontFamilies:[...new Set(page.findAllWithCriteria({types:['TEXT']}).map(t=>typeof t.fontName==='object'?t.fontName.family:'mixed'))]};
