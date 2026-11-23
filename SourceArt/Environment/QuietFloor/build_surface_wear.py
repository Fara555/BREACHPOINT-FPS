"""Build the HDRP floor maps using the shared X/2, Z/2 deck UVs."""

import argparse
from pathlib import Path

import numpy as np
from PIL import Image


PROJECT = Path(__file__).resolve().parents[3]
TEXTURES = PROJECT / "Assets/Project/Art/Environment/ServiceQuarter/QuietFloor/Textures"
SIZE = 2048
X_MIN, X_MAX = -19.0, 19.0
Z_MIN, Z_MAX = 19.0, 65.0


def smoothstep(low, high, value):
    t = np.clip((value - low) / (high - low), 0, 1)
    return t * t * (3 - 2 * t)


def noise(rng, columns, rows):
    samples = rng.integers(0, 256, (rows, columns), dtype=np.uint8)
    image = Image.fromarray(samples).resize((SIZE, SIZE), Image.Resampling.BICUBIC)
    return np.asarray(image, dtype=np.float32) / 255


def track(x, z, points, width, feather):
    distance_squared = np.full((SIZE, SIZE), np.inf, dtype=np.float32)
    for (ax, az), (bx, bz) in zip(points, points[1:]):
        dx, dz = bx - ax, bz - az
        t = np.clip(((x - ax) * dx + (z - az) * dz) / (dx * dx + dz * dz), 0, 1)
        offset_x, offset_z = x - (ax + dx * t), z - (az + dz * t)
        np.minimum(distance_squared, offset_x * offset_x + offset_z * offset_z, out=distance_squared)
    return 1 - smoothstep(width, width + feather, np.sqrt(distance_squared))


def floor_masks(x, z):
    rng = np.random.default_rng(4861)
    coating = noise(rng, 26, 33)
    patch = noise(rng, 85, 105)
    grain = noise(rng, 280, 350)
    irregularity = 0.25 + 0.64 * smoothstep(0.16, 0.82, 0.62 * coating + 0.38 * patch)

    traffic = np.zeros_like(coating)
    routes = (
        # Entry, east aisle around the cases, gallery, west lane and north approach.
        ([(4.4, 21), (3.0, 25), (2.5, 29.3), (5.5, 33.7)], 0.55, 1.00, 0.83),
        ([(5.5, 33.7), (7.5, 38), (6.6, 43), (7.8, 48), (6.8, 53)], 0.65, 1.22, 1.00),
        ([(15.6, 32), (15.3, 40), (15.7, 47.2), (15.3, 55.7)], 0.47, 0.94, 0.77),
        ([(-8.5, 31.7), (-8.0, 36.4), (-7.4, 41.6), (-8.9, 46.5)], 0.51, 0.84, 0.79),
        ([(-10.8, 47.3), (-8.5, 49.0), (-4.2, 49.7), (-2.9, 53.9), (-4.4, 61.8)], 0.58, 1.05, 0.86),
        # Market doors and the aisle where the counter service traffic meets it.
        ([(-14.55, 32.4), (-14.43, 36.0), (-14.73, 40.2), (-14.12, 45), (-14.45, 48.6)], 0.56, 0.88, 0.92),
        ([(-14.6, 38.5), (-13.7, 37.4), (-12.9, 37.3)], 0.21, 0.65, 0.54),
    )
    for points, width, feather, amount in routes:
        np.maximum(traffic, amount * track(x, z, points, width, feather), out=traffic)
    traffic *= irregularity

    dust = np.zeros_like(coating)
    sheltered_edges = (
        [(4.12, 36.12), (4.12, 45.82)],
        [(-6.12, 36.12), (-6.12, 45.82)],
        [(5.9, 46.1), (4.1, 46.1), (-5.8, 46.1)],
        [(-10.9, 34.3), (-10.9, 46.65)],
        [(-17.69, 34.35), (-17.69, 46.7)],
        [(17.79, 22.0), (17.79, 61.5)],
        [(-17.78, 48.0), (-17.78, 61.2)],
        [(-17.75, 63.76), (17.75, 63.76)],
        [(-0.9, 55.90), (10.95, 55.90)],
    )
    for edge in sheltered_edges:
        np.maximum(dust, track(x, z, edge, 0.03, 0.44), out=dust)
    dust *= irregularity * (1 - 0.72 * traffic)

    handling = np.zeros_like(coating)
    scuffs = np.zeros_like(coating)
    for cx, cz, side in [(1.0, 31.6, -1), (8.5, 43.6, -1), (-5.5, 51.6, 1)]:
        drag = [(cx - 0.9, cz + side * 0.7), (cx - 0.7, cz + side * 1.4), (cx - 0.47, cz + side * 2.05)]
        np.maximum(handling, track(x, z, drag, 0.1, 0.53), out=handling)
        for shift, length in [(-0.32, 0.95), (0.12, 1.31), (0.43, 0.53)]:
            scrape = [(cx + shift, cz + side * 0.72), (cx + shift + 0.16, cz + side * (0.72 + length))]
            np.maximum(scuffs, track(x, z, scrape, 0.012, 0.065), out=scuffs)

    for threshold in [34.02, 46.98]:
        jamb = [(-15.76, threshold), (-13.04, threshold)]
        np.maximum(handling, 0.67 * track(x, z, jamb, 0.015, 0.31), out=handling)
    handling *= irregularity
    scuffs *= smoothstep(0.26, 0.77, 0.8 * patch + 0.2 * grain)
    return coating, patch, grain, traffic, dust, handling, scuffs


def floor_inlays(x, z, patch, grain, traffic):
    grip_coat = np.zeros_like(patch)
    for threshold in [34.02, 46.98]:
        edge = np.maximum(np.abs(x + 14.4) - 1.28, np.abs(z - threshold) - 0.29)
        band = 1 - smoothstep(-0.035, 0.045, edge)
        np.maximum(grip_coat, band, out=grip_coat)
    grip_coat *= (0.90 + 0.10 * grain) * (1 - 0.21 * traffic)

    loading_mark = np.zeros_like(patch)
    # The corners frame the east loading area, where the cases can be staged.
    for cx, cz, sx, sz in [(6.52, 42.43, 1, 1), (10.52, 42.43, -1, 1),
                           (6.52, 44.77, 1, -1), (10.52, 44.77, -1, -1)]:
        points = [(cx, cz + sz * 0.52), (cx, cz), (cx + sx * 0.65, cz)]
        np.maximum(loading_mark, track(x, z, points, 0.037, 0.019), out=loading_mark)
    loading_mark *= 0.48 + 0.31 * smoothstep(0.23, 0.70, 0.39 * patch + 0.61 * grain)
    return grip_coat, loading_mark


def byte(values):
    return np.rint(255 * np.clip(values, 0, 1)).astype(np.uint8)


def srgb(linear):
    return np.where(linear <= 0.0031308, linear * 12.92, 1.055 * linear ** (1 / 2.4) - 0.055)


def render(output, strength):
    steps = (np.arange(SIZE, dtype=np.float32) + 0.5) / SIZE
    x = (X_MIN + steps * (X_MAX - X_MIN))[None, :]
    z = (Z_MAX - steps * (Z_MAX - Z_MIN))[:, None]
    coating, patch, grain, traffic, dust, handling, scuffs = floor_masks(x, z)
    grip_coat, loading_mark = floor_inlays(x, z, patch, grain, traffic)
    # Darker compressed rubber/scuff deposits, cool exposed coating and a slight warm edge tone.
    tint = np.clip(0.99 - strength * (0.050 * (1 - coating) + 0.085 * traffic
                   + 0.115 * dust + 0.063 * handling + 0.125 * scuffs), 0.70, 1)
    tint *= 1 - 0.25 * grip_coat
    color = np.stack((byte(srgb(tint + 0.009 * dust - 0.03 * loading_mark)),
                      byte(srgb(tint - 0.22 * loading_mark)),
                      byte(srgb(tint - 0.47 * loading_mark - 0.016 * dust))), axis=-1)
    Image.fromarray(color).save(output / "FloorWearColor.png")

    smoothness = np.clip(0.163 + 0.027 * (patch - 0.5) + 0.011 * (grain - 0.5)
                         + strength * (0.231 * traffic - 0.065 * dust
                                       + 0.090 * handling + 0.125 * scuffs), 0.088, 0.435)
    smoothness += (0.105 + 0.038 * grain - smoothness) * grip_coat
    smoothness += (0.22 - smoothness) * loading_mark
    mask = np.empty((SIZE, SIZE, 4), dtype=np.uint8)
    mask[..., 0] = byte(0.20 + 0.045 * strength * scuffs - 0.11 * grip_coat - 0.13 * loading_mark)
    mask[..., 1] = 255
    mask[..., 2] = byte(1 - strength * (0.38 * traffic + 0.22 * handling))
    mask[..., 3] = byte(smoothness)
    Image.fromarray(mask).save(output / "FloorWearMask.png")

    normal = np.asarray(Image.open(TEXTURES / "FloorFineNormal.png").convert("RGB"))
    detail = np.full(normal.shape[:2] + (4,), 128, dtype=np.uint8)
    # HDRP Lit detail mapping: R albedo, G normal Y, B smoothness, A normal X.
    detail[..., 1], detail[..., 3] = normal[..., 1], normal[..., 0]
    Image.fromarray(detail).save(output / "FloorCoatingDetail.png")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=TEXTURES)
    parser.add_argument("--strength", type=float, default=1.0)
    options = parser.parse_args()
    options.output.mkdir(parents=True, exist_ok=True)
    render(options.output, options.strength)


if __name__ == "__main__":
    main()
