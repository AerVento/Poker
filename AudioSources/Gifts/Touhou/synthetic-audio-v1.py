"""Reproducible original stereo spell sound design (numpy only, no sampled music)."""
from pathlib import Path
import json, wave
import numpy as np

ROOT=Path(__file__).resolve().parents[3]
RATE=44100
NAMES=('ResurrectionButterfly','FantasySeal','MasterSpark','TheWorld','HourOfDaybreak')
rng=np.random.default_rng(61003)

def time(d): return np.arange(round(d*RATE))/RATE

def tone(d,f,end=None):
    t=time(d)
    return np.sin(2*np.pi*(f*t if end is None else f*t+(end-f)*t*t/(2*d)))

def noise(d,low=100,high=7000):
    n=len(time(d));freq=np.fft.rfftfreq(n,1/RATE)
    spectrum=np.fft.rfft(rng.normal(size=n))
    window=(1-np.exp(-(freq/max(low,1))**4))*np.exp(-(freq/high)**4)
    x=np.fft.irfft(spectrum*window,n)
    return x/max(1e-6,np.std(x))

def env(d,attack=.02,decay=3):
    t=time(d)
    return np.minimum(1,t/max(attack,.0001))*np.exp(-t*decay)*np.minimum(1,(d-t)/.04)

def bell(d,f):
    t=time(d);x=np.zeros(len(t))
    for ratio,gain,decay in [(1,1,2.8),(2.01,.32,5.4),(2.76,.17,7),(4.07,.07,12)]:
        x+=gain*np.sin(2*np.pi*f*ratio*t)*env(d,.004,decay)
    return x

def swoosh(d,bright=1):
    t=time(d);window=np.sin(np.pi*t/d)**1.7
    return (noise(d,300,3500+1800*bright)*.23+tone(d,360*bright,120*bright)*.07)*window

def hit(d,bright=1):
    return tone(d,145,38)*env(d,.002,13)*.5+noise(d,800,6800)*env(d,.001,28)*.18+bell(d,950*bright)*.13

def stereo(d):return np.zeros((len(time(d)),2))

def add(dst,x,at=0,gain=1,pan=0):
    start=round(at*RATE);count=min(len(x),len(dst)-start)
    if count<=0:return
    # Equal-power panning, capped so both channels retain the cue.
    pan=np.clip(pan,-.85,.85);g=np.array([np.sqrt((1-pan)/2),np.sqrt((1+pan)/2)])
    dst[start:start+count]+=x[:count,None]*g*gain

def finish(name,cue,x,echo=.15):
    dry=x.copy()
    for delay,gain in [(.047,echo),(.083,echo*.7),(.137,echo*.5),(.211,echo*.28)]:
        n=round(delay*RATE)
        x[n:]+=dry[:-n,::-1]*gain
    # Gentle saturation controls stacked transients; explicit fades avoid clicks.
    x=np.tanh(x*1.15)
    peak=max(.001,float(np.max(np.abs(x))))
    x*=.83/peak
    n=min(len(x),int(RATE*.025));x[:n]*=np.linspace(0,1,n)[:,None];x[-n:]*=np.linspace(1,0,n)[:,None]
    path=ROOT/'Assets/Audio/SFX/Gifts'/name/('gift-'+cue+'.wav')
    path.parent.mkdir(parents=True,exist_ok=True)
    with wave.open(str(path),'wb') as f:
        f.setnchannels(2);f.setsampwidth(2);f.setframerate(RATE);f.writeframes((x*32767).astype('<i2').tobytes())
    return {'file':str(path.relative_to(ROOT)),'seconds':round(len(x)/RATE,3),'peak':round(float(np.max(abs(x))),4),'rms':round(float(np.sqrt(np.mean(x*x))),4)}

reports=[]
for index,name in enumerate(NAMES):
    f=(698,784,440,1109,587)[index]
    x=stereo(.7);add(x,swoosh(.48,.65),0,.5,-.25);add(x,bell(.6,f),.08,.18,.25)
    reports.append(finish(name,'arrive',x,.09))
    if name=='TheWorld':
        x=stereo(.68)
        for at,pitch in [(0,1800),(.33,1350)]:
            tick=(noise(.09,1800,8500)*.22+tone(.09,pitch)*.45)*env(.09,.001,65)
            add(x,tick,at,1,-.4 if at==0 else .4)
        reports.append(finish(name,'cast',x,0))
        x=stereo(.48);add(x,tone(.4,430,42)*env(.4,.001,9),0,.8);add(x,bell(.35,1680),0,.28)
        add(x,noise(.11,1600,9000)*env(.11,.001,40),0,.28)
        reports.append(finish(name,'stop',x,.03))
        x=stereo(1.8);add(x,hit(.45,1.8),0,.6)
        for i in range(14):add(x,swoosh(.22,1.5),.09+i*.043,.48,-.75+1.5*i/13)
        reports.append(finish(name,'release',x,.12))
        x=stereo(1.0)
        for i in range(5):add(x,hit(.45,1.6+i*.14),i*.065,.36,(-1)**i*.35)
    elif name=='MasterSpark':
        x=stereo(1.4);t=time(1.35)
        charge=(tone(1.35,95,760)*.27+tone(1.35,150,1150)*.14+noise(1.35,150,4500)*.1)*(t/1.35)**1.5
        add(x,charge,0,1)
        reports.append(finish(name,'cast',x,.16))
        x=stereo(2.9);d=2.65;t=time(d)
        sustain=(tone(d,72)*.35+tone(d,146)*.15+noise(d,180,4500)*.23)*(1+.12*np.sin(2*np.pi*13*t))*env(d,.02,.25)
        add(x,sustain,0,1);add(x,noise(d,1700,6200)*env(d,.04,.7),0,.08,.7);add(x,hit(.65,.6),0,.75)
        reports.append(finish(name,'release',x,.2))
        x=stereo(1.15);add(x,hit(.85,.65),0,.6);add(x,swoosh(.85,.45),.05,.4)
    else:
        d=.83 if name=='FantasySeal' else 1.1
        x=stereo(d);swell=bell(d,f)[::-1]*np.linspace(.1,.8,len(time(d)))
        add(x,swell,0,.4,-.3);add(x,tone(d,f*.25,f)*np.sin(np.pi*time(d)/d)**1.3,0,.12,.3)
        reports.append(finish(name,'cast',x,.15))
        x=stereo(3.1)
        if name=='FantasySeal':
            for i,ratio in enumerate([1,1.125,1.25,1.5,1.75,2]):
                add(x,swoosh(.38,1+i*.05),i*.13,.44,-.7+i*.28)
                add(x,bell(.8,480*ratio),i*.13,.25,-.7+i*.28)
        elif name=='ResurrectionButterfly':
            d=2.7;t=time(d)
            add(x,noise(d,320,2800)*(.6+.4*np.sin(2*np.pi*8*t)**2)*env(d,.22,.6),0,.14)
            for i,ratio in enumerate([1,1.2,1.5,1.8]):add(x,bell(1.8,440*ratio),i*.27,.2,(-1)**i*.6)
        else:
            for i,ratio in enumerate([1,1.25,1.5,2,2.5,3,4]):
                add(x,bell(1.25,330*ratio),i*.22,.25,-.7+i*.23)
                add(x,swoosh(.48,.65+i*.08),i*.22,.12,(-1)**i*.5)
        reports.append(finish(name,'release',x,.24))
        x=stereo(1.6)
        if name=='HourOfDaybreak':
            for ratio in [1,1.25,1.5,2]:add(x,bell(1.5,660*ratio),0,.2,(ratio-1.5)*.7)
            add(x,swoosh(.95,.9),0,.3)
        elif name=='ResurrectionButterfly':
            add(x,bell(1.5,880),0,.22,-.3);add(x,bell(1.5,1056),.08,.18,.3);add(x,swoosh(1,.6),0,.3)
        else:add(x,hit(.75,1.2),0,.35);add(x,bell(1.2,1176),.06,.25)
    reports.append(finish(name,'impact',x,.23))
(ROOT/'Logs/spell-audio-validation.json').write_text(json.dumps(reports,indent=2),encoding='utf-8')
print('Wrote',len(reports),'stereo cues; all peaks below 0 dBFS.')
