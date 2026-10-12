// Already applied. Re-screenshot blocked by Starter plan MCP call limit.
const page=await figma.getNodeByIdAsync('0:1');
await figma.setCurrentPageAsync(page);
const changed=[];
const description=await figma.getNodeByIdAsync('5:83');
await figma.loadFontAsync(description.fontName);
description.resize(428,84);
changed.push(description.id);
for(const id of ['4:4','4:81']){
 const fade=await figma.getNodeByIdAsync(id);
 fade.x=0;
 fade.resize(1920,1080);
 changed.push(fade.id);
}
return {mutatedNodeIds:changed};
