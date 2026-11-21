"""Build the linear HDRP mask and detail maps shared by SP6 and SR7."""

from pathlib import Path

import numpy as np
from PIL import Image


OUTPUT = (
    Path(__file__).resolve().parents[2]
    / "Assets/Project/Art/Weapons/SurfaceTextures"
)


def noise(generator, size, cells_x, cells_y):
    grid = generator.uniform(-1.0, 1.0, (cells_y, cells_x))
    x = np.arange(size) * cells_x / size
    y = np.arange(size) * cells_y / size
    ix = x.astype(int)
    iy = y.astype(int)
    fx = x - ix
    fy = y - iy
    fx = fx * fx * (3.0 - 2.0 * fx)
    fy = fy * fy * (3.0 - 2.0 * fy)
    upper = (
        grid[iy[:, None], ix] * (1.0 - fx)
        + grid[iy[:, None], (ix + 1) % cells_x] * fx
    )
    lower = (
        grid[((iy + 1) % cells_y)[:, None], ix] * (1.0 - fx)
        + grid[((iy + 1) % cells_y)[:, None], (ix + 1) % cells_x] * fx
    )
    return upper * (1.0 - fy[:, None]) + lower * fy[:, None]


def write_map(name, channels):
    values = np.stack(np.broadcast_arrays(*channels), axis=-1)
    pixels = np.rint(np.clip(values, 0.0, 1.0) * 255.0).astype(np.uint8)
    Image.fromarray(pixels).save(OUTPUT / f"T_Weapon_{name}.png")


def mask_maps():
    generator = np.random.default_rng(2036)
    size = 512
    broad = noise(generator, size, 9, 7)
    streak = noise(generator, size, 18, 87)
    wear = np.maximum(0.0, (streak + 0.4 * broad - 0.46) / 0.94) ** 2
    metal = 0.69 + 0.10 * broad + 0.14 * wear
    smooth_metal = 0.53 + 0.10 * broad + 0.08 * streak + 0.14 * wear
    # HDRP mask channels are metallic, occlusion, detail weight, and smoothness.
    write_map("Gunmetal_Mask", (metal, 0.98, 0.94, smooth_metal))

    mold = noise(generator, size, 11, 13)
    rub = noise(generator, size, 31, 24)
    smooth_polymer = 0.35 + 0.065 * mold + 0.05 * rub
    write_map("Polymer_Mask", (0.01, 0.985, 1.0, smooth_polymer))


def detail_map(name, height, smoothness, slope):
    dx = np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)
    dy = np.roll(height, 1, axis=0) - np.roll(height, -1, axis=0)
    nx = -dx * slope
    ny = -dy * slope
    length = np.sqrt(1.0 + nx * nx + ny * ny)
    # HDRP detail stores albedo, normal Y, smoothness, normal X in RGBA.
    write_map(name, (0.5, 0.5 + ny / (2.0 * length), smoothness, 0.5 + nx / (2.0 * length)))


def detail_maps():
    generator = np.random.default_rng(8033)
    size = 256
    machining = noise(generator, size, 14, 79)
    fine_metal = noise(generator, size, 50, 99)
    detail_map(
        "Gunmetal_Detail",
        0.74 * machining + 0.26 * fine_metal,
        0.5 + 0.22 * machining,
        0.49,
    )

    pebble = noise(generator, size, 65, 69)
    fine_polymer = noise(generator, size, 110, 108)
    detail_map(
        "Polymer_Detail",
        0.78 * pebble + 0.22 * fine_polymer,
        0.5 + 0.18 * pebble,
        0.72,
    )


if __name__ == "__main__":
    OUTPUT.mkdir(parents=True, exist_ok=True)
    mask_maps()
    detail_maps()
