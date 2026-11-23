"""Generate matte enamel maps and the hatch's electrical safety label."""

from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

OUT = Path(__file__).resolve().parents[3] / "Assets/Project/Art/Props/GalleryServiceHatch/Textures"
OUT.mkdir(parents=True, exist_ok=True)
SIZE = 512
rng = np.random.default_rng(173)
y, x = np.mgrid[0:SIZE, 0:SIZE] / (SIZE - 1)


def noise(scale):
    source = Image.fromarray(rng.integers(0, 256, (scale, scale), dtype=np.uint8))
    return np.asarray(source.resize((SIZE, SIZE), Image.Resampling.BICUBIC)) / 255 - .5


def write(data, name):
    pixels = np.uint8((np.clip(data, 0, 1) * 255 + .5))
    Image.fromarray(pixels).save(OUT / (name + ".png"))


grain, powder, variation = noise(310), noise(59), noise(14)
edge = np.minimum(np.minimum(x, 1 - x), np.minimum(y, 1 - y))
dirt = np.maximum(0, 1 - edge * 15) * (.54 + variation)
exposure = np.maximum(0, (grain + .62 * powder - .32)) * np.maximum(0, 1 - edge * 74)
scratches = np.zeros_like(x)
for _ in range(19):
    a, b = rng.uniform(.76, .97), rng.uniform(.21, .70)
    length = rng.uniform(.008, .044)
    line = np.sqrt(((x - a) / .0018) ** 2 + ((y - b) / length) ** 2)
    scratches += np.maximum(0, 1 - line) * rng.uniform(.035, .13)
wear = np.minimum(.65, exposure * 2.6 + scratches)
color = np.array([.429, .459, .443]) * (1 + powder * .016 + grain * .019 + variation * .022 - dirt * .054)[:, :, None]
color = color * (1 - wear[:, :, None]) + np.array([.60, .625, .608]) * wear[:, :, None]
write(color, "HatchEnamel_BaseColor")
write(np.stack((.06 + wear * .66, 1 - dirt * .055, np.ones_like(x),
                .33 + powder * .045 + grain * .040 + wear * .25 - dirt * .045), axis=-1), "HatchEnamel_Mask")
height = grain * .052 + powder * .050 - scratches * .12
dy, dx = np.gradient(height)
normals = np.stack((-dx * 1.08, dy * 1.08, np.ones_like(x)), axis=-1)
normals /= np.linalg.norm(normals, axis=-1, keepdims=True)
write(normals * .5 + .5, "HatchEnamel_Normal")

label = Image.new("RGB", (1024, 404), "#a59c7e")
draw = ImageDraw.Draw(label)
fonts = Path("C:/Windows/Fonts")
bold = ImageFont.truetype(str(fonts / "bahnschrift.ttf"), 68)
small = ImageFont.truetype(str(fonts / "bahnschrift.ttf"), 47)
mono = ImageFont.truetype(str(fonts / "consola.ttf"), 34)
draw.rounded_rectangle((16, 16, 1008, 388), radius=17, outline="#343d39", width=7)
draw.rectangle((45, 44, 977, 119), fill="#414a43")
draw.text((73, 48), "LV DISTRIBUTION", font=bold, fill="#d1c9ac")
draw.polygon([(55, 315), (153, 146), (251, 315)], fill="#ae824c", outline="#373e35", width=9)
draw.polygon([(161, 175), (127, 241), (155, 241), (141, 283),
              (191, 220), (161, 220)], fill="#384138")
draw.text((281, 156), "ISOLATE BEFORE SERVICE", font=small, fill="#303a32")
draw.text((283, 235), "SERVICE QUARTER  /  EAST", font=mono, fill="#424940")
draw.text((283, 295), "AUTHORIZED ACCESS", font=mono, fill="#424940")
label.save(OUT / "HatchLabel_BaseColor.png")
