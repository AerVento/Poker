"""Original deterministic cigar cutter and lighter foley; no sampled recordings."""
import math
import random
import wave
from array import array
from pathlib import Path

RATE = 44100
OUT = Path(__file__).resolve().parents[2] / 'Assets/Audio/SFX/Gifts'

def texture(count, seed):
    rng = random.Random(seed)
    low = high = 0.0
    result = []
    for _ in range(count):
        value = rng.uniform(-1, 1)
        low += 0.13 * (value - low)
        high += 0.72 * (value - high)
        result.append(high - low)
    return result


def transient(samples, noise, at, duration, gain, decay, modes=()):
    start = round(at * RATE)
    for j in range(min(round(duration * RATE), len(samples) - start)):
        t = j / RATE
        attack = min(1, t / 0.00035)
        release = min(1, (duration - t) / 0.003)
        body = sum(weight * math.sin(2 * math.pi * frequency * t) * math.exp(-damping * t)
                   for frequency, weight, damping in modes)
        samples[start+j] += gain * attack * release * (
            noise[start+j] * math.exp(-decay * t) + body)


def save(name, samples):
    # Dry close-up foley: strong attack, no reverb, no long hiss or tonal tail.
    gain = 0.68 / max(abs(v) for v in samples)
    pcm = array('h', (int(v * gain * 32767) for v in samples))
    with wave.open(str(OUT / name), 'wb') as f:
        f.setparams((1, 2, RATE, 0, 'NONE', 'not compressed'))
        f.writeframes(pcm.tobytes())

OUT.mkdir(parents=True, exist_ok=True)

# One short shearing stroke ends in a clean blade-stop click, without a second clack.
snip = [0.0] * round(0.10 * RATE)
grain = texture(len(snip), 101)
transient(snip, grain, 0.000, 0.015, 0.27, 105)
transient(snip, grain, 0.012, 0.040, 1.0, 245,
          ((1250, 0.19, 260), (3450, 0.16, 340), (5900, 0.09, 420)))
save('gift-cigar-snip.wav', snip)

# A flint wheel's tiny teeth scrape rapidly, followed by its sharp mechanical snap.
ignite = [0.0] * round(0.16 * RATE)
grain = texture(len(ignite), 102)
for tooth in range(9):
    transient(ignite, grain, tooth * 0.0032, 0.005,
              0.15 + tooth * 0.014, 530, ((4600, 0.07, 650),))
transient(ignite, grain, 0.030, 0.044, 1.0, 215,
          ((1850, 0.17, 240), (3700, 0.22, 290), (6300, 0.11, 390)))
transient(ignite, grain, 0.040, 0.025, 0.08, 100)
save('gift-cigar-ignite.wav', ignite)
