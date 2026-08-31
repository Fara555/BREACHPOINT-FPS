from pathlib import Path
from math import cos, sin, pi

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Assets" / "Project" / "Art" / "Weapons" / "SR7"
OUT.mkdir(parents=True, exist_ok=True)

vertices = []
faces = []
groups = []


def add_box(name, center, size, material="Graphite", bevel=0.0):
    x, y, z = center
    sx, sy, sz = (v * 0.5 for v in size)
    start = len(vertices) + 1
    vertices.extend([
        (x-sx,y-sy,z-sz),(x+sx,y-sy,z-sz),(x+sx,y+sy,z-sz),(x-sx,y+sy,z-sz),
        (x-sx,y-sy,z+sz),(x+sx,y-sy,z+sz),(x+sx,y+sy,z+sz),(x-sx,y+sy,z+sz),
    ])
    local = [(0,1,2,3),(4,7,6,5),(0,4,5,1),(3,2,6,7),(1,5,6,2),(0,3,7,4)]
    groups.append((name, material, [tuple(start+i for i in reversed(face)) for face in local]))


def add_prism(name, center, radius, length, sides=8, axis="z", material="Metal"):
    cx, cy, cz = center
    start = len(vertices) + 1
    for end in (-0.5, 0.5):
        for i in range(sides):
            a = 2*pi*i/sides
            u, v = radius*cos(a), radius*sin(a)
            if axis == "z": vertices.append((cx+u, cy+v, cz+end*length))
            elif axis == "x": vertices.append((cx+end*length, cy+u, cz+v))
            else: vertices.append((cx+u, cy+end*length, cz+v))
    fs = []
    fs.append(tuple(start+i for i in reversed(range(sides))))
    fs.append(tuple(start+sides+i for i in range(sides)))
    for i in range(sides):
        j = (i+1) % sides
        fs.append((start+i, start+j, start+sides+j, start+sides+i))
    groups.append((name, material, fs))


def add_wedge(name, center, size, slope="top_front", material="Graphite"):
    """Add a six-sided hard-surface wedge for angled silhouette breaks."""
    x, y, z = center
    sx, sy, sz = (v * 0.5 for v in size)
    start = len(vertices) + 1
    if slope == "top_front":
        points = [(-sx,-sy,-sz),(sx,-sy,-sz),(sx,sy,-sz),(-sx,sy,-sz),
                  (-sx,-sy,sz),(sx,-sy,sz),(sx,0.0,sz),(-sx,0.0,sz)]
    else:
        points = [(-sx,-sy,-sz),(sx,-sy,-sz),(sx,0.0,-sz),(-sx,0.0,-sz),
                  (-sx,-sy,sz),(sx,-sy,sz),(sx,sy,sz),(-sx,sy,sz)]
    vertices.extend((x+px, y+py, z+pz) for px,py,pz in points)
    local = [(0,1,2,3),(4,7,6,5),(0,4,5,1),(3,2,6,7),(1,5,6,2),(0,3,7,4)]
    groups.append((name, material, [tuple(start+i for i in reversed(face)) for face in local]))


# Main readable silhouette (Unity: +Z forward, +Y up, metres).
add_box("Receiver_Core", (0, .02, .05), (.105, .14, .34))
add_box("Receiver_Upper", (0, .105, .09), (.095, .055, .30), "DarkMetal")
add_box("Rear_Housing", (0, .015, -.165), (.115, .165, .13))
add_box("Stock", (0, .005, -.30), (.10, .13, .17))
add_box("Buttpad", (0, .005, -.395), (.105, .135, .025), "Rubber")
add_box("Handguard", (0, .015, .285), (.09, .115, .16))
add_box("Handguard_Lower", (0, -.055, .275), (.075, .04, .19), "DarkMetal")

# Barrel assembly.
add_prism("Barrel", (0, .025, .47), .018, .30, 10, "z", "Metal")
add_prism("Muzzle_Brake", (0, .025, .635), .032, .07, 8, "z", "DarkMetal")
add_prism("Muzzle_Opening", (0, .025, .674), .019, .006, 12, "z", "Black")
add_box("Gas_Block", (0, .065, .37), (.065, .07, .055), "DarkMetal")

# Pistol grip and trigger guard, angled silhouette approximated with stepped forms.
add_box("Grip_Upper", (0, -.09, -.035), (.075, .12, .075), "Rubber")
add_box("Grip_Lower", (0, -.175, -.075), (.07, .09, .075), "Rubber")
add_box("Trigger_Guard_Front", (0, -.075, .045), (.012, .075, .018), "Metal")
add_box("Trigger_Guard_Bottom", (0, -.11, .005), (.012, .015, .095), "Metal")
add_box("Trigger", (0, -.075, -.005), (.012, .055, .012), "Accent")

# Detachable magazine with a slight stepped curve.
add_box("Magazine_Top", (0, -.075, -.165), (.075, .08, .085), "DarkMetal")
add_box("Magazine_Body", (0, -.145, -.185), (.07, .09, .08), "DarkMetal")
add_box("Magazine_Base", (0, -.198, -.17), (.078, .025, .09), "Accent")

# FPS-facing details, mirrored enough for world readability.
for side in (-1, 1):
    x = side * .055
    add_box(f"Side_Panel_{side}", (x, .025, .045), (.012, .08, .205), "DarkMetal")
    add_box(f"Accent_Stripe_{side}", (x + side*.007, .055, .105), (.008, .018, .12), "Accent")
    add_prism(f"Receiver_Bolt_{side}_A", (x + side*.009, .075, -.035), .008, .007, 8, "x", "Metal")
    add_prism(f"Receiver_Bolt_{side}_B", (x + side*.009, .075, .105), .008, .007, 8, "x", "Metal")
    for n, z in enumerate((.225, .27, .315, .36)):
        add_box(f"Vent_{side}_{n}", (x, .04, z), (.014, .025, .022), "Black")

# Top rail, sights, charging handle and small emissive status strip.
add_box("Top_Rail", (0, .145, .075), (.07, .025, .33), "Metal")
for n, z in enumerate((-.06, -.01, .04, .09, .14, .19)):
    add_box(f"Rail_Slot_{n}", (0, .164, z), (.074, .012, .018), "Black")
add_box("Rear_Sight", (0, .19, -.075), (.065, .07, .035), "DarkMetal")
add_box("Front_Sight", (0, .18, .225), (.05, .06, .025), "DarkMetal")
add_box("Charging_Handle", (-.075, .105, -.035), (.055, .025, .035), "Metal")
add_box("Status_Light", (-.061, .02, .14), (.008, .016, .065), "EmissiveRed")

# MK II replaces the first version's exterior kit instead of stacking on top
# of it. Keep the structural receiver, stock, controls and barrel only.
replaced_exact = {
    "Receiver_Upper",
    "Handguard",
    "Handguard_Lower",
    "Top_Rail",
    "Rear_Sight",
    "Front_Sight",
    "Status_Light",
}
replaced_prefixes = (
    "Side_Panel_",
    "Accent_Stripe_",
    "Vent_",
    "Rail_Slot_",
)
groups = [
    group for group in groups
    if group[0] not in replaced_exact
    and not group[0].startswith(replaced_prefixes)
]

# MK II layered armour and silhouette treatment.
add_box("MK2_Receiver_Spine", (0, .112, .055), (.078, .042, .29), "DarkMetal")
add_wedge("MK2_Handguard_Core", (0, .015, .29), (.078, .095, .19), "top_front", "DarkMetal")
add_wedge("Upper_Shroud", (0, .135, .285), (.098, .07, .22), "top_front", "Graphite")
add_wedge("Stock_Cheek_Riser", (0, .105, -.285), (.09, .055, .18), "bottom_front", "Rubber")
add_wedge("Lower_Receiver_Fairing", (0, -.055, .135), (.095, .065, .17), "bottom_front", "Graphite")
for side in (-1, 1):
    x = side * .061
    add_wedge(f"Receiver_Armour_{side}", (x, .035, -.075), (.016, .095, .19), "top_front", "Graphite")
    add_wedge(f"Handguard_Armour_{side}", (x*.82, .02, .29), (.014, .09, .18), "bottom_front", "Graphite")
    add_box(f"Ejection_Frame_{side}", (x + side*.006, .055, -.015), (.009, .052, .095), "Metal")
    add_box(f"Selector_Plate_{side}", (x + side*.011, -.005, -.085), (.006, .04, .07), "DarkMetal")
    add_prism(f"Selector_Dial_{side}", (x + side*.016, .008, -.085), .017, .008, 12, "x", "Accent")
    for n, z in enumerate((-.315, -.275, -.235)):
        add_box(f"Stock_Rib_{side}_{n}", (side*.052, .025, z), (.012, .07, .018), "DarkMetal")
    for n, z in enumerate((.225, .26, .295, .33, .365)):
        add_box(f"Mlok_Recess_{side}_{n}", (side*.051, -.012, z), (.009, .018, .026), "Black")
    add_box(f"Cable_Channel_{side}", (side*.064, .09, .245), (.009, .018, .12), "Accent")

# Detailed magazine: reinforced spine, witness marks and release controls.
add_box("Magazine_Spine", (0, -.145, -.229), (.076, .10, .016), "Metal")
for n, y in enumerate((-.11, -.145, -.18)):
    add_box(f"Magazine_Witness_{n}", (-.039, y, -.184), (.006, .013, .042), "EmissiveRed")
add_box("Magazine_Release", (-.061, -.06, -.125), (.025, .022, .036), "Accent")

# Collimator sight with protected glass and brightness controls.
add_box("Optic_Mount", (0, .183, .045), (.068, .035, .105), "DarkMetal")
add_wedge("Optic_Housing", (0, .225, .055), (.078, .075, .095), "top_front", "Graphite")
add_box("Optic_Glass_Front", (0, .23, .107), (.055, .045, .006), "OpticGlass")
add_box("Optic_Glass_Rear", (0, .23, .003), (.055, .045, .006), "OpticGlass")
add_prism("Optic_Dial", (.048, .23, .055), .015, .018, 12, "x", "Accent")
add_box("Optic_Reticle", (0, .23, .101), (.004, .018, .003), "EmissiveRed")

# More convincing multi-stage muzzle device and barrel hardware.
add_prism("Barrel_Collar_Rear", (0, .025, .405), .03, .035, 12, "z", "Metal")
add_prism("Barrel_Collar_Front", (0, .025, .545), .027, .025, 12, "z", "Metal")
for n, angle in enumerate((0, pi/2, pi, 3*pi/2)):
    x = .026*cos(angle)
    y = .025 + .026*sin(angle)
    add_box(f"Muzzle_Port_{n}", (x, y, .64), (.018, .018, .038), "Black")

# Foregrip, sling points, tactile fasteners and underside accessory rail.
add_wedge("Angled_Foregrip", (0, -.125, .285), (.065, .115, .09), "top_front", "Rubber")
add_box("Under_Rail", (0, -.095, .23), (.06, .018, .17), "Metal")
for n, z in enumerate((.18, .22, .26, .30)):
    add_box(f"Under_Rail_Slot_{n}", (0, -.108, z), (.064, .009, .014), "Black")
add_prism("Rear_Sling_Loop", (-.055, .075, -.345), .018, .012, 10, "x", "Metal")
add_prism("Front_Sling_Loop", (-.05, .085, .345), .015, .012, 10, "x", "Metal")
for side in (-1, 1):
    for n, z in enumerate((-.35, -.24, -.12, .02, .15, .255, .345)):
        add_prism(f"Fastener_{side}_{n}", (side*.061, .075 if z < .2 else .055, z), .006, .006, 10, "x", "Metal")

# Attachment markers are visible tiny triangles in the mesh and named objects after import.
add_prism("Muzzle", (0, .025, .68), .004, .008, 6, "z", "EmissiveRed")
add_prism("Grip", (0, -.14, -.05), .003, .008, 6, "y", "EmissiveRed")
add_prism("Magazine", (0, -.15, -.18), .003, .008, 6, "y", "EmissiveRed")

obj = ["# SR-7 Breacher MK II detailed FPS rifle", "mtllib SR7_Breacher_MK2.mtl", "s off"]
obj.extend(f"v {x:.6f} {y:.6f} {z:.6f}" for x,y,z in vertices)
for name, material, group_faces in groups:
    obj.extend((f"o {name}", f"usemtl {material}"))
    obj.extend("f " + " ".join(map(str, face)) for face in group_faces)

mtl = """# SR-7 material palette
newmtl Graphite
Kd 0.055 0.065 0.075
Ks 0.18 0.18 0.18
Ns 90
newmtl DarkMetal
Kd 0.018 0.022 0.028
Ks 0.42 0.42 0.42
Ns 180
newmtl Metal
Kd 0.16 0.18 0.20
Ks 0.55 0.55 0.55
Ns 240
newmtl Rubber
Kd 0.025 0.025 0.026
Ks 0.04 0.04 0.04
Ns 18
newmtl Accent
Kd 0.55 0.025 0.018
Ks 0.28 0.08 0.06
Ns 110
newmtl Black
Kd 0.003 0.003 0.004
Ks 0.02 0.02 0.02
Ns 8
newmtl EmissiveRed
Kd 1.0 0.015 0.005
Ke 2.0 0.01 0.003
Ns 40
newmtl OpticGlass
Kd 0.02 0.09 0.11
Ks 0.75 0.85 0.88
Ns 420
d 0.42
"""

(OUT / "SR7_Breacher_MK2.obj").write_text("\n".join(obj) + "\n", encoding="ascii")
(OUT / "SR7_Breacher_MK2.mtl").write_text(mtl, encoding="ascii")
print(f"Generated {OUT / 'SR7_Breacher_MK2.obj'}: {len(vertices)} vertices, {sum(len(g[2]) for g in groups)} faces")
