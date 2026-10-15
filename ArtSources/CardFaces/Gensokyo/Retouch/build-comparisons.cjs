const fs=require('fs'),path=require('path');
const sharp=require('C:/Users/jinshengkai/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root=__dirname;
const groups=[['spade-10','spade-6','club-k','club-8','club-q'],['spade-4','diamond-a','spade-2','heart-4','diamond-7'],['spade-7','heart-7','heart-k','diamond-6','spade-k','diamond-4','diamond-9','diamond-5','diamond-8']];
const rois={
 'spade-10':[120,482,655,355],'spade-6':[265,690,370,390],'club-k':[176,126,577,590],
 'club-8':[102,615,484,289],'club-q':[196,758,195,288],'spade-4':[385,530,230,250],
 'diamond-a':[228,740,225,370],'spade-2':[78,697,465,322],'heart-4':[195,855,456,175],'diamond-7':[323,652,453,421]
};
const label=(s,w,h=30)=>Buffer.from(`<svg width="${w}" height="${h}"><text x="6" y="23" font-family="sans-serif" font-size="20" fill="#eee9dd">${s}</text></svg>`);
(async()=>{
 for(let group=0;group<groups.length;group++){
  const list=groups[group],layers=[];
  for(let i=0;i<list.length;i++){
   const f=list[i];fs.copyFileSync(path.join(root,'../IndexPreview/Names/Final',f+'.png'),path.join(root,'Final',f+'.png'));
   const pair=[];
   for(let j=0;j<2;j++){
    const b=await sharp(path.join(root,j?'Final':'Before',f+'.png')).resize(306,432).png().toBuffer();
    layers.push({input:b,left:i%3*628+j*310,top:Math.floor(i/3)*470+32});
    pair.push({input:await sharp(path.join(root,j?'Final':'Before',f+'.png')).png().toBuffer(),left:j*832,top:36});
   }
   layers.push({input:label(f+'   BEFORE / AFTER',620),left:i%3*628,top:Math.floor(i/3)*470});
   pair.push({input:label(f+'   BEFORE (left) / AFTER (right)',1648),left:0,top:0});
   await sharp({create:{width:1648,height:1188,channels:4,background:'#203331'}}).composite(pair).png().toFile(path.join(root,'Comparisons',f+'.png'));
   if(rois[f]){
    const [x,y,w,h]=rois[f],z=[];
    for(let j=0;j<2;j++)z.push({input:await sharp(path.join(root,j?'Final':'Before',f+'.png')).extract({left:x,top:y,width:w,height:h}).resize(w*2,h*2).png().toBuffer(),left:j*(w*2+16),top:32});
    z.push({input:label(f+'  DETAIL BEFORE / AFTER',w*4+16),left:0,top:0});
    await sharp({create:{width:w*4+16,height:h*2+32,channels:4,background:'#203331'}}).composite(z).png().toFile(path.join(root,'Comparisons',f+'-detail.png'));
   }
  }
  await sharp({create:{width:1884,height:Math.ceil(list.length/3)*470,channels:4,background:'#203331'}}).composite(layers).png().toFile(path.join(root,'Comparisons','group-'+group+'.png'));
 }
 const small=[];let i=0;
 for(const f of groups.flat()){
  for(let j=0;j<2;j++)small.push({input:await sharp(path.join(root,'Final',f+'.png')).resize(j?102:204,j?144:288).png().toBuffer(),left:i%5*318+(j?210:0),top:Math.floor(i/5)*320+30});
  small.push({input:label(f,310),left:i%5*318,top:Math.floor(i/5)*320});i++;
 }
 await sharp({create:{width:1590,height:Math.ceil(i/5)*320,channels:4,background:'#203331'}}).composite(small).png().toFile(path.join(root,'Comparisons/small-sizes.png'));
})();
