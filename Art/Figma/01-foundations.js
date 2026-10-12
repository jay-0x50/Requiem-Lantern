// Requiem Lantern v0.2 / Figma Plugin API. Run in the existing file.
const page=await figma.getNodeByIdAsync('0:1');
await figma.setCurrentPageAsync(page);
page.name='Requiem Lantern · v0.2';
const created=[], mutated=[page.id], variableIds=[];
const colors={night:'#0D1117',navy:'#1B2233',panel:'#151C27',line:'#3D4655',ivory:'#E7DCC9',muted:'#9CA7B5',amber:'#F2A541',radiance:'#FFE29A',fire:'#E77A5D',water:'#75C8C4',shadow:'#B09BD8',frost:'#A7D4E9',success:'#98CFA3'};
function rgb(h){return {r:parseInt(h.slice(1,3),16)/255,g:parseInt(h.slice(3,5),16)/255,b:parseInt(h.slice(5,7),16)/255};}
// Helper adapted from figma-generate-library/scripts/createVariableCollection.js.
function collection(name){const c=figma.variables.createVariableCollection(name);c.renameMode(c.modes[0].modeId,'영야');return c;}
const prim=collection('RL / Primitives'),sem=collection('RL / UI');
const palette={};
for(const [name,hex] of Object.entries(colors)){
 const p=figma.variables.createVariable('palette/'+name,prim,'COLOR');p.scopes=[];p.setValueForMode(prim.defaultModeId,rgb(hex));p.setVariableCodeSyntax('WEB','var(--rl-palette-'+name+')');
 const s=figma.variables.createVariable('color/'+name,sem,'COLOR');s.scopes=['FRAME_FILL','SHAPE_FILL','TEXT_FILL','STROKE_COLOR'];s.setValueForMode(sem.defaultModeId,{type:'VARIABLE_ALIAS',id:p.id});s.setVariableCodeSyntax('WEB','var(--rl-'+name+')');palette[name]=s;variableIds.push(p.id,s.id);
}
for(const [name,val,scope] of [['space/24',24,'GAP'],['space/16',16,'GAP'],['radius/4',4,'CORNER_RADIUS']]){const v=figma.variables.createVariable(name,sem,'FLOAT');v.scopes=[scope];v.setValueForMode(sem.defaultModeId,val);v.setVariableCodeSyntax('WEB','var(--rl-'+name.replace('/','-')+')');variableIds.push(v.id);}
const fontList=[{family:'Noto Sans KR',style:'Regular'},{family:'Noto Sans KR',style:'Medium'},{family:'Noto Sans KR',style:'Bold'},{family:'Noto Serif KR',style:'Regular'},{family:'Noto Serif KR',style:'Medium'}];
await Promise.all(fontList.map(f=>figma.loadFontAsync(f)));
const styles={};
for(const [name,size,font] of [['Display',82,fontList[4]],['Title',46,fontList[4]],['Section',32,fontList[2]],['Body',24,fontList[0]],['Label',22,fontList[1]],['Caption',18,fontList[0]]]){const s=figma.createTextStyle();s.name='RL / '+name;s.fontName=font;s.fontSize=size;s.lineHeight={unit:'PERCENT',value:150};styles[name]=s.id;}
function paint(key){return figma.variables.setBoundVariableForPaint({type:'SOLID',color:rgb(colors[key])},'color',palette[key]);}
function track(n){created.push(n.id);return n;}
const comps=[];
for(const [i,state] of ['Primary','Secondary','Disabled'].entries()){
 const c=track(figma.createComponent());c.name='Style='+state;c.resize(320,76);c.layoutMode='HORIZONTAL';c.primaryAxisAlignItems='CENTER';c.counterAxisAlignItems='CENTER';c.paddingLeft=c.paddingRight=24;c.paddingTop=c.paddingBottom=16;c.fills=[paint(state==='Primary'?'amber':'panel')];c.strokes=[paint(state==='Primary'?'amber':'line')];c.cornerRadius=4;c.description='Touch target 76px at 1920x1080. Korean action label is editable.';
 c.setBoundVariable('paddingLeft',await figma.variables.getVariableByIdAsync(variableIds[26]));c.setBoundVariable('paddingRight',await figma.variables.getVariableByIdAsync(variableIds[26]));
 const t=track(figma.createText());t.fontName=fontList[1];t.characters='계속하기';await t.setTextStyleIdAsync(styles.Label);t.fills=[paint(state==='Primary'?'night':state==='Disabled'?'muted':'ivory')];c.appendChild(t);comps.push(c);
}
const set=track(figma.combineAsVariants(comps,page));set.name='RL / Action button';set.x=100;set.y=3900;set.resize(1060,120);set.description='Primary for the next story or confirmed combat action; Secondary for reversible navigation; Disabled for unavailable content.';comps.forEach((c,i)=>{c.x=20+i*350;c.y=20});
const assets=['2:2','2:3','2:4','2:5','2:6','2:7','2:8'];
const names=['카이','리안','세라','미나토','노아','하이델','오르페'];
for(let i=0;i<assets.length;i++){const n=await figma.getNodeByIdAsync(assets[i]);n.name='Source artwork / '+names[i];n.x=100+i*280;n.y=4200;n.resize(240,320);mutated.push(n.id);}
return {createdNodeIds:created,mutatedNodeIds:mutated,variableIds,collections:[prim.id,sem.id],styles,buttons:comps.map(c=>({name:c.name,id:c.id})),assets:assets.map((id,i)=>({id,name:names[i]})),fontFamilies:fontList.map(f=>f.family)};
