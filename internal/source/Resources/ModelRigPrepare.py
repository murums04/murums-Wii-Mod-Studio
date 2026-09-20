import bpy
import sys
import os
import json
import math
import re
import xml.etree.ElementTree as ET
from mathutils import Matrix, Vector
from mathutils.kdtree import KDTree

source, reference, output = sys.argv[sys.argv.index('--') + 1:][:3]
limit = int(sys.argv[sys.argv.index('--') + 4])
size_percent = float(sys.argv[sys.argv.index('--') + 5])
output = os.path.abspath(output)
os.makedirs(output, exist_ok=True)
root = ET.parse(reference).getroot()
for element in root.iter():
    element.tag = element.tag.split('}')[-1]
ids = {element.get('id'): element for element in root.iter() if element.get('id')}
bones = []

def transform(node):
    result = Matrix.Identity(4)
    for child in node:
        if child.tag not in ('translate', 'rotate', 'scale', 'matrix'):
            continue
        values = [float(v) for v in child.text.split()]
        if child.tag == 'translate':
            part = Matrix.Translation(values)
        elif child.tag == 'rotate':
            part = Matrix.Rotation(math.radians(values[3]), 4, Vector(values[:3]))
        elif child.tag == 'scale':
            part = Matrix.Diagonal(Vector(values + [1]))
        else:
            part = Matrix([values[i:i+4] for i in range(0, 16, 4)])
        result = result @ part
    return result

def visit(node, parent, inherited):
    world = inherited @ transform(node)
    next_parent = parent
    if node.get('type') == 'JOINT':
        next_parent = len(bones)
        bones.append({'Name': node.get('sid') or node.get('name') or node.get('id'), 'Parent': parent,
                      'Matrix': [v for row in world for v in row]})
    for child in node.findall('node'):
        visit(child, next_parent, world)
for scene in root.findall('./library_visual_scenes/visual_scene'):
    for node in scene.findall('node'):
        visit(node, -1, Matrix.Identity(4))
if not bones or len(bones) > 256:
    raise ValueError('The selected RR model has no supported skeleton.')
bone_names = {bone['Name']: i for i, bone in enumerate(bones)}
reference_points, reference_weights = [], []
for skin in root.findall('./library_controllers/controller/skin'):
    geometry = next(g for g in root.findall('./library_geometries/geometry') if g.get('id') == skin.get('source').lstrip('#'))
    mesh = geometry.find('mesh')
    vertex = mesh.find('vertices/input[@semantic="POSITION"]')
    positions = ids[vertex.get('source').lstrip('#')]
    values = [float(v) for v in positions.findtext('float_array').split()]
    accessor = positions.find('technique_common/accessor')
    stride = int(accessor.get('stride', '3'))
    vertex_points = [Vector(values[i:i+3]) for i in range(0, len(values), stride)]
    joint_id = skin.find('joints/input[@semantic="JOINT"]').get('source').lstrip('#')
    names_node = ids[joint_id].find('Name_array')
    if names_node is None:
        names_node = ids[joint_id].find('IDREF_array')
    joint_names = names_node.text.split()
    vertex_weights = skin.find('vertex_weights')
    inputs = vertex_weights.findall('input')
    joint_offset = int(next(x for x in inputs if x.get('semantic') == 'JOINT').get('offset'))
    weight_input = next(x for x in inputs if x.get('semantic') == 'WEIGHT')
    weight_offset = int(weight_input.get('offset'))
    step = max(int(x.get('offset')) for x in inputs) + 1
    weights = [float(v) for v in ids[weight_input.get('source').lstrip('#')].findtext('float_array').split()]
    indices = [int(v) for v in vertex_weights.findtext('v').split()]
    counts = [int(v) for v in vertex_weights.findtext('vcount').split()]
    cursor = 0
    for point, count in zip(vertex_points, counts):
        assignment = {}
        for j in range(count):
            ji = indices[cursor + joint_offset]
            wi = indices[cursor + weight_offset]
            name = joint_names[ji] if ji >= 0 else bones[0]['Name']
            if name in bone_names:
                assignment[bone_names[name]] = weights[wi]
            cursor += step
        total = sum(assignment.values())
        if total > 0:
            reference_points.append(point)
            reference_weights.append({k: v / total for k, v in assignment.items()})
if not reference_points:
    raise ValueError('RR reference has no vertex weights.')
rr_min = [min(p[i] for p in reference_points) for i in range(3)]
rr_max = [max(p[i] for p in reference_points) for i in range(3)]
rr_height = rr_max[1] - rr_min[1]
tree = KDTree(len(reference_points))
for i, point in enumerate(reference_points):
    tree.insert(point, i)
tree.balance()

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.scene.view_settings.view_transform = 'Standard'
bpy.context.scene.render.image_settings.file_format = 'PNG'
bpy.context.scene.render.image_settings.color_mode = 'RGBA'
if os.path.splitext(source)[1].lower() == '.obj':
    bpy.ops.wm.obj_import(filepath=source)
else:
    # DAE-Zwischenkopien enthalten getrennte UV-Eckpunkte; vor dem Vereinfachen wieder verbinden.
    merge_vertices = sys.argv[sys.argv.index('--') + 6:] == ['merge']
    bpy.ops.import_scene.gltf(filepath=source, merge_vertices=merge_vertices)
armatures = [o for o in bpy.context.scene.objects if o.type == 'ARMATURE']
for armature in armatures:
    armature.data.pose_position = 'REST'
bpy.context.view_layer.update()
objects = [o for o in bpy.context.scene.objects if o.type == 'MESH']
if not objects:
    raise ValueError('The source contains no meshes.')
original_triangles = sum(sum(len(p.vertices) - 2 for p in obj.data.polygons) for obj in objects)
for obj in objects:
    bpy.context.view_layer.objects.active = obj
    if limit > 0 and original_triangles > limit:
        if obj.data.shape_keys:
            obj.shape_key_clear()
        decimate = obj.modifiers.new('Studio preview optimization', 'DECIMATE')
        decimate.ratio = max(0.01, limit / original_triangles)
        bpy.ops.object.modifier_apply(modifier=decimate.name)
    obj.data.calc_loop_triangles()
convert = Matrix(((1,0,0),(0,0,1),(0,-1,0)))
positions = [convert @ (obj.matrix_world @ vertex.co) for obj in objects for vertex in obj.data.vertices]
minimum = [min(p[i] for p in positions) for i in range(3)]
maximum = [max(p[i] for p in positions) for i in range(3)]
height = maximum[1] - minimum[1]
if height <= 0.000001:
    raise ValueError('The source has no usable vertical height.')
factor = rr_height / height * size_percent / 100
shift = Vector(((rr_min[0] + rr_max[0]) / 2 - (minimum[0] + maximum[0]) / 2 * factor,
                rr_min[1] - minimum[1] * factor,
                (rr_min[2] + rr_max[2]) / 2 - (minimum[2] + maximum[2]) / 2 * factor))
# Benannte menschliche Quell-Rigs samt Gewichten erhalten.
def human_bone(name):
    if name in bone_names and not name.startswith('pcd_'):
        return name
    text = re.sub(r'[^a-z]', '', name.lower())
    if any(word in text for word in ('adjust', 'twist', 'end', 'weapon')):
        return None
    for prefix in ('mixamorig', 'bip', 'def'):
        if text.startswith(prefix):
            text = text[len(prefix):]
    centers = {'hips':'skl_root', 'pelvis':'skl_root', 'spine':'spin', 'head':'face_1'}
    if text in centers:
        return centers[text]
    for side, longside in (('l', 'left'), ('r', 'right')):
        part = None
        if text.startswith(longside): part = text[len(longside):]
        elif text.startswith(side): part = text[1:]
        elif text.endswith(side): part = text[:-1]
        targets = {'upperarm':'arm_'+side+'1', 'arm':'arm_'+side+'1',
                   'forearm':'arm_'+side+'2', 'lowerarm':'arm_'+side+'2',
                   'hand':'wrist_'+side+'1', 'thigh':'leg_'+side+'1', 'upleg':'leg_'+side+'1',
                   'calf':'leg_'+side+'2', 'shin':'leg_'+side+'2', 'leg':'leg_'+side+'2',
                   'foot':'ankle_'+side+'1'}
        if part in targets: return targets[part]
    return None

source_maps = {}
source_guides = {}
for armature in armatures:
    direct = {bone.name: human_bone(bone.name) for bone in armature.data.bones}
    required = ['arm_l1','arm_l2','wrist_l1','arm_r1','arm_r2','wrist_r1',
                'leg_l1','leg_l2','ankle_l1','leg_r1','leg_r2','ankle_r1','skl_root','spin','face_1']
    if not all(name in direct.values() and name in bone_names for name in required):
        continue
    mapping = {}
    for bone in armature.data.bones:
        target = direct[bone.name]
        if target and target in bone_names and target not in source_guides:
            source_guides[target] = list(convert @ (armature.matrix_world @ bone.head_local) * factor + shift)
        ancestor = bone
        while ancestor and not direct[ancestor.name]:
            ancestor = ancestor.parent
        if ancestor and direct[ancestor.name] in bone_names:
            mapping[bone.name] = bone_names[direct[ancestor.name]]
    source_maps[armature.name] = mapping

materials, material_ids, saved_textures = [], {}, {}
def material_index(material):
    key = material.name if material else 'Default'
    if key in material_ids:
        return material_ids[key]
    index = len(materials)
    material_ids[key] = index
    color = [0.7, 0.7, 0.75, 1]
    texture = None
    if material:
        color = list(material.diffuse_color)
        if material.use_nodes:
            bsdf = next((n for n in material.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
            if bsdf:
                color = list(bsdf.inputs['Base Color'].default_value)
                links = bsdf.inputs['Base Color'].links
                image_node = links[0].from_node if links else None
                if image_node and image_node.type == 'TEX_IMAGE' and image_node.image:
                    image_key = image_node.image.name
                    if image_key in saved_textures:
                        texture = saved_textures[image_key]
                    else:
                        image = image_node.image.copy()
                        if max(image.size) > 512:
                            ratio = 512 / max(image.size)
                            image.scale(max(1, round(image.size[0] * ratio)), max(1, round(image.size[1] * ratio)))
                        texture = 'studio_tex_' + str(len(saved_textures)) + '.png'
                        image.filepath_raw = os.path.join(output, texture)
                        image.file_format = 'PNG'
                        image.save_render(filepath=image.filepath_raw, scene=bpy.context.scene)
                        bpy.data.images.remove(image)
                        saved_textures[image_key] = texture
    if texture is None:
        image_key = str(color)
        if image_key in saved_textures:
            texture = saved_textures[image_key]
        else:
            image = bpy.data.images.new('Studio colour', width=4, height=4, alpha=True)
            image.pixels = color * 16
            texture = 'studio_tex_' + str(len(saved_textures)) + '.png'
            image.filepath_raw = os.path.join(output, texture)
            image.file_format = 'PNG'
            image.save_render(filepath=image.filepath_raw, scene=bpy.context.scene)
            bpy.data.images.remove(image)
            saved_textures[image_key] = texture
    materials.append({'Name':'studio_' + str(index), 'Color':color, 'Texture':texture})
    return index
points, normals, uvs, faces, face_materials, bone_indices, bone_weights = [], [], [], [], [], [], []
for obj in objects:
    mesh = obj.data
    normal_matrix = convert @ obj.matrix_world.to_3x3().inverted_safe().transposed()
    uv_layer = mesh.uv_layers.active
    vertex_cache = {}
    assigned = {}
    source_armature = obj.find_armature()
    source_map = source_maps.get(source_armature.name, {}) if source_armature else {}
    for vertex in mesh.vertices:
        point = convert @ (obj.matrix_world @ vertex.co) * factor + shift
        weights = {}
        for group in vertex.groups:
            name = obj.vertex_groups[group.group].name
            target = source_map.get(name, bone_names.get(name))
            if target is not None and group.weight > 0:
                weights[target] = weights.get(target, 0) + group.weight
        if not weights:
            for _, reference_index, distance in tree.find_n(point, 4):
                influence = 1 / max(distance * distance, 0.000001)
                for bone, weight in reference_weights[reference_index].items():
                    weights[bone] = weights.get(bone, 0) + weight * influence
        selected = sorted(weights.items(), key=lambda p: p[1], reverse=True)[:4]
        total = sum(w for _, w in selected)
        assigned[vertex.index] = (list(point), [i for i, _ in selected], [w / total for _, w in selected])
    for triangle in mesh.loop_triangles:
        face = []
        for loop_index in triangle.loops:
            loop = mesh.loops[loop_index]
            normal = normal_matrix @ loop.normal
            normal.normalize()
            uv = tuple(uv_layer.data[loop_index].uv) if uv_layer else (0.0, 0.0)
            key = (loop.vertex_index, tuple(normal), uv)
            if key not in vertex_cache:
                vertex_cache[key] = len(points)
                point, indices, weights = assigned[loop.vertex_index]
                points.append(point); bone_indices.append(indices); bone_weights.append(weights)
                normals.append(list(normal)); uvs.append(list(uv))
            face.append(vertex_cache[key])
        faces.append(face)
        slot = triangle.material_index
        mat = obj.material_slots[slot].material if slot < len(obj.material_slots) else None
        face_materials.append(material_index(mat))
    if len(points) > 200000 or len(faces) > 200000:
        raise ValueError('Optimized source still exceeds the 200,000-vertex/face limit.')
result = {'Points':points,'Normals':normals,'Uvs':uvs,'Faces':faces,'FaceMaterials':face_materials,
          'Materials':materials,'Bones':bones,'BoneIndices':bone_indices,'BoneWeights':bone_weights,
          'ReferenceHeight':rr_height,'OriginalTriangles':original_triangles,'SizePercent':size_percent}
if source_guides:
    result['SourceJointGuides'] = source_guides
    result['SourceBoneIndices'] = bone_indices
    result['SourceBoneWeights'] = bone_weights
with open(os.path.join(output, 'rig.json'), 'w', encoding='utf-8') as stream:
    json.dump(result, stream, separators=(',',':'), allow_nan=False)
print('Studio model prepared:', len(points), 'vertices,', len(faces), 'triangles,', len(bones), 'bones')
