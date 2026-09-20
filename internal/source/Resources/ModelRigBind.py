import bpy
import bmesh
import json
import sys

source, destination = sys.argv[sys.argv.index('--') + 1:]
with open(source, encoding='utf-8-sig') as stream:
    data = json.load(stream)
bpy.ops.wm.read_factory_settings(use_empty=True)
# Blender erwartet hier Modellgrößen nahe seiner üblichen Einheit.
scale = 2.0 / max(max(p[1] for p in data['Points']) - min(p[1] for p in data['Points']), 0.000001)
data['Points'] = [[value * scale for value in point] for point in data['Points']]
for item in data['Segments']:
    item['Head'] = [value * scale for value in item['Head']]
    item['Tail'] = [value * scale for value in item['Tail']]
data['WeldDistance'] *= scale
mesh = bpy.data.meshes.new('Binding surface')
mesh.from_pydata(data['Points'], [], data['Faces'])
mesh.update()
obj = bpy.data.objects.new('Binding surface', mesh)
bpy.context.collection.objects.link(obj)
# UV-Nähte auf der Arbeitskopie verbinden; Originalgeometrie und Texturen bleiben unverändert.
bm = bmesh.new()
bm.from_mesh(mesh)
bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=data['WeldDistance'])
bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
bm.to_mesh(mesh)
bm.free()
# Geschlossene Hilfsoberfläche verhindert den Bone-Heat-Abbruch an Kleidungsnähten.
bpy.context.view_layer.objects.active = obj
obj.select_set(True)
remesh = obj.modifiers.new('Binding proxy', 'REMESH')
remesh.mode = 'VOXEL'
remesh.voxel_size = max(max(p[1] for p in data['Points']) - min(p[1] for p in data['Points']), 1.0) / 160
bpy.ops.object.modifier_apply(modifier=remesh.name)
mesh = obj.data
adjacency = [set() for _ in mesh.vertices]
for edge in mesh.edges:
    a, b = edge.vertices
    adjacency[a].add(b); adjacency[b].add(a)
remaining = set(range(len(mesh.vertices)))
components = []
while remaining:
    component = {remaining.pop()}
    pending = list(component)
    while pending:
        current = pending.pop()
        for neighbour in adjacency[current]:
            if neighbour in remaining:
                remaining.remove(neighbour); component.add(neighbour); pending.append(neighbour)
    components.append(component)
largest = max(components, key=len)
if len(largest) < len(mesh.vertices) * 0.9:
    raise ValueError('The binding surface has separate body sections. Check the joint positions and mesh connections.')
# Kleine schwebende Flächen machen Blenders Wärmelösung singulär.
bm = bmesh.new()
bm.from_mesh(mesh)
bm.verts.ensure_lookup_table()
bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.index not in largest], context='VERTS')
bm.to_mesh(mesh)
bm.free()
obj.select_set(False)
armature = bpy.data.armatures.new('Body guides')
rig = bpy.data.objects.new('Body guides', armature)
bpy.context.collection.objects.link(rig)
bpy.context.view_layer.objects.active = rig
rig.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
for item in data['Segments']:
    bone = armature.edit_bones.new(str(item['Index']))
    bone.head = item['Head']
    bone.tail = item['Tail']
bpy.ops.object.mode_set(mode='OBJECT')
obj.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.parent_set(type='ARMATURE_AUTO')
target_mesh = bpy.data.meshes.new('Original surface')
target_mesh.from_pydata(data['Points'], [], data['Faces'])
target_mesh.update()
target = bpy.data.objects.new('Original surface', target_mesh)
bpy.context.collection.objects.link(target)
bpy.ops.object.select_all(action='DESELECT')
obj.select_set(True)
target.select_set(True)
bpy.context.view_layer.objects.active = obj
bpy.ops.object.data_transfer(data_type='VGROUP_WEIGHTS', use_create=True,
    vert_mapping='POLYINTERP_NEAREST', layers_select_src='ALL', layers_select_dst='NAME')
weights = []
missing = 0
for vertex in target_mesh.vertices:
    values = [(int(target.vertex_groups[group.group].name), group.weight)
              for group in vertex.groups if group.weight > 0.0001]
    values.sort(key=lambda item: item[1], reverse=True)
    values = values[:4]
    total = sum(weight for _, weight in values)
    if total <= 0:
        missing += 1
    weights.append({'Indices': [bone for bone, _ in values],
                    'Weights': [weight / total for _, weight in values]})
with open(destination, 'w', encoding='utf-8') as stream:
    json.dump({'Vertices': weights, 'Missing': missing}, stream, allow_nan=False)
print('Bone heat binding:', len(weights), 'vertices;', missing, 'without weights')
