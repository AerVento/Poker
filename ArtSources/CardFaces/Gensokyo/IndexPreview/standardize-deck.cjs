// Reuses extracted artwork, never typesets replacement card indices.
const fs=require('fs'),path=require('path');
const sharp=require('C:/Users/jinshengkai/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root=__dirname,assets=path.resolve(root,'../../../../Assets/Sprites/CardFaces/Gensokyo');
const originals=path.join(root,'OriginalDeck'),output=path.join(root,'StandardizedDeck');
const manifest=JSON.parse(fs.readFileSync(path.join(root,'Glyphs/manifest.json')));
const ranks=['a','2','3','4','5','6','7','8','9','10','j','q','k'];
const W=816,H=1152,center=94,rankTop=50,gap=6;
fs.mkdirSync(originals,{recursive:true});fs.mkdirSync(output,{recursive:true});
function cleanCorner(buf,suit,rank,reverse){
 const ox=37,oy=39,w=148,h=267;
 const at=(x,y)=>{let gx=x+ox,gy=y+oy;if(reverse){gx=W-1-gx;gy=H-1-gy}return (gy*W+gx)*4};
 const mask=new Uint8Array(w*h),seen=new Uint8Array(w*h),parts=[];
 for(let y=0;y<h;y++)for(let x=0;x<w;x++){
  const i=at(x,y),r=buf[i],g=buf[i+1],b=buf[i+2];
  const v=suit==='spade'?g-Math.max(r,b):suit==='club'?b-Math.max(r,g):suit==='heart'?r-Math.max(g,b):Math.min(r-g,(g-b)*2);
  mask[y*w+x]=v>(suit==='diamond'?55:13)?1:0;
 }
 for(let p=0;p<w*h;p++)if(mask[p]&&!seen[p]){
  let stack=[p],pixels=[],x0=w,y0=h,x1=0,y1=0;seen[p]=1;
  while(stack.length){const q=stack.pop(),x=q%w,y=Math.floor(q/w);pixels.push(q);x0=Math.min(x0,x);y0=Math.min(y0,y);x1=Math.max(x1,x);y1=Math.max(y1,y);
   for(const [nx,ny]of [[x-1,y],[x+1,y],[x,y-1],[x,y+1]])if(nx>=0&&nx<w&&ny>=0&&ny<h){const n=ny*w+nx;if(mask[n]&&!seen[n]){seen[n]=1;stack.push(n)}}
  }
  if(pixels.length>120)parts.push({pixels,x0,y0,x1,y1});
 }
 parts.sort((a,b)=>a.y0-b.y0);
 const selected=[];
 for(const p of parts){
  if(p.x0===0||p.x1===w-1||p.y1>h-4||(p.x0+p.x1)/2>100||p.x1-p.x0<12||p.pixels.length<250)continue;
  selected.push(p);if(p.y1>200)break;
 }
 if(!selected.length||selected[selected.length-1].y1<200)throw Error(`Missing indices ${suit}-${rank} reverse=${reverse} parts=${JSON.stringify(parts.map(p=>[p.x0,p.y0,p.x1,p.y1,p.pixels.length]))}`);
 // Dilate just the old ink, preserving all unmasked illustration and frame pixels.
 const erase=new Uint8Array(w*h);
 for(const part of selected)for(const p of part.pixels){const x=p%w,y=Math.floor(p/w);for(let dy=-3;dy<=3;dy++)for(let dx=-3;dx<=3;dx++)if(dx*dx+dy*dy<=9){const xx=x+dx,yy=y+dy;if(xx>=0&&xx<w&&yy>=0&&yy<h)erase[yy*w+xx]=1}}
 const values=new Float32Array(w*h*3),points=[];
 let mean=[0,0,0],count=0;
 for(let p=0;p<w*h;p++){const x=p%w,y=Math.floor(p/w),i=at(x,y);for(let c=0;c<3;c++)values[p*3+c]=buf[i+c];if(erase[p])points.push(p);else if(x>2&&x<w-3&&y>2&&y<h-3&&(erase[p-1]||erase[p+1]||erase[p-w]||erase[p+w])){for(let c=0;c<3;c++)mean[c]+=buf[i+c];count++}}
 mean=mean.map(v=>v/count);for(const p of points)for(let c=0;c<3;c++)values[p*3+c]=mean[c];
 // Harmonic fill uses nearby paper color; no solid rectangular patch.
 for(let iter=0;iter<450;iter++)for(const p of points){const x=p%w,y=Math.floor(p/w);if(x===0||x===w-1||y===0||y===h-1)continue;for(let c=0;c<3;c++)values[p*3+c]=(values[(p-1)*3+c]+values[(p+1)*3+c]+values[(p-w)*3+c]+values[(p+w)*3+c])*.25}
 for(const p of points){const i=at(p%w,Math.floor(p/w));for(let c=0;c<3;c++)buf[i+c]=Math.round(values[p*3+c])}
 return {reverse,components:selected.map(p=>[p.x0+ox,p.y0+oy,p.x1-p.x0+1,p.y1-p.y0+1]),changedPixels:points.length};
}
async function index(suit,rank){
 const rh=rank==='q'?118:106,sh=94;
 const glyphs=[];
 for(const [key,height,top]of [[`rank-${rank}`,rh,rankTop],[`suit-${suit}`,sh,rankTop+rh+gap]]){
  const source=path.join(root,'Glyphs',`${key}-${suit}.png`),m=manifest.glyphs.find(x=>x.key===key),width=Math.round(m.width/m.height*height);
  const b=await sharp(source).resize(width,height).png().toBuffer();glyphs.push({input:b,left:Math.round(center-width/2),top});
 }
 return sharp({create:{width:W,height:H,channels:4,background:'#00000000'}}).composite(glyphs).png().toBuffer();
}
(async()=>{
 const audit=[];
 for(const suit of Object.keys(manifest.colors))for(const rank of ranks){
  const file=`${suit}-${rank}.png`,source=path.join(originals,file);
  if(!fs.existsSync(source))fs.copyFileSync(path.join(assets,file),source);
  const {data,info}=await sharp(source).ensureAlpha().raw().toBuffer({resolveWithObject:true});
  if(info.width!==W||info.height!==H)throw Error('Unexpected size '+file);
  const corners=[cleanCorner(data,suit,rank,false),cleanCorner(data,suit,rank,true)];
  const upper=await index(suit,rank),lower=await sharp(upper).rotate(180).png().toBuffer();
  const result=await sharp(data,{raw:{width:W,height:H,channels:4}}).composite([{input:upper},{input:lower}]).raw().toBuffer();
  for(let i=0;i<result.length;i+=4){const p=i/4,x=p%W,y=Math.floor(p/W);result[i+3]=data[i+3];if(!((x>=37&&x<185&&y>=39&&y<306)||(x>=631&&x<779&&y>=846&&y<1113))){result[i]=data[i];result[i+1]=data[i+1];result[i+2]=data[i+2]}}
  await sharp(result,{raw:{width:W,height:H,channels:4}}).png().toFile(path.join(output,file));
  audit.push({file,corners});console.log(file);
 }
 fs.writeFileSync(path.join(root,'standardization.json'),JSON.stringify({colors:manifest.colors,layout:{width:W,height:H,center,rankTop,rankHeight:106,qHeight:118,suitHeight:94,gap},cards:audit},null,2));
 const composites=[];
 for(const [i,a]of audit.entries()){const b=await sharp(path.join(output,a.file)).resize(153,216).png().toBuffer();composites.push({input:b,left:i%13*159,top:Math.floor(i/13)*222})}
 await sharp({create:{width:2067,height:888,channels:4,background:'#152b29'}}).composite(composites).png().toFile(path.join(root,'standardized-deck.png'));
})();





