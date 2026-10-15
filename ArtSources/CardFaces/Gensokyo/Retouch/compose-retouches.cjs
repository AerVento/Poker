const fs=require('fs'),path=require('path');
const sharp=require('C:/Users/jinshengkai/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root=__dirname,W=816,H=1152;
const original=f=>path.join(root,'../IndexPreview/StandardizedDeck',f+'.png');
const out=path.join(root,'NoNames');fs.mkdirSync(out,{recursive:true});
const layers=path.join(root,'Layers');fs.mkdirSync(layers,{recursive:true});
let paperTexture,paperMean;
const paperColors=new WeakMap();
const poly=p=>`<polygon points="${p}" fill="white"/>`;
const rect=(x,y,w,h)=>`<rect x="${x}" y="${y}" width="${w}" height="${h}" fill="white"/>`;
async function mask(svg,w=W,h=H,blur=2){return sharp(Buffer.from(`<svg width="${w}" height="${h}" xmlns="http://www.w3.org/2000/svg">${svg}</svg>`)).blur(blur).ensureAlpha().raw().toBuffer();}
async function raw(input,w,h){let s=sharp(input);if(w)s=s.resize(w,h,{fit:'fill'});return s.ensureAlpha().raw().toBuffer();}
function mix(base,edit,m,w=W,h=H){const b=Buffer.from(base);for(let i=0;i<w*h;i++){const a=m[i*4+3]/255;if(a)for(let c=0;c<3;c++)b[i*4+c]=Math.round(base[i*4+c]*(1-a)+edit[i*4+c]*a);}return b;}
async function write(f,b){
 const base=await raw(original(f)),patch=Buffer.alloc(b.length);
 for(let y=0;y<H;y++)for(let x=0;x<W;x++){
  const i=(y*W+x)*4;
  if(x<40||x>=777||y<61||y>=1110||(x<151&&y<274)||(x>673&&y>880)||(x>708&&y<118)||(x<118&&y>1028))base.copy(b,i,i,i+4);
  if(b[i]!==base[i]||b[i+1]!==base[i+1]||b[i+2]!==base[i+2]){b.copy(patch,i,i,i+3);patch[i+3]=255;}
 }
 await sharp(b,{raw:{width:W,height:H,channels:4}}).png().toFile(path.join(out,f+'.png'));
 await sharp(patch,{raw:{width:W,height:H,channels:4}}).png().toFile(path.join(layers,f+'-repair.png'));
}
// Mask coordinates below are relative to the 752x994 illustration crop (32,110).
const edits=[
 ['spade-10','spade-10-v4',poly('530,375 747,375 747,698 602,696 559,580 562,520 548,458')+poly('97,547 316,532 298,586 253,653 203,702 95,718')],
 ['spade-6','spade-6-v2',poly('345,565 540,570 604,645 562,694 556,981 270,981 272,727 247,729 265,677 320,611')],
 ['club-k','club-k-v6',poly('170,20 747,20 747,653 451,653 451,448 447,414 450,374 477,338 502,301 494,274 469,247 438,220 411,200 387,187 350,173 325,171 307,181 277,177 251,195 220,237 207,285 187,311 165,296')+poly('190,140 430,140 430,210 352,202 321,190 302,196 290,206 269,207 250,225 232,245 205,260 190,250')],
 ['club-8','club-8',rect(14,513,487,271)],
 ['club-q','club-q',rect(184,682,155,248)],
 ['spade-4','spade-4',rect(364,431,209,218)],
 ['heart-4','heart-4-v2',rect(160,724,449,179)],
];
async function editStructure(f,g,shape){
 const base=await raw(original(f));let e=await raw(path.join(root,'Generated',g+'.png'),752,994);
 const full=Buffer.from(base);for(let y=0;y<994;y++)e.copy(full,((y+110)*W+32)*4,y*752*4,(y+1)*752*4);
 const m=await mask(`<g transform="translate(32 110)">${shape}</g>`);
 let result=mix(base,full,m);
 if(f==='club-k'){
  const clean=await raw(path.join(root,'Generated/club-k-background-cleanup.png'),225,155),layer=Buffer.from(result);
  for(let y=0;y<155;y++)clean.copy(layer,((y+520)*W+465)*4,y*225*4,(y+1)*225*4);
  result=mix(result,layer,await mask(rect(526,543,95,84)));
  const hair=await raw(path.join(root,'Generated/club-k-ear-occlusion.png'),194,211),hairLayer=Buffer.from(result);
  for(let y=0;y<211;y++)hair.copy(hairLayer,((y+416)*W+423)*4,y*194*4,(y+1)*194*4);
  result=mix(result,hairLayer,await mask(poly('473,420 573,420 596,513 594,573 553,594 480,582 475,533 480,489 472,462'),W,H,2));
 }
 await write(f,result);
}
function paper(base,x,y){
 // Reuse the original blank-card reference texture, matched to each existing card's paper tone.
 let values=paperColors.get(base);
 if(!values){values=[0,0,0];for(let yy=40;yy<60;yy+=2)for(let xx=170;xx<250;xx+=4){const i=(yy*W+xx)*4;for(let c=0;c<3;c++)values[c]+=base[i+c]/200;}paperColors.set(base,values);}
 return values.map((v,c)=>Math.max(0,Math.min(255,Math.round(v+paperTexture[(y*W+x)*4+c]-paperMean[c]))));
}
function protect(x,y){return x<40||x>=777||y<61||y>=1110||(x<151&&y<274)||(x>664&&y>880)||(x>708&&y<118)||(x<118&&y>1028);}
async function transformIllustration(f,scale,down){
 const base=await raw(original(f)),clean=Buffer.from(base);
 // Remove fixed corner graphics from the transform layer. They are restored pixel-for-pixel below.
 for(let y=38;y<1110;y++)for(let x=38;x<778;x++)if(protect(x,y)){
  const p=paper(base,x,y);for(let c=0;c<3;c++)clean[(y*W+x)*4+c]=p[c];
 }
 const source=await sharp(clean,{raw:{width:W,height:H,channels:4}}).extract({left:40,top:61,width:737,height:1049}).resize(Math.round(737*scale),Math.round(1049*scale)).raw().toBuffer();
 const sw=Math.round(737*scale),sh=Math.round(1049*scale),left=Math.round(408-(408-40)*scale),top=Math.round(1105-(1105-61)*scale+down);
 const b=Buffer.from(base);
 for(let y=61;y<1110;y++)for(let x=40;x<777;x++)if(!protect(x,y)){
  const sx=x-left,sy=y-top,i=(y*W+x)*4,p=sx>=0&&sx<sw&&sy>=0&&sy<sh?source.subarray((sy*sw+sx)*4,(sy*sw+sx)*4+3):paper(base,x,y);
  for(let c=0;c<3;c++)b[i+c]=p[c];
 }
 await write(f,b);
}
async function remilia(){
 const f='heart-k',base=await raw(original(f)),edit=Buffer.from(base);
 const shape=poly('288,58 710,58 721,530 645,535 630,457 579,423 578,353 557,326 549,279 536,244 513,216 495,199 473,191 454,193 426,178 397,169 370,163 344,163 322,168 289,179');
 const m=await mask(shape,W,H,2);
 for(let y=58;y<534;y++)for(let x=280;x<730;x++){
  const i=(y*W+x)*4,sy=Math.round(58+(y-118)*(534-58)/(534-118));
  const p=sy<58?paper(base,x,y):base.subarray((sy*W+x)*4,(sy*W+x)*4+3);
  for(let c=0;c<3;c++)edit[i+c]=p[c];
 }
 await write(f,mix(base,edit,m));
}
async function trimOkina(){
 const f='spade-k',base=await raw(original(f)),edit=Buffer.from(base);
 const m=await mask(rect(175,100,488,36),W,H,4);
 for(let y=94;y<140;y++)for(let x=170;x<670;x++){
  const a=Math.max(0,Math.min(1,(133-y)/15)),p=paper(base,x,y),i=(y*W+x)*4;
  for(let c=0;c<3;c++)edit[i+c]=Math.round(base[i+c]*(1-a)+p[c]*a);
 }await write(f,mix(base,edit,m));
}
async function ear(){
 const f='diamond-6',base=await raw(original(f)),e=await raw(path.join(root,'Generated/diamond-6-ear.png'),350,230),full=Buffer.from(base);
 for(let y=0;y<230;y++)e.copy(full,((y+70)*W+190)*4,y*350*4,(y+1)*350*4);
 const m=await mask(poly('224,81 419,81 420,237 398,257 375,254 352,205 296,287 239,301 220,255'));
 await write(f,mix(base,full,m));
}
(async()=>{
 paperTexture=await sharp(path.join(root,'../card-frame-reference.png')).extract({left:1190,top:390,width:180,height:280}).resize(W,H).ensureAlpha().raw().toBuffer();
 paperMean=[0,0,0];for(let i=0;i<W*H;i++)for(let c=0;c<3;c++)paperMean[c]+=paperTexture[i*4+c]/(W*H);
 await sharp(paperTexture,{raw:{width:W,height:H,channels:4}}).png().toFile(path.join(layers,'reference-paper-texture.png'));
 for(const e of edits)await editStructure(...e);
 // User rejected these anatomy changes; retain the original illustration on future exports.
 for(const f of ['spade-2','diamond-a','diamond-7'])await write(f,await raw(original(f)));
 await transformIllustration('spade-7',0.96,0);
 await transformIllustration('heart-7',0.973,0);
 await transformIllustration('diamond-4',1,10);
 await transformIllustration('diamond-9',1,10);
 await remilia();await trimOkina();await ear();
 console.log('Prepared 14 retouched illustrations and 3 restored originals without names; official Assets not modified.');
})();
