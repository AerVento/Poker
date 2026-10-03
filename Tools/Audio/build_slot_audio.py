"""Original deterministic slot-machine foley and musical cues; no external samples."""
import json
import math
import random
import wave
from array import array
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
RATE = 44100
OUT = ROOT / 'Assets/Audio/SFX/SlotMachine'
OUT.mkdir(parents=True, exist_ok=True)
RNG = random.Random(714)
manifest = []


def tone(data, start, duration, hz, gain=0.25, decay=4, sweep=0):
    offset = int(start * RATE)
    for i in range(min(int(duration * RATE), len(data) - offset)):
        t = i / RATE
        envelope = min(1, t / 0.006) * min(1, (duration - t) / 0.035) * math.exp(-decay * t)
        phase = 2 * math.pi * (hz * t + sweep * t * t / 2)
        data[offset + i] += gain * envelope * (math.sin(phase) + 0.2 * math.sin(phase * 2) + 0.08 * math.sin(phase * 3))


def click(data, at, gain=0.15, duration=0.045):
    offset = int(at * RATE)
    for i in range(min(int(duration * RATE), len(data) - offset)):
        t = i / RATE
        data[offset + i] += RNG.uniform(-1, 1) * gain * math.exp(-110 * t) * min(1, t / 0.001)
    tone(data, at, duration, 210, gain, 65)


def save(name, data, peak=0.65, music=False):
    maximum = max(abs(x) for x in data) or 1
    data = [x * peak / maximum for x in data]
    for i in range(min(220, len(data))):
        data[i] *= i / 220
        data[-1-i] *= i / 220
    path = (ROOT / 'Assets/Audio/Music' if music else OUT) / (name + '.wav')
    with wave.open(str(path), 'wb') as f:
        f.setnchannels(1); f.setsampwidth(2); f.setframerate(RATE)
        f.writeframes(array('h', [round(x * 32767) for x in data]).tobytes())
    manifest.append({'file': path.relative_to(ROOT).as_posix(), 'seconds':len(data)/RATE,
                     'peak_dbfs': round(20 * math.log10(peak), 2), 'source': 'original local synthesis'})


for name, rising in [('sfx-slot-lever-down', False), ('sfx-slot-lever-up', True)]:
    data = [0.] * int(RATE * 0.36)
    tone(data, 0, 0.30, 130 if rising else 330, 0.18, 9, 900 if rising else -850)
    for at in [0.015, 0.065, 0.12, 0.18, 0.27]: click(data, at, 0.08)
    click(data, 0.29, 0.28)
    save(name, data, 0.6)

data = [0.] * int(RATE * 0.2)
click(data, 0.0, 0.25)
tone(data, 0.001, 0.16, 880, 0.10, 25)
save('sfx-slot-reel-stop', data, 0.88)

# Short ceremonial air-cannon thump and fizz, triggered once per pair of cannons.
data = [0.] * int(RATE * .65)
for i in range(len(data)):
    t=i/RATE
    attack=min(1,t/.003)
    data[i] += attack * (math.sin(2*math.pi*(95*t-35*t*t))*.42*math.exp(-15*t)
                        + RNG.uniform(-1,1)*.27*math.exp(-12*t))
tone(data,.04,.5,1760,.055,9)
save('sfx-slot-cannon',data,.58)

for name, notes, interval, tail in [
    ('small', [523.25,659.25,783.99,1046.5],0.12,0.85),
    ('medium',[523.25,659.25,783.99,1046.5,1318.5,1568],0.14,1.15),
    ('grand',[523.25,783.99,1046.5,659.25,987.77,1318.5,783.99,1174.66,1568,2093],0.16,1.7),
    ('lose',[392,349.23,261.63],0.18,0.6)]:
    data=[0.] * int(RATE * (len(notes)*interval+tail))
    for i,hz in enumerate(notes):
        tone(data,i*interval,tail,hz,0.2,3.5)
        if name != 'lose': tone(data,i*interval+0.02,tail,hz*2,0.05,7)
    if name in ('medium','grand'):
        at=(len(notes)-1)*interval
        for hz in [523.25,659.25,783.99]: tone(data,at,tail,hz,0.08,2.5)
    save('sfx-slot-'+name,data,0.48 if name=='lose' else 0.68)

# Four-second rolling arpeggio; mechanical stop clicks are separate event-driven cues.
data=[0.] * (RATE*4)
notes=[523.25,659.25,783.99,1046.5,587.33,783.99,987.77,1174.66]
for i in range(32):
    tone(data,i*0.125,0.12,notes[i%8],0.11,18)
    if i%4==0: tone(data,i*0.125,0.45,130.81 if i<16 else 146.83,0.17,8)
save('bgm-slot-spin',data,0.55,True)
folder=ROOT/'AudioSources/SlotMachine'
folder.mkdir(parents=True,exist_ok=True)
(folder/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
print('Generated',len(manifest),'original cues')
