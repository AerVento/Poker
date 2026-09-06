"""Select energetic whole-bar phrases and build click-free, circular music edits.

Original recordings remain in AudioSources/Incompetech. Requires numpy and
FFmpeg (--ffmpeg). Output edits retain the original CC BY 4.0 attribution.
"""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

import numpy as np

ROOT = Path(__file__).resolve().parents[2]
RATE = 44100


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--ffmpeg", required=True)
    ffmpeg = parser.parse_args().ffmpeg
    reports = []
    for event, title, bpm, bars in [("Lobby", "Lobby Time", 128, 32),
                                    ("Game", "Covert Affair", 68, 16),
                                    ("AllIn", "Crypto", 80, 16)]:
        source = ROOT / "AudioSources/Incompetech" / (title + ".mp3")
        decoded = subprocess.run([ffmpeg, "-v", "error", "-xerror", "-i", str(source),
                                  "-f", "f32le", "-ar", str(RATE), "-ac", "2", "pipe:1"],
                                 capture_output=True, check=True)
        x = np.frombuffer(decoded.stdout, dtype="<f4").reshape(-1, 2).astype(np.float64)
        mono = x.mean(axis=1)
        # Refine the published tempo against the recording's onset envelope.
        hop = 441
        size = len(mono) // hop
        energy = np.sqrt(np.mean(mono[:size * hop].reshape(size, hop) ** 2, axis=1))
        onset = np.maximum(energy - np.roll(energy, 1), 0)
        lo = 20.0
        hi = len(x) / RATE - 15
        grid = np.arange(size) * hop / RATE
        candidates = []
        for tempo in np.linspace(bpm * 0.995, bpm * 1.005, 61):
            beat = 60 / tempo
            for phase in np.linspace(0, beat, 80, endpoint=False):
                beats = np.arange(lo + phase, hi, beat)
                score = np.interp(beats, grid, onset).mean()
                candidates.append((score, tempo, phase))
        _, tempo, phase = max(candidates)
        beat = 60 / tempo
        duration = bars * 4 * beat
        # Listen-oriented edit selection: sustained intensity with similar
        # beginning/ending timbre; avoid the introduction and closing cadence.
        choices = []
        for start in np.arange(lo + phase, hi - duration, beat):
            a, b = round(start * RATE), round((start + duration) * RATE)
            phrase = x[a:b]
            rms = np.sqrt(np.mean(phrase ** 2))
            n = round(0.10 * RATE)
            before = x[a-n:a]
            ending = x[b-n:b]
            def spectrum(v):
                w = np.hanning(len(v))
                spec = np.abs(np.fft.rfft(v.mean(axis=1) * w))
                spec = np.log1p(spec)
                return spec / max(np.linalg.norm(spec), 1e-9)
            similarity = float(np.dot(spectrum(before), spectrum(ending)))
            # Prefer stronger downbeats over weak subdivisions.
            downbeat = float(np.interp(start, grid, onset))
            score = np.log(max(rms, 1e-9)) + 0.6 * similarity + 0.8 * downbeat
            choices.append((score, start, a, b))
        _, start, a, b = max(choices)
        loop = x[a:b].copy()
        # Mix only the pre-downbeat tail. The musical length and first beat
        # remain intact, and the endpoint approaches the sample before start.
        overlap = round(min(0.075, beat / 8) * RATE)
        weight = (0.5 - 0.5 * np.cos(np.linspace(0, np.pi, overlap)))[:, None]
        loop[-overlap:] = loop[-overlap:] * (1 - weight) + x[a-overlap:a] * weight
        # Match backing-track levels while reserving transient headroom.
        rms = np.sqrt(np.mean(loop ** 2))
        gain = min(10 ** (-22 / 20) / rms, 10 ** (-3 / 20) / np.max(np.abs(loop)))
        loop *= gain
        output = ROOT / f"Assets/Audio/Music/BGM_{event}_Loop_v1.ogg"
        output.parent.mkdir(parents=True, exist_ok=True)
        subprocess.run([ffmpeg, "-v", "error", "-y", "-f", "f32le", "-ar", str(RATE), "-ac", "2",
                        "-i", "pipe:0", "-c:a", "libvorbis", "-q:a", "6", str(output)],
                       input=loop.astype("<f4").tobytes(), capture_output=True, check=True)
        check = subprocess.run([ffmpeg, "-v", "error", "-xerror", "-i", str(output),
                                "-f", "f32le", "-ar", str(RATE), "-ac", "2", "pipe:1"],
                               capture_output=True, check=True)
        result = np.frombuffer(check.stdout, dtype="<f4").reshape(-1, 2)
        assert abs(len(result) - len(loop)) < 1024 and np.max(np.abs(result)) < 1
        report = dict(event=event, original_title=title, author="Kevin MacLeod",
                      original_file=source.relative_to(ROOT).as_posix(),
                      output_file=output.relative_to(ROOT).as_posix(), license="CC-BY-4.0",
                      source_start_seconds=round(start, 4), source_end_seconds=round(start + duration, 4),
                      estimated_bpm=round(tempo, 4), bars=bars, duration_seconds=round(len(result) / RATE, 4),
                      tail_blend_seconds=overlap/RATE, applied_gain_db=round(20*np.log10(gain), 3),
                      peak_dbfs=round(float(20*np.log10(np.max(np.abs(result)))), 3),
                      boundary_step=round(float(np.max(np.abs(result[0]-result[-1]))), 7),
                      sha256=hashlib.sha256(output.read_bytes()).hexdigest())
        reports.append(report)
        print(json.dumps(report))
    (ROOT / "AudioSources/Incompetech/loop_edits.json").write_text(
        json.dumps(reports, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
