const fs=require('fs'),path=require('path'),crypto=require('crypto'),assert=require('assert/strict');
const sharp=require('C:/Users/jinshengkai/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root=__dirname,assets=path.resolve(root,'../../../../Assets/Sprites/CardFaces/Gensokyo');
const named=path.resolve(root,'../IndexPreview/Names/Final');
const sha=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
(async()=>{
 const files=fs.readdirSync(path.join(root,'Before')).filter(f=>f.endsWith('.png'));
 assert.equal(files.length,19);
 const all=JSON.parse(fs.readFileSync(path.join(named,'manifest.json'))).cards;
 const report={date:'2026-10-05',dimensions:[816,1152],changed:[],unchanged:[],metaBefore:{},backBefore:sha(path.join(assets,'back.png'))};
 for(const card of all){
  const f=card.file,oldPath=path.join(assets,f),newPath=path.join(named,f);
  const a=await sharp(oldPath).ensureAlpha().raw().toBuffer(),b=await sharp(newPath).ensureAlpha().raw().toBuffer();
  assert.equal(a.length,816*1152*4);assert.equal(b.length,a.length);
  if(!files.includes(f)){assert.deepEqual(b,a,'Unrequested card changed: '+f);report.unchanged.push(f);continue;}
  const baseline=await sharp(path.join(root,'Before',f)).ensureAlpha().raw().toBuffer();
  let changedPixels=0;
  for(let y=0;y<1152;y++)for(let x=0;x<816;x++){
   const i=(y*816+x)*4;assert.equal(b[i+3],baseline[i+3],f+' alpha');
   const differs=b[i]!==baseline[i]||b[i+1]!==baseline[i+1]||b[i+2]!==baseline[i+2];if(differs)changedPixels++;
   if(x<40||x>=777||y<61||y>=1110||(x<151&&y<274)||(x>673&&y>880)||(x>708&&y<118)||(x<118&&y>1028))assert.equal(differs,false,f+' fixed decoration changed at '+x+','+y);
  }
  report.metaBefore[f]=sha(oldPath+'.meta');report.changed.push({file:f,name:card.name,changedPixels});
 }
 assert.equal(report.unchanged.length,33);
 for(const f of files){fs.copyFileSync(path.join(named,f),path.join(assets,f));assert.equal(sha(path.join(assets,f)+'.meta'),report.metaBefore[f]);}
 assert.equal(sha(path.join(assets,'back.png')),report.backBefore);
 fs.mkdirSync('Logs/CardLayout',{recursive:true});fs.writeFileSync('Logs/CardLayout/retouch-validation.json',JSON.stringify(report,null,2));
 console.log('Installed 19 cards; other 33 faces and back unchanged. All dimensions, alpha, fixed decoration and 19 meta hashes verified.');
})();
