"""Build CC0 Tiby-derived police/robber visuals. Run with Blender in background mode."""
import bpy, math, os
from mathutils import Vector, Quaternion

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '../..'))
OUT = os.path.join(ROOT, 'Assets/MoraeGames/Art/SDCharacters')
PALETTE = {'Skin': (0.88, .56, .32, 1), 'Uniform': (.025, .24, .72, 1),
           'Pants': (.025, .045, .10, 1), 'Boots': (.035, .045, .065, 1),
           'Hair': (.055, .035, .025, 1), 'White': (.93, .96, 1, 1),
           'Ink': (.012, .018, .03, 1), 'Gold': (1, .65, .035, 1)}

def build(role):
    bpy.ops.wm.open_mainfile(filepath=os.path.join(ROOT, 'SourceAssets/Tiby/TIBY.blend'))
    rig = bpy.data.objects['Armature']
    body, hair = bpy.data.objects['Cube'], bpy.data.objects['Cube.001']
    for obj in list(bpy.data.objects):
        if obj not in (rig, body, hair): bpy.data.objects.remove(obj, do_unlink=True)
    rig.animation_data_clear()
    for action in list(bpy.data.actions): bpy.data.actions.remove(action)
    for bone in rig.pose.bones:
        for constraint in list(bone.constraints): bone.constraints.remove(constraint)
        bone.matrix_basis.identity()
        bone.rotation_mode = 'QUATERNION'
    for obj in (body, hair):
        bpy.context.view_layer.objects.active = obj
        for modifier in list(obj.modifiers):
            if modifier.type != 'ARMATURE': bpy.ops.object.modifier_apply(modifier=modifier.name)
    materials = {}
    for name, color in PALETTE.items():
        mat = bpy.data.materials.new('SD_' + name)
        mat.diffuse_color = ((1, .23, .035, 1) if name == 'Uniform' and role == 'Robber' else color)
        mat.use_nodes = True
        shader = next(n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
        shader.inputs['Base Color'].default_value = mat.diffuse_color
        shader.inputs['Roughness'].default_value = .72
        materials[name] = mat
    names = list(materials)
    body.data.materials.clear()
    for mat in materials.values(): body.data.materials.append(mat)
    for face in body.data.polygons:
        center = body.matrix_world @ face.center
        x, y, z = center
        kind = 'Skin'
        if z < .83: kind = 'Boots'
        elif z < 2.18: kind = 'Pants'
        elif z < 4.16 and abs(x) < 1.70: kind = 'Uniform'
        face.material_index = names.index(kind)
        face.use_smooth = True
    hair.data.materials.clear(); hair.data.materials.append(materials['Hair'])
    for face in hair.data.polygons: face.use_smooth = True
    bpy.data.objects.remove(hair, do_unlink=True)

    def part(name, position, scale, material, bone='Head', shape='sphere'):
        if shape == 'cube': bpy.ops.mesh.primitive_cube_add(size=2, location=position)
        else: bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, radius=1, location=position)
        obj = bpy.context.object; obj.name = name; obj.scale = scale
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        obj.data.materials.append(materials[material])
        for face in obj.data.polygons: face.use_smooth = shape != 'cube'
        group = obj.vertex_groups.new(name=bone)
        group.add(list(range(len(obj.data.vertices))), 1, 'REPLACE')
        modifier = obj.modifiers.new('Rig', 'ARMATURE'); modifier.object = rig
        return obj

    # Clear, exaggerated facial features visible from the overhead game camera.
    for sign in (-1, 1):
        part('SideHair', (sign*1.02, .25, 5.85), (.18, .73, .62), 'Hair')
        part('EyeWhite', (sign*.40, -1.04, 5.72), (.27, .12, .30), 'White')
        part('Pupil', (sign*.40, -1.15, 5.70), (.115, .07, .19), 'Ink')
        brow = part('Brow', (sign*.40, -1.13, 6.07), (.30, .08, .07), 'Ink', shape='cube')
        brow.rotation_euler.y = sign * -.14
    part('Nose', (0, -1.19, 5.38), (.13, .16, .13), 'Skin')
    part('Smile', (0, -1.12, 5.13), (.23, .055, .045), 'Ink', shape='cube')
    part('Belt', (0, .08, 2.25), (.70, .46, .12), 'Boots', bone='Stomack')
    part('Buckle', (0, -.40, 2.27), (.17, .06, .14), 'Gold', bone='Stomack', shape='cube')
    if role == 'Cop':
        part('PoliceCap', (0, .05, 6.70), (1.25, 1.12, .48), 'Uniform')
        part('CapBand', (0, -.08, 6.45), (1.26, 1.10, .13), 'Boots')
        part('Visor', (0, -.98, 6.40), (.95, .53, .09), 'Boots')
        part('CapBadge', (0, -1.08, 6.71), (.22, .07, .25), 'Gold')
        part('ChestBadge', (.31, -.36, 3.67), (.14, .07, .18), 'Gold', bone='Chest')
    else:
        part('Beanie', (0, .12, 6.67), (1.23, 1.11, .47), 'Pants')
        part('BeanieCuff', (0, -.02, 6.43), (1.25, 1.12, .15), 'Uniform')
        # Individual dark patches keep the white eyes readable, even at gameplay scale.
        for sign in (-1, 1):
            part('EyeMask', (sign*.40, -1.02, 5.72), (.39, .11, .38), 'Ink')
        part('LootBag', (0, .67, 3.18), (.67, .40, .80), 'Gold', bone='Chest')
        part('BagStrap', (.43, .44, 3.75), (.10, .12, .50), 'Pants', bone='Chest')

    # All render geometry shares a single skinned renderer, retaining status tints.
    bpy.ops.object.select_all(action='DESELECT')
    for obj in bpy.data.objects:
        if obj.type == 'MESH': obj.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.join(); body.name = 'CharacterMesh'
    # Join retains material groups and the original vertex weights.
    for modifier in list(body.modifiers):
        if modifier.type != 'ARMATURE': body.modifiers.remove(modifier)
    rig.data.pose_position = 'POSE'
    def rotate(name, axis, angle, additional=None):
        rest = rig.data.bones[name].matrix_local.to_quaternion()
        world = Quaternion(axis, angle)
        if additional is not None: world = additional @ world
        rig.pose.bones[name].rotation_quaternion = rest.inverted() @ world @ rest
    for action_name, frames in [('Idle', 61), ('Run', 25), ('Arrested', 61)]:
        action = bpy.data.actions.new(action_name)
        rig.animation_data_create(); rig.animation_data.action = action
        for frame in range(1, frames + 1, 3):
            phase = (frame - 1) / (frames - 1) * math.tau
            for bone in rig.pose.bones:
                bone.location = (0, 0, 0); bone.rotation_quaternion = Quaternion(); bone.scale = (1, 1, 1)
                if bone.name == 'Head': bone.scale = (1.06, 1.06, 1.06)
                if bone.name.startswith('Hand'): bone.scale = (1.65, 1.65, 1.65)
                if bone.name.startswith('Foot'): bone.scale = (1.25, 1.25, 1.25)
                if bone.name.startswith(('Upper Leg', 'Lower Leg')): bone.scale = (1.12, 1, 1.12)
            for side, sign in [('L', 1), ('R', -1)]:
                rotate('Upper Arm.'+side, (0, 1, 0), sign * .80)
                if action_name == 'Run':
                    rotate('Upper Arm.'+side, (0, 1, 0), sign * .80, Quaternion((1,0,0), sign * math.sin(phase) * .65))
                    rotate('Upper Leg.'+side, (1,0,0), -sign * math.sin(phase) * .65)
                    rotate('Lower Leg.'+side, (1,0,0), max(0, sign * math.sin(phase)) * .65)
                    rotate('Lower Arm.'+side, (1,0,0), -.55)
                if action_name == 'Arrested':
                    rotate('Upper Arm.'+side, (0,1,0), sign * -.55)
                    rotate('Lower Arm.'+side, (0,1,0), sign * -1.0)
            rotate('Chest', (1,0,0), (.10 if action_name == 'Run' else .02 * math.sin(phase)))
            rotate('Head', (1,0,0), (.22 if action_name == 'Arrested' else .025 * math.sin(phase)))
            for bone in rig.pose.bones:
                bone.keyframe_insert('rotation_quaternion', frame=frame)
                bone.keyframe_insert('scale', frame=frame)
        action.use_fake_user = True
    rig.animation_data.action = bpy.data.actions['Idle']
    bpy.context.scene.frame_set(1)
    bpy.ops.object.select_all(action='DESELECT'); rig.select_set(True); body.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, 'SD_'+role+'.fbx'), use_selection=True,
        object_types={'ARMATURE', 'MESH'}, global_scale=.32, apply_unit_scale=True,
        add_leaf_bones=False, bake_anim=True, bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=True, bake_anim_simplify_factor=0,
        axis_forward='-Z', axis_up='Y')
    # Artist preview; Unity will also render the imported prefabs for verification.
    scene = bpy.context.scene
    bpy.ops.object.camera_add(location=(10, -17, 10))
    camera = bpy.context.object
    camera.rotation_euler = (Vector((0, 0, 3.6)) - camera.location).to_track_quat('-Z', 'Y').to_euler()
    camera.data.type = 'ORTHO'; camera.data.ortho_scale = 9.5; scene.camera = camera
    bpy.ops.object.light_add(type='AREA', location=(3, -6, 11)); bpy.context.object.data.energy = 1600; bpy.context.object.data.shape='DISK'; bpy.context.object.data.size=8
    scene.world.color = (.2, .2, .2)
    scene.render.engine='BLENDER_EEVEE_NEXT'; scene.render.resolution_x=640; scene.render.resolution_y=640; scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG'; scene.render.filepath=os.path.join(ROOT, 'TestResults', 'SD_'+role+'_source.png')
    scene.render.film_transparent=False
    bpy.ops.render.render(write_still=True)

for role in ('Cop', 'Robber'): build(role)

