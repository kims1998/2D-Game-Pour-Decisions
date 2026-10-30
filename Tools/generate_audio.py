"""
Pour Decisions - procedural sound generator.

Synthesizes every sound effect with numpy/scipy
and writes 16-bit mono WAVs into Assets/PourDecisions/Resources/Audio.
Nothing here is sampled from recordings; each sound is built from noise,
oscillators, filters and envelopes.

Run from the repo root:  python3 Tools/generate_audio.py
"""
import os

import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, lfilter, sosfilt

SR = 44100
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets/PourDecisions/Resources/Audio")
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(42)


# ---------------------------------------------------------------- building blocks
def t_axis(dur):
    return np.arange(int(SR * dur)) / SR


def noise(dur):
    return rng.uniform(-1, 1, int(SR * dur))


def bandpass(x, lo, hi, order=2):
    sos = butter(order, [lo, hi], btype="band", fs=SR, output="sos")
    return sosfilt(sos, x)


def lowpass(x, f, order=2):
    return sosfilt(butter(order, f, btype="low", fs=SR, output="sos"), x)


def highpass(x, f, order=2):
    return sosfilt(butter(order, f, btype="high", fs=SR, output="sos"), x)


def env_ad(n, attack, decay_tau):
    """Linear attack then exponential decay. attack/decay in seconds."""
    t = np.arange(n) / SR
    a = np.clip(t / max(attack, 1e-4), 0, 1)
    return a * np.exp(-np.maximum(0, t - attack) / decay_tau)


def fade(x, fin=0.003, fout=0.01):
    n_in, n_out = int(SR * fin), int(SR * fout)
    x = x.copy()
    if n_in:
        x[:n_in] *= np.linspace(0, 1, n_in)
    if n_out:
        x[-n_out:] *= np.linspace(1, 0, n_out)
    return x


def normalize(x, peak=0.9):
    m = np.max(np.abs(x))
    return x if m == 0 else x / m * peak


def place(buf, x, at):
    i = int(at * SR)
    end = min(len(buf), i + len(x))
    buf[i:end] += x[: end - i]
    return buf


def save(name, x, peak=0.9, loop=False):
    # Loops skip the edge fades so the wrap point stays seamless.
    x = normalize(x if loop else fade(x), peak)
    wavfile.write(os.path.join(OUT, name + ".wav"), SR, (x * 32767).astype(np.int16))


def bell(freq, dur, bright=1.0, decay=0.6):
    """Inharmonic bell/glass tone."""
    t = t_axis(dur)
    out = np.zeros_like(t)
    for ratio, amp, d in ((1.0, 1.0, 1.0), (2.76, 0.5 * bright, 0.6), (5.40, 0.3 * bright, 0.4),
                          (8.93, 0.15 * bright, 0.25)):
        out += amp * np.sin(2 * np.pi * freq * ratio * t) * np.exp(-t / (decay * d))
    return out * np.clip(t / 0.002, 0, 1)


def fm_tone(freq, dur, index=2.0, ratio=1.0, decay=0.8, idx_decay=0.25):
    """Simple two-operator FM voice (electric-piano-ish)."""
    t = t_axis(dur)
    mod = index * np.exp(-t / idx_decay) * np.sin(2 * np.pi * freq * ratio * t)
    car = np.sin(2 * np.pi * freq * t + mod)
    return car * env_ad(len(t), 0.004, decay)


def midi_hz(m):
    return 440.0 * 2 ** ((m - 69) / 12)


# ---------------------------------------------------------------- pour + liquids
def make_loop(x, xfade=0.25):
    """Crossfade the tail into the head so the clip loops without a click."""
    n = int(SR * xfade)
    head, tail = x[:n], x[-n:]
    w = np.linspace(0, 1, n)
    body = x[n:-n].copy()
    mixed = tail * (1 - w) + head * w
    return np.concatenate([body, mixed])


def pour_loop(dur=3.0, thick=False):
    n = noise(dur + 0.5)
    if thick:
        base = bandpass(n, 120, 900) * 0.8 + bandpass(n, 900, 2000) * 0.15
        mod_rate = 3.0
    else:
        base = bandpass(n, 300, 2600) * 0.7 + bandpass(n, 2600, 6000) * 0.25
        mod_rate = 9.0
    t = t_axis(dur + 0.5)
    # bubbly amplitude flutter
    flutter = 0.75 + 0.25 * np.sin(2 * np.pi * mod_rate * t + 2 * np.sin(2 * np.pi * 0.7 * t))
    x = base * flutter
    # sprinkle tiny resonant bubble "plinks" (these give the pitch the game shifts as the glass fills)
    for _ in range(int((dur + 0.5) * (14 if not thick else 5))):
        f0 = rng.uniform(700, 1500) if not thick else rng.uniform(250, 500)
        d = 0.04 if not thick else 0.09
        tt = t_axis(d)
        f = f0 * (1 + 1.5 * tt / d)
        p = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-tt / (d / 3))
        place(x, p * rng.uniform(0.15, 0.35), rng.uniform(0, dur + 0.4))
    return make_loop(x[: int(SR * (dur + 0.25))])


def glug(f0):
    d = 0.22
    t = t_axis(d)
    f = f0 * (1 + 2.2 * (t / d) ** 0.7)
    tone = np.sin(2 * np.pi * np.cumsum(f) / SR) * env_ad(len(t), 0.005, 0.06)
    thump = lowpass(noise(d), 300) * env_ad(len(t), 0.002, 0.03) * 1.5
    return tone + thump


def splash(dur=0.45, bright=1.0):
    x = bandpass(noise(dur), 500, 5000 * bright) * env_ad(int(SR * dur), 0.004, dur / 5)
    for _ in range(10):
        f0 = rng.uniform(900, 2500)
        tt = t_axis(0.05)
        p = np.sin(2 * np.pi * np.cumsum(f0 * (1 + tt * 20)) / SR) * np.exp(-tt / 0.012)
        place(x, p * 0.4, rng.uniform(0.02, dur * 0.8))
    return x


def drip():
    d = 0.12
    t = t_axis(d)
    f = 900 * (1 + 4 * t / d)
    return np.sin(2 * np.pi * np.cumsum(f) / SR) * env_ad(len(t), 0.001, 0.025)


# ---------------------------------------------------------------- glassware + handling
def clink():
    x = np.zeros(int(SR * 0.6))
    place(x, bell(2350, 0.6, 1.2, 0.18) * 0.7, 0)
    place(x, bell(3100, 0.4, 1.0, 0.1) * 0.3, 0.004)
    place(x, lowpass(noise(0.05), 400) * env_ad(int(SR * 0.05), 0.001, 0.01) * 1.2, 0)
    return x


def slide():
    d = 0.55
    t = t_axis(d)
    shape = np.sin(np.pi * t / d) ** 1.5
    x = bandpass(noise(d), 200, 1400) * shape * 0.8
    x += bandpass(noise(d), 2500, 4500) * shape * 0.12
    return x


def bottle_lift():
    x = np.zeros(int(SR * 0.35))
    place(x, lowpass(noise(0.08), 250) * env_ad(int(SR * 0.08), 0.002, 0.02) * 1.4, 0)
    place(x, bell(1500, 0.3, 0.6, 0.08) * 0.25, 0.01)
    return x


def cork_pop():
    x = np.zeros(int(SR * 0.4))
    pop = bandpass(noise(0.03), 600, 4000) * env_ad(int(SR * 0.03), 0.0005, 0.006)
    place(x, pop * 1.4, 0)
    t = t_axis(0.25)
    res = np.sin(2 * np.pi * 380 * t * (1 - 0.3 * t)) * env_ad(len(t), 0.002, 0.05)
    place(x, res * 0.6, 0.004)
    return x


# ---------------------------------------------------------------- feedback stingers
def chord_notes(midis, dur, voice, spacing=0.0, amp=1.0):
    x = np.zeros(int(SR * (dur + spacing * len(midis))))
    for i, m in enumerate(midis):
        place(x, voice(midi_hz(m), dur) * amp, i * spacing)
    return x


def perfect():
    v = lambda f, d: bell(f, d, 0.8, 0.5) * 0.6 + fm_tone(f, d, 1.2, 1.0, 0.6) * 0.5
    x = chord_notes([84, 88, 91, 96], 0.9, v, 0.07)
    sp = highpass(noise(0.9), 6000) * env_ad(int(SR * 0.9), 0.01, 0.25) * 0.08
    return place(x, sp, 0.2)


def great():
    v = lambda f, d: bell(f, d, 0.6, 0.45) * 0.6 + fm_tone(f, d, 1.0, 1.0, 0.5) * 0.4
    return chord_notes([84, 91], 0.7, v, 0.08)


def good():
    v = lambda f, d: fm_tone(f, d, 1.5, 1.0, 0.4) * 0.8
    return chord_notes([79], 0.5, v)


def miss():
    t = t_axis(0.55)
    sq = np.sign(np.sin(2 * np.pi * 110 * t)) + np.sign(np.sin(2 * np.pi * 116.5 * t))
    return lowpass(sq, 1600) * env_ad(len(t), 0.005, 0.25) * 0.5


def spilled():
    """Descending 'wah-wah' brass-ish phrase."""
    out = np.zeros(int(SR * 1.3))
    for i, (m, d) in enumerate(((58, 0.28), (57, 0.28), (56, 0.6))):
        t = t_axis(d)
        f = midi_hz(m) * (1 + 0.01 * np.sin(2 * np.pi * 6 * t))
        if i == 2:
            f *= 1 - 0.05 * t / d
        ph = np.cumsum(f) / SR
        saw = 2 * (ph % 1) - 1
        wah = lowpass(saw, 1200) * 0.6 + saw * 0.15
        place(out, wah * env_ad(len(t), 0.02, d * 0.7), i * 0.3)
    return out


def click():
    t = t_axis(0.05)
    return (np.sin(2 * np.pi * 1800 * t) * 0.6 + highpass(noise(0.05), 3000) * 0.3) * env_ad(len(t), 0.0005, 0.008)


def hover():
    t = t_axis(0.04)
    return np.sin(2 * np.pi * 2600 * t) * env_ad(len(t), 0.0005, 0.006) * 0.4


def register():
    x = np.zeros(int(SR * 1.1))
    for k in range(3):  # mechanical drawer clicks
        place(x, bandpass(noise(0.03), 800, 3000) * env_ad(int(SR * 0.03), 0.001, 0.006), k * 0.06)
    place(x, lowpass(noise(0.12), 500) * env_ad(int(SR * 0.12), 0.002, 0.04) * 0.8, 0.2)
    place(x, bell(2093, 0.9, 1.0, 0.45) * 0.8, 0.22)
    place(x, bell(2637, 0.8, 0.8, 0.4) * 0.6, 0.24)
    return x


def coin():
    x = np.zeros(int(SR * 0.5))
    place(x, bell(3800, 0.4, 1.3, 0.12), 0)
    place(x, bell(4400, 0.35, 1.0, 0.1) * 0.6, 0.07)
    return x


def tick(urgent=False):
    t = t_axis(0.06)
    f = 2200 if urgent else 1400
    return (np.sin(2 * np.pi * f * t) + 0.3 * highpass(noise(0.06), 4000)) * env_ad(len(t), 0.0005, 0.01)


def level_complete():
    v = lambda f, d: fm_tone(f, d, 1.4, 1.0, 0.9) * 0.6 + bell(f * 2, d, 0.5, 0.5) * 0.3
    x = chord_notes([72, 76, 79, 84], 0.6, v, 0.12)
    final = chord_notes([72, 76, 79, 84, 88], 1.6, v, 0.0, 0.5)
    return np.concatenate([x[: int(SR * 0.48)], final])


def level_fail():
    v = lambda f, d: fm_tone(f, d, 1.0, 1.0, 0.8) * 0.7
    return chord_notes([67, 63, 60, 55], 0.8, v, 0.22)


def whoosh():
    d = 0.4
    t = t_axis(d)
    x = noise(d)
    out = np.zeros_like(x)
    # sweep a band through the noise
    for i in range(0, len(x), 512):
        seg = x[max(0, i - 2048): i + 512]
        f = 400 + 3000 * (i / len(x))
        out[i: i + 512] = bandpass(seg, f, f * 1.8)[-len(out[i: i + 512]):]
    return out * np.sin(np.pi * t / d) ** 2


if __name__ == "__main__":
    save("pour_loop", pour_loop(3.0, False), 0.7, loop=True)
    save("pour_thick_loop", pour_loop(3.0, True), 0.7, loop=True)
    for i, f in enumerate((170, 210, 250)):
        save(f"glug_{i + 1}", glug(f), 0.8)
    save("splash", splash(0.45), 0.7)
    save("spill", splash(0.9, 0.7), 0.8)
    save("drip", drip(), 0.5)
    save("clink", clink(), 0.7)
    save("slide", slide(), 0.6)
    save("bottle_lift", bottle_lift(), 0.6)
    save("cork_pop", cork_pop(), 0.8)
    save("grade_perfect", perfect(), 0.8)
    save("grade_great", great(), 0.75)
    save("grade_good", good(), 0.7)
    save("grade_miss", miss(), 0.6)
    save("grade_spilled", spilled(), 0.7)
    save("ui_click", click(), 0.6)
    save("ui_hover", hover(), 0.35)
    save("register", register(), 0.75)
    save("coin", coin(), 0.6)
    save("tick", tick(False), 0.5)
    save("tick_urgent", tick(True), 0.6)
    save("level_complete", level_complete(), 0.8)
    save("level_fail", level_fail(), 0.7)
    save("whoosh", whoosh(), 0.5)
    print("wrote", len([f for f in os.listdir(OUT) if f.endswith('.wav')]), "files")
