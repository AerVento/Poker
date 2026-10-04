"""Build spell Foley from CC0 recordings. Requires numpy and soundfile; no generated tones or music."""
from pathlib import Path
import sys,json,hashlib
ROOT=Path(__file__).resolve().parents[3]
sys.path.insert(0,str(ROOT/'Logs/audio-tools'))
import numpy as np
import soundfile as sf
RATE=44100
SOURCE=Path(__file__).parent/'External'
used=set()

def load(path):
    used.add(path)
    x,r=sf.read(str(SOURCE/path),always_2d=True)
    if x.shape[1]==1:x=np.repeat(x,2,axis=1)
    if r!=RATE:
        t=np.arange(round(len(x)*RATE/r))*r/RATE
        x=np.stack([np.interp(t,np.arange(len(x)),x[:,c]) for c in range(2)],axis=1)
    return x[:,:2]

def cut(x,d,offset=0):return x[round(offset*RATE):round((offset+d)*RATE)].copy()
def speed(x,rate):
    t=np.arange(0,len(x)-1,rate)
    return np.stack([np.interp(t,np.arange(len(x)),x[:,c]) for c in range(2)],axis=1)
def fade(x,attack=.003,tail=.03):
    x=x.copy();a=min(len(x),round(attack*RATE));b=min(len(x),round(tail*RATE))
    if a:x[:a]*=np.linspace(0,1,a)[:,None]
    if b:x[-b:]*=np.linspace(1,0,b)[:,None]
    return x
def gain(x,peak=1):return x*peak/max(1e-6,np.max(abs(x)))
def filter_audio(x,lo=70,hi=10000):
    f=np.fft.rfftfreq(len(x),1/RATE)
    response=(1-np.exp(-(f/lo)**4))*np.exp(-(f/hi)**6)
    return np.fft.irfft(np.fft.rfft(x,axis=0)*response[:,None],len(x),axis=0)
def canvas(d):return np.zeros((round(d*RATE),2))
def add(dst,x,at=0,level=1,pan=0):
    a=round(at*RATE);n=min(len(x),len(dst)-a)
    if n>0:dst[a:a+n]+=x[:n]*np.array([1-max(0,pan)*.55,1+min(0,pan)*.55])*level

def transient(x,d=.16):
    mono=np.max(abs(x),axis=1);start=max(0,int(np.argmax(mono))-round(.012*RATE))
    return fade(gain(cut(x,d,start/RATE)))

cloth=fade(gain(filter_audio(cut(load('RPG/Audio/cloth1.ogg'),.3),140,6500)))
blade=transient(filter_audio(load('RPG/Audio/drawKnife2.ogg'),900,10000),.19)
slice_fx=transient(filter_audio(load('RPG/Audio/knifeSlice.ogg'),450,8500),.16)
glass=transient(filter_audio(load('Impact/Audio/impactGlass_light_002.ogg'),500,10000),.23)
metal=fade(gain(filter_audio(load('Impact/Audio/impactMetal_heavy_001.ogg'),45,7000)))
plate=fade(gain(filter_audio(load('Impact/Audio/impactPlate_heavy_000.ogg'),65,8500)))
punch=transient(filter_audio(load('Impact/Audio/impactPunch_heavy_000.ogg'),45,5500),.28)
clock=transient(filter_audio(cut(load('clock-tick-preview.mp3'),8),250,9500),.11)
reports=[]
def save(name,cue,x,peak=.78):
    x=filter_audio(x,35,12000)
    # Normalize only downwards where possible; no clipped or flattened attack.
    x*=min(1,peak/max(1e-6,np.max(abs(x))))
    x=fade(x,.001,.04)
    out=ROOT/'Assets/Audio/SFX/Gifts'/name/('gift-'+cue+'.wav')
    sf.write(str(out),x,RATE,subtype='PCM_16')
    reports.append({'file':str(out.relative_to(ROOT)),'seconds':round(len(x)/RATE,3),'peak':round(float(np.max(abs(x))),4),'rms':round(float(np.sqrt(np.mean(x*x))),4)})

for name in ['ResurrectionButterfly','FantasySeal','MasterSpark','TheWorld','HourOfDaybreak']:
    x=canvas(.38);add(x,cloth,0,.18);save(name,'arrive',x,.25)
    if name=='TheWorld':
        x=canvas(.66);add(x,clock,0,.6);add(x,speed(clock,1.06),.34,.58);save(name,'cast',x,.65)
        # A reverse metal intake pulls into a heavy, short impact, then collapses to silence.
        x=canvas(.94);swell=fade(cut(speed(plate,.58),.28)[::-1],.09,.008)
        add(x,swell,0,.4);add(x,punch,.27,.8);add(x,cut(speed(metal,.33),.53),.27,.85)
        add(x,blade,.27,.27);add(x,cut(speed(plate,.48),.3),.34,.2,-.65)
        x[round(.62*RATE):]*=np.linspace(1,0,len(x)-round(.62*RATE))[:,None]**3
        save(name,'stop',x,.94)
        x=canvas(1.3)
        for i in range(14):add(x,speed(slice_fx,1.15+(i%3)*.025),.05+i*.042,.26,-.7+1.4*i/13)
        add(x,blade,0,.32);save(name,'release',x,.78)
        x=canvas(.9)
        for i in range(5):add(x,cut(speed(plate,1.5),.17),i*.09,.22,(-1)**i*.5)
    elif name=='MasterSpark':
        x=canvas(1.35);swell=fade(cut(speed(metal,.5),1.2)[::-1],.12,.025)
        add(x,gain(swell,.45),.05);add(x,blade,.85,.18);save(name,'cast',x,.6)
        x=canvas(2.85);low=fade(cut(speed(metal,.2),2.6),.015,.3)
        add(x,low,0,.55);add(x,punch,0,.72);add(x,speed(plate,.55),.03,.32)
        add(x,cut(speed(metal,.22),2.3),.22,.24,.5);save(name,'release',x,.9)
        x=canvas(1);add(x,punch,0,.48);add(x,cut(speed(metal,.5),.8),0,.32)
    else:
        x=canvas(.62);add(x,fade(glass[::-1],.06,.015),.12,.22);add(x,cloth,0,.12);save(name,'cast',x,.38)
        x=canvas(2.9)
        if name=='FantasySeal':
            for i in range(6):
                add(x,speed(slice_fx,1.55),i*.13,.16,-.6+i*.24)
                add(x,speed(glass,1.06),i*.13,.24,-.6+i*.24)
        elif name=='ResurrectionButterfly':
            for i in range(8):
                add(x,speed(slice_fx,1.3),i*.19,.085,(-1)**i*.55)
                add(x,glass,i*.19,.12,(-1)**i*.55)
        else:
            for wave in range(3):
                for i in range(4):add(x,speed(glass,1.22),wave*.49+i*.075,.16,(-1)**i*.6)
        save(name,'release',x,.68)
        x=canvas(.85);add(x,glass,0,.3);add(x,cut(speed(plate,1.25),.4),.03,.12)
        if name=='HourOfDaybreak':add(x,cut(speed(metal,1.2),.5),0,.14)
    save(name,'impact',x,.65)
(ROOT/'Logs/spell-audio-validation.json').write_text(json.dumps(reports,indent=2),encoding='utf-8')
manifest={'license':'CC0-1.0','music':'No music recording or synthesized melody included.','sources':[
 {'author':'Kenney','page':'https://kenney.nl/assets/rpg-audio','licenseFile':'External/RPG/License.txt'},
 {'author':'Kenney','page':'https://kenney.nl/assets/impact-sounds','licenseFile':'External/Impact/License.txt'},
 {'author':'Zermonth','title':'Clock Tick.wav','page':'https://freesound.org/people/Zermonth/sounds/116636/','license':'https://creativecommons.org/publicdomain/zero/1.0/','download':'https://cdn.freesound.org/previews/116/116636_994962-hq.mp3','note':'Public high-quality preview, trimmed and filtered.'}],
 'files':[{'path':p,'sha256':hashlib.sha256((SOURCE/p).read_bytes()).hexdigest()} for p in sorted(used)]}
(Path(__file__).parent/'audio-sources.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Wrote',len(reports),'sample-based cues. Sources:',len(used))
