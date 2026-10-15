const fs=require('fs'),path=require('path');
const deps='C:/Users/jinshengkai/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/';
const {chromium}=require(deps+'playwright'),sharp=require(deps+'sharp');
const root=__dirname,out=path.join(root,'Names','Final');fs.mkdirSync(out,{recursive:true});
const cards=JSON.parse(fs.readFileSync(path.resolve(root,'../DeckGeneration.json'))).cards.filter(x=>x.file!=='back.png');
for(const [suit,name] of Object.entries({heart:'蕾米莉亚·斯卡蕾特',diamond:'八意永琳',club:'风见幽香',spade:'摩多罗隐岐奈'})){
 if(!cards.some(c=>c.file===suit+'-k.png'))cards.push({file:suit+'-k.png',name});
}
if(!cards.some(c=>c.file==='spade-7.png'))cards.push({file:'spade-7.png',name:'射命丸文'});
for(const card of cards){
 if(card.file==='diamond-5.png')card.name='因幡帝';
 if(card.file==='diamond-8.png')card.name='四季映姬·亚玛萨那度';
}
if(cards.length!==52||new Set(cards.map(c=>c.file)).size!==52)throw Error('Expected 52 unique faces');
const font=fs.readFileSync(path.join(root,'Names/Fonts/lxgw-wenkai-medium.ttf'));
(async()=>{
 const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe'});
 const page=await browser.newPage({viewport:{width:816,height:44},deviceScaleFactor:1});
 await page.setContent(`<style>@font-face{font-family:WenKai;src:url(data:font/ttf;base64,${font.toString('base64')})}html,body{margin:0;background:transparent}div{width:816px;height:44px;line-height:44px;text-align:center;font-family:WenKai;font-size:36px;letter-spacing:2px;color:#75644B;white-space:nowrap;text-shadow:0 0 2px #fffdf3,0 0 4px #fffdf3,0 0 6px #fffdf3}</style><div id="name"></div>`);
 await page.evaluate(()=>document.fonts.load('36px WenKai'));
 const record=[];
 for(const card of cards){
  await page.locator('#name').evaluate((e,name)=>e.textContent=name,card.name);
  const width=await page.locator('#name').evaluate(e=>{const r=document.createRange();r.selectNodeContents(e);return r.getBoundingClientRect().width});
  if(width>516)throw Error('Name too wide: '+card.name);
  const layer=path.join(out,card.file.replace('.png','-name.png'));await page.screenshot({path:layer,omitBackground:true});
  const retouched=path.resolve(root,'../Retouch/NoNames',card.file);
  const src=fs.existsSync(retouched)?retouched:path.join(root,'StandardizedDeck',card.file),dest=path.resolve(root,'../../../../Assets/Sprites/CardFaces/Gensokyo',card.file);
  const {data,info}=await sharp(src).ensureAlpha().raw().toBuffer({resolveWithObject:true});
  const result=await sharp(src).composite([{input:layer,left:0,top:66}]).raw().toBuffer();
  for(let i=0;i<result.length;i+=4){const y=Math.floor(i/4/816);result[i+3]=data[i+3];if(y<66||y>=110){result[i]=data[i];result[i+1]=data[i+1];result[i+2]=data[i+2]}}
  await sharp(result,{raw:{width:info.width,height:info.height,channels:4}}).png().toFile(path.join(out,card.file));
  if(!process.argv.includes('--preview'))fs.copyFileSync(path.join(out,card.file),dest);
  record.push({file:card.file,name:card.name,width});
 }
 const sheet=[];for(const [i,c]of cards.entries()){const b=await sharp(path.join(out,c.file)).extract({left:155,top:54,width:510,height:64}).resize(510,64).png().toBuffer();sheet.push({input:b,left:i%4*518,top:Math.floor(i/4)*72})}
 await sharp({create:{width:2072,height:936,channels:4,background:'#152b29'}}).composite(sheet).png().toFile(path.join(out,'all-names.png'));
 fs.writeFileSync(path.join(out,'manifest.json'),JSON.stringify({font:'LXGW WenKai Medium',version:'1.522',size:36,tracking:2,color:'#75644B',top:66,lineHeight:44,cards:record},null,2));
 console.log('Applied '+cards.length+' names; max width '+Math.max(...record.map(r=>r.width)));await browser.close();
})();
