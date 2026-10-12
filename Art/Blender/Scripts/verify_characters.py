"""Reopen and verify the saved native file. Run in Blender background mode."""
import json
from pathlib import Path
import bpy

root = Path(__file__).resolve().parents[3]
native = root / "Art/Blender/requiem_lantern_cast.blend"
bpy.ops.wm.open_mainfile(filepath=str(native))
scene = bpy.context.scene
expected = ("kai", "lian", "sera", "minato", "noa", "heidel", "orphe")
report = {"file": str(native.relative_to(root)), "blender": bpy.app.version_string, "characters": []}
for identifier in expected:
    rig = bpy.data.objects[identifier + "_RIG"]
    obj = bpy.data.objects[identifier + "_2D_Illustration"]
    assert len(rig.pose.bones) == 5
    assert len(obj.data.vertices) == 1617
    assert all(abs(sum(group.weight for group in vertex.groups) - 1) < .0001 for vertex in obj.data.vertices)
    actions = [bpy.data.actions[identifier + "_" + name] for name in ("Idle", "Resonance", "HitReact")]
    assert all(action.use_fake_user for action in actions)
    rig.animation_data.action = actions[0]
    positions = {}
    for frame in (1, 25, 97):
        scene.frame_set(frame)
        depsgraph = bpy.context.evaluated_depsgraph_get()
        evaluated = obj.evaluated_get(depsgraph)
        mesh = evaluated.to_mesh()
        positions[frame] = [vertex.co.copy() for vertex in mesh.vertices]
        evaluated.to_mesh_clear()
    loop_delta = max((a-b).length for a,b in zip(positions[1], positions[97]))
    movement = max((a-b).length for a,b in zip(positions[1], positions[25]))
    feet_delta = max((positions[1][vertex.index]-positions[25][vertex.index]).length
                     for vertex in obj.data.vertices if vertex.co.z < .1)
    assert loop_delta < .000001, (identifier, loop_delta)
    assert movement > .001, (identifier, movement)
    assert feet_delta < .000001, (identifier, feet_delta)
    image_nodes = [node for node in obj.data.materials[0].node_tree.nodes if node.type == "TEX_IMAGE"]
    assert len(image_nodes) == 1 and image_nodes[0].image.packed_file
    report["characters"].append({"id": identifier, "bones": 5, "vertices": 1617,
        "actions": [action.name for action in actions], "texture_packed": True,
        "loop_seam_delta_m": loop_delta, "max_idle_movement_m": movement,
        "foot_movement_m": feet_delta})
report["result"] = "PASS"
scene.frame_set(1)
(root / "Art/Blender/validation.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
print("CHARACTER_VALIDATION_PASS", len(expected), "rigs, 35 bones, 21 actions, packed textures, closed loops, fixed feet")
