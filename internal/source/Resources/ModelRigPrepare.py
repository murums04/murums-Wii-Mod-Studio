import bpy
import bmesh
import sys
import os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
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
    # Die in glTF gespeicherte Haltung verwenden; geratene Bindeposen koennen mehrere Rigs verzerren.
    bpy.ops.import_scene.gltf(filepath=source, merge_vertices=merge_vertices, guess_original_bind_pose=False)
armatures = [o for o in bpy.context.scene.objects if o.type == 'ARMATURE']
for armature in armatures:
    armature.data.pose_position = 'REST'
bpy.context.view_layer.update()
objects = [o for o in bpy.context.scene.objects if o.type == 'MESH' and o.visible_get()]
if not objects:
    raise ValueError('The source contains no meshes.')

def visible_color_input(shader):
    if shader.type == 'EMISSION':
        return shader.inputs['Color']
    base = shader.inputs['Base Color']
    emission = shader.inputs['Emission Color']
    strength = shader.inputs['Emission Strength']
    # Schwarze, untexturierte Grundfarbe: Die Oberflaeche stammt allein aus der Emission.
    if (not base.links and max(abs(v) for v in base.default_value[:3]) < 0.000001
            and not strength.links and strength.default_value > 0
            and (emission.links or max(emission.default_value[:3]) > 0)):
        return emission
    return base


# Gemischte Grundfarben (etwa Textur mal Vertexfarbe) in der Arbeitskopie backen.
for obj in objects:
    complex_materials = []
    for material in obj.data.materials:
        if not material or not material.use_nodes:
            continue
        shader = next((n for n in material.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
        if shader and visible_color_input(shader).links:
            color_socket = visible_color_input(shader)
            if color_socket.links[0].from_node.type != 'TEX_IMAGE':
                complex_materials.append((material, color_socket))
    if not complex_materials:
        continue
    if not obj.data.uv_layers:
        raise ValueError('The coloured surface needs a UV map before importing: ' + obj.name)
    # Gemeinsam verwendete Materialien duerfen keine Farben eines anderen Objekts uebernehmen.
    complex_materials = []
    for slot in obj.material_slots:
        if slot.material is None:
            continue
        slot.material = slot.material.copy()
        material = slot.material
        material.use_nodes = True
        shader = next((n for n in material.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
        if shader is None:
            shader = next((n for n in material.node_tree.nodes if n.type == 'EMISSION'), None)
        if shader is None:
            raise ValueError('Unsupported base colour shader: ' + material.name)
        complex_materials.append((material, visible_color_input(shader)))
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.context.scene.render.engine = 'CYCLES'
    bpy.context.scene.cycles.samples = 1
    bpy.context.scene.render.bake.margin = 4
    prepared = []
    for material, color_socket in complex_materials:
        nodes, links = material.node_tree.nodes, material.node_tree.links
        output_node = next(n for n in nodes if n.type == 'OUTPUT_MATERIAL' and n.is_active_output)
        old_surface = output_node.inputs['Surface'].links[0].from_socket
        original_color = color_socket.links[0].from_socket if color_socket.links else None
        texture_sizes = [max(n.image.size) for n in nodes if n.type == 'TEX_IMAGE' and n.image]
        colour_varies = any(len({tuple(c.color) for c in a.data}) > 1 for a in obj.data.color_attributes)
        resolution = min(1024, max(texture_sizes)) if texture_sizes else (256 if colour_varies else 4)
        image = bpy.data.images.new('Studio base colour', width=resolution, height=resolution, alpha=True)
        image.generated_color = (0, 0, 0, 1)
        target = nodes.new('ShaderNodeTexImage')
        target.image = image
        nodes.active = target
        emission = nodes.new('ShaderNodeEmission')
        if original_color:
            links.new(original_color, emission.inputs['Color'])
        else:
            emission.inputs['Color'].default_value = color_socket.default_value
        links.new(emission.outputs[0], output_node.inputs['Surface'])
        prepared.append((nodes, links, output_node, old_surface, color_socket, target, emission))
    bpy.ops.object.bake(type='EMIT', use_clear=True)
    for nodes, links, output_node, old_surface, color_socket, target, emission in prepared:
        target.image.pack()
        links.new(old_surface, output_node.inputs['Surface'])
        links.new(target.outputs['Color'], color_socket)
        nodes.remove(emission)
    print('Baked source colours:', obj.name, len(prepared))

original_triangles = sum(sum(len(p.vertices) - 2 for p in obj.data.polygons) for obj in objects)
convert = Matrix(((1,0,0),(0,0,1),(0,-1,0)))
binding_points, binding_faces = [], []
if limit > 6000 and original_triangles > 6000:
    # Die bewährte grobe Bindefläche unabhängig von sichtbaren Gesichtsdetails erhalten.
    # glTF-Meshbloecke vor der Vereinfachung verbinden, sonst entstehen Loecher.
    proxy_mesh = bpy.data.meshes.new('Studio binding surface')
    proxy_vertices, proxy_polygons = [], []
    for obj in objects:
        offset = len(proxy_vertices)
        proxy_vertices.extend(convert @ (obj.matrix_world @ vertex.co) for vertex in obj.data.vertices)
        proxy_polygons.extend([offset + v for v in polygon.vertices] for polygon in obj.data.polygons)
    proxy_mesh.from_pydata(proxy_vertices, [], proxy_polygons)
    proxy = bpy.data.objects.new('Studio binding surface', proxy_mesh)
    bpy.context.collection.objects.link(proxy)
    surface = bmesh.new()
    surface.from_mesh(proxy_mesh)
    span = max(max(p[a] for p in proxy_vertices) - min(p[a] for p in proxy_vertices) for a in range(3))
    bmesh.ops.remove_doubles(surface, verts=list(surface.verts), dist=max(span * .000001, 1e-9))
    bmesh.ops.recalc_face_normals(surface, faces=list(surface.faces))
    surface.to_mesh(proxy_mesh)
    surface.free()
    bpy.context.view_layer.objects.active = proxy
    decimate = proxy.modifiers.new('Studio binding surface', 'DECIMATE')
    decimate.ratio = max(.001, 6000 / original_triangles)
    bpy.ops.object.modifier_apply(modifier=decimate.name)
    proxy.data.calc_loop_triangles()
    binding_points = [vertex.co.copy() for vertex in proxy.data.vertices]
    binding_faces = [list(triangle.vertices) for triangle in proxy.data.loop_triangles]
    proxy_mesh = proxy.data
    bpy.data.objects.remove(proxy, do_unlink=True)
    bpy.data.meshes.remove(proxy_mesh)
for obj in objects:
    bpy.context.view_layer.objects.active = obj
    if original_triangles > 200000:
        if obj.data.shape_keys:
            obj.shape_key_clear()
        decimate = obj.modifiers.new('Studio preview optimization', 'DECIMATE')
        decimate.ratio = max(0.01, 199000 / original_triangles)
        bpy.ops.object.modifier_apply(modifier=decimate.name)
    obj.data.calc_loop_triangles()
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
binding_points = [list(point * factor + shift) for point in binding_points]
# Benannte menschliche Quell-Rigs samt Gewichten erhalten.
from ModelRigAnalysis import analyze
from ModelGripGeometry import open_hand_grip, guard_hand_grips

source_maps, source_direct_maps, source_guides, source_trusted = {}, {}, {}, set()
source_kinds, conflicts = [], set()
# Das tragende Koerper-Rig bestimmt die Gelenke, nicht ein zuerst importiertes Zubehoer-Rig.
armatures.sort(key=lambda rig: sum(len(obj.data.vertices) for obj in objects if obj.find_armature() == rig), reverse=True)
for armature in armatures:
    source_bones = list(armature.data.bones)
    lookup = {bone.name: i for i, bone in enumerate(source_bones)}
    nodes = [{'Name': bone.name, 'Parent': lookup[bone.parent.name] if bone.parent else -1,
              'Point': list(convert @ (armature.matrix_world @ bone.head_local) * factor + shift)}
             for bone in source_bones]
    detected, trusted, kind = analyze(nodes)
    source_kinds.append(kind)
    direct = {source_bones[i].name: target for i, target in detected.items() if target in bone_names}
    source_direct_maps[armature.name] = direct
    prior_guides = set(source_guides)
    mapping = {}
    for i, bone in enumerate(source_bones):
        target = direct.get(bone.name)
        if target:
            point = nodes[i]['Point']
            if target not in source_guides:
                source_guides[target] = point
                if target in trusted:
                    source_trusted.add(target)
            elif target in prior_guides and (Vector(point) - Vector(source_guides[target])).length > rr_height * .005:
                conflicts.add(target)
        ancestor = bone
        while ancestor and ancestor.name not in direct:
            ancestor = ancestor.parent
        if ancestor:
            mapping[bone.name] = bone_names[direct[ancestor.name]]
    source_maps[armature.name] = mapping
source_trusted.difference_update(conflicts)

# Finger in der Fahrkopie schliessen; die Menuegeometrie bleibt erhalten.
def make_hand_grips(armature, resolved_bones):
    transforms, contacts = {}, {}
    def position(bone):
        return convert @ (armature.matrix_world @ bone.head_local) * factor + shift
    for side in ('l', 'r'):
        target = 'wrist_' + side + '1'
        wrists = [b for b in armature.data.bones if resolved_bones.get(b.name) == target]
        if len(wrists) != 1:
            continue
        wrist = wrists[0]
        chains = []
        for child in wrist.children:
            label = re.sub(r'[^a-z]', '', child.name.lower())
            if label.startswith(('deformwrist', 'wristdeform')):
                continue
            if 'metacarpal' in label:
                if len(child.children) != 1:
                    continue
                child = child.children[0]
            # Mehrere Finger koennen eine gemeinsame, unbewegte Handbasis haben.
            starts = list(child.children) if len(child.children) > 1 else [child]
            for start in starts:
                chain = [start]
                while len(chain) < 4 and len(chain[-1].children) == 1:
                    chain.append(chain[-1].children[0])
                if len(chain) >= 3:
                    chains.append(chain)
        if len(chains) != 5:
            continue
        origin = position(wrist)
        thumb = min(chains, key=lambda c: (position(c[0]) - origin).length)
        fingers = [c for c in chains if c is not thumb]
        fingers.sort(key=lambda c: (position(c[0]) - position(thumb[0])).length)
        knuckles = sum((position(c[0]) for c in fingers), Vector()) / 4
        along = (knuckles - origin).normalized()
        across = position(fingers[-1][0]) - position(fingers[0][0])
        across = (across - along * across.dot(along)).normalized()
        palm = along.cross(across) * (-1 if side == 'l' else 1)
        proximal = sum((position(c[1]) - position(c[0])).length for c in fingers) / 4
        if min(across.length, palm.length, along.length) < .9 or proximal < .00001:
            continue
        center = knuckles + along * proximal * .35 + palm * proximal * .7
        contacts[target] = {'Point': list(center), 'Axis': list(across)}
        for chain in fingers + [thumb]:
            heads = [position(b) for b in chain]
            if len(heads) == 3:
                heads.append(heads[-1] + (heads[-1] - heads[-2]) * .7)
            desired = heads[0].copy()
            for i, bone in enumerate(chain[:3]):
                segment = heads[i + 1] - heads[i]
                if segment.length < .00001:
                    continue
                if chain is thumb:
                    direction = (center + across * proximal * .3 - desired).normalized()
                else:
                    angle = math.radians((35, 110, 165)[i])
                    direction = along * math.cos(angle) + palm * math.sin(angle)
                rotation = segment.rotation_difference(direction).to_matrix()
                matrix = rotation.to_4x4()
                matrix.translation = desired - rotation @ heads[i]
                transforms[bone.name] = matrix
                desired += direction * segment.length
            for bone in chain[3:]:
                transforms[bone.name] = transforms[chain[2].name]
    return transforms, contacts

hand_transforms, hand_contacts = {}, {}
for armature in armatures:
    if armature.name not in source_maps:
        continue
    transforms, contacts = make_hand_grips(armature, source_direct_maps[armature.name])
    hand_transforms[armature.name] = transforms
    for name, contact in contacts.items():
        if name not in hand_contacts:
            hand_contacts[name] = contact

materials, material_ids, saved_textures = [], {}, {}
def material_index(material):
    key = material.name if material else 'Default'
    if key in material_ids:
        return material_ids[key]
    index = len(materials)
    material_ids[key] = index
    color = [0.7, 0.7, 0.75, 1]
    texture = None
    opacity = 1.0
    if material:
        color = list(material.diffuse_color)
        if material.use_nodes:
            bsdf = next((n for n in material.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
            if bsdf is None:
                bsdf = next((n for n in material.node_tree.nodes if n.type == 'EMISSION'), None)
            if bsdf:
                color_input = visible_color_input(bsdf)
                color = list(color_input.default_value)
                alpha = bsdf.inputs.get('Alpha')
                if alpha and not alpha.links:
                    opacity = max(0.0, min(1.0, alpha.default_value))
                    color[3] = opacity
                links = color_input.links
                image_node = links[0].from_node if links else None
                if image_node and image_node.type == 'TEX_IMAGE' and image_node.image:
                    if not len(image_node.image.pixels):
                        adjacent = os.path.join(os.path.dirname(source), os.path.basename(image_node.image.filepath))
                        if not os.path.isfile(adjacent):
                            raise ValueError('Missing texture: ' + image_node.image.filepath)
                        image_node.image.filepath = adjacent
                        image_node.image.reload()
                    image_key = (image_node.image.name, opacity)
                    if image_key in saved_textures:
                        texture = saved_textures[image_key]
                    else:
                        image = image_node.image.copy()
                        if max(image.size) > 1024:
                            ratio = 1024 / max(image.size)
                            image.scale(max(1, round(image.size[0] * ratio)), max(1, round(image.size[1] * ratio)))
                        if opacity != 1.0:
                            pixels = list(image.pixels)
                            pixels[3::4] = [a * opacity for a in pixels[3::4]]
                            image.pixels = pixels
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
    materials.append({'Name':'studio_' + str(index), 'SourceName':key, 'Color':color, 'Texture':texture,
                      'DoubleSided':not material.use_backface_culling if material else True})
    return index
points, normals, uvs, faces, face_materials, bone_indices, bone_weights = [], [], [], [], [], [], []
grip_vertices = []
geometric_vertices, geometric_contacts = [], {}
source_weighted, vertex_components = [], []
component_names = [obj.name for obj in objects]
for component, obj in enumerate(objects):
    mesh = obj.data
    normal_matrix = convert @ obj.matrix_world.to_3x3().inverted_safe().transposed()
    uv_layer = mesh.uv_layers.active
    vertex_cache = {}
    assigned = {}
    source_armature = obj.find_armature()
    source_map = source_maps.get(source_armature.name, {}) if source_armature else {}
    finger_transforms = hand_transforms.get(source_armature.name, {}) if source_armature else {}
    geometric_grips = {}
    if source_armature:
        direct = source_direct_maps.get(source_armature.name, {})
        mesh_points, mesh_faces = None, None
        for side in ('l', 'r'):
            target = 'wrist_' + side + '1'
            wrists = [b for b in source_armature.data.bones if direct.get(b.name) == target]
            if target in geometric_contacts or len(wrists) != 1:
                continue
            wrist = wrists[0]
            if wrist.parent is None:
                continue
            if mesh_points is None:
                mesh_points = [list(convert @ (obj.matrix_world @ v.co) * factor + shift) for v in mesh.vertices]
                mesh_faces = [list(p.vertices) for p in mesh.polygons]
            hand_bones = {wrist.name} | {bone.name for bone in wrist.children_recursive}
            wrist_weights = [sum(g.weight for g in v.groups
                                 if obj.vertex_groups[g.group].name in hand_bones) for v in mesh.vertices]
            grip = open_hand_grip(mesh_points, mesh_faces, wrist_weights,
                                  convert @ (source_armature.matrix_world @ wrist.head_local) * factor + shift,
                                  convert @ (source_armature.matrix_world @ wrist.parent.head_local) * factor + shift,
                                  side)
            if grip:
                geometric_grips.update(grip['Vertices'])
                geometric_contacts[target] = grip['Contact']
                if target not in hand_contacts:
                    hand_contacts[target] = grip['Contact']
    for vertex in mesh.vertices:
        point = convert @ (obj.matrix_world @ vertex.co) * factor + shift
        weights = {}
        unmapped_weight = 0.0
        for group in vertex.groups:
            name = obj.vertex_groups[group.group].name
            target = source_map.get(name, bone_names.get(name))
            if group.weight > 0 and (target is None or bones[target]['Name'] in conflicts):
                unmapped_weight += group.weight
            if target is not None and group.weight > 0:
                weights[target] = weights.get(target, 0) + group.weight
        has_source_weights = bool(weights) and unmapped_weight < .0001
        if not weights:
            for _, reference_index, distance in tree.find_n(point, 4):
                influence = 1 / max(distance * distance, 0.000001)
                for bone, weight in reference_weights[reference_index].items():
                    weights[bone] = weights.get(bone, 0) + weight * influence
        selected = sorted(weights.items(), key=lambda p: p[1], reverse=True)[:4]
        total = sum(w for _, w in selected)
        assigned[vertex.index] = (list(point), [i for i, _ in selected], [w / total for _, w in selected], has_source_weights)
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
                point, indices, weights, has_source_weights = assigned[loop.vertex_index]
                source_weighted.append(has_source_weights)
                vertex_components.append(component)
                points.append(point); bone_indices.append(indices); bone_weights.append(weights)
                normals.append(list(normal)); uvs.append(list(uv))
                vertex = mesh.vertices[loop.vertex_index]
                if vertex.index in geometric_grips:
                    geometric_point, geometric_normal = geometric_grips[vertex.index]
                    geometric_vertices.append({'Index': len(points) - 1, 'Point': list(geometric_point),
                                               'Normal': list((geometric_normal @ normal).normalized())})
                finger_weights = [(finger_transforms[obj.vertex_groups[g.group].name], g.weight)
                                  for g in vertex.groups if obj.vertex_groups[g.group].name in finger_transforms]
                if finger_weights:
                    original = Vector(point)
                    posed, posed_normal = original.copy(), normal.copy()
                    for matrix, weight in finger_weights:
                        posed += (matrix @ original - original) * weight
                        posed_normal += (matrix.to_3x3() @ normal - normal) * weight
                    grip_vertices.append({'Index': len(points) - 1, 'Point': list(posed),
                                          'Normal': list(posed_normal.normalized())})
                elif vertex.index in geometric_grips:
                    posed, rotation = geometric_grips[vertex.index]
                    grip_vertices.append({'Index': len(points) - 1, 'Point': list(posed),
                                          'Normal': list((rotation @ normal).normalized())})
            face.append(vertex_cache[key])
        faces.append(face)
        slot = triangle.material_index
        mat = obj.material_slots[slot].material if slot < len(obj.material_slots) else None
        face_materials.append(material_index(mat))
    # UV-/Normalennaehte erzeugen vor dem Verschweissen bis zu drei Punkte pro Dreieck.
    if len(points) > 600000 or len(faces) > 200000:
        raise ValueError('Source exceeds the temporary 600,000-vertex/200,000-face limit.')
result = {'Points':points,'Normals':normals,'Uvs':uvs,'Faces':faces,'FaceMaterials':face_materials,
          'Materials':materials,'Bones':bones,'BoneIndices':bone_indices,'BoneWeights':bone_weights,
          'ReferenceHeight':rr_height,'OriginalTriangles':original_triangles,'SizePercent':size_percent}
original_contacts = hand_contacts
grip_vertices, hand_contacts = guard_hand_grips(points, normals, faces, grip_vertices, original_contacts,
                                               bone_indices, bone_weights, bones, materials, face_materials)
adjusted = {name for name, contact in original_contacts.items()
            if name not in hand_contacts or hand_contacts[name]['Point'] != contact['Point']}
if adjusted and geometric_vertices:
    alternatives, alternative_contacts = guard_hand_grips(points, normals, faces, geometric_vertices, geometric_contacts,
                                                          bone_indices, bone_weights, bones, materials, face_materials)
    replacements = adjusted & set(alternative_contacts)
    def grip_side(grip):
        index = grip['Index']
        return max(((weight, bones[bone]['Name']) for bone, weight in zip(bone_indices[index], bone_weights[index])
                    if bones[bone]['Name'] in original_contacts), default=(-1, None))[1]
    grip_vertices = [grip for grip in grip_vertices if grip_side(grip) not in replacements]
    grip_vertices.extend(grip for grip in alternatives if grip_side(grip) in replacements)
    for name in replacements:
        hand_contacts[name] = alternative_contacts[name]
    grip_vertices, hand_contacts = guard_hand_grips(points, normals, faces, grip_vertices, hand_contacts,
                                                   bone_indices, bone_weights, bones, materials, face_materials)
if hand_contacts and grip_vertices:
    result['SourceHandGrips'] = hand_contacts
    result['GripVertices'] = grip_vertices
if binding_points:
    result['BindingProxyPoints'] = binding_points
    result['BindingProxyFaces'] = binding_faces
if source_guides:
    result['SourceJointGuides'] = source_guides
    result['SourceBoneIndices'] = bone_indices
    result['SourceBoneWeights'] = bone_weights
result['SourceRigKind'] = ('multiple' if len(armatures) > 1 else source_kinds[0] if source_kinds else 'unrigged')
result['SourceTrustedJoints'] = sorted(source_trusted)
result['SourceWeightedVertices'] = source_weighted
result['SourceComponentNames'] = component_names
result['VertexComponents'] = vertex_components
from ModelMeshOptimize import simplify
simplify(result, limit, output)
if len(result['Points']) > 200000 or len(result['Faces']) > 200000:
    raise ValueError('Optimized source still exceeds the 200,000-vertex/face limit.')
with open(os.path.join(output, 'rig.json'), 'w', encoding='utf-8') as stream:
    json.dump(result, stream, separators=(',',':'), allow_nan=False)
print('Studio model prepared:', len(result['Points']), 'vertices,', len(result['Faces']), 'triangles,', len(bones), 'bones')
