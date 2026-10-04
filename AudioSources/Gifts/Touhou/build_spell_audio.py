"""Original spell synthesis. Approved charge WAVs are preserved byte-for-byte; no phase notification sounds."""
from pathlib import Path
import json,wave,shutil
import numpy as np
ROOT=Path(__file__).resolve().parents[3]
SOURCE=Path(__file__).parent
RATE=44100
rng=np.random.default_rng(61004)
def t(d):return np.arange(round(d*RATE))/RATE
def tone(d,f,end=None):
    x=t(d);return np.sin(2*np.pi*(f*x if end is None else f*x+(end-f)*x*x/(2*d)))
def band(d,lo,hi):
    n=len(t(d));f=np.fft.rfftfreq(n,1/RATE)
    mask=(1-np.exp(-(f/lo)**6))*np.exp(-(f/hi)**6)
    x=np.fft.irfft(np.fft.rfft(rng.normal(size=n))*mask,n)
    return x/max(1e-6,np.std(x))
def env(d,attack=.005,decay=8):
    x=t(d);return np.minimum(1,x/attack)*np.exp(-x*decay)*np.minimum(1,(d-x)/.025)
def canvas(d):return np.zeros((len(t(d)),2))
def add(dst,x,at=0,level=1,pan=0):
    start=round(at*RATE);n=min(len(x),len(dst)-start)
    if n<=0:return
    if x.ndim==1:x=x[:,None]*np.array([np.sqrt((1-pan)/2),np.sqrt((1+pan)/2)])
    dst[start:start+n]+=x[:n]*level
def read(path):
    with wave.open(str(path)) as f:
        assert f.getframerate()==RATE and f.getsampwidth()==2
        return np.frombuffer(f.readframes(f.getnframes()),dtype='<i2').reshape(-1,f.getnchannels())/32768.0
def swish(d=.17,brightness=1):
    x=t(d);window=np.sin(np.pi*x/d)**2.2
    # Brief filtered air and a faint pitch fall: a distinct xiou, without an impact or sustained hiss.
    return (band(d,1800*brightness,6500*brightness)*.13+tone(d,2100*brightness,650*brightness)*.035)*window
reports=[]
def report(path):
    x=read(path);return {'file':str(path.relative_to(ROOT)),'seconds':round(len(x)/RATE,3),'peak':round(float(np.max(abs(x))),4),'rms':round(float(np.sqrt(np.mean(x*x))),4)}
def save(name,cue,x,peak=.7,echo=0):
    dry=x.copy()
    for delay,g in [(.073,echo),(.131,echo*.55)]:
        k=round(delay*RATE)
        if k<len(x):x[k:]+=dry[:-k,::-1]*g
    x*=peak/max(.0001,np.max(abs(x)))
    a=min(len(x),round(.0015*RATE));b=min(len(x),round(.035*RATE))
    x[:a]*=np.linspace(0,1,a)[:,None];x[-b:]*=np.linspace(1,0,b)[:,None]
    path=ROOT/'Assets/Audio/SFX/Gifts'/name/('gift-'+cue+'.wav')
    with wave.open(str(path),'wb') as f:
        f.setnchannels(2);f.setsampwidth(2);f.setframerate(RATE);f.writeframes((x*32767).astype('<i2').tobytes())
    reports.append(report(path))
for name in ['ResurrectionButterfly','FantasySeal','MasterSpark','HourOfDaybreak']:
    path=ROOT/'Assets/Audio/SFX/Gifts'/name/'gift-cast.wav'
    shutil.copyfile(SOURCE/'Charge'/(name+'.wav'),path)
    reports.append(report(path))
# Six short mechanical ticks, alternating timbre, completed before the stop impact.
x=canvas(1.98)
for i,at in enumerate([0,.09,.25,.53,1.01,1.84]):
    d=.08;pitch=1800 if i%2==0 else 1350
    click=(tone(d,pitch)*.35+tone(d,pitch*2.71)*.18+tone(d,pitch*4.13)*.08)*env(d,.0005,150)
    click+=band(d,1400,9000)*env(d,.0003,750)*.05
    add(x,click,at,1,-.2 if i%2==0 else .2)
save('TheWorld','cast',x,.65)
# Keep the approved strong stop attack, extend the spatial resonance after it.
x=canvas(2.16);original=read(SOURCE/'Charge/time-stop-impact.wav');add(x,original)
d=1.48;u=t(d)
tail=(tone(d,205,46)*.24+tone(d,309,71)*.11)*np.sin(np.pi*u/d)**.7*np.exp(-u*1.7)
add(x,tail,.57,1,-.6);add(x,tail,.67,.65,.6)
save('TheWorld','stop',x,.94,echo=.035)
x=canvas(.78)
for i in range(14):add(x,swish(.14,1+(i%3)*.04),.05+i*.028,.22,-.7+i*1.4/13)
save('TheWorld','release',x,.82)
# Master Spark: coherent electrical resonance and a wide pitch sweep; no noise generator in the beam.
x=canvas(2.86);d=2.65;u=t(d)
phase=2*np.pi*(72*u+18*(1-np.exp(-u*9)))
# Low-mid harmonics keep the sustained hum audible on small speakers.
carrier=tone(d,110)*.48+tone(d,220)*.29+tone(d,330)*.16
carrier+=np.sin(phase+.8*np.sin(2*np.pi*143*u))*.18
carrier*=np.minimum(1,u/.04)*np.minimum(1,(d-u)/.27)*(0.86+0.14*np.cos(2*np.pi*5.5*u))
add(x,carrier,0,1,-.12);add(x,carrier,.009,.3,.45)
blast=(tone(.30,2100,95)*.20+tone(.30,260,55)*.28)*env(.30,.002,11)
add(x,blast,0,1)
add(x,tone(.55,940,310)*env(.55,.008,8),.04,.07)
save('MasterSpark','release',x,.94,echo=.025)
# Quiet crystal wing glints: inharmonic, airy chimes rather than air noise.
x=canvas(3.68)
for i in range(15):
    d=.42;u=t(d);f=[1568,2093,1760,2349,1976][i%5]
    chime=(tone(d,f)*np.exp(-u*11)+tone(d,f*2.756)*.18*np.exp(-u*20)+tone(d,f*4.11)*.055*np.exp(-u*28))
    chime*=np.minimum(1,u/.006)*np.minimum(1,(d-u)/.06)
    add(x,chime,i*.22,.14,(-1)**i*.55)
save('ResurrectionButterfly','release',x,.24,echo=.18)
# Six simultaneous orbiting spirit orbs, followed by one localized white burst.
x=canvas(1.06)
for i in range(6):
    d=.94;u=t(d)
    flight=tone(d,620+i*21,1250+i*17)*np.sin(np.pi*u/d)**2*.09
    add(x,flight,0,.22,-.7+i*.28)
add(x,swish(.27,.7),0,.23)
save('FantasySeal','release',x,.48,echo=.035)
# Three paired collisions, each followed by a short recoil and renewed attraction.
x=canvas(1.72)
for i,at in enumerate([0,.47,.97]):
    d=.68;u=t(d)
    burst=band(d,300,5300)*env(d,.006,12)*.16+tone(d,280+i*35,60)*env(d,.004,13)*.085
    add(x,burst,at,.72+i*.14,(-1)**i*.15)
save('FantasySeal','impact',x,.72,echo=.045)
# Three already-summoned rings release in quick succession, without notes announcing each phase.
x=canvas(1.4)
for i in range(4):
    add(x,swish(.31,1.1),i*.13,.18,(-1)**i*.4)
    d=.48;u=t(d);glide=tone(d,1500,750)*np.sin(np.pi*u/d)**2*.018
    add(x,glide,i*.13,.4,(-1)**i*.6)
save('HourOfDaybreak','release',x,.44,echo=.07)
# Yuyuko: three tiny electrical zizz pulses, then a sharp descending jiu.
x=canvas(.48)
for i,at in enumerate([0,.052,.104]):
    d=.048;u=t(d)
    phase=2*np.pi*(2450+i*170)*u
    zizz=np.sin(phase+1.25*np.sin(2*np.pi*920*u))
    zizz*=np.sin(np.pi*u/d)**.75
    add(x,zizz,at,.20,(-1)**i*.24)
d=.22;u=t(d)
phase=2*np.pi*(3900*u+(780-3900)*u*u/(2*d))
jiu=(np.sin(phase)+.12*np.sin(phase*2.01))*np.minimum(1,u/.003)*np.exp(-u*13)*np.minimum(1,(d-u)/.03)
add(x,jiu,.15,.85,-.12);add(x,jiu,.163,.16,.35)
save('ResurrectionButterfly','laser',x,.48,echo=.065)
(ROOT/'Logs/spell-audio-validation.json').write_text(json.dumps(reports,indent=2),encoding='utf-8')
print('Built',len(reports),'active cues; four approved charge recordings copied unchanged.')
