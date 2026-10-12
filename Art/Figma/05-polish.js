// Append to 02-common.js. Replace source fills with cutouts and add scene backgrounds.
const cutouts=['05a9c77fa9856d348a2754d926c8e1a87c61f0de','87f431f8bc410be9438ee0774c265122157aaa47','1450c3d9840929a9cbc72cc655bbfbb1b437f4cf','5aacf9e7bba068baa72c5926d592f151130ac2ab','8540db4dc51051dfbfd1138c9f4bcf18e7646082','49ea66459501c975807a6610f5a522b21d7c8efc','c2a720dfeb3ce0d0bfac49b3671238998e9a8237'];
const ids=['4:2','4:24','4:65','5:5','5:50','5:100','5:184'];
const frames=await Promise.all(ids.map(id=>figma.getNodeByIdAsync(id)));
for(const f of frames){for(const a of f.findAll(n=>n.type==='RECTANGLE'&&n.name.startsWith('Artwork / '))){const index=hashes.indexOf(a.fills[0].imageHash);if(index>=0&&cutouts[index])a.fills=[{type:'IMAGE',imageHash:cutouts[index],scaleMode:'FIT'}];a.opacity=1;mutated.push(a.id);}}
const bgHash='77eda69ca5d8b6fbf2b34b6c8ac7aed11c6d2211';
for(const [i,opacity] of [[0,.55],[1,.20],[2,.48],[3,.14],[4,.25]]){const f=frames[i];const bg=rect(f,'Environment / lantern ruins',0,0,1920,1080);bg.fills=[{type:'IMAGE',imageHash:bgHash,scaleMode:'FILL'}];bg.opacity=opacity;f.insertChild(0,bg);}
const field=frames[5].findOne(n=>n.name==='Battlefield');field.fills=[{type:'IMAGE',imageHash:bgHash,scaleMode:'FILL'}];field.opacity=.48;mutated.push(field.id);
const hubArt=frames[1].findOne(n=>n.name==='Artwork / 리안');hubArt.x=74;hubArt.y=172;hubArt.resize(650,770);mutated.push(hubArt.id);
const titleArt=frames[0].findOne(n=>n.name==='Artwork / 카이');titleArt.x=1050;titleArt.y=30;titleArt.resize(800,1050);mutated.push(titleArt.id);
const storyArt=frames[2].findOne(n=>n.name==='Artwork / 리안');storyArt.x=945;storyArt.y=88;storyArt.resize(850,1130);mutated.push(storyArt.id);
for(const p of frames[2].findAll(n=>n.name==='Ruined pillar')){p.opacity=.08;mutated.push(p.id);}
const assetNames=['Background','카이','리안','세라','미나토','노아','하이델','오르페'];
for(const [i,id] of ['5:241','5:242','5:243','5:244','5:245','5:246','6:8','6:9'].entries()){const a=await figma.getNodeByIdAsync(id);a.name='Production asset / '+assetNames[i];a.x=100+i*310;a.y=4630;a.resize(i===0?300:240,320);mutated.push(a.id);}
async function link(n,dest){await n.setReactionsAsync([{trigger:{type:'ON_CLICK'},actions:[{type:'NODE',destinationId:dest,navigation:'NAVIGATE',transition:{type:'DISSOLVE',easing:{type:'EASE_OUT'},duration:.25},preserveScrollPosition:false}]}]);mutated.push(n.id);}
for(const [fi,label,dest] of [[0,'등불을 밝히다  →','4:24'],[1,'이야기 계속  →','4:65'],[1,'출격 준비','5:50'],[3,'리안의 기억','4:65'],[4,'출격하기  →','5:100']]){const n=frames[fi].findOne(n=>n.name==='Action / '+label);if(n)await link(n,dest);}
for(const f of frames){for(const n of f.findAll(n=>n.name.startsWith('Navigation / '))){const dest={'Navigation / 등대 요새':'4:24','Navigation / 잔영 기록':'5:5','Navigation / 출격':'5:50','Navigation / 기억 보관함':'4:65'}[n.name];if(dest&&dest!==f.id)await link(n,dest);}}
await link(frames[2].findOne(n=>n.name==='Continue hint'),'4:24');
return {createdNodeIds:created,mutatedNodeIds:mutated,screenCount:frames.length,prototypeLinks:frames.reduce((a,f)=>a+f.findAll(n=>'reactions' in n&&n.reactions.length>0).length,0)};
