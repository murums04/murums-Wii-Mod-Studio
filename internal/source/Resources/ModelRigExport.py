import sys
import json
import os
import copy
import xml.etree.ElementTree as E
from mathutils import Matrix, Vector
import math

def compact_weight_mixes(data, budget=2048):
    vertices = []
    for bones, weights in zip(data['BoneIndices'], data['BoneWeights']):
        grouped = {}
        for bone, weight in zip(bones, weights):
            if not math.isfinite(weight) or weight < 0:
                raise ValueError('Invalid bone weight during Wii export.')
            if weight > 0:
                grouped[bone] = grouped.get(bone, 0.0) + weight
        total = sum(grouped.values())
        if total <= 0:
            raise ValueError('An exported vertex has no bone assignment.')
        vertices.append(tuple((bone, weight / total) for bone, weight in sorted(grouped.items())))
    if len(set(vertices)) <= budget:
        return 0

    # Nahezu gleiche Mischungen teilen Wii-Matrizen; die editierbaren Gewichte bleiben unverändert.
    for steps in (4096, 2048, 1024, 512, 256, 128, 64):
        rounded = []
        for entries in vertices:
            raw = [weight * steps for _, weight in entries]
            ticks = [int(value) for value in raw]
            remaining = steps - sum(ticks)
            order = sorted(range(len(entries)), key=lambda index: raw[index] - ticks[index], reverse=True)
            for index in order[:remaining]:
                ticks[index] += 1
            rounded.append(tuple((bone, tick) for (bone, _), tick in zip(entries, ticks) if tick))
        if len(set(rounded)) <= budget:
            data['BoneIndices'] = [[bone for bone, _ in entries] for entries in rounded]
            data['BoneWeights'] = [[tick / steps for _, tick in entries] for entries in rounded]
            print('Wii matrix mixes:', len(set(rounded)), 'weight grid:', steps)
            return steps
    raise ValueError('Too many distinct bone-weight combinations for the Studio Wii matrix budget. Simplify the movement assignment.')


arguments = sys.argv[sys.argv.index('--') + 1:]
source, reference, destination = arguments[:3]
limit = int(arguments[3]) if len(arguments) > 3 else 0
with open(source, encoding='utf-8-sig') as stream:
    data = json.load(stream)
# Identische Materialdefinitionen gemeinsam exportieren, damit Namen stabil bleiben.
canonical_materials = {}
material_aliases = {}
for index in sorted(set(data['FaceMaterials'])):
    material = data['Materials'][index]
    key = ('texture', material['Texture']) if material['Texture'] else ('color', tuple(material['Color']))
    if key not in canonical_materials:
        canonical_materials[key] = index
    material_aliases[index] = canonical_materials[key]
data['FaceMaterials'] = [material_aliases[index] for index in data['FaceMaterials']]
used_materials = set(data['FaceMaterials'])

if limit > 0 and len(data['Faces']) > limit:
    import bpy
    bpy.ops.wm.read_factory_settings(use_empty=True)
    grouped_faces = {index: [] for index in used_materials}
    for face, material in zip(data['Faces'], data['FaceMaterials']):
        grouped_faces[material].append(face)
    minimum = {index: min(48, len(faces)) for index, faces in grouped_faces.items()}
    if sum(minimum.values()) > limit:
        raise ValueError('Too many material regions for the Wii detail budget.')
    remaining = limit - sum(minimum.values())
    excess = sum(len(faces) - minimum[index] for index, faces in grouped_faces.items())
    budgets = {index: minimum[index] + int(remaining * (len(faces) - minimum[index]) / max(1, excess))
               for index, faces in grouped_faces.items()}
    result = {name: [] for name in ['Points', 'Normals', 'Uvs', 'Faces',
                                  'FaceMaterials', 'BoneIndices', 'BoneWeights']}
    for material, faces in grouped_faces.items():
        # Geometrisch gleiche Eckpunkte verbinden; verschiedene Gewichte bleiben getrennt.
        welded = {}
        originals = []
        source_to_local = {}
        for vertex in sorted({vertex for face in faces for vertex in face}):
            key = (tuple(round(value, 6) for value in data['Points'][vertex]),
                   tuple(sorted((bone, round(weight, 6)) for bone, weight in
                                zip(data['BoneIndices'][vertex], data['BoneWeights'][vertex]))))
            if key not in welded:
                welded[key] = len(originals)
                originals.append(vertex)
            source_to_local[vertex] = welded[key]
        mesh = bpy.data.meshes.new('Studio material detail')
        mesh.from_pydata([data['Points'][vertex] for vertex in originals], [],
                         [[source_to_local[vertex] for vertex in face] for face in faces])
        obj = bpy.data.objects.new('Studio material detail', mesh)
        bpy.context.collection.objects.link(obj)
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        uv_layer = mesh.uv_layers.new(name='UV0')
        for polygon, source_face in zip(mesh.polygons, faces):
            polygon.use_smooth = True
            for loop_index, source_vertex in zip(polygon.loop_indices, source_face):
                uv_layer.data[loop_index].uv = data['Uvs'][source_vertex]
        for bone in data['Bones']:
            obj.vertex_groups.new(name=bone['Name'])
        for vertex, original in enumerate(originals):
            for bone, weight in zip(data['BoneIndices'][original], data['BoneWeights'][original]):
                obj.vertex_groups[bone].add([vertex], weight, 'REPLACE')
        if len(faces) > budgets[material]:
            modifier = obj.modifiers.new('Studio material detail', 'DECIMATE')
            modifier.ratio = max(0.001, (budgets[material] - 8) / len(faces))
            bpy.ops.object.modifier_apply(modifier=modifier.name)
        mesh = obj.data
        mesh.calc_loop_triangles()
        if not mesh.loop_triangles:
            raise ValueError('A material region disappeared during detail optimization.')
        cache = {}
        for triangle in mesh.loop_triangles:
            face = []
            for loop_index in triangle.loops:
                loop = mesh.loops[loop_index]
                vertex = mesh.vertices[loop.vertex_index]
                uv = tuple(mesh.uv_layers.active.data[loop_index].uv)
                normal = tuple(loop.normal)
                key = (vertex.index, uv, normal)
                if key not in cache:
                    cache[key] = len(result['Points'])
                    result['Points'].append(list(vertex.co))
                    result['Normals'].append(list(normal))
                    result['Uvs'].append(list(uv))
                    weights = sorted([(group.group, group.weight) for group in vertex.groups
                                      if group.weight > 0], key=lambda value: value[1], reverse=True)[:4]
                    if not weights:
                        raise ValueError('A distance-model vertex lost its assignment.')
                    total = sum(weight for _, weight in weights)
                    result['BoneIndices'].append([bone for bone, _ in weights])
                    result['BoneWeights'].append([weight / total for _, weight in weights])
                face.append(cache[key])
            result['Faces'].append(face)
            result['FaceMaterials'].append(material)
        bpy.data.objects.remove(obj, do_unlink=True)
        bpy.data.meshes.remove(mesh)
    data.update(result)
    if len(data['Faces']) > limit * 1.1:
        raise ValueError('Distance-model optimization could not reach the requested budget.')

ref = E.parse(reference).getroot()
for node in ref.iter():
    node.tag = node.tag.split('}')[-1]
reference_bones = []
def bone_transform(element):
    result = Matrix.Identity(4)
    for child in element:
        if child.tag not in ('translate','rotate','scale','matrix'):
            continue
        values = [float(value) for value in child.text.split()]
        if child.tag == 'translate':
            part = Matrix.Translation(values)
        elif child.tag == 'rotate':
            part = Matrix.Rotation(math.radians(values[3]), 4, Vector(values[:3]))
        elif child.tag == 'scale':
            part = Matrix.Diagonal(Vector(values + [1]))
        else:
            part = Matrix([values[i:i+4] for i in range(0,16,4)])
        result = result @ part
    return result
def read_bone(element, parent, inherited):
    world = inherited @ bone_transform(element)
    if element.get('type') == 'JOINT':
        index = len(reference_bones)
        reference_bones.append({'Name':element.get('sid') or element.get('name') or element.get('id'),
                                'Parent':parent,'Matrix':[value for row in world for value in row]})
        parent = index
    for child in element.findall('node'):
        read_bone(child, parent, world)
for element in ref.findall('./library_visual_scenes/visual_scene/node'):
    read_bone(element, -1, Matrix.Identity(4))
if not reference_bones:
    raise ValueError('The RR detail model has no skeleton.')
if [bone['Name'] for bone in reference_bones] != [bone['Name'] for bone in data['Bones']]:
    target_names = {bone['Name']: i for i, bone in enumerate(reference_bones)}
    mapping = {}
    used_bones = {bone for indices, weights in zip(data['BoneIndices'], data['BoneWeights'])
                  for bone, weight in zip(indices, weights) if weight > 0}
    for old, bone in enumerate(data['Bones']):
        if old not in used_bones:
            mapping[old] = 0
            continue
        if bone['Name'].startswith('pcd_') and bone['Name'][4:] in target_names:
            mapping[old] = target_names[bone['Name'][4:]]
            continue
        ancestor = old
        while ancestor >= 0 and data['Bones'][ancestor]['Name'] not in target_names:
            ancestor = data['Bones'][ancestor]['Parent']
        if ancestor < 0 and len(reference_bones) != 1:
            raise ValueError('The RR detail skeleton cannot preserve the reviewed assignment.')
        mapping[old] = target_names[data['Bones'][ancestor]['Name']] if ancestor >= 0 else 0
    for vertex, (bones, weights) in enumerate(zip(data['BoneIndices'],data['BoneWeights'])):
        grouped = {}
        for bone, weight in zip(bones, weights):
            index = mapping[bone]
            grouped[index] = grouped.get(index, 0) + weight
        data['BoneIndices'][vertex] = list(grouped)
        data['BoneWeights'][vertex] = list(grouped.values())
data['Bones'] = reference_bones

# Gelenke und Gewichtsmischungen teilen denselben Wii-Matrixvorrat.
compact_weight_mixes(data, 2048 - len(data["Bones"]))

root = E.Element('COLLADA', {'xmlns':'http://www.collada.org/2005/11/COLLADASchema','version':'1.4.1'})
def node(parent, tag, text=None, **attributes):
    element = E.SubElement(parent, tag, {k:str(v) for k,v in attributes.items()})
    element.text = text
    return element
asset = node(root,'asset')
node(asset,'unit',name='centimeter',meter='0.01')
node(asset,'up_axis','Y_UP')
images = node(root,'library_images')
effects = node(root,'library_effects')
materials = node(root,'library_materials')
for material_index, material in enumerate(data['Materials']):
    if material_index not in used_materials:
        continue
    name = material['Name']
    texture = material['Texture']
    mat = node(materials,'material',id=name,name=name)
    node(mat,'instance_effect',url='#'+name+'-fx')
    effect = node(effects,'effect',id=name+'-fx')
    profile = node(effect,'profile_COMMON')
    if texture:
        image = node(images,'image',id=name+'-image',name=name)
        node(image,'init_from',texture)
        param = node(profile,'newparam',sid=name+'-surface')
        node(node(param,'surface',type='2D'),'init_from',name+'-image')
        param = node(profile,'newparam',sid=name+'-sampler')
        node(node(param,'sampler2D'),'source',name+'-surface')
    phong = node(node(profile,'technique',sid='common'),'phong')
    diffuse = node(phong,'diffuse')
    if texture:
        node(diffuse,'texture',texture=name+'-sampler',texcoord='UV0')
    else:
        node(diffuse,'color',' '.join(str(v) for v in material['Color']))
    node(node(phong,'specular'),'color','0 0 0 1')
    node(node(phong,'shininess'),'float','0')

def array(parent, identifier, values, stride, params, kind='float'):
    source = node(parent,'source',id=identifier)
    values = list(values)
    node(source,'Name_array' if kind == 'Name' else 'float_array', ' '.join(str(v) for v in values),id=identifier+'-array',count=len(values))
    accessor = node(node(source,'technique_common'),'accessor',source='#'+identifier+'-array',count=len(values)//stride,stride=stride)
    for name, type_name in params:
        node(accessor,'param',name=name,type=type_name)
    return source

geometries = node(root, 'library_geometries')
controllers = node(root, 'library_controllers')
scene = node(node(root, 'library_visual_scenes'), 'visual_scene', id='Scene')
for original in ref.findall('./library_visual_scenes/visual_scene/node'):
    if original.get('type') == 'JOINT':
        scene.append(copy.deepcopy(original))

inverses = []
for bone in data['Bones']:
    matrix = Matrix([bone['Matrix'][i:i + 4] for i in range(0, 16, 4)]).inverted().transposed()
    inverses.extend(value for row in matrix for value in row)

# Die Wii-Importbibliothek übernimmt je Geometrie nur einen Materialbereich.
for material_index, material in enumerate(data['Materials']):
    faces = [face for face, index in zip(data['Faces'], data['FaceMaterials']) if index == material_index]
    if not faces:
        continue
    used_vertices = sorted({vertex for face in faces for vertex in face})
    local_index = {vertex: index for index, vertex in enumerate(used_vertices)}
    prefix = 'studio_part_' + str(material_index)
    geometry = node(geometries, 'geometry', id=prefix, name=material['Name'])
    mesh = node(geometry, 'mesh')
    array(mesh, prefix + '-positions',
          (value for vertex in used_vertices for value in data['Points'][vertex]),
          3, [(axis, 'float') for axis in ('X', 'Y', 'Z')])
    array(mesh, prefix + '-normals',
          (value for vertex in used_vertices for value in data['Normals'][vertex]),
          3, [(axis, 'float') for axis in ('X', 'Y', 'Z')])
    array(mesh, prefix + '-uv',
          (value for vertex in used_vertices for value in data['Uvs'][vertex]),
          2, [('S', 'float'), ('T', 'float')])
    node(node(mesh, 'vertices', id=prefix + '-vertices'), 'input',
         semantic='POSITION', source='#' + prefix + '-positions')
    triangles = node(mesh, 'triangles', count=len(faces), material=material['Name'])
    node(triangles, 'input', semantic='VERTEX', source='#' + prefix + '-vertices', offset=0)
    node(triangles, 'input', semantic='NORMAL', source='#' + prefix + '-normals', offset=1)
    node(triangles, 'input', semantic='TEXCOORD', source='#' + prefix + '-uv', offset=2, set=0)
    node(triangles, 'p', ' '.join(str(local_index[vertex])
         for face in faces for vertex in face for component in range(3)))

    controller = node(controllers, 'controller', id=prefix + '-skin')
    skin = node(controller, 'skin', source='#' + prefix)
    node(skin, 'bind_shape_matrix', '1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1')
    array(skin, prefix + '-joints', (bone['Name'] for bone in data['Bones']),
          1, [('JOINT', 'Name')], kind='Name')
    array(skin, prefix + '-poses', inverses, 16, [('TRANSFORM', 'float4x4')])
    weights, indices, counts = [], [], []
    for vertex in used_vertices:
        bones = data['BoneIndices'][vertex]
        influences = data['BoneWeights'][vertex]
        counts.append(len(bones))
        for bone, weight in zip(bones, influences):
            indices.extend((bone, len(weights)))
            weights.append(weight)
    array(skin, prefix + '-weights', weights, 1, [('WEIGHT', 'float')])
    joints = node(skin, 'joints')
    node(joints, 'input', semantic='JOINT', source='#' + prefix + '-joints')
    node(joints, 'input', semantic='INV_BIND_MATRIX', source='#' + prefix + '-poses')
    vertex_weights = node(skin, 'vertex_weights', count=len(counts))
    node(vertex_weights, 'input', semantic='JOINT', source='#' + prefix + '-joints', offset=0)
    node(vertex_weights, 'input', semantic='WEIGHT', source='#' + prefix + '-weights', offset=1)
    node(vertex_weights, 'vcount', ' '.join(str(value) for value in counts))
    node(vertex_weights, 'v', ' '.join(str(value) for value in indices))

    model = node(scene, 'node', id=prefix + '-node', name=material['Name'])
    instance = node(model, 'instance_controller', url='#' + prefix + '-skin')
    for bone in data['Bones']:
        if bone['Parent'] == -1:
            node(instance, 'skeleton', '#' + bone['Name'])
    bind = node(node(instance, 'bind_material'), 'technique_common')
    target = node(bind, 'instance_material', symbol=material['Name'], target='#' + material['Name'])
    node(target, 'bind_vertex_input', semantic='UV0', input_semantic='TEXCOORD', input_set=0)

node(node(root, 'scene'), 'instance_visual_scene', url='#Scene')
E.ElementTree(root).write(destination, encoding='utf-8', xml_declaration=True)
print('Studio rig export:', len(data['Faces']), 'triangles,', len(geometries), 'material regions')
