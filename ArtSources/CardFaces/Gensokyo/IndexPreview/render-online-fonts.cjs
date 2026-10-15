const fs=require('fs'),path=require('path');
const deps='C:/Users/jinshengkai/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/';
const {chromium}=require(deps+'playwright'),sharp=require(deps+'sharp');
const root=path.join(__dirname,'Names','Online');fs.mkdirSync(root,{recursive:true});
const fonts=[
 {id:'zhuque',name:'朱雀仿宋',file:'Zhuque/ZhuqueFangsong-Regular.ttf',note:'古籍印刷感，清雅克制',url:'https://github.com/TrionesType/zhuque'},
 {id:'wenkai',name:'霞鹜文楷 Medium',file:'lxgw-wenkai-medium.ttf',note:'温润手写感，笔画较柔和',url:'https://github.com/lxgw/LxgwWenKai'},
 {id:'mashan',name:'马善政毛笔楷书',file:'ma-shan-zheng.ttf',note:'毛笔题字感，笔画更鲜明',url:'https://github.com/googlefonts/mashanzheng'}
];
function supports(buf,cp){
 let cmap=0;for(let i=0;i<buf.readUInt16BE(4);i++){const p=12+i*16;if(buf.toString('ascii',p,p+4)==='cmap')cmap=buf.readUInt32BE(p+8)}
 for(let t=0;t<buf.readUInt16BE(cmap+2);t++){
  const off=cmap+buf.readUInt32BE(cmap+4+t*8+4),format=buf.readUInt16BE(off);
  if(format===12){const n=buf.readUInt32BE(off+12);for(let k=0;k<n;k++){const p=off+16+k*12,a=buf.readUInt32BE(p),b=buf.readUInt32BE(p+4);if(cp>=a&&cp<=b&&buf.readUInt32BE(p+8)+cp-a!==0)return true}}
  if(format===4&&cp<=65535){const n=buf.readUInt16BE(off+6)/2,end=off+14,start=end+n*2+2,delta=start+n*2,range=delta+n*2;for(let k=0;k<n;k++){const a=buf.readUInt16BE(start+k*2),b=buf.readUInt16BE(end+k*2);if(cp<a||cp>b)continue;const r=buf.readUInt16BE(range+k*2),d=buf.readInt16BE(delta+k*2);let gid=r?buf.readUInt16BE(range+k*2+r+(cp-a)*2):(cp+d)&65535;if(r&&gid)gid=(gid+d)&65535;if(gid)return true}}
 }
 return false;
}
(async()=>{
 const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe'});
 const page=await browser.newPage({viewport:{width:816,height:44},deviceScaleFactor:1});
 const names=JSON.parse(fs.readFileSync(path.resolve(__dirname,'../DeckGeneration.json'))).cards.map(x=>x.name).filter(Boolean).join('');
 const chars=[...new Set([...names])];
 for(const f of fonts){
  const font=fs.readFileSync(path.resolve(__dirname,'Names/Fonts',f.file));f.missing=chars.filter(c=>!supports(font,c.codePointAt(0)));
  const face=`@font-face{font-family:Candidate;src:url(data:font/ttf;base64,${font.toString('base64')})}`;
  await page.setContent(`<style>${face}html,body{margin:0;background:transparent}div{width:816px;height:44px;line-height:44px;text-align:center;font-family:Candidate;font-size:36px;letter-spacing:2px;color:#75644B;white-space:nowrap}</style><div>芙兰朵露·斯卡蕾特</div>`);
  await page.evaluate(()=>document.fonts.ready);const layer=path.join(root,f.id+'-name.png');await page.screenshot({path:layer,omitBackground:true});
  await sharp(path.resolve(__dirname,'../../../../Assets/Sprites/CardFaces/Gensokyo/heart-2.png')).composite([{input:layer,left:0,top:66}]).png().toFile(path.join(root,f.id+'.png'));
  await page.setContent(`<style>${face}body{margin:0;background:#fffef4;color:#75644B}div{font-family:Candidate;font-size:36px;line-height:60px;letter-spacing:2px;text-align:center}</style><div>姬海棠果　八意永琳</div><div>芙兰朵露·斯卡蕾特</div>`);
  await page.setViewportSize({width:520,height:120});await page.evaluate(()=>document.fonts.ready);await page.screenshot({path:path.join(root,f.id+'-detail.png')});await page.setViewportSize({width:816,height:44});
 }
 const data=p=>'data:image/png;base64,'+fs.readFileSync(path.join(root,p)).toString('base64');
 await page.setViewportSize({width:1180,height:100});
 await page.setContent(`<style>body{margin:0;padding:40px;background:#152b29;color:#eee9dd;font-family:'Microsoft YaHei'}h1{font-size:28px;margin:0 0 8px}p{color:#b6c8ba;font-size:16px;margin:0 0 26px}.row{display:flex;gap:26px}.col{width:348px}h2{font-size:21px;margin:0 0 7px}.note{font-size:14px;margin-bottom:18px}.card{width:348px;height:auto}.detail{width:348px;border-radius:10px;margin-top:18px}</style><h1>角色名字 · 三款开源字体对照</h1><p>统一 36px 字号、颜色与位置；下方为字形放大对照</p><div class="row">${fonts.map(f=>`<div class="col"><h2>${f.name}</h2><p class="note">${f.note}</p><img class="card" src="${data(f.id+'.png')}"><img class="detail" src="${data(f.id+'-detail.png')}"></div>`).join('')}</div>`);
 await page.evaluate(async()=>{await document.fonts.ready;await Promise.all([...document.images].map(i=>i.decode()))});await page.screenshot({path:path.join(root,'font-comparison.png'),fullPage:true});
 fs.writeFileSync(path.join(root,'fonts.json'),JSON.stringify(fonts,null,2));console.log(fonts.map(f=>({name:f.name,missing:f.missing})));await browser.close();
})();
