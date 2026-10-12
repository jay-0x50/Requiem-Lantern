"""Build editable front-facing 2.5D illustration rigs. Run with Blender 5.2.

This is a textured planar mesh, not a volumetric 3D character. No source
artwork is modified. The alpha cutouts are made with built-in imagegen.
"""
import argparse
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / "Art" / "Blender"
CUTOUTS = ROOT / "Art" / "Characters" / "Cutouts"
SPECS = [
    {"id": "kai", "name": "카이", "height": 1.68, "role": "등불지기", "color": (0.949, 0.647, 0.255)},
    {"id": "lian", "name": "리안", "height": 1.72, "role": "광휘 / 수호자", "color": (0.949, 0.647, 0.255)},
    {"id": "sera", "name": "세라", "height": 1.63, "role": "화염 / 돌격수", "color": (0.83, 0.33, 0.19)},
    {"id": "minato", "name": "미나토", "height": 1.58, "role": "물결 / 치유사", "color": (0.35, 0.72, 0.71)},
    {"id": "noa", "name": "노아", "height": 1.76, "role": "그림자 / 조율사", "color": (0.58, 0.46, 0.75)},
    {"id": "heidel", "name": "하이델", "height": 1.52, "role": "서리 / 사수", "color": (0.40, 0.69, 0.88)},
    {"id": "orphe", "name": "오르페", "height": 1.88, "role": "종지기", "color": (0.75, 0.73, 0.66)},
]


def emission_material(name, rgba):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeEmission")
    shader.inputs["Color"].default_value = rgba
    material.node_tree.links.new(shader.outputs[0], output.inputs["Surface"])
    return material


def illustration_material(identifier, image):
    material = bpy.data.materials.new(identifier + " | original illustration + alpha")
    material.use_nodes = True
    material.surface_render_method = "DITHERED"
    nodes = material.node_tree.nodes
    nodes.clear()
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = image
    texture.interpolation = "Linear"
    texture.extension = "CLIP"
    texture.location = (-450, 140)
    emission = nodes.new("ShaderNodeEmission")
    emission.location = (-180, 100)
    transparent = nodes.new("ShaderNodeBsdfTransparent")
    transparent.location = (-180, -80)
    mix = nodes.new("ShaderNodeMixShader")
    mix.location = (80, 80)
    output = nodes.new("ShaderNodeOutputMaterial")
    output.location = (300, 80)
    links = material.node_tree.links
    links.new(texture.outputs["Color"], emission.inputs["Color"])
    links.new(texture.outputs["Alpha"], mix.inputs[0])
    links.new(transparent.outputs[0], mix.inputs[1])
    links.new(emission.outputs[0], mix.inputs[2])
    links.new(mix.outputs[0], output.inputs["Surface"])
    return material


def make_mesh(spec, collection):
    image = bpy.data.images.load(str(CUTOUTS / (spec["id"] + ".png")), check_existing=True)
    image.alpha_mode = "STRAIGHT"
    image.pack()
    # Image canvas preserves the original silhouette. Relative heights follow
    # the art guide; alpha margins mean the figures are approximately scaled.
    height = spec["height"]
    width = height * image.size[0] / image.size[1]
    nx, nz = 32, 48
    verts = [((i / nx - .5) * width, 0, j / nz * height)
             for j in range(nz + 1) for i in range(nx + 1)]
    faces = []
    for j in range(nz):
        for i in range(nx):
            a = j * (nx + 1) + i
            faces.append((a, a + 1, a + nx + 2, a + nx + 1))
    mesh = bpy.data.meshes.new(spec["id"] + "_illustration_grid")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(spec["id"] + "_2D_Illustration", mesh)
    collection.objects.link(obj)
    uv = mesh.uv_layers.new(name="Original_Artwork_UV")
    for poly in mesh.polygons:
        for index in poly.loop_indices:
            vertex = mesh.vertices[mesh.loops[index].vertex_index]
            uv.data[index].uv = (vertex.co.x / width + .5, vertex.co.z / height)
    obj.data.materials.append(illustration_material(spec["id"], image))
    obj["asset_type"] = "2.5D single-illustration deformable billboard; no side or back geometry"
    obj["source_cutout"] = "Art/Characters/Cutouts/" + spec["id"] + ".png"
    obj["art_direction"] = "Original face, silhouette, clothing and props preserved. Subtle motion only."
    return obj, width, height


def make_rig(spec, collection, obj, width, height):
    armature = bpy.data.armatures.new(spec["id"] + "_planar_rig")
    rig = bpy.data.objects.new(spec["id"] + "_RIG", armature)
    collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    rig.show_in_front = True
    bpy.ops.object.mode_set(mode="EDIT")
    positions = {
        "ROOT": ((0, 0, 0), (0, 0, .22 * height), None),
        "BREATH": ((0, 0, .43 * height), (0, 0, .77 * height), "ROOT"),
        "HEAD": ((0, 0, .82 * height), (0, 0, .96 * height), "BREATH"),
        "COAT_L": ((-.22 * width, 0, .52 * height), (-.34 * width, 0, .25 * height), "ROOT"),
        "COAT_R": ((.22 * width, 0, .52 * height), (.34 * width, 0, .25 * height), "ROOT"),
    }
    for name, (head, tail, parent) in positions.items():
        bone = armature.edit_bones.new(name)
        bone.head, bone.tail = head, tail
        if parent:
            bone.parent = armature.edit_bones[parent]
    bpy.ops.object.mode_set(mode="OBJECT")
    rig.select_set(False)
    groups = {name: obj.vertex_groups.new(name=name) for name in positions}
    for vertex in obj.data.vertices:
        x = vertex.co.x / width
        z = vertex.co.z / height
        head = max(0, min(1, (z - .79) / .08))
        breath = max(0, min(1, (z - .30) / .33)) * (1 - head)
        lateral = max(0, min(1, (abs(x) - .14) / .20))
        coat = lateral * max(0, 1 - abs(z - .35) / .25) * .70 * (1 - head)
        breath *= (1 - coat)
        root = max(0, 1 - head - breath - coat)
        weights = {"ROOT": root, "BREATH": breath, "HEAD": head,
                   "COAT_L" if x < 0 else "COAT_R": coat}
        total = sum(weights.values())
        for name, value in weights.items():
            if value > 0:
                groups[name].add([vertex.index], value / total, "REPLACE")
    modifier = obj.modifiers.new("Subtle illustration deformation", "ARMATURE")
    modifier.object = rig
    obj.parent = rig
    rig["controls"] = "ROOT, BREATH, HEAD, COAT_L, COAT_R. Fixed front camera only."
    return rig


def key(rig, frame, breath=0, lean=0, head=0, coat=0, recoil=0):
    for bone in rig.pose.bones:
        bone.rotation_mode = "XYZ"
        bone.location = (0, 0, 0)
        bone.rotation_euler = (0, 0, 0)
    # Local Y follows each bone's length; local Z rotates in the image plane.
    rig.pose.bones["BREATH"].location.y = breath
    rig.pose.bones["BREATH"].rotation_euler.z = lean
    rig.pose.bones["HEAD"].rotation_euler.z = head
    rig.pose.bones["COAT_L"].rotation_euler.z = coat
    rig.pose.bones["COAT_R"].rotation_euler.z = -coat * .75
    rig.pose.bones["ROOT"].location.x = recoil
    for bone in rig.pose.bones:
        bone.keyframe_insert(data_path="location", frame=frame, group=bone.name)
        bone.keyframe_insert(data_path="rotation_euler", frame=frame, group=bone.name)


def make_actions(spec, rig):
    rig.animation_data_create()
    actions = {}
    for name in ("Idle", "Resonance", "HitReact"):
        action = bpy.data.actions.new(spec["id"] + "_" + name)
        action.use_fake_user = True
        rig.animation_data.action = action
        if name == "Idle":
            for frame in range(1, 98, 6):
                phase = (frame - 1) * math.tau / 96
                key(rig, frame, breath=.004 * math.sin(phase),
                    lean=.002 * math.sin(phase), head=.0025 * math.sin(phase),
                    coat=.012 * math.sin(phase))
        elif name == "Resonance":
            for frame, t in ((1, 0), (9, -.25), (19, 1), (31, .35), (49, 0)):
                key(rig, frame, breath=.012*t, lean=-.006*t,
                    head=.009*t, coat=.022*t)
        else:
            for frame, t in ((1, 0), (4, 1), (10, -.25), (19, 0), (25, 0)):
                key(rig, frame, lean=.013*t, head=-.008*t,
                    coat=.025*t, recoil=-.025*t)
        actions[name] = action
    rig.animation_data.action = actions["Idle"]
    return actions


def setup_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.name = "Requiem Lantern | Character Atelier"
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 768
    scene.render.resolution_y = 1024
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = True
    scene.render.fps = 24
    scene.frame_start, scene.frame_end = 1, 96
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    scene.world = bpy.data.worlds.new("Atelier darkness")
    scene.world.color = (.025, .032, .048)
    camera_data = bpy.data.cameras.new("CAM_Front_Orthographic")
    camera = bpy.data.objects.new("CAM_Front_Orthographic", camera_data)
    scene.collection.objects.link(camera)
    camera.location = (0, -6, .96)
    camera.rotation_euler = (Vector((0, 0, .96)) - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 2.05
    scene.camera = camera
    scene["README"] = "Artwork billboards rigged for front-facing 2D presentation. NOT full 3D models."
    scene["animation"] = "24fps; Idle frames 1-96 loops (97 duplicates 1); Resonance 1-49; HitReact 1-25."
    scene["source"] = "Original art in image folders; generated alpha cutouts in Art/Characters/Cutouts."
    scene["integration"] = "Use transparent PNGs in Unity 2D. Blender materials are for authoring only."
    for frame, name in ((1, "IDLE START"), (25, "INHALE"), (49, "CENTER"), (73, "EXHALE"), (97, "LOOP SEAM")):
        scene.timeline_markers.new(name, frame=frame)
    return scene


def add_presentation_board(scene, records):
    collection = bpy.data.collections.new("PRESENTATION | hide for transparent exports")
    scene.collection.children.link(collection)
    font_path = Path("C:/Windows/Fonts/malgun.ttf")
    font = bpy.data.fonts.load(str(font_path)) if font_path.exists() else None
    cream = emission_material("Board | ivory lettering", (.80, .83, .85, 1))
    muted = emission_material("Board | muted lettering", (.32, .41, .48, 1))
    navy = emission_material("Board | midnight background", (.010, .017, .028, 1))
    mid = (len(records) - 1) * .8
    def panel(name, center_x, center_z, width, height, depth, material):
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata([(center_x-width/2, depth, center_z-height/2),
                          (center_x+width/2, depth, center_z-height/2),
                          (center_x+width/2, depth, center_z+height/2),
                          (center_x-width/2, depth, center_z+height/2)], [], [(0,1,2,3)])
        obj = bpy.data.objects.new(name, mesh)
        collection.objects.link(obj)
        obj.data.materials.append(material)
    def label(name, text, x, z, size, material, align="CENTER"):
        curve = bpy.data.curves.new(name, "FONT")
        curve.body, curve.size, curve.align_x = text, size, align
        if font:
            curve.font = font
        obj = bpy.data.objects.new(name, curve)
        collection.objects.link(obj)
        obj.location, obj.rotation_euler = (x, -.04, z), (math.pi/2, 0, 0)
        obj.data.materials.append(material)
    panel("Midnight", mid, .93, 18, 8, .25, navy)
    for index, (spec, _, _, rig, _) in enumerate(records):
        accent = emission_material(spec["id"] + " | accent", (*spec["color"], 1))
        x = rig.location.x
        panel(spec["id"] + " baseline", x, -.014, 1.3, .007, -.02, accent)
        label(spec["id"] + " name", spec["name"], x, -.16, .090, cream)
        label(spec["id"] + " role", spec["role"], x, -.255, .052, muted)
        label(spec["id"] + " height", str(round(spec["height"]*100)) + " cm", x, -.35, .046, muted)
    label("Title", "REQUIEM  LANTERN", mid, 2.22, .13, cream)
    label("Subtitle", "CHARACTER ATELIER  /  2.5D ILLUSTRATION RIGS  /  v0.1", mid, 2.09, .048, muted)
    label("Footnote", "원화 기반 정면 리그  ·  IDLE / RESONANCE / HIT REACT", mid, -.52, .045, muted)
    scene.camera.location.z = .90
    scene.camera.data.ortho_scale = max(3.1 * (2560 / 720), len(records) * 1.58)
    scene.render.film_transparent = False
    return collection


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--characters", default="all")
    parser.add_argument("--render", action="store_true")
    parser.add_argument("--idle-frames", action="store_true")
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
    selected = args.characters.split(",")
    specs = [spec for spec in SPECS if (args.characters == "all" or spec["id"] in selected)
             and (CUTOUTS / (spec["id"] + ".png")).exists()]
    if not specs:
        raise RuntimeError("No character cutout inputs found")
    scene = setup_scene()
    records = []
    for index, spec in enumerate(specs):
        collection = bpy.data.collections.new(spec["id"].upper() + " | " + spec["name"])
        scene.collection.children.link(collection)
        obj, width, height = make_mesh(spec, collection)
        rig = make_rig(spec, collection, obj, width, height)
        actions = make_actions(spec, rig)
        rig.location.x = index * 1.6
        records.append((spec, collection, obj, rig, actions))
    scene.frame_set(1)
    # Render each rig independently while retaining the complete cast scene.
    for spec, collection, obj, rig, actions in records:
        for other_spec, other_collection, other_obj, other_rig, _ in records:
            other_collection.hide_render = other_spec["id"] != spec["id"]
        scene.camera.location.x = rig.location.x
        scene.camera.location.z = spec["height"] / 2
        scene.camera.data.ortho_scale = spec["height"] * 1.08
        if args.render:
            scene.render.filepath = str(OUT / "Preview" / (spec["id"] + "_idle_01.png"))
            scene.frame_set(1)
            bpy.ops.render.render(write_still=True)
        if args.idle_frames:
            scene.render.resolution_x, scene.render.resolution_y = 384, 512
            folder = ROOT / "Art" / "Characters" / "IdleFrames" / spec["id"]
            folder.mkdir(parents=True, exist_ok=True)
            for index, frame in enumerate(range(1, 97, 12)):
                scene.frame_set(frame)
                scene.render.filepath = str(folder / (spec["id"] + f"_idle_{index:02d}.png"))
                bpy.ops.render.render(write_still=True)
            scene.render.resolution_x, scene.render.resolution_y = 768, 1024
        scene.frame_set(1)
    # Final authoring file opens with the whole cast in an orthographic lineup.
    for spec, collection, obj, rig, actions in records:
        collection.hide_render = False
    scene.camera.location.x = (len(records) - 1) * .8
    scene.camera.location.z = .97
    scene.camera.data.ortho_scale = max(2.25 * (2560 / 720), len(records) * 1.55)
    scene.render.resolution_x, scene.render.resolution_y = 2560, 720
    bpy.context.view_layer.objects.active = records[0][3]
    records[0][3].select_set(True)
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == "VIEW_3D":
                area.spaces.active.region_3d.view_perspective = "CAMERA"
                area.spaces.active.shading.type = "MATERIAL"
    suffix = "cast" if args.characters == "all" else "pilot"
    path = OUT / ("requiem_lantern_" + suffix + ".blend")
    bpy.ops.wm.save_as_mainfile(filepath=str(path))
    if args.render:
        scene.render.filepath = str(OUT / "Preview" / (suffix + "_lineup.png"))
        bpy.ops.render.render(write_still=True)
    board = add_presentation_board(scene, records)
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(path))
    if args.render:
        scene.render.filepath = str(OUT / "Preview" / (suffix + "_presentation.png"))
        bpy.ops.render.render(write_still=True)
    manifest = {"blend": path.name, "blender": bpy.app.version_string, "type": "2.5D deformable illustration billboard",
        "fps": 24, "idle_period_frames": 96, "camera": "front orthographic", "texture_alpha": "straight",
        "characters": [{**spec, "mesh_vertices": len(obj.data.vertices), "bones": len(rig.pose.bones),
                        "actions": {name: action.name for name, action in actions.items()},
                        "cutout": "Art/Characters/Cutouts/" + spec["id"] + ".png"}
                       for spec, _, obj, rig, actions in records]}
    (OUT / (suffix + "_manifest.json")).write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    print("CHARACTER_BUILD_OK", path, len(records))


if __name__ == "__main__":
    main()
