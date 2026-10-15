// Deterministic typography proof; ivory corner plates are provisional.
const fs = require('fs');
const path = require('path');
const { chromium } = require('C:/Users/jinshengkai/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root = __dirname;
const colors = {spade:'#37855B',club:'#397DB5',diamond:'#D98A32',heart:'#C94F58'};
const names = {spade:'黑桃',club:'梅花',diamond:'方片',heart:'红桃'};
const shapes = {
 spade:'M50 0 C42 14 4 39 4 62 C4 89 36 91 47 73 C45 86 41 94 34 100 L66 100 C59 94 55 86 53 73 C64 91 96 89 96 62 C96 39 58 14 50 0Z',
 heart:'M50 100 C40 86 4 59 4 30 C4 -2 39 -7 50 18 C61 -7 96 -2 96 30 C96 59 60 86 50 100Z',
 diamond:'M50 0 L94 50 L50 100 L6 50Z',
 club:'M50 0 C26 0 20 28 34 40 C10 29 -3 52 6 72 C14 91 36 86 47 73 C45 86 41 94 34 100 L66 100 C59 94 55 86 53 73 C64 86 86 91 94 72 C103 52 90 29 66 40 C80 28 74 0 50 0Z'
};
const img = p=>'data:image/png;base64,'+fs.readFileSync(p).toString('base64');
const label = (s)=>`<span style="color:${colors[s]}">${names[s]} ${colors[s]}</span>`;
const index = (s,r)=>`<g fill="${colors[s]}"><g class="rank" data-rank="${r}"><text x="0" y="0" font-family="Georgia" font-weight="bold" font-size="144">${r}</text></g><svg x="60" y="180" width="88" height="94" viewBox="0 0 100 100" preserveAspectRatio="none"><path d="${shapes[s]}"/></svg></g>`;
const svg=(w,h,body)=>`<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}">${body}</svg>`;
const card=(base,clean)=>svg(816,1152,`<defs><clipPath id="corners"><rect x="37" y="43" width="108" height="244"/><rect x="674" y="867" width="105" height="230"/></clipPath></defs><image href="${base}" width="816" height="1152"/>${clean?`<g clip-path="url(#corners)"><rect width="816" height="1152" fill="#fffef4"/></g>${index('spade','6')}<g transform="translate(816 1152) rotate(180)">${index('spade','6')}</g>`:''}`);
const style=`<style>*{box-sizing:border-box}body{margin:0;padding:40px;background:#152b29;color:#eee9dd;font-family:'Microsoft YaHei',sans-serif}h1{font-size:30px;margin:0 0 12px}p{font-size:17px;color:#b4c4b9;margin:0 0 25px}.row{display:flex;gap:22px;align-items:center;margin:16px 0}.label{width:168px;font-size:18px}.tile{width:140px;height:207px;border-radius:17px;background:#fffef4;border:1px solid #cfb678;overflow:hidden}.tile>svg{width:140px;height:207px}.cards{display:flex;gap:32px}.cards figure{margin:0}.cards figcaption{font-size:22px;margin-bottom:14px}.cards svg{width:408px;height:576px}footer{font-size:15px;color:#b4c4b9;margin-top:25px}</style>`;
async function render(browser,file,html,width){
 const page=await browser.newPage({viewport:{width,height:100},deviceScaleFactor:2});
 await page.setContent(style+html);
 await page.evaluate(async()=>{await document.fonts.ready;document.querySelectorAll('.rank').forEach(g=>{const ctx=document.createElement("canvas").getContext("2d");ctx.font="bold 144px Georgia";const m=ctx.measureText(g.dataset.rank);const b={x:-m.actualBoundingBoxLeft,y:-m.actualBoundingBoxAscent,width:m.actualBoundingBoxLeft+m.actualBoundingBoxRight,height:m.actualBoundingBoxAscent+m.actualBoundingBoxDescent};const sy=106/b.height;const sx=Math.min(sy,130/b.width);g.setAttribute('transform',`translate(${104-(b.x+b.width/2)*sx} ${58-b.y*sy}) scale(${sx} ${sy})`)});});
 const height=await page.evaluate(()=>document.documentElement.scrollHeight);
 await page.setViewportSize({width,height});
 fs.writeFileSync(path.join(root,file+'.html'),await page.content());
 await page.screenshot({path:path.join(root,file+'.png'),fullPage:true});
 await page.close();
}
(async()=>{
 const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe'});
 let html='<h1>幻想乡绘札 · 牌角样式预览</h1><p>四种固定色 · 同一套字形 · 同一中心线与间距</p>';
 for(const s of Object.keys(colors))html+=`<div class="row"><div class="label">${label(s)}</div>${['A','6','10','J','Q','K'].map(r=>`<div class="tile">${svg(208,306,index(s,r))}</div>`).join('')}</div>`;
 html+='<footer>预览字体：Georgia Bold　｜　成牌基准：816 × 1152　｜　数字与花色均为纯色</footer>';
 await render(browser,'index-grid',html,1240);
 {
  const base=img(path.resolve(root,'../../../../Assets/Sprites/CardFaces/Gensokyo/spade-6.png'));
  const clean=true;
  await render(browser,'spade-6-comparison',`<h1>黑桃 6 · 修改前后对照</h1><p>姬海棠果 · 保留原人物与场景，仅统一两个牌角</p><div class="cards"><figure><figcaption>原版</figcaption>${card(base,null)}</figure><figure><figcaption>统一模板 · #37855B</figcaption>${card(base,clean)}</figure></div><footer>牌角预览稿，尚未替换 Unity 中的正式牌面。</footer>`,928);
 }
 await browser.close();
})();



