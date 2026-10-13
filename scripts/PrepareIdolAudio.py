"""Prepare the project's embedded audio banks from the original Soundly WAVs.

Python 3.10+, standard library only. Input: the two 96 kHz / 24-bit PCM WAVs.
Output: versioned 48 kHz mono float banks in Assets/Audio, optional local WAV previews.
"""
import argparse
import array
import hashlib
import json
import struct
import sys
import wave
from pathlib import Path

RANGES = {
    "duck_steps": [(0, .23), (.49, .84), (3.10, 3.50), (6.60, 6.94),
                   (9.43, 9.93), (10.31, 10.668208333333334)],
    "spring_accent": [(0, 1.95), (2.38, 4.35), (4.90, 6.87), (7.44, 9.50)],
}


def prepare(source, key, output, previews):
    with wave.open(str(source), "rb") as wav:
        if (wav.getframerate(), wav.getsampwidth(), wav.getnchannels()) != (96000, 3, 2):
            raise ValueError(f"{source}: expected the exported 96 kHz / 24-bit stereo PCM")
        data = wav.readframes(wav.getnframes())
    clips = []
    for start, end in RANGES[key]:
        begin, finish = round(start * 96000), round(end * 96000)
        if finish * 6 > len(data):
            raise ValueError(f"{source}: too short for {key} selection")
        # Average each pair of stereo frames before 2:1 downsampling.
        samples = array.array("f")
        for frame in range(begin, finish - 1, 2):
            offset = frame * 6
            value = sum(int.from_bytes(data[i:i+3], "little", signed=True)
                        for i in range(offset, offset + 12, 3)) / (4 * 8388608)
            samples.append(value)
        gain = min(2.0, .65 / max(abs(value) for value in samples))
        fade = 240  # 5 ms anti-click edges, leaving the recorded tails intact.
        for i in range(len(samples)):
            samples[i] *= gain * min(1, i / fade, (len(samples) - 1 - i) / fade)
        clips.append(samples)
    output.mkdir(parents=True, exist_ok=True)
    target = output / f"{key}.pcm"
    with target.open("wb") as stream:
        stream.write(struct.pack("<III", 0x31415343, 48000, len(clips)))  # CSA1
        for samples in clips:
            stream.write(struct.pack("<I", len(samples)))
            if sys.byteorder != "little":
                samples.byteswap()
            stream.write(samples.tobytes())
            if sys.byteorder != "little":
                samples.byteswap()
    if previews:
        previews.mkdir(parents=True, exist_ok=True)
        for index, samples in enumerate(clips, 1):
            with wave.open(str(previews / f"{key}_{index:02}.wav"), "wb") as wav:
                wav.setparams((1, 2, 48000, 0, "NONE", "not compressed"))
                wav.writeframes(b"".join(struct.pack("<h", round(v * 32767)) for v in samples))
    return {"bank": key, "source": source.name, "sourceSha256": hashlib.sha256(source.read_bytes()).hexdigest(),
            "cutsSeconds": RANGES[key], "sampleRate": 48000, "channels": 1,
            "samples": [len(clip) for clip in clips], "sha256": hashlib.sha256(target.read_bytes()).hexdigest()}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--duck-source", required=True, type=Path)
    parser.add_argument("--spring-source", required=True, type=Path)
    parser.add_argument("--output-dir", type=Path, default=Path("Assets/Audio"))
    parser.add_argument("--preview-dir", type=Path)
    args = parser.parse_args()
    result = [prepare(args.duck_source, "duck_steps", args.output_dir, args.preview_dir),
              prepare(args.spring_source, "spring_accent", args.output_dir, args.preview_dir)]
    metadata = args.output_dir / "cuts.json"
    metadata.write_text(json.dumps(result, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(metadata.resolve())
