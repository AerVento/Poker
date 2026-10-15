const fs=require('fs'),path=require('path');
const deps='C:/Users/jinshengkai/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/';
const {chromium}=require(deps+'playwright'),sharp=require(deps+'sharp');
const root=path.join(__dirname,'Names');
fs.mkdirSync(root,{recursive:true});
const examples=[{file:'spade-6',name:'姬海棠果'},{file:'heart-2',name:'芙兰朵露·斯卡蕾特'}];
(async()=>{
 const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe'});
 const page=await browser.newPage({viewport:{width:816,height:44},deviceScaleFactor:1});
 for(const item of examples){
  await page.setContent(`<style>html,body{margin:0;background:transparent}div{width:816px;height:44px;line-height:44px;text-align:center;font-family:'Microsoft YaHei';font-size:32px;font-weight:400;letter-spacing:2px;color:#75644B;white-space:nowrap}</style><div>${item.name}</div>`);
  await page.evaluate(()=>document.fonts.ready);
  const layer=path.join(root,item.file+'-name.png');
  await page.screenshot({path:layer,omitBackground:true});
  const original=path.resolve(__dirname,'../../../../Assets/Sprites/CardFaces/Gensokyo',item.file+'.png');
  const {data,info}=await sharp(original).ensureAlpha().raw().toBuffer({resolveWithObject:true});
  const merged=await sharp(original).composite([{input:layer,left:0,top:66}]).raw().toBuffer();
  // Preserve every source pixel outside the text strip, including transparent RGB.
  for(let i=0;i<merged.length;i+=4){const y=Math.floor(i/4/816);merged[i+3]=data[i+3];if(y<66||y>=110){merged[i]=data[i];merged[i+1]=data[i+1];merged[i+2]=data[i+2]}}
  await sharp(merged,{raw:{width:info.width,height:info.height,channels:4}}).png().toFile(path.join(root,item.file+'.png'));
 }
 await page.setViewportSize({width:928,height:100});
 const image=p=>'data:image/png;base64,'+fs.readFileSync(p).toString('base64');
 await page.setContent(`<style>body{margin:0;padding:40px;background:#152b29;color:#eee9dd;font-family:'Microsoft YaHei'}h1{font-size:28px;margin:0 0 10px}p{font-size:16px;color:#becabe;margin:0 0 24px}.cards{display:flex;gap:32px}.cards img{width:408px;height:576px}</style><h1>角色名称 · 拼接预览</h1><p>同一字体与字号 · 阴阳鱼下方居中 · 暖灰褐色</p><div class="cards">${examples.map(e=>`<img src="${image(path.join(root,e.file+'.png'))}">`).join('')}</div>`);
  await page.evaluate(async()=>{await document.fonts.ready;await Promise.all([...document.images].map(i=>i.decode()))});
  await page.screenshot({path:path.join(root,'name-preview.png'),fullPage:true});
  fs.writeFileSync(path.join(root,'layout.json'),JSON.stringify({status:'preview',font:'Microsoft YaHei',fontSize:32,fontWeight:400,letterSpacing:2,color:'#75644B',centerX:408,stripTop:66,stripHeight:44,examples},null,2));
 await browser.close();
})();
