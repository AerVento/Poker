"""Build Swiss-bank-note gift audio from the user's recording and original paper foley.
Requires numpy and FFmpeg; preserves the archived source recording.
"""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import wave

import numpy as np

ROOT = Path(__file__).resolve().parents[2]
RATE = 44100
SOURCE = ROOT / 'AudioSources/Gifts/SwissBankNote/UserRecording.m4a'
OUT = ROOT / 'Assets/Audio/SFX/Gifts'


def save(name, samples, peak_db, attack=0.002, release=0.025):
    samples = np.asarray(samples, dtype=float).copy()
    samples -= np.mean(samples)
    a, r = round(attack * RATE), round(release * RATE)
    samples[:a] *= np.linspace(0, 1, a)
    samples[-r:] *= np.linspace(1, 0, r)
    samples *= 10 ** (peak_db / 20) / max(1e-9, np.max(np.abs(samples)))
    pcm = np.rint(samples * 32767).astype('<i2')
    path = OUT / (name + '.wav')
    with wave.open(str(path), 'wb') as wav:
        wav.setnchannels(1)
        wav.setsampwidth(2)
        wav.setframerate(RATE)
        wav.writeframes(pcm.tobytes())
    return {'file': path.relative_to(ROOT).as_posix(), 'seconds': len(pcm) / RATE,
            'peak_dbfs': peak_db, 'rms_dbfs': float(20 * np.log10(np.sqrt(np.mean(samples ** 2)))),
            'sha256': hashlib.sha256(path.read_bytes()).hexdigest()}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--ffmpeg', required=True)
    args = parser.parse_args()
    OUT.mkdir(parents=True, exist_ok=True)
    # The opening/ending of the phone recording are quiet room noise. This middle
    # phrase has sustained musical energy and avoids both recording transients.
    start, duration = 2.55, 2.4
    filters = ('highpass=f=95,lowpass=f=7500,afftdn=nr=8:nf=-50:tn=1,'
               'equalizer=f=280:t=q:w=0.8:g=-2,equalizer=f=2500:t=q:w=0.8:g=1.5,'
               'acompressor=threshold=0.025:ratio=3:attack=2:release=90')
    result = subprocess.run([args.ffmpeg, '-v', 'error', '-i', str(SOURCE),
                             '-ss', str(start), '-t', str(duration), '-af', filters,
                             '-ac', '1', '-ar', str(RATE), '-f', 'f32le', 'pipe:1'],
                            check=True, capture_output=True)
    music = np.frombuffer(result.stdout, dtype='<f4').astype(float)
    clips = [save('gift-bank-note-reveal', music, -9, attack=0.045, release=0.24)]

    rng = np.random.default_rng(7041)

    def band_noise(seconds, low, high):
        count = round(seconds * RATE)
        raw = rng.normal(0, 1, count)
        spectrum = np.fft.rfft(raw)
        frequencies = np.fft.rfftfreq(count, 1 / RATE)
        # Smooth spectral rolloff keeps the dry paper transient free of hard ringing.
        mask = (1 - np.exp(-(frequencies / low) ** 2)) * np.exp(-(frequencies / high) ** 4)
        return np.fft.irfft(spectrum * mask, n=count)

    t = np.arange(round(0.34 * RATE)) / RATE
    papery = band_noise(0.34, 450, 8200)
    body = band_noise(0.34, 200, 1800)
    impact = papery * np.exp(-105 * t) * 0.8 + body * np.exp(-70 * t) * 0.5
    for at, strength in [(0.016, 0.45), (0.052, 0.2), (0.096, 0.09)]:
        age = np.maximum(0, t - at)
        impact += papery * strength * (t >= at) * np.exp(-100 * age)
    clips.append(save('gift-bank-note-impact', impact, -9, attack=0.0005))

    t = np.arange(round(0.25 * RATE)) / RATE
    flutter = band_noise(0.25, 700, 6200) * np.sin(np.pi * t / 0.25) ** 1.6
    flutter *= 0.55 + 0.45 * np.sin(t * 70) ** 2
    clips.append(save('gift-bank-note-throw', flutter, -16, attack=0.012))
    manifest = {'recipe_revision': 1, 'source': SOURCE.relative_to(ROOT).as_posix(),
                'source_sha256': hashlib.sha256(SOURCE.read_bytes()).hexdigest(),
                'source_description': 'Music recorded on a phone and supplied by the user; original musical authorship unspecified.',
                'selected_start_seconds': start, 'selected_end_seconds': round(start + duration, 2),
                'selection_reason': 'Stable, stronger middle phrase; excludes quiet room noise at both ends.',
                'filters': filters, 'reveal_fades_seconds': [0.045, 0.24], 'sample_rate': RATE,
                'foley_origin': 'Original local synthesis, no external samples', 'clips': clips}
    (SOURCE.parent / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(manifest, indent=2))


if __name__ == '__main__':
    main()
