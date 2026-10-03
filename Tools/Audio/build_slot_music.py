"""Original festive casino disco loop. All voices are synthesized, with wrapped tails."""
from array import array
import json
import math
from pathlib import Path
import random
import wave

ROOT = Path(__file__).resolve().parents[2]
RATE = 44100
BPM = 132
BEAT = 60 / BPM
BARS = 16
FRAMES = round(BARS * 4 * BEAT * RATE)
left = array('d', [0.0]) * FRAMES
right = array('d', [0.0]) * FRAMES
rng = random.Random(2064)
TAU = 2 * math.pi


def note(at, midi, length, gain, pan=0, voice='keys'):
    hz = 440 * 2 ** ((midi - 69) / 12)
    start = round(at * RATE)
    lg = math.sqrt((1 - pan) / 2) * gain
    rg = math.sqrt((1 + pan) / 2) * gain
    for i in range(round(length * RATE)):
        t = i / RATE
        phase = TAU * hz * t
        release = min(1.0, (length - t) / 0.075)
        if voice == 'bass':
            env = min(1, t / 0.008) * math.exp(-4.8 * t) * release
            value = (math.sin(phase) + 0.3 * math.sin(2 * phase) + 0.1 * math.sin(3 * phase)) * env
        elif voice == 'brass':
            env = min(1, t / 0.018) * min(1, (length - t) / 0.065) * math.exp(-1.8*t)
            phase += 0.025 * math.sin(TAU * 5.5*t)
            value = (math.sin(phase) + .48*math.sin(phase*2) + .24*math.sin(phase*3)
                     + .12*math.sin(phase*4) + .06*math.sin(phase*5)) * env
        elif voice == 'bell':
            env = min(1, t / 0.009) * math.exp(-3.8 * t) * release
            value = (math.sin(phase) + 0.24 * math.sin(phase * 4) * math.exp(-8*t)) * env
        else:
            env = min(1, t / 0.012) * math.exp(-2.4 * t) * release
            # Soft FM tine attack and a gently modulated electric-piano body.
            value = math.sin(phase + 1.1 * math.exp(-5*t) * math.sin(phase * 2))
            value = (value + 0.12 * math.sin(phase * 3)) * env * (0.94 + 0.06 * math.cos(TAU * 4.7 * t))
        index = (start + i) % FRAMES
        left[index] += value * lg
        right[index] += value * rg


def drum(at, kind, gain, pan=0):
    duration = 0.24 if kind == 'kick' else 0.14
    start = round(at * RATE)
    previous = 0.0
    for i in range(round(duration * RATE)):
        t = i / RATE
        env = min(1, t / 0.002) * min(1, (duration - t) / 0.015)
        raw = rng.uniform(-1, 1)
        if kind == 'kick':
            value = math.sin(TAU * (48*t + 1.3*(1-math.exp(-40*t)))) * math.exp(-22*t)
        elif kind == 'clap':
            previous = .35*previous + .65*raw
            hits = sum(math.exp(-90*(t-on)) if t >= on else 0 for on in (0, .013, .027))
            value = previous * hits * .5 + math.sin(TAU*185*t)*math.exp(-35*t)*.22
        else:
            value = (raw - previous) * math.exp(-55*t) * 0.3
            previous = raw
        index = (start + i) % FRAMES
        left[index] += value * gain * env * math.sqrt((1-pan)/2)
        right[index] += value * gain * env * math.sqrt((1+pan)/2)


# Bright C-major party groove: brass calls, answering bells and a driving disco beat.
chords = [[60,64,67,69], [57,60,64,67], [57,60,65,69], [59,62,65,67]]
bass_roots = [36,33,41,43]
motifs = [[76,79,81,79,76,72], [76,79,84,83,81,79],
          [77,81,84,81,79,77], [79,83,86,84,83,79]]
for bar in range(BARS):
    at = bar * 4 * BEAT
    chord = chords[bar % 4]
    for beat, gain in [(0,.105), (.75,.07), (1.5,.085), (2.75,.10), (3.5,.085)]:
        for j,pitch in enumerate(chord):
            note(at+beat*BEAT+j*.004, pitch, .27, gain, (j-1.5)*.22, 'brass')
    root=bass_roots[bar%4]
    for beat,pitch in zip([0,.5,1,1.75,2,2.5,3,3.5],[root,root+12,root+7,root+12,root,root+7,root+12,root+7]):
        note(at+beat*BEAT,pitch,.28,.30,-.08,'bass')
    for j,pitch in enumerate(motifs[bar%4]):
        note(at+[.25,.75,1.5,2,2.75,3.25][j]*BEAT,pitch,.24,.11,.16,'brass' if bar%4<2 else 'bell')
    for j,pitch in enumerate(chord):
        note(at+(3+j*.25)*BEAT,pitch+12,.35,.055,-.28,'bell')
    for beat in range(4):
        drum(at+beat*BEAT,'kick',.34)
        if beat in (1,3): drum(at+beat*BEAT,'clap',.34,-.08)
        drum(at+beat*BEAT,'hat',.10,.35)
        drum(at+(beat+.5)*BEAT,'hat',.17,.35)
        if bar%4==3 and beat==3:
            for part in [.25,.5,.75]: drum(at+(beat+part)*BEAT,'clap',.13,-.15+part*.3)

# Circular stereo room reflections preserve the exact bar boundary, including note tails.
dry_left = array('d', left)
dry_right = array('d', right)
for delay, gain in [(0.071, .12), (.139, .075), (BEAT*.75, .07)]:
    offset = round(delay * RATE)
    for i in range(FRAMES):
        left[i] += dry_right[(i-offset) % FRAMES] * gain
        right[i] += dry_left[(i-offset) % FRAMES] * gain
peak = max(max(abs(v) for v in left), max(abs(v) for v in right))
scale = 0.72 / peak
pcm = array('h')
for l, r in zip(left, right):
    pcm.append(round(l * scale * 32767))
    pcm.append(round(r * scale * 32767))
path = ROOT / 'Assets/Audio/Music/bgm-slot-lounge.wav'
with wave.open(str(path), 'wb') as output:
    output.setnchannels(2)
    output.setsampwidth(2)
    output.setframerate(RATE)
    output.writeframes(pcm.tobytes())
metadata = {
    'file': path.relative_to(ROOT).as_posix(), 'source': 'Original local synthesis; no external samples',
    'style': 'Celebratory brass disco, bright major-key hook, claps and driving bass',
    'bpm': BPM, 'bars': BARS, 'seconds': FRAMES / RATE, 'channels': 2,
    'loop': 'Exact 16-bar circular mix with wrapped note and room tails; no intro or outro fade',
    'peak_dbfs': round(20*math.log10(.72), 2),
    'rms_dbfs': round(20*math.log10(math.sqrt(sum((l*scale)**2 for l in left)/FRAMES)), 2),
    'boundary_jump': max(abs(left[0]-left[-1]),abs(right[0]-right[-1]))*scale
}
(ROOT/'AudioSources/SlotMachine/lounge-music.json').write_text(json.dumps(metadata, indent=2)+'\n', encoding='utf-8')
print(json.dumps(metadata))
