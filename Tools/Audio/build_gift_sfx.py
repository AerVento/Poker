"""Deterministic original gift foley. Uses Python's standard library, no samples."""
import json
import math
import random
import wave
from array import array
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
RATE = 44100
OUT = ROOT / 'Assets/Audio/SFX/Gifts'
OUT.mkdir(parents=True, exist_ok=True)
rng = random.Random(9362)


def noise(count, smooth=0.0):
    previous = 0.0
    values = []
    for _ in range(count):
        previous = smooth * previous + (1 - smooth) * rng.uniform(-1, 1)
        values.append(previous)
    return values


def add_tone(samples, at, duration, frequency, gain, decay, chirp=0):
    start = int(at * RATE)
    for i in range(min(int(duration * RATE), len(samples) - start)):
        t = i / RATE
        env = min(1, t / 0.002) * math.exp(-decay * t)
        samples[start + i] += gain * env * math.sin(2 * math.pi * (frequency * t + chirp * t * t / 2))


def band_noise(count, low, high):
    """Broadband air/shell texture, with the low thud removed."""
    raw = noise(count)
    low_state = high_state = 0.0
    low_alpha = 1 - math.exp(-2 * math.pi * low / RATE)
    high_alpha = 1 - math.exp(-2 * math.pi * high / RATE)
    result = []
    for sample in raw:
        high_state += high_alpha * (sample - high_state)
        low_state += low_alpha * (sample - low_state)
        result.append(high_state - low_state)
    return result


def save(name, data, peak_db, attack=0.01):
    # Short fades remove waveform discontinuities at both ends.
    attack_frames = max(1, round(attack * RATE))
    for i in range(min(attack_frames, len(data))):
        data[i] *= i / attack_frames
    for i in range(min(441, len(data))):
        data[-1 - i] *= i / 441
    gain = 10 ** (peak_db / 20) / max(0.00001, max(abs(x) for x in data))
    pcm = array('h', (round(max(-1, min(1, x * gain)) * 32767) for x in data))
    path = OUT / (name + '.wav')
    with wave.open(str(path), 'wb') as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(RATE)
        output.writeframes(pcm.tobytes())
    return dict(file=path.relative_to(ROOT).as_posix(), seconds=len(data)/RATE,
                peak_dbfs=peak_db, origin='Original local synthesis; no external samples')


manifest = []
length = int(RATE * 0.38)
raw = noise(length)
whoosh = []
phase = 0.0
x1 = x2 = y1 = y2 = 0.0
for i, value in enumerate(raw):
    t = i / RATE
    u = t / 0.38
    # A breathy falling whistle: "shiu", rather than a low gust or impact.
    frequency = 2400 * (480 / 2400) ** u
    phase += 2 * math.pi * frequency / RATE
    omega = 2 * math.pi * frequency * 1.35 / RATE
    alpha = math.sin(omega) / (2 * 1.15)
    a0 = 1 + alpha
    filtered = (alpha * (value - x2) + 2 * math.cos(omega) * y1 - (1 - alpha) * y2) / a0
    x2, x1, y2, y1 = x1, value, y1, filtered
    envelope = min(1, t / 0.04) * (1 - u) ** 1.5
    whistle = math.sin(phase) + 0.08 * math.sin(2 * phase)
    whoosh.append(envelope * (filtered * 0.78 + whistle * 0.10))
manifest.append(save('gift-egg-throw', whoosh, -10))

length = int(RATE * 0.40)
shell = band_noise(length, 1700, 11000)
wet = band_noise(length, 650, 3400)
impact = [0.0] * length
# A fast brittle crack followed by several tiny shell fractures.
for at, strength, decay in [(0.002, 1.0, 230), (0.013, 0.72, 190),
                             (0.030, 0.48, 160), (0.052, 0.31, 145),
                             (0.088, 0.15, 120), (0.124, 0.07, 100)]:
    start = round(at * RATE)
    for j in range(min(round(RATE * 0.055), length - start)):
        t = j / RATE
        envelope = min(1, t / 0.0005) * math.exp(-decay * t)
        impact[start + j] += shell[start + j] * strength * envelope
for i in range(length):
    t = max(0, i / RATE - 0.014)
    envelope = min(1, t / 0.009) * math.exp(-30 * t)
    impact[i] += wet[i] * 0.38 * envelope * (0.7 + 0.3 * math.sin(t * 165) ** 2)
manifest.append(save('gift-egg-splat', impact, -8, attack=0.0005))

pour_duration = 1.0
length = int(RATE * pour_duration)
water = noise(length, 0.70)
fizz = noise(length, 0.10)
pour = []
for i in range(length):
    t = i / RATE
    env = min(1, t / 0.08) * min(1, (pour_duration - t) / 0.10)
    pour.append(env * (water[i] * 0.21 + fizz[i] * 0.016) * (0.85 + 0.15 * math.sin(t * 29)))
for i in range(29):
    at = 0.06 + i * 0.029 + rng.uniform(0, 0.014)
    add_tone(pour, at, 0.05, 700 + at * 330 + rng.uniform(-200, 500), rng.uniform(0.01, 0.035), 70, 1500)
manifest.append(save('gift-champagne-pour', pour, -15))

clink = [0.0] * int(RATE * 1.05)
for at, strength in [(0.0, 1.0), (0.018, 0.45)]:
    for frequency, gain, decay in [(2630, 0.40, 5.8), (3971, 0.20, 8), (5813, 0.08, 13), (7421, 0.025, 21)]:
        add_tone(clink, at, 1.0, frequency, gain * strength, decay)
manifest.append(save('gift-champagne-clink', clink, -12))

# Bright metal-on-metal pull and lock, aligned with the existing slide animation.
length = int(RATE * 0.44)
metal = band_noise(length, 2600, 14500)
rack = [0.0] * length
for at, gain, decay in [(0.0, 0.85, 190), (0.026, 0.24, 240),
                         (0.29, 1.0, 220), (0.308, 0.35, 270)]:
    start = round(at * RATE)
    for j in range(length - start):
        t = j / RATE
        rack[start + j] += metal[start + j] * gain * min(1, t / 0.0003) * math.exp(-decay * t)
    for frequency, strength, damping in [(2870, 0.12, 125), (4630, 0.08, 170), (7210, 0.045, 230)]:
        add_tone(rack, at, 0.08, frequency, gain * strength, damping)
for i in range(length):
    t = i / RATE
    if 0.018 < t < 0.18:
        rack[i] += metal[i] * 0.055 * math.sin(math.pi * (t - 0.018) / 0.162) ** 2
manifest.append(save('gift-gun-rack', rack, -9, attack=0.0003))

# Cinematic large-calibre report: short attack, dense low-mid blast and a deep tail.
# The tail overlaps the next shot lightly, while the three attacks stay distinct.
length = int(RATE * 0.55)
crack = band_noise(length, 800, 6500)
body = band_noise(length, 65, 850)
rumble = band_noise(length, 45, 260)
shot = []
for i in range(length):
    t = i / RATE
    attack = min(1, t / 0.0007)
    blast = (crack[i] * 0.38 * math.exp(-115 * t)
             + body[i] * 3.2 * math.exp(-20 * t)
             + rumble[i] * 1.4 * math.exp(-11 * t))
    shot.append(attack * blast)
add_tone(shot, 0.002, 0.32, 115, 0.20, 21, -150)
add_tone(shot, 0.003, 0.23, 205, 0.07, 32, -220)
# Gentle saturation thickens the blast before peak normalization, without clipping PCM.
shot = [math.tanh(sample * 1.7) for sample in shot]
manifest.append(save('gift-gunshot', shot, -5.5, attack=0.0005))
source = ROOT / 'AudioSources/Gifts'
source.mkdir(parents=True, exist_ok=True)
(source / 'manifest.json').write_text(json.dumps(dict(recipe_revision=4, sample_rate=RATE, seed=9362, clips=manifest), indent=2) + '\n', encoding='utf-8')
print(json.dumps(manifest, indent=2))
