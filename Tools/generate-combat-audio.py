"""Generate the project's original dry, physical combat sounds. Requires NumPy.

No recordings, downloaded samples, voice, pitch sweeps, or third-party assets.
Run from any directory; outputs are resolved relative to this saved script.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import uuid
import wave

import numpy as np

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "RequiemLantern/Assets/Resources/Audio/Combat"
ART = ROOT / "Art/Audio"
RATE = 48000
MASTER_SEED = 20261013
DURATIONS = {
    "clash": .42, "hit_cut": .22, "hit_blunt": .26, "hit_heavy": .39,
    "swing": .21, "brace": .27, "ignite": .13, "break": .60,
}
DESCRIPTIONS = {
    "clash": "Dry steel contact: impact noise and many short, inharmonic resonances.",
    "hit_cut": "A crisp cutting contact: rough cloth snap, leather slap and a short body hit.",
    "hit_blunt": "A close punch: broad mid-low body, skin/leather snap and no ringing tail.",
    "hit_heavy": "A heavy shield/hammer contact: low body, rough crack and a short roomless thud.",
    "swing": "An unpitched weapon rush: filtered air, fast rise, no tonal glide.",
    "brace": "A shield brace: grip friction, wooden/metal knock and a small buckle rattle.",
    "ignite": "A tiny dry flint/coal click followed by two low-volume crackles.",
    "break": "A fracture: short crack, low body and irregular falling fragments.",
}


def texture(rng, n, low, high, tilt=0.0):
    """Smoothly band-limit independent noise; never impose a time-varying pitch."""
    frequencies = np.fft.rfftfreq(n, 1 / RATE)
    safe = np.maximum(frequencies, 1)
    high_pass = 1 - np.exp(-(safe / low) ** 4) if low else np.ones_like(safe)
    low_pass = np.exp(-(safe / high) ** 4)
    shaping = high_pass * low_pass * (safe / 1000) ** tilt
    shaping[0] = 0
    result = np.fft.irfft(np.fft.rfft(rng.standard_normal(n)) * shaping, n)
    return result / max(np.std(result), 1e-9)


def impulse(t, decay, attack=.0008):
    return (1 - np.exp(-t / attack)) * np.exp(-t / decay)


def resonances(t, rng, frequencies, decay, level=1.0):
    """An impulse exciting fixed modes, similar to struck irregular material."""
    result = np.zeros_like(t)
    for i, frequency in enumerate(frequencies):
        frequency *= rng.uniform(.94, 1.06)
        damping = decay * rng.uniform(.7, 1.25) / (1 + i * .13)
        amplitude = level * rng.uniform(.75, 1.15) / (1 + i * .37)
        phase = rng.uniform(-.55, .55)
        result += amplitude * np.sin(2 * np.pi * frequency * t + phase) * impulse(t, damping)
    return result


def delayed_fragment(result, rng, start, length, low, high, level):
    offset = round(start * RATE)
    n = min(round(length * RATE), len(result) - offset)
    if n <= 0:
        return
    t = np.arange(n) / RATE
    result[offset:offset+n] += texture(rng, n, low, high) * impulse(t, length / 5) * level


def synthesize(family, variant):
    family_index = list(DURATIONS).index(family)
    seed = MASTER_SEED + family_index * 1009 + variant * 7919
    rng = np.random.default_rng(seed)
    duration = DURATIONS[family] + (variant - 2) * .009
    n = round(RATE * duration)
    t = np.arange(n) / RATE
    bright = texture(rng, n, 900, 9800)
    rough = texture(rng, n, 120, 2700)
    body = texture(rng, n, 35, 500)

    if family == "clash":
        # A dense cluster gives a struck-metal character instead of one electronic note.
        fixed_modes = [470, 733, 1097, 1523, 2071, 2879, 3719, 4933, 6491, 8107]
        signal = resonances(t, rng, fixed_modes, .083, .23)
        signal += bright * impulse(t, .011) * .58
        signal += rough * impulse(t, .026) * .24
        signal += resonances(t, rng, [155, 237], .027, .17)
        delayed_fragment(signal, rng, .012, .036, 1000, 7200, .10)
    elif family == "hit_cut":
        signal = bright * impulse(t, .009, .00045) * .60
        signal += rough * impulse(t, .034) * .51
        signal += body * impulse(t, .028) * .25
        signal += resonances(t, rng, [142, 271, 607], .024, .10)
        delayed_fragment(signal, rng, .010, .045, 500, 5200, .10)
    elif family == "hit_blunt":
        signal = rough * impulse(t, .027, .00055) * .63
        signal += bright * impulse(t, .005, .0004) * .29
        signal += body * impulse(t, .045) * .38
        signal += resonances(t, rng, [97, 163, 287], .039, .23)
    elif family == "hit_heavy":
        signal = body * impulse(t, .067, .0007) * .60
        signal += resonances(t, rng, [61, 106, 183, 341], .061, .40)
        signal += rough * impulse(t, .025) * .47
        signal += bright * impulse(t, .005, .0004) * .29
        delayed_fragment(signal, rng, .023, .09, 100, 1900, .16)
    elif family == "swing":
        # Air pressure rises and falls, while its frequency distribution stays fixed.
        envelope = np.sin(np.pi * np.minimum(t / duration, 1)) ** 2 * np.exp(-t / .18)
        signal = texture(rng, n, 170, 7400, -.1) * envelope
        signal += rough * envelope * .12
    elif family == "brace":
        signal = rough * impulse(t, .027) * .37
        signal += body * impulse(t, .040) * .26
        signal += resonances(t, rng, [174, 331, 731, 1217], .025, .20)
        for start in [.036, .072, .102]:
            delayed_fragment(signal, rng, start, .04, 900, 5600, rng.uniform(.05, .12))
    elif family == "ignite":
        signal = bright * impulse(t, .0022, .00035) * .46
        signal += rough * impulse(t, .005) * .28
        for start in [.019, .048, .069]:
            delayed_fragment(signal, rng, start, .012, 800, 7100, rng.uniform(.035, .075))
    elif family == "break":
        signal = rough * impulse(t, .018, .00045) * .63
        signal += bright * impulse(t, .007, .0004) * .36
        signal += body * impulse(t, .060) * .41
        signal += resonances(t, rng, [83, 211, 511, 1099], .035, .20)
        for start in sorted(rng.uniform(.025, .47, 13)):
            delayed_fragment(signal, rng, start, rng.uniform(.022, .071),
                             rng.uniform(150, 800), rng.uniform(2300, 7400),
                             .22 * np.exp(-start / .23))
    else:
        raise ValueError(family)

    # Soft material saturation thickens the contact, without a clipped sample.
    if family in {"hit_blunt", "hit_heavy", "hit_cut", "break"}:
        signal = np.tanh(signal * 1.15)
    signal -= np.mean(signal)
    attack_n = max(2, round(RATE * .0005))
    release_n = min(n // 5, round(RATE * .018))
    signal[:attack_n] *= np.sin(np.linspace(0, np.pi / 2, attack_n)) ** 2
    signal[-release_n:] *= np.cos(np.linspace(0, np.pi / 2, release_n)) ** 2

    # Very small independent early reflections retain a close, dry stereo image.
    left, right = signal.copy(), signal.copy()
    if family == "clash":
        for channel, delay in [(left, 91), (right, 137)]:
            channel[delay:] += signal[:-delay] * .055
    elif family in {"swing", "break"}:
        secondary = texture(rng, n, 250, 5500) * np.abs(signal) * .10
        left += secondary
        right -= secondary
    stereo = np.column_stack([left, right])
    stereo -= np.mean(stereo, axis=0)
    # Zero endpoints and remove DC with a tapered correction, preserving fades.
    taper = np.ones(n)
    taper[:attack_n] = np.sin(np.linspace(0, np.pi / 2, attack_n)) ** 2
    taper[-release_n:] = np.cos(np.linspace(0, np.pi / 2, release_n)) ** 2
    stereo *= taper[:, None]
    stereo -= taper[:, None] * (np.sum(stereo, axis=0) / np.sum(taper))
    peak_target = .80 if family != "ignite" else .63
    stereo *= peak_target / max(np.max(np.abs(stereo)), 1e-9)
    pcm = np.rint(stereo * 32767).astype("<i2")
    return seed, pcm


def write_wav(path, pcm):
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), "wb") as output:
        output.setnchannels(2)
        output.setsampwidth(2)
        output.setframerate(RATE)
        output.writeframes(pcm.tobytes())


def write_unity_meta(path):
    meta = Path(str(path) + ".meta")
    if meta.exists():
        return
    guid = uuid.uuid5(uuid.NAMESPACE_URL, "requiem-lantern-original-audio/" + path.name).hex
    meta.write_text(f"""fileFormatVersion: 2
guid: {guid}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 7
  defaultSettings:
    serializedVersion: 2
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 48000
    compressionFormat: 0
    quality: 1
    conversionMode: 0
    preloadAudioData: 1
  platformSettingOverrides: {{}}
  forceToMono: 0
  normalize: 0
  loadInBackground: 0
  ambisonic: 0
  3D: 0
  userData:
  assetBundleName:
  assetBundleVariant:
""", encoding="utf-8")


def write_resource_metadata():
    for path, folder in [(OUTPUT.parent, True), (OUTPUT, True), (OUTPUT / "manifest.json", False)]:
        meta = Path(str(path) + ".meta")
        if meta.exists():
            continue
        identity = path.relative_to(ROOT).as_posix()
        guid = uuid.uuid5(uuid.NAMESPACE_URL, "requiem-lantern-original-audio/" + identity).hex
        folder_line = "folderAsset: yes\n" if folder else ""
        importer = "DefaultImporter" if folder else "TextScriptImporter"
        meta.write_text(f"fileFormatVersion: 2\nguid: {guid}\n{folder_line}{importer}:\n"
                        "  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n", encoding="utf-8")


def validate(pcm):
    values = pcm.astype(np.float64) / 32767
    peak = float(np.max(np.abs(values)))
    rms = float(np.sqrt(np.mean(values ** 2)))
    dc = float(np.max(np.abs(np.mean(values, axis=0))))
    assert pcm.ndim == 2 and pcm.shape[1] == 2, "Expected stereo"
    assert peak <= .88 and np.max(np.abs(pcm.astype(np.int32))) < 32767, "Clipping"
    assert rms > .013, "Unexpectedly silent asset"
    assert dc < .0001, "DC offset"
    assert np.max(np.abs(values[[0, -1]])) <= 1 / 32767, "Non-zero edge"
    return {"duration": round(len(pcm) / RATE, 6), "peak": round(peak, 6),
            "rms": round(rms, 6), "dcOffset": round(dc, 9),
            "sha256": hashlib.sha256(pcm.tobytes()).hexdigest()}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="Regenerate in memory and verify the saved WAVs.")
    args = parser.parse_args()
    manifest = {"schemaVersion": 1, "sampleRate": RATE, "channels": 2,
                "bitsPerSample": 16, "masterSeed": MASTER_SEED,
                "source": "Original deterministic NumPy synthesis; no recordings or external samples.",
                "assets": []}
    assets = {}
    for family in DURATIONS:
        hashes = set()
        for variant in range(1, 4):
            asset_id = f"{family}_{variant:02}"
            seed, pcm = synthesize(family, variant)
            metrics = validate(pcm)
            assert metrics["sha256"] not in hashes, "Variant duplicates"
            hashes.add(metrics["sha256"])
            path = OUTPUT / f"{asset_id}.wav"
            if args.check:
                with wave.open(str(path), "rb") as saved:
                    assert saved.getparams()[:3] == (2, 2, RATE), "Wrong PCM format"
                    assert saved.readframes(saved.getnframes()) == pcm.tobytes(), "Generated source differs"
            else:
                write_wav(path, pcm)
                write_unity_meta(path)
            manifest["assets"].append({"id": asset_id, "family": family, "seed": seed,
                                       "resourcePath": f"Audio/Combat/{asset_id}",
                                       "description": DESCRIPTIONS[family], **metrics})
            assets[asset_id] = pcm

    order = ["swing_01", "clash_01", "clash_02", "hit_cut_01", "hit_blunt_01",
             "hit_blunt_02", "hit_heavy_01", "hit_heavy_02", "brace_01", "ignite_01", "break_01"]
    silence = np.zeros((round(RATE * .275), 2), dtype="<i2")
    audition, cursor, timeline = [silence], len(silence), []
    for asset_id in order:
        timeline.append({"id": asset_id, "start": round(cursor / RATE, 3),
                         "duration": round(len(assets[asset_id]) / RATE, 3)})
        audition.extend([assets[asset_id], silence])
        cursor += len(assets[asset_id]) + len(silence)
    manifest["audition"] = {"path": "Art/Audio/combat-audition.wav", "duration": round(cursor / RATE, 3),
                            "timeline": timeline}
    if not args.check:
        write_wav(ART / "combat-audition.wav", np.concatenate(audition))
        (OUTPUT / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        write_resource_metadata()
    print(json.dumps({"result": "passed", "mode": "check" if args.check else "generate", "assets": len(assets),
                      "auditionSeconds": manifest["audition"]["duration"],
                      "peakMax": max(x["peak"] for x in manifest["assets"]),
                      "rmsMin": min(x["rms"] for x in manifest["assets"]),
                      "checks": ["PCM format", "peak and clipping", "RMS", "DC offset", "zero edges", "distinct variants", "deterministic source"]}))


if __name__ == "__main__":
    main()
