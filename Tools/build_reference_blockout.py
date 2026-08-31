import bpy
import math
from pathlib import Path
from mathutils import Vector

ROOT = Path(r"A:\UnityProjects\BREACHPOINT - FPS")
OUT = ROOT / "Assets" / "Project" / "Art" / "Weapons" / "ReferenceRifle"
OUT.mkdir(parents=True, exist_ok=True)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)


def mat(name, color, metallic=0.0, roughness=0.4):
    material = bpy.data.materials.new(name)
    material.diffuse_color = (*color, 1.0)
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*color, 1.0)
    shader.inputs["Metallic"].default_value = metallic
    shader.inputs["Roughness"].default_value = roughness
    return material


GUNMETAL = mat("Blockout Gunmetal", (0.075, 0.082, 0.09), 0.75, 0.27)
POLYMER = mat("Blockout Polymer", (0.025, 0.028, 0.032), 0.05, 0.52)
ACCENT = mat("Blockout Accent", (0.9, 0.22, 0.015), 0.35, 0.25)


def profile(name, points, width, material, bevel=0.006):
    """Extrude a clockwise Y/Z side silhouette across local X."""
    half = width * 0.5
    count = len(points)
    vertices = [(-half, y, z) for y, z in points]
    vertices += [(half, y, z) for y, z in points]
    faces = [tuple(range(count)), tuple(reversed(range(count, count * 2)))]
    for index in range(count):
        next_index = (index + 1) % count
        faces.append((index, count + index, count + next_index, next_index))
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(material)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    modifier = obj.modifiers.new("Blockout Bevel", "BEVEL")
    modifier.width = bevel
    modifier.segments = 2
    modifier.limit_method = "ANGLE"
    modifier.use_clamp_overlap = True
    return obj


def cube(name, location, dimensions, material, bevel=0.004, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material)
    modifier = obj.modifiers.new("Blockout Bevel", "BEVEL")
    modifier.width = bevel
    modifier.segments = 2
    modifier.limit_method = "ANGLE"
    modifier.use_clamp_overlap = True
    return obj


def cylinder(name, location, radius, depth, material, vertices=20, rotation=(math.pi/2, 0, 0)):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices, radius=radius, depth=depth,
        location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(material)
    modifier = obj.modifiers.new("Blockout Bevel", "BEVEL")
    modifier.width = min(radius * 0.12, 0.003)
    modifier.segments = 2
    return obj


# Reference proportions: muzzle at +Y, stock at -Y, top approximately Z=.15.
profile("UpperReceiver", [
    (-.38, .15), (-.29, .18), (.18, .18), (.30, .145),
    (.32, .075), (.26, .025), (-.17, .035), (-.34, .065)
], .105, GUNMETAL, .008)

profile("TopBarrelHousing", [
    (.05, .12), (.39, .13), (.45, .095), (.43, .02),
    (.30, -.005), (.02, .02), (-.04, .065)
], .092, GUNMETAL, .007)

# Open stock frame assembled from shaped structural members, preserving the
# large negative spaces that dominate the supplied design.
profile("StockUpperFrame", [
    (-.64, .11), (-.37, .16), (-.23, .14), (-.27, .085),
    (-.47, .075), (-.62, .045), (-.68, .065)
], .10, GUNMETAL, .008)
profile("StockRearFrame", [
    (-.69, .06), (-.63, .11), (-.60, -.10), (-.65, -.18),
    (-.70, -.145), (-.73, -.01)
], .105, GUNMETAL, .009)
profile("StockLowerFrame", [
    (-.67, -.19), (-.58, -.12), (-.35, -.16), (-.18, -.30),
    (-.25, -.34), (-.48, -.255), (-.64, -.24)
], .095, GUNMETAL, .008)
profile("StockSidePlate", [
    (-.71, .00), (-.58, .015), (-.48, -.075), (-.56, -.19),
    (-.69, -.15), (-.75, -.055)
], .112, GUNMETAL, .006)

# Ergonomic pistol grip inside the stock opening.
profile("PistolGrip", [
    (-.49, .015), (-.40, .025), (-.37, -.035), (-.42, -.19),
    (-.48, -.225), (-.525, -.19), (-.54, -.045)
], .068, POLYMER, .009)
profile("Trigger", [
    (-.365,.005),(-.337,.002),(-.334,-.032),(-.349,-.064),
    (-.366,-.078),(-.378,-.068),(-.360,-.035)
], .014, ACCENT, .0025)

# Trigger guard is built from connected shaped members, leaving a clean open
# finger volume instead of a floating trigger in empty space.
profile("TriggerGuardFront", [
    (-.315,.026),(-.278,.018),(-.284,-.078),(-.305,-.098),(-.324,-.086)
], .034, GUNMETAL, .004)
profile("TriggerGuardLower", [
    (-.302,-.098),(-.395,-.112),(-.445,-.090),(-.452,-.066),
    (-.433,-.054),(-.397,-.082),(-.312,-.073)
], .034, GUNMETAL, .004)
profile("TriggerGuardRearMount", [
    (-.452,-.066),(-.430,-.050),(-.421,.005),(-.445,.020),(-.466,-.020)
], .038, GUNMETAL, .004)
cylinder("TriggerPivot", (0,-.368,.004), .010, .052, GUNMETAL, 16, (0,math.pi/2,0))
for side in (-1,1):
    cylinder(f"TriggerPivotCap_{side}", (side*.031,-.368,.004),
             .007,.007,ACCENT,14,(0,math.pi/2,0))

# Large curved magazine, intentionally dominant like the reference.
profile("Magazine", [
    (-.33, -.13), (-.18, -.10), (-.095, -.19), (-.07, -.36),
    (-.10, -.54), (-.19, -.63), (-.29, -.57), (-.37, -.40), (-.40, -.23)
], .09, GUNMETAL, .010)

# Central energy/chamber ring and straight barrel axis.
cylinder("MainBarrel", (0, .54, .025), .045, .78, GUNMETAL, 24)
cylinder("ChamberRing", (0, -.02, .025), .086, .135, GUNMETAL, 24)
cylinder("ChamberCore", (0, -.02, .025), .066, .145, POLYMER, 20)
for index, angle in enumerate((0, math.pi/2, math.pi, 3*math.pi/2)):
    cube(f"ChamberAccent_{index}",
         (0.061*math.cos(angle), -.02, .035+0.061*math.sin(angle)),
         (.014, .095, .025), ACCENT, .003, (0, angle, 0))

# Long slotted muzzle device.
cylinder("MuzzleBody", (0, .96, .025), .050, .18, GUNMETAL, 32)

# Lower accessory module and connecting armour plate.
profile("LowerArmour", [
    (-.22,-.065),(.035,-.055),(.18,-.105),(.13,-.285),(-.035,-.34),(-.22,-.25)
], .096, GUNMETAL, .008)
profile("UnderbarrelModule", [
    (.13,-.11),(.37,-.10),(.44,-.15),(.405,-.265),(.22,-.285),(.12,-.225)
], .082, GUNMETAL, .008)
profile("LowerModuleBridge", [
    (.02,-.055),(.22,-.05),(.27,-.105),(.18,-.16),(.045,-.145)
], .074, POLYMER, .006)
cylinder("UnderbarrelEmitter", (0, .425, -.19), .032, .065, GUNMETAL, 18)

# Coarse sight and rail landmarks for silhouette approval.
cube("TopRail", (0, -.02, .198), (.064,.62,.024), GUNMETAL, .003)

# Detail pass 1: layered side armour and the distinctive reference language.
visible_x = .058
upper_plate = profile("VisibleUpperPlate", [
    (-.34,.135),(-.22,.155),(.12,.155),(.25,.125),(.26,.075),
    (.18,.045),(-.12,.055),(-.29,.075)
], .008, GUNMETAL, .002)
upper_plate.location.x = visible_x

lower_plate = profile("VisibleLowerPlate", [
    (-.19,-.09),(.015,-.08),(.12,-.135),(.075,-.285),(-.045,-.315),(-.18,-.24)
], .008, GUNMETAL, .002)
lower_plate.location.x = visible_x

stock_plate = profile("VisibleStockPlate", [
    (-.69,-.015),(-.58,.005),(-.49,-.08),(-.57,-.18),(-.68,-.145),(-.73,-.06)
], .008, GUNMETAL, .002)
stock_plate.location.x = visible_x + .004

# Three diagonal receiver vents and matching stock engravings.
for index, y in enumerate((.02,.075,.13)):
        cube(f"ReceiverVent_{index}", (visible_x+.012,y,.082), (.016,.035,.015), POLYMER, .003,
         (math.radians(18),0,0))
for index, y in enumerate((-.61,-.565,-.52)):
    cube(f"StockGroove_{index}", (visible_x+.008,y,-.075), (.007,.025,.012), POLYMER, .002,
         (math.radians(-18),0,0))

# Chamber cage with luminous inserts, matching the key focal point.
for index, z in enumerate((-.005,.035,.075)):
    cube(f"ChamberLight_{index}", (visible_x+.018,-.02,z), (.014,.075,.012), ACCENT, .002)
for y in (-.07,.03):
    cylinder(f"ChamberCollar_{y}", (0,y,.035), .074, .012, GUNMETAL, 24)

# Picatinny teeth along the upper rail.
for index in range(12):
    y = -.27 + index * .05
    cube(f"RailTooth_{index}", (0,y,.216), (.07,.024,.015), GUNMETAL, .002)

# Fasteners on the visible side.
bolt_positions = [
    (-.31,.105),(-.17,.10),(.17,.09),(.285,.075),
    (-.66,-.06),(-.57,-.13),(-.22,-.20),(.03,-.17)
]
for index, (y,z) in enumerate(bolt_positions):
    cylinder(f"SideBolt_{index}", (visible_x+.012,y,z), .008, .007, GUNMETAL, 14, (0,math.pi/2,0))

# Mirror the approved visible-side treatment to the opposite side. The detail
# pieces are symmetric across X, so duplicating their transforms produces an
# identical layout without introducing negative object scale for Unity.
mirror_prefixes = (
    "VisibleUpperPlate", "VisibleLowerPlate", "VisibleStockPlate",
    "ReceiverVent_", "StockGroove_", "MagazineRib_",
    "ChamberLight_", "SideBolt_",
)
side_details = [
    obj for obj in list(bpy.context.scene.objects)
    if obj.type == "MESH" and obj.name.startswith(mirror_prefixes)
]
for source in side_details:
    mirrored = source.copy()
    mirrored.data = source.data.copy()
    mirrored.name = source.name + "_Opposite"
    mirrored.location.x = -source.location.x
    bpy.context.collection.objects.link(mirrored)

# Detail pass 4: true radial chamber cage around the barrel axis.
for index in range(8):
    angle = index * math.tau / 8.0
    radius = .077
    x = radius * math.cos(angle)
    z = .025 + radius * math.sin(angle)
    rib = cube(
        f"ChamberCageRib_{index}", (x, -.02, z),
        (.025, .105, .038), GUNMETAL, .004,
        (0, angle, 0))
    if index in (0, 2, 4, 6):
        light = cube(
            f"ChamberCageGlow_{index}",
            (x * 1.035, -.071, .025 + (z-.025)*1.035),
            (.013, .012, .024), ACCENT, .002,
            (0, angle, 0))

# Layered upper shroud strips and recessed cooling slots on both sides.
for side in (-1, 1):
    side_x = side * .069
    spine = profile(f"UpperSpineLayer_{side}", [
        (-.30,.145),(-.20,.17),(.16,.17),(.25,.135),(.22,.115),(-.24,.12)
    ], .009, GUNMETAL, .002)
    spine.location.x = side_x
    for index, y in enumerate((-.04,.025,.09)):
        cube(f"CoolingSlot_{side}_{index}", (side_x+side*.006,y,.105),
             (.008,.038,.014), POLYMER, .003, (math.radians(20),0,0))

# Raised magazine armour with inset channels on both faces.
for side in (-1, 1):
    mag_x = side * .052
    plate = profile(f"MagazineFace_{side}", [
        (-.325,-.17),(-.19,-.14),(-.12,-.22),(-.105,-.39),
        (-.15,-.55),(-.225,-.585),(-.31,-.49),(-.37,-.29)
    ], .012, GUNMETAL, .003)
    plate.location.x = mag_x
    channel_shapes = (
        [(-.315,-.22),(-.270,-.21),(-.205,-.34),(-.225,-.49),(-.248,-.50),(-.232,-.35)],
        [(-.260,-.19),(-.215,-.18),(-.150,-.33),(-.170,-.51),(-.192,-.52),(-.177,-.34)],
        [(-.205,-.17),(-.165,-.17),(-.110,-.31),(-.125,-.47),(-.145,-.50),(-.135,-.32)],
    )
    for index, shape in enumerate(channel_shapes):
        channel = profile(f"MagazineChannel_{side}_{index}", shape, .008, POLYMER, .002)
        channel.location.x = side * .061
    for index, (y,z) in enumerate(((-.285,-.285),(-.230,-.365),(-.180,-.435))):
        cube(f"MagazineWitness_{side}_{index}", (side*.066,y,z),
             (.009,.030,.010), ACCENT,.002,(math.radians(12),0,0))

# Feed neck and reinforced floor plate are structural parts of the magazine.
profile("MagazineFeedNeck", [
    (-.355,-.085),(-.170,-.080),(-.135,-.125),(-.175,-.165),
    (-.325,-.165),(-.375,-.120)
], .084, POLYMER, .006)
profile("MagazineFloorPlate", [
    (-.295,-.565),(-.205,-.635),(-.150,-.610),(-.105,-.535),
    (-.125,-.515),(-.205,-.595),(-.285,-.545)
], .100, POLYMER, .006)

# Grip contour pads and finger channels, mirrored for equal FPS readability.
for side in (-1, 1):
    pad = profile(f"GripPad_{side}", [
        (-.505,-.025),(-.43,-.02),(-.405,-.07),(-.445,-.185),
        (-.48,-.205),(-.515,-.17),(-.53,-.07)
    ], .009, POLYMER, .003)
    pad.location.x = side * .039
    for index, z in enumerate((-.075,-.115,-.155)):
        cylinder(f"GripGroove_{side}_{index}", (side*.045,-.46,z),
                 .009, .008, GUNMETAL, 12, (0,math.pi/2,0))

# Stock side inset echoes the large framed cutout from the reference.
for side in (-1, 1):
    inset = profile(f"StockInset_{side}", [
        (-.675,-.015),(-.59,.005),(-.515,-.075),(-.58,-.155),(-.66,-.13),(-.705,-.055)
    ], .007, POLYMER, .002)
    inset.location.x = side * .066

# Detail pass 5: recognizable folding sights. Torus axes face both weapon sides.
def torus(name, location, major_radius, minor_radius, material, segments=24):
    bpy.ops.mesh.primitive_torus_add(
        major_radius=major_radius, minor_radius=minor_radius,
        major_segments=segments, minor_segments=12,
        location=location, rotation=(math.pi/2, 0, 0))
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(material)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    return obj

# Rear aperture: raised octagonal ring with a clear sight channel beneath it.
torus("RearSightAperture", (0,-.265,.298), .036, .005, GUNMETAL, 32)
for side in (-1, 1):
    cube(f"RearSightLeg_{side}", (side*.039,-.265,.251),
         (.009,.018,.060), GUNMETAL, .002)
cube("RearSightBase", (0,-.265,.221), (.088,.032,.016), GUNMETAL, .003)

# Front sight based on the supplied reference: broad shoulders, two curved
# protective ears, a rounded central post, and a small triangular marker.
def sight_profile(name, points, y, depth, material, bevel=.0025):
    half = depth * .5
    count = len(points)
    vertices = [(x,y-half,z) for x,z in points] + [(x,y+half,z) for x,z in points]
    faces = [tuple(range(count)), tuple(reversed(range(count,count*2)))]
    for index in range(count):
        nxt = (index+1) % count
        faces.append((index,nxt,count+nxt,count+index))
    mesh = bpy.data.meshes.new(name+"Mesh")
    mesh.from_pydata(vertices,[],faces)
    mesh.materials.append(material)
    obj = bpy.data.objects.new(name,mesh)
    bpy.context.collection.objects.link(obj)
    modifier = obj.modifiers.new("Sight Bevel","BEVEL")
    modifier.width = bevel
    modifier.segments = 3
    modifier.limit_method = "ANGLE"
    modifier.use_clamp_overlap = True
    return obj

left_ear = [
    (-.056,.210),(-.020,.210),(-.019,.231),(-.027,.242),
    (-.034,.258),(-.045,.258),(-.043,.243),(-.055,.232)
]
right_ear = [(-x,z) for x,z in reversed(left_ear)]
sight_profile("FrontSightLeftEar",left_ear,.255,.034,GUNMETAL,.003)
sight_profile("FrontSightRightEar",right_ear,.255,.034,GUNMETAL,.003)
sight_profile("FrontSightShoulders",[
    (-.064,.205),(.064,.205),(.060,.228),(.049,.239),(.034,.238),
    (.025,.226),(-.025,.226),(-.034,.238),(-.049,.239),(-.060,.228)
],.255,.040,GUNMETAL,.004)
sight_profile("FrontSightPost",[
    (-.011,.222),(.011,.222),(.013,.258),(.009,.274),(0,.282),
    (-.009,.274),(-.013,.258)
],.255,.018,POLYMER,.003)

def sight_bar(name, point_a, point_b, y, thickness, material):
    ax,az = point_a
    bx,bz = point_b
    dx,dz = bx-ax,bz-az
    length = math.sqrt(dx*dx+dz*dz)
    angle = -math.atan2(dz,dx)
    return cube(name,((ax+bx)*.5,y,(az+bz)*.5),(length,.012,thickness),
                material,.0015,(0,angle,0))

sight_profile("FrontSightSolidMarker",[
    (-.014,.266),(.014,.266),(0,.290)
],.237,.012,ACCENT,.0015)

# Multi-stage muzzle shell, collars, and identical side port treatment.
cylinder("MuzzleRearCollar", (0,.865,.025), .055, .028, GUNMETAL, 24)
cylinder("MuzzleFrontCollar", (0,1.038,.025), .055, .024, GUNMETAL, 24)

# Four structural rails are partially embedded into the cylindrical shell and
# captured by both collars, so no element floats above the muzzle.
for index, angle in enumerate((0, math.pi/2, math.pi, 3*math.pi/2)):
    rail_radius = .046
    x = rail_radius * math.cos(angle)
    z = .025 + rail_radius * math.sin(angle)
    cube(f"MuzzleStructuralRail_{index}", (x,.957,z),
         (.016,.160,.016), GUNMETAL,.003,(0,angle,0))

# Separate recessed bore sits just beyond every front-facing surface. Its rear
# face starts after the shell and collar end, preventing coplanar z-fighting.
cylinder("MuzzleBoreRecess", (0,1.056,.025), .037, .010, POLYMER, 32)
cylinder("MuzzleInnerBore", (0,1.062,.025), .026, .006, POLYMER, 32)

# Detail pass 12: barrel transitions and serviceable mechanical interfaces.
for index, y in enumerate((.205,.445,.705,.848)):
    cylinder(f"BarrelSupportCollar_{index}", (0,y,.025),
             .050 if index < 3 else .048, .018, GUNMETAL, 28)

# Reinforced chamber-to-receiver bridge removes the visually weak gap around
# the central cylinder and makes the barrel assembly feel load-bearing.
profile("ChamberUpperBridge", [
    (-.105,.105),(.075,.105),(.135,.075),(.105,.035),(-.09,.04),(-.13,.07)
], .080, GUNMETAL, .006)
profile("ChamberLowerBridge", [
    (-.115,-.04),(.075,-.04),(.12,-.075),(.07,-.115),(-.10,-.10),(-.14,-.07)
], .076, GUNMETAL, .006)

# Symmetric access plates, selector controls and recessed panel seams.
for side in (-1, 1):
    side_x = side * .064
    cylinder(f"SafetySelector_{side}", (side*.071,-.12,.035),
             .018,.010,GUNMETAL,18,(0,math.pi/2,0))
    cube(f"SafetyLever_{side}", (side*.078,-.105,.047),
         (.007,.040,.009), ACCENT,.002,(math.radians(25),0,0))
    cylinder(f"ChargingHandlePivot_{side}", (side*.069,.145,.105),
             .011,.010,GUNMETAL,16,(0,math.pi/2,0))

    # Lower module ventilation is inset into its side plate, not suspended.
    for index, y in enumerate((.215,.270,.325)):
        cube(f"LowerVent_{side}_{index}", (side*.055,y,-.190),
             (.008,.030,.012), POLYMER,.003,(math.radians(16),0,0))

# Shoulder contact and stock hinge hardware.
profile("ButtContactPad", [
    (-.735,.055),(-.690,.070),(-.665,.025),(-.675,-.105),
    (-.705,-.155),(-.745,-.120),(-.760,-.015)
], .108, POLYMER, .007)
for side in (-1,1):
    cylinder(f"StockHinge_{side}", (side*.060,-.595,-.045),
             .022,.012,GUNMETAL,20,(0,math.pi/2,0))
    cylinder(f"StockHingePin_{side}", (side*.068,-.595,-.045),
             .008,.006,ACCENT,14,(0,math.pi/2,0))

# Layered underbarrel equipment housing and forward emitter.
for side in (-1, 1):
    module_plate = profile(f"UnderbarrelPlate_{side}", [
        (.17,-.13),(.36,-.12),(.405,-.165),(.375,-.245),(.235,-.26),(.155,-.215)
    ], .009, GUNMETAL, .003)
    module_plate.location.x = side * .048
    cube(f"UnderbarrelRail_{side}", (side*.046,.29,-.105),
         (.012,.20,.018), POLYMER, .003)
    for index, y in enumerate((.22,.28,.34)):
        cylinder(f"UnderbarrelBolt_{side}_{index}", (side*.055,y,-.18),
                 .006,.006,GUNMETAL,12,(0,math.pi/2,0))
cylinder("UnderbarrelEmitterCollar", (0,.40,-.19), .043, .026, GUNMETAL, 20)
cylinder("UnderbarrelEmitterLens", (0,.462,-.19), .026, .012, ACCENT, 20)

# Mechanical transitions around chamber and magazine feed area.
cylinder("ChamberRearLock", (0,-.105,.025), .075, .028, GUNMETAL, 24)
profile("FeedHousing", [
    (-.25,-.06),(-.12,-.06),(-.075,-.12),(-.11,-.20),(-.25,-.18),(-.285,-.115)
], .088, GUNMETAL, .007)
for side in (-1,1):
    cylinder(f"FeedRelease_{side}", (side*.052,-.15,-.105),
             .014,.009,ACCENT,16,(0,math.pi/2,0))

# Force consistent outward normals for every generated closed mesh.
for obj in [item for item in bpy.context.scene.objects if item.type == "MESH"]:
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode="OBJECT")

for name, location in {
    "MuzzleSocket": (0, 1.068, .025),
    "LeftHandGrip": (0, .30, -.20),
    "MagazineSocket": (0, -.24, -.15),
    "ShellEjection": (-.06, -.14, .10),
}.items():
    empty = bpy.data.objects.new(name, None)
    empty.empty_display_type = "PLAIN_AXES"
    empty.empty_display_size = .03
    empty.location = location
    bpy.context.collection.objects.link(empty)

blend_path = OUT / "ReferenceRifle_DetailPass15.blend"
fbx_path = OUT / "ReferenceRifle_DetailPass15.fbx"
bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.fbx(
    filepath=str(fbx_path), use_selection=True,
    axis_forward="-Z", axis_up="Y",
    apply_scale_options="FBX_SCALE_UNITS",
    use_mesh_modifiers=True, mesh_smooth_type="FACE",
    add_leaf_bones=False, bake_anim=False)
print(f"Built {blend_path}")
print(f"Exported {fbx_path}")

# Side-view validation render, matching the supplied reference angle.
bpy.ops.object.camera_add(location=(3.2, .12, -.08))
camera = bpy.context.object
camera.name = "PreviewCamera"
target = Vector((0, .15, -.10))
camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
camera.data.type = "ORTHO"
camera.data.ortho_scale = 2.15
bpy.context.scene.camera = camera

for location, energy, size in [((2.0,.2,2.5),1100,3.0),((-1.5,.8,1.0),700,2.0)]:
    bpy.ops.object.light_add(type="AREA", location=location)
    light = bpy.context.object
    light.data.energy = energy
    light.data.shape = "DISK"
    light.data.size = size
    light.rotation_euler = (target - light.location).to_track_quat("-Z", "Y").to_euler()

scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1200
scene.render.resolution_y = 700
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.filepath = str(OUT / "ReferenceRifle_DetailPass15_Preview.png")
scene.world.color = (0.055, 0.055, 0.065)
bpy.ops.render.render(write_still=True)

# Matching opposite-side preview to verify symmetry before Unity import.
camera.location.x = -3.2
camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
scene.render.filepath = str(OUT / "ReferenceRifle_DetailPass15_Opposite_Preview.png")
bpy.ops.render.render(write_still=True)

# Aligned sight-picture validation matching the user's Unity screenshots.
camera.location = (0, -1.55, .300)
aim_target = Vector((0, .45, .300))
camera.rotation_euler = (aim_target - camera.location).to_track_quat("-Z", "Y").to_euler()
camera.data.type = "PERSP"
camera.data.lens = 70
scene.render.filepath = str(OUT / "ReferenceRifle_DetailPass15_AimPreview.png")
bpy.ops.render.render(write_still=True)
