"""Builds Feared's player sound effects into Assets/_MyFiles/Audio/SFX.

Sources are two CC0 foley libraries from OpenGameArt, unpacked under Assets/_ThirdParty:
  AudioTinysized  https://opengameart.org/content/fantasy-sound-effects-tinysized-sfx
  AudioWeapons    https://opengameart.org/content/fantasy-weapons-and-apparel-sfx-library
Neither has a usable blade swing, so swings and body thumps are synthesized here. Standard library only.

Run from the project root: python Tools/make_sfx.py
"""
import math
import os
import random
import struct
import wave

RATE = 44100
ROOT = os.path.join(os.path.dirname(__file__), "..", "Assets")
SRC = os.path.join(ROOT, "_ThirdParty")
OUT = os.path.join(ROOT, "_MyFiles", "Audio", "SFX")


def load(name):
    """Mono float samples of a 16-bit wav from one of the source libraries."""
    for folder in ("AudioTinysized", "AudioWeapons"):
        path = os.path.join(SRC, folder, name + ".wav")
        if os.path.exists(path):
            with wave.open(path) as w:
                channels = w.getnchannels()
                raw = w.readframes(w.getnframes())
            data = struct.unpack("<%dh" % (len(raw) // 2), raw)
            return [sum(data[i:i + channels]) / (channels * 32768.0) for i in range(0, len(data), channels)]
    raise FileNotFoundError(name)


def trim(samples, threshold=0.2):
    """Drops everything before the first transient (threshold is a fraction of the peak) so the sound lands on
    the frame it is played."""
    level = threshold * max(abs(s) for s in samples)
    for i, s in enumerate(samples):
        if abs(s) > level:
            return samples[max(0, i - 40):]
    return samples


def pitch(samples, ratio):
    """Resamples by ratio (< 1 is lower and longer)."""
    count = int(len(samples) / ratio)
    out = []
    for i in range(count):
        p = i * ratio
        j = int(p)
        f = p - j
        a = samples[j] if j < len(samples) else 0.0
        b = samples[j + 1] if j + 1 < len(samples) else 0.0
        out.append(a + (b - a) * f)
    return out


def fit(samples, seconds, fade=0.05):
    """Cuts to length with a short fade out."""
    samples = samples[:int(seconds * RATE)]
    n = min(len(samples), int(fade * RATE))
    for i in range(n):
        samples[len(samples) - n + i] *= 1.0 - i / n
    return samples


def mix(*layers):
    length = max(len(layer) for layer, _ in layers)
    out = [0.0] * length
    for layer, gain in layers:
        for i, s in enumerate(layer):
            out[i] += s * gain
    return out


def bandpass_sweep(noise, centers, q):
    """Resonant band-pass (RBJ biquad) whose centre frequency follows `centers` sample by sample."""
    x1 = x2 = y1 = y2 = 0.0
    out = []
    for x, fc in zip(noise, centers):
        w = 2 * math.pi * fc / RATE
        alpha = math.sin(w) / (2 * q)
        a0 = 1 + alpha
        b0, b2 = alpha / a0, -alpha / a0
        a1, a2 = -2 * math.cos(w) / a0, (1 - alpha) / a0
        y = b0 * x + b2 * x2 - a1 * y1 - a2 * y2
        x2, x1, y2, y1 = x1, x, y1, y
        out.append(y)
    return out


def whoosh(seconds, low, high, peak_at, q, rng):
    """Blade swing: filtered noise that rises to a peak and falls away, pitch following loudness."""
    n = int(seconds * RATE)
    noise = [rng.uniform(-1, 1) for _ in range(n)]
    env, centers = [], []
    for i in range(n):
        t = i / n
        e = (t / peak_at) ** 2 if t < peak_at else math.exp(-5.0 * (t - peak_at) / (1 - peak_at))
        env.append(e)
        centers.append(low + (high - low) * e)
    body = bandpass_sweep(noise, centers, q)
    return [s * e for s, e in zip(body, env)]


def thump(seconds, start_hz, end_hz, rng):
    """Low body impact: a falling sine with a fast exponential decay and a click of noise on top."""
    out, phase = [], 0.0
    n = int(seconds * RATE)
    for i in range(n):
        t = i / n
        hz = start_hz + (end_hz - start_hz) * t
        phase += 2 * math.pi * hz / RATE
        click = rng.uniform(-1, 1) * math.exp(-60 * t)
        out.append((math.sin(phase) + 0.25 * click) * math.exp(-7 * t))
    return out


def normalize(samples, peak_db):
    peak = max(abs(s) for s in samples) or 1.0
    gain = 10 ** (peak_db / 20) / peak
    return [s * gain for s in samples]


def save(name, samples, peak_db=-3.0):
    os.makedirs(OUT, exist_ok=True)
    samples = normalize(samples, peak_db)
    with wave.open(os.path.join(OUT, name + ".wav"), "w") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, s)) * 32767)) for s in samples))
    print("wrote", name)


def main():
    rng = random.Random(13)

    for i in range(4):
        swing = whoosh(0.26 + 0.03 * i, 250, 1500 + 150 * i, 0.3, 2.2, rng)
        air = whoosh(0.26 + 0.03 * i, 90, 320, 0.32, 0.9, rng)
        save("swing_%02d" % (i + 1), mix((swing, 1.0), (air, 0.6)), -6.0)

    for i in range(2):
        rush = whoosh(0.22, 120, 650, 0.25, 1.2, rng)
        step = fit(trim(load("boots-leather-step-%02d" % (i + 1))), 0.25)
        save("dash_%02d" % (i + 1), mix((rush, 1.0), (step, 0.35)), -8.0)

    for i, name in enumerate(("handcuffs-metal-open-01", "handcuffs-metal-lock-02")):
        save("chain_throw_%02d" % (i + 1), fit(pitch(trim(load(name)), 0.8), 0.45), -6.0)

    for i, name in enumerate(("handcuffs-metal-lock-01", "handcuffs-metal-lock-02")):
        latch = fit(pitch(trim(load(name)), 0.7), 0.4)
        save("chain_latch_%02d" % (i + 1), mix((latch, 1.0), (thump(0.15, 110, 60, rng), 0.5)), -5.0)

    for i in range(2):
        scrape = fit(pitch(trim(load("metal-knife-scrape-%02d" % (i + 1))), 0.75), 0.4, 0.15)
        save("chain_zip_%02d" % (i + 1), scrape, -9.0)

    for i in range(3):
        cut = fit(pitch(trim(load("apple-cut-%02d" % (i + 1))), 0.85), 0.3)
        save("hit_flesh_%02d" % (i + 1), mix((cut, 1.0), (thump(0.16, 95, 50, rng), 0.7)), -3.0)

    for i in range(2):
        leather = fit(trim(load("quiver-leather-squeeze-%02d" % (i + 3))), 0.25)
        save("hurt_%02d" % (i + 1), mix((thump(0.24, 75, 38, rng), 1.0), (leather, 0.5)), -3.0)

    for i in range(3):
        save("parry_%02d" % (i + 1), fit(trim(load("sword-clash-%02d" % (i + 1))), 0.9, 0.4), -4.0)


if __name__ == "__main__":
    main()
