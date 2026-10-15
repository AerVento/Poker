const fs=require('fs'),path=require('path'),crypto=require('crypto'),assert=require('assert/strict');
const sharp=require('C:/Users/jinshengkai/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root=__dirname,assets=path.resolve(root,'../../../../Assets/Sprites/CardFaces/Gensokyo');
const changed=['spade-6','club-k','spade-10'],restored=['spade-2','diamond-a','diamond-7'],targets=[...changed,...restored];
const sha=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const pixels=p=>sharp(p).ensureAlpha().raw().toBuffer();
const label=(s,w)=>Buffer.from(`<svg width="${w}" height="30"><text x="6" y="22" font-family="sans-serif" font-size="19" fill="#eee9dd">${s}</text></svg>`);
(async()=>{
 const baseline=JSON.parse(fs.readFileSync('Logs/CardLayout/followup-before-hashes.json'));
 const named=path.join(root,'../IndexPreview/Names/Final'),report={targets,restored,unchangedFaces:0,metaUnchanged:0};
 for(const f of fs.readdirSync(assets).filter(f=>f.endsWith('.png'))){
  if(f==='back.png'){assert.equal(sha(path.join(assets,f)),baseline[f]);continue;}
  const a=await pixels(path.join(assets,f)),b=await pixels(path.join(named,f));
  assert.equal(b.length,816*1152*4);
  if(!targets.includes(f.slice(0,-4))){assert.deepEqual(b,a,'Unexpected change: '+f);report.unchangedFaces++;continue;}
  const old=await pixels(path.join(root,'PreviousPass',f));
  for(let y=0;y<1152;y++)for(let x=0;x<816;x++){
   const i=(y*816+x)*4;assert.equal(b[i+3],old[i+3],f+' alpha');
   if(x<40||x>=777||y<110||y>=1110||(x<151&&y<274)||(x>673&&y>880)||(x>708&&y<118)||(x<118&&y>1028))for(let c=0;c<3;c++)assert.equal(b[i+c],old[i+c],f+' fixed graphics');
  }
  if(restored.includes(f.slice(0,-4)))assert.deepEqual(b,await pixels(path.join(root,'Before',f)),'Restore mismatch '+f);
 }
 // Original canopy and finial are untouched; only lower shaft/grip are composited.
 const yuuka=await pixels(path.join(named,'club-k.png')),original=await pixels(path.join(root,'Before/club-k.png'));
 assert.deepEqual(yuuka.subarray(0,410*816*4),original.subarray(0,410*816*4));
 assert.equal(report.unchangedFaces,46);
 for(const f of targets)fs.copyFileSync(path.join(named,f+'.png'),path.join(assets,f+'.png'));
 for(const f of fs.readdirSync(assets).filter(f=>f.endsWith('.meta'))){assert.equal(sha(path.join(assets,f)),baseline[f]);report.metaUnchanged++;}
 const overview=[],small=[];
 for(let row=0;row<changed.length;row++){
  const f=changed[row],pair=[];
  for(let side=0;side<2;side++){
   const src=path.join(root,side?'Final':'PreviousPass',f+'.png');
   overview.push({input:await sharp(src).resize(306,432).png().toBuffer(),left:side*318,top:row*472+32});
   pair.push({input:await sharp(src).png().toBuffer(),left:side*832,top:32});
  }
  overview.push({input:label(f+'  PREVIOUS / CURRENT',624),left:0,top:row*472});
  pair.push({input:label(f+'  PREVIOUS / CURRENT',1648),left:0,top:0});
  await sharp({create:{width:1648,height:1184,channels:4,background:'#203331'}}).composite(pair).png().toFile(path.join(root,'Comparisons',f+'-followup.png'));
  small.push({input:await sharp(path.join(root,'Final',f+'.png')).resize(204,288).png().toBuffer(),left:row*318,top:30});
  small.push({input:await sharp(path.join(root,'Final',f+'.png')).resize(102,144).png().toBuffer(),left:row*318+210,top:30});
  small.push({input:label(f,306),left:row*318,top:0});
 }
 await sharp({create:{width:624,height:1416,channels:4,background:'#203331'}}).composite(overview).png().toFile(path.join(root,'Comparisons/followup-overview.png'));
 await sharp({create:{width:954,height:326,channels:4,background:'#203331'}}).composite(small).png().toFile(path.join(root,'Comparisons/followup-small.png'));
 for(const [f,box] of [['spade-6',[270,695,380,395]],['club-k',[475,408,205,358]],['spade-10',[122,485,653,355]]]){
  const [left,top,width,height]=box;
  await sharp(path.join(root,'Final',f+'.png')).extract({left,top,width,height}).resize(width*2,height*2).png().toFile(path.join(root,'Comparisons',f+'-followup-detail.png'));
 }
 fs.writeFileSync('Logs/CardLayout/followup-validation.json',JSON.stringify(report,null,2));console.log(report);
})();
