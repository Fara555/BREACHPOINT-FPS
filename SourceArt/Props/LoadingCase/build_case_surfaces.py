"""Author the case paint maps and its physical identification plate."""
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / "Assets/Project/Art/Props/LoadingCase/Textures"
OUT.mkdir(parents=True, exist_ok=True)
rng = np.random.default_rng(48301)
size = 512


def noise(side):
    source = Image.fromarray(rng.integers(0, 256, (side, side), dtype=np.uint8))
    image = source.resize((size, size), Image.Resampling.BICUBIC)
    return np.asarray(image, dtype=float) / 255 - .5


def save(name, data):
    pixels = (np.clip(data, 0, 1) * 255 + .5).astype(np.uint8)
    Image.fromarray(pixels).save(OUT / (name + ".png"))


grain, powder, variation = noise(256), noise(82), noise(17)
scuffs = Image.new("L", (size, size))
draw = ImageDraw.Draw(scuffs)
for _ in range(32):
    x, y = rng.integers(15, size - 30, size=2)
    length = int(rng.integers(3, 23))
    draw.line([(x, y), (x + length, y + int(rng.integers(-3, 4)))],
              fill=int(rng.integers(35, 113)))
wear = np.asarray(scuffs, dtype=float) / 255
tint = np.array([.56, .505, .391])
paint = tint[None, None, :] * (1 + .034 * variation + .027 * powder + .027 * grain)[:, :, None]
paint += .055 * wear[:, :, None]
save("CasePaint_BaseColor", paint)
smoothness = .34 + variation * .052 + powder * .068 + grain * .042 + wear * .23
save("CasePaint_Mask", np.stack((.055 + wear * .46, .99 - wear * .045,
                                np.ones((size, size)), smoothness), axis=-1))

height = powder * .16 + grain * .063 - wear * .088
dx = (np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)) * .57
dy = (np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)) * .57
normal = np.stack((-dx, dy, np.ones((size, size))), axis=-1)
normal /= np.linalg.norm(normal, axis=-1, keepdims=True)
save("CasePaint_Normal", normal * .5 + .5)

plate = Image.new("RGB", (768, 360), "#9b9d8d")
draw = ImageDraw.Draw(plate)
fonts = Path("C:/Windows/Fonts")
regular = ImageFont.truetype(str(fonts / "bahnschrift.ttf"), 40)
bold = ImageFont.truetype(str(fonts / "bahnschrift.ttf"), 90)
small = ImageFont.truetype(str(fonts / "consola.ttf"), 34)
draw.rounded_rectangle((16, 16, 752, 344), radius=18, outline="#484e43", width=5)
draw.rectangle((41, 45, 721, 104), fill="#434e47")
draw.text((58, 47), "SERVICE QUARTER   /   LOGISTICS", font=regular, fill="#d6d6bd")
draw.text((48, 121), "SQ–08", font=bold, fill="#323b33")
draw.text((55, 223), "LOAD CASE   /   STACK  2 MAX", font=small, fill="#384139")
draw.text((55, 274), "HANDLING SIDE", font=small, fill="#384139")
for horizontal, width in ((550, 10), (568, 7), (580, 15), (601, 7), (613, 11),
                          (633, 5), (646, 12), (663, 7), (676, 17), (703, 4)):
    draw.rectangle((horizontal, 144, horizontal + width, 203), fill="#50594d")
plate.save(OUT / "CaseIdentification_BaseColor.png")
