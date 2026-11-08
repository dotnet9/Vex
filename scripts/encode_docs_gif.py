"""Encode consecutive documentation UI frames; requires Pillow."""
import sys
from pathlib import Path
from PIL import Image

source, target = map(Path, sys.argv[1:3])
paths = sorted(source.glob("[0-9][0-9][0-9].png"))
if len(paths) != 60:
    raise ValueError(f"Expected 60 consecutive UI frames, found {len(paths)}")
frames = []
for path in paths:
    with Image.open(path) as frame:
        frame = frame.convert("RGB")
        frame.thumbnail((1080, 1080), Image.Resampling.LANCZOS)
        frames.append(frame.copy())
if len({frame.tobytes() for frame in frames}) < 2:
    raise ValueError("Capture contains no visible interaction")
# 全段取色，保证明暗模式和不同配色不会被第一帧的调色板抹掉。
swatches = Image.new("RGB", (180, 120 * len(frames)))
for index, frame in enumerate(frames):
    sample = frame.copy()
    sample.thumbnail((180, 120), Image.Resampling.LANCZOS)
    swatches.paste(sample, (0, index * 120))
palette = swatches.quantize(colors=256)
frames = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in frames]
target.parent.mkdir(parents=True, exist_ok=True)
durations = [100] * len(frames)
durations[0] = 700
durations[-1] = 1200
frames[0].save(target, save_all=True, append_images=frames[1:], duration=durations, loop=0, optimize=True)
with Image.open(target) as result:
    if result.n_frames < 2:
        raise ValueError("Encoded GIF lost its interaction frames")
    print(f"{target.name}: {result.n_frames} frames, {result.size}, {target.stat().st_size} bytes")
