const fs = require('fs');
const path = require('path');
const sharp = require('C:/Users/jinshengkai/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const out = path.join(__dirname,'Glyphs');
const ranks = ['a','2','3','4','5','6','7','8','9','10','j','q','k'];
const colors = {spade:'#37855B',club:'#397DB5',diamond:'#D98A32',heart:'#C94F58'};
fs.mkdirSync(out,{recursive:true});
async function extract(suit,rank,wantSuit=false){
 const source=path.resolve(__dirname,'../Deck',`${suit}-${rank}.png`);
 const {data,info}=await sharp(source).removeAlpha().raw().toBuffer({resolveWithObject:true});
 const sx=info.width/816,sy=info.height/1152;
 const left=Math.round(38*sx),top=Math.round(42*sy),w=Math.round(146*sx),h=Math.round(248*sy);
 let strength=new Float32Array(w*h),values=[];
 for(let y=0;y<h;y++)for(let x=0;x<w;x++){
  const i=((y+top)*info.width+x+left)*3;const [r,g,b]=data.subarray(i,i+3);
  let v=suit==='spade'?g-Math.max(r,b):suit==='club'?b-Math.max(r,g):suit==='heart'?r-Math.max(g,b):Math.min(r-g,(g-b)*2);
  v=Math.max(0,v);strength[y*w+x]=v;if(v>20)values.push(v);
 }
 values.sort((a,b)=>a-b);const opaque=values[Math.floor(values.length*.25)]*.75;
 const visited=new Uint8Array(w*h),parts=[];
 for(let p=0;p<w*h;p++)if(!visited[p]&&strength[p]>12){
  let stack=[p],pixels=[];visited[p]=1;let minX=w,maxX=0,minY=h,maxY=0;
  while(stack.length){const q=stack.pop(),x=q%w,y=Math.floor(q/w);pixels.push(q);minX=Math.min(x,minX);maxX=Math.max(x,maxX);minY=Math.min(y,minY);maxY=Math.max(y,maxY);
   for(const [nx,ny]of [[x-1,y],[x+1,y],[x,y-1],[x,y+1]])if(nx>=0&&nx<w&&ny>=0&&ny<h){const n=ny*w+nx;if(!visited[n]&&strength[n]>12){visited[n]=1;stack.push(n)}}
  }
  if(pixels.length>100)parts.push({pixels,minX,maxX,minY,maxY});
 }
 parts.sort((a,b)=>a.minY-b.minY);
 const count=rank==='10'?2:1;
 const selected=wantSuit?[parts[count]]:parts.slice(0,count);
 if(selected.some(x=>!x))throw Error('Missing glyph '+source);
 const x0=Math.min(...selected.map(p=>p.minX)),y0=Math.min(...selected.map(p=>p.minY));
 const ww=Math.max(...selected.map(p=>p.maxX))-x0+1,hh=Math.max(...selected.map(p=>p.maxY))-y0+1;
 const buf=Buffer.alloc(ww*hh*4);
 for(const part of selected)for(const p of part.pixels){const x=p%w-x0,y=Math.floor(p/w)-y0;const n=(y*ww+x)*4;buf[n]=buf[n+1]=buf[n+2]=255;buf[n+3]=Math.round(Math.min(1,strength[p]/opaque)*255)}
 const key=wantSuit?`suit-${suit}`:`rank-${rank}`;
 await sharp(buf,{raw:{width:ww,height:hh,channels:4}}).png().toFile(path.join(out,key+'.png'));
 return {key,source:path.relative(path.resolve(__dirname,'..'),source).replaceAll('\\','/'),bounds:[left+x0,top+y0,ww,hh],width:ww,height:hh};
}
(async()=>{
 const manifest={colors,glyphs:[]};
 for(const r of ranks)manifest.glyphs.push(await extract('spade',r));
 for(const s of Object.keys(colors))manifest.glyphs.push(await extract(s,'a',true));
 for(const g of manifest.glyphs)for(const [s,c]of Object.entries(colors)){
  const {data,info}=await sharp(path.join(out,g.key+'.png')).raw().toBuffer({resolveWithObject:true});
  const rgb=[1,3,5].map(i=>parseInt(c.slice(i,i+2),16));
  for(let i=0;i<data.length;i+=4){data[i]=rgb[0];data[i+1]=rgb[1];data[i+2]=rgb[2]}
  await sharp(data,{raw:{width:info.width,height:info.height,channels:4}}).png().toFile(path.join(out,`${g.key}-${s}.png`));
 }
 fs.writeFileSync(path.join(out,'manifest.json'),JSON.stringify(manifest,null,2));
 console.log(manifest.glyphs.map(g=>`${g.key}: ${g.width}x${g.height}`).join('\n'));
})();

