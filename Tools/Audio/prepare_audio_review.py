"""Rebuild Poker's audition SFX from the archived CC0 pack and synthesized tones.

Requires Python 3, numpy, and an FFmpeg executable supplied with --ffmpeg.
No network calls; music files remain unchanged from their original downloads.
"""

import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import wave
import zipfile

import numpy as np


ROOT = Path(__file__).resolve().parents[2]
RATE = 44100
KENNEY_URL = "https://kenney.nl/assets/casino-audio"
LICENSE_URL = "https://creativecommons.org/publicdomain/zero/1.0/"


def fade(samples, attack=0.003, release=0.015):
    samples = samples.copy()
    a = min(len(samples), round(attack * RATE))
    r = min(len(samples), round(release * RATE))
    samples[:a] *= np.linspace(0, 1, a)
    samples[-r:] *= np.linspace(1, 0, r)
    return samples


def normalize(samples, peak_db=-7):
    peak = np.max(np.abs(samples))
    return samples * (10 ** (peak_db / 20) / max(peak, 1e-9))


def mix(duration, events):
    result = np.zeros(round(duration * RATE))
    for start, samples, gain in events:
        offset = round(start * RATE)
        size = min(len(samples), len(result) - offset)
        if size > 0:
            result[offset:offset + size] += samples[:size] * gain
    return result


def bell(freq, duration, bright=0.16):
    t = np.arange(round(duration * RATE)) / RATE
    signal = (np.sin(2 * np.pi * freq * t) * np.exp(-5 * t / duration)
              + bright * np.sin(2 * np.pi * freq * 2 * t) * np.exp(-12 * t / duration)
              + 0.035 * np.sin(2 * np.pi * freq * 3 * t) * np.exp(-18 * t / duration))
    return fade(signal, 0.007, 0.035)


def knock(seed):
    # Synthesized muted tabletop knock; no third-party recording.
    rng = np.random.default_rng(seed)
    t = np.arange(round(0.12 * RATE)) / RATE
    texture = np.convolve(rng.normal(size=len(t)), np.ones(15) / 15, mode="same")
    signal = (np.sin(2 * np.pi * 185 * t) * np.exp(-65 * t)
              + 0.4 * np.sin(2 * np.pi * 390 * t) * np.exp(-90 * t)
              + 0.9 * texture * np.exp(-120 * t))
    return fade(signal, 0.001, 0.02)


def write_wav(path, samples):
    path.parent.mkdir(parents=True, exist_ok=True)
    pcm = np.rint(np.clip(samples, -1, 1) * 32767).astype("<i2")
    with wave.open(str(path), "wb") as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(RATE)
        output.writeframes(pcm.tobytes())


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--ffmpeg", required=True)
    args = parser.parse_args()
    archive_path = ROOT / "AudioSources/Kenney/kenney_casino-audio.zip"
    archive = zipfile.ZipFile(archive_path)

    def sample(name):
        raw = archive.read("Audio/" + name + ".ogg")
        decoded = subprocess.run(
            [args.ffmpeg, "-v", "error", "-i", "pipe:0", "-f", "f32le",
             "-ac", "1", "-ar", str(RATE), "pipe:1"],
            input=raw, capture_output=True, check=True)
        signal = np.frombuffer(decoded.stdout, dtype="<f4").astype(float)
        active = np.flatnonzero(np.abs(signal) > max(np.max(np.abs(signal)) * 0.008, 1e-6))
        if len(active):
            signal = signal[max(0, active[0] - 100):min(len(signal), active[-1] + 450)]
        return fade(normalize(signal, -4))

    turn = mix(0.62, [(0, bell(659.255, 0.36), 0.55),
                      (0.19, bell(987.767, 0.43), 0.43)])
    check = mix(0.35, [(0, knock(19), 0.7), (0.15, knock(23), 0.6)])
    bet = mix(0.46, [(0, sample("chips-stack-3"), 0.9),
                     (0.10, sample("chip-lay-2"), 0.4)])
    call = mix(0.42, [(0, sample("chips-handle-3"), 0.55),
                      (0.15, sample("chip-lay-1"), 0.6)])
    raise_sound = mix(0.62, [(0, sample("chips-handle-4"), 0.6),
                             (0.12, sample("chips-stack-3"), 0.8),
                             (0.32, sample("chips-collide-1"), 0.4)])
    t = np.arange(round(0.48 * RATE)) / RATE
    accent = fade(np.sin(2 * np.pi * (105 * t - 35 * t * t)) * np.exp(-9 * t), 0.008, 0.04)
    allin = mix(0.95, [(0, sample("chips-handle-5"), 0.65),
                       (0.12, sample("chips-stack-3"), 0.7),
                       (0.27, sample("chips-collide-3"), 0.6),
                       (0.25, accent, 0.15)])
    fold = mix(0.55, [(0, sample("card-slide-4"), 0.7),
                      (0.07, sample("card-slide-2")[:round(0.45 * RATE)], 0.35)])
    win = mix(1.65, [(0, bell(523.251, 0.6), 0.45),
                     (0.16, bell(659.255, 0.6), 0.43),
                     (0.32, bell(783.991, 0.65), 0.4),
                     (0.5, bell(1046.502, 1.1), 0.38),
                     (0.72, sample("chips-handle-1"), 0.18)])
    lose = mix(1.05, [(0, bell(493.883, 0.55, 0.07), 0.4),
                      (0.22, bell(391.995, 0.55, 0.07), 0.38),
                      (0.43, bell(329.628, 0.62, 0.07), 0.34)])
    specs = [
        ("01", "YourTurn", "轮到自己行动", "柔和双音提示", turn, [], -9),
        ("02", "Check", "过牌", "两下轻敲桌面（合成）", check, [], -10),
        ("03", "Bet", "下注", "小叠筹码落桌", bet, ["chips-stack-3", "chip-lay-2"], -8),
        ("04", "Call", "跟注", "轻量筹码移动", call, ["chips-handle-3", "chip-lay-1"], -9),
        ("05", "Raise", "加注", "较密集的筹码叠放", raise_sound, ["chips-handle-4", "chips-stack-3", "chips-collide-1"], -7),
        ("06", "AllIn", "全下", "筹码推进与低音重音", allin, ["chips-handle-5", "chips-stack-3", "chips-collide-3"], -6),
        ("07", "Fold", "弃牌", "两张牌滑过桌面", fold, ["card-slide-4", "card-slide-2"], -10),
        ("08", "Win", "胜利", "上行音符与收筹码声", win, ["chips-handle-1"], -8),
        ("09", "Lose", "失败", "柔和下行音符", lose, [], -11),
    ]
    entries = []
    for number, event, title, note, signal, materials, peak_db in specs:
        signal = normalize(fade(signal), peak_db)
        relative = f"Assets/Audio/SFX/sfx-{''.join('-' + c.lower() if c.isupper() and i else c.lower() for i, c in enumerate(event))}.wav"
        path = ROOT / relative
        write_wav(path, signal)
        entry = dict(number=number, event=event, title=title, description=note,
                     path=relative, duration_seconds=round(len(signal) / RATE, 3),
                     sample_rate=RATE, channels=1, format="WAV PCM16", loop_ready=False,
                     peak_dbfs=round(20 * np.log10(np.max(np.abs(signal))), 2),
                     rms_dbfs=round(20 * np.log10(np.sqrt(np.mean(signal ** 2))), 2),
                     sha256=hashlib.sha256(path.read_bytes()).hexdigest(),
                     origin="Kenney CC0 samples + local processing" if materials else "Locally synthesized; no external samples",
                     source_files=["Audio/" + name + ".ogg" for name in materials],
                     source_url=KENNEY_URL if materials else None,
                     license="CC0-1.0" if materials else "Project-generated audio; no external samples")
        entries.append(entry)
    target = ROOT / "AudioSources/sfx_manifest.json"
    target.write_text(json.dumps(dict(version=1, clips=entries), ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    for e in entries:
        print(e["number"], e["path"], e["duration_seconds"])


if __name__ == "__main__":
    main()
