import bpy
import os
import sys
import base64
import json
import struct
import zlib



def normalize_specular_slots(path):
    with open(path, 'rb') as stream:
        content = stream.read()
    length, kind = struct.unpack_from('<II', content, 12)
    if content[:4] != b'glTF' or kind != 0x4e4f534a:
        raise ValueError('The internal model converter did not produce a GLB file.')
    document = json.loads(content[20:20 + length])
    affected = [m.get('extensions', {}).get('KHR_materials_specular', {})
                for m in document.get('materials', [])]
    affected = [m for m in affected if 'specularColorTexture' in m and 'specularTexture' not in m]
    if not affected:
        return

    # Assimp verlangt Textur-Slot 0 vor Slot 1; weisses Alpha erhaelt die Staerke unveraendert.
    def chunk(kind, data):
        return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', zlib.crc32(kind + data))

    png = (b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', 1, 1, 8, 6, 0, 0, 0))
           + chunk(b'IDAT', zlib.compress(b'\x00\xff\xff\xff\xff')) + chunk(b'IEND', b''))
    images = document.setdefault('images', [])
    textures = document.setdefault('textures', [])
    images.append({'uri': 'data:image/png;base64,' + base64.b64encode(png).decode('ascii')})
    textures.append({'source': len(images) - 1})
    for material in affected:
        material['specularTexture'] = {'index': len(textures) - 1}
    encoded = json.dumps(document, separators=(',', ':')).encode('utf-8')
    encoded += b' ' * (-len(encoded) % 4)
    tail = content[20 + length:]
    with open(path, 'wb') as stream:
        stream.write(struct.pack('<4sII', b'glTF', 2, 20 + len(encoded) + len(tail)))
        stream.write(struct.pack('<II', len(encoded), kind))
        stream.write(encoded)
        stream.write(tail)
    print('Normalized specular texture slots:', len(affected))


# Statische USD-Skinningdaten vor der Konvertierung mit OpenUSD auswerten.
def repair_static_usd(path):
    import math
    import re
    from pxr import Usd, UsdGeom, UsdSkel, Gf
    from mathutils import Matrix, Vector
    stage = Usd.Stage.Open(path)
    skeletons = [p for p in stage.Traverse() if p.IsA(UsdSkel.Skeleton)]
    if not skeletons:
        return
    for prim in stage.Traverse():
        for attribute in prim.GetAttributes():
            times = attribute.GetTimeSamples()
            if len(times) > 1:
                first = attribute.Get(times[0])
                if any((attribute.Get(time) != first for time in times[1:])):
                    return
    if any((obj.type == 'MESH' and obj.data.shape_keys for obj in bpy.context.scene.objects)):
        return
    cache = UsdSkel.Cache()
    for prim in stage.Traverse():
        if prim.IsA(UsdSkel.Root):
            cache.Populate(UsdSkel.Root(prim), Usd.PrimDefaultPredicate)
    unit = UsdGeom.GetStageMetersPerUnit(stage)
    if not math.isfinite(unit) or unit <= 0:
        return
    up = str(UsdGeom.GetStageUpAxis(stage))

    def convert(point):
        x, y, z = point
        return Vector((x, -z, y) if up == 'Y' else (x, y, z)) * unit

    def human_names(points, parents):
        children = [[j for j, p in enumerate(parents) if p == i] for i in range(len(points))]

        def chain(start):
            result = [start]
            while len(children[result[-1]]) == 1:
                result.append(children[result[-1]][0])
            return result
        wrists = [i for i, ch in enumerate(children) if len(ch) == 5 and all((3 <= len(chain(c)) <= 5 for c in ch))]
        if len(wrists) != 2:
            return {}

        def ancestors(i):
            result = []
            while i >= 0:
                result.append(i)
                i = parents[i]
            return result
        common = next((i for i in ancestors(wrists[0]) if i in ancestors(wrists[1])), None)
        if common is None:
            return {}
        feet = []
        hips = None
        for ancestor in ancestors(common):
            branches = [chain(c) for c in children[ancestor] if c not in ancestors(common)]
            legs = [c for c in branches if len(c) >= 3 and points[c[1]].z < points[ancestor].z and (points[c[0]].z > points[c[1]].z > points[c[2]].z)]
            if len(legs) == 2:
                hips = ancestor
                feet = legs
                break
        if hips is None:
            return {}
        head_candidates = [c for c in children[common] if all((c not in ancestors(w) for w in wrists)) and points[c].z > points[common].z and children[c]]
        if len(head_candidates) != 1:
            return {}
        head = chain(head_candidates[0])[-1]
        height = points[head].z - min((points[c[2]].z for c in feet))
        if height <= 0:
            return {}
        mapping = {hips: 'hips', head: 'head'}
        spine = next((c for c in children[hips] if c in ancestors(common)), None)
        if spine is None:
            return {}
        mapping[spine] = 'spine'
        wrists.sort(key=lambda i: points[i].x, reverse=True)
        feet.sort(key=lambda c: points[c[0]].x, reverse=True)
        for side, wrist, leg in zip(('left', 'right'), wrists, feet):
            arm = [parents[parents[wrist]], parents[wrist], wrist]
            if min(arm) < 0:
                return {}
            lengths = [(points[arm[i + 1]] - points[arm[i]]).length / height for i in range(2)]
            lengths += [(points[leg[i + 1]] - points[leg[i]]).length / height for i in range(2)]
            if any((v < 0.06 or v > 0.4 for v in lengths)):
                return {}
            for i, name in zip(arm, ('upperarm', 'forearm', 'hand')):
                mapping[i] = side + name
            for i, name in zip(leg[:3], ('thigh', 'calf', 'foot')):
                mapping[i] = side + name
        return mapping
    prepared = []
    for prim in skeletons:
        query = cache.GetSkelQuery(UsdSkel.Skeleton(prim))
        if not query:
            return
        names = [str(n).rsplit('/', 1)[-1] for n in query.GetJointOrder()]
        if len(set(names)) != len(names):
            return
        parents = list(query.GetTopology().GetParentIndices())
        if len(parents) != len(names) or any((parent >= i or parent < -1 for i, parent in enumerate(parents))):
            return
        world = UsdGeom.Xformable(prim).ComputeLocalToWorldTransform(Usd.TimeCode.Default())
        points = [convert((matrix * world).ExtractTranslation()) for matrix in query.ComputeJointSkelTransforms(Usd.TimeCode.Default())]
        if any((not math.isfinite(value) for point in points for value in point)):
            return
        generic = all((re.fullmatch('(?:n|joint|bone)[_ .-]*\\d+', name, re.IGNORECASE) for name in names))
        renamed = human_names(points, parents) if generic else {}
        final_names = [renamed.get(i, name) for i, name in enumerate(names)]
        if len(set(final_names)) != len(final_names):
            return
        meshes = []
        for mesh_prim in stage.Traverse():
            if not mesh_prim.IsA(UsdGeom.Mesh):
                continue
            skeleton = UsdSkel.BindingAPI(mesh_prim).GetInheritedSkeleton()
            if not skeleton or skeleton.GetPrim() != prim:
                continue
            matches = [o for o in bpy.context.scene.objects if o.type == 'MESH' and o.name == mesh_prim.GetName()]
            vertices = UsdGeom.Mesh(mesh_prim).GetPointsAttr().Get()
            if len(matches) != 1 or vertices is None or len(vertices) != len(matches[0].data.vertices):
                return
            skin = cache.GetSkinningQuery(mesh_prim)
            if not skin or not skin.ComputeSkinnedPoints(query.ComputeSkinningTransforms(Usd.TimeCode.Default()), vertices):
                return
            meshes.append((matches[0], [convert(world.Transform(Gf.Vec3d(*v))) for v in vertices]))
        if not meshes:
            continue
        prepared.append((names, parents, points, renamed, meshes))
    for names, parents, points, renamed, meshes in prepared:
        data = bpy.data.armatures.new('Studio USD skeleton')
        armature = bpy.data.objects.new('Studio USD skeleton', data)
        bpy.context.collection.objects.link(armature)
        bpy.ops.object.select_all(action='DESELECT')
        bpy.context.view_layer.objects.active = armature
        armature.select_set(True)
        bpy.ops.object.mode_set(mode='EDIT')
        bones = []
        for i, name in enumerate(names):
            bone = data.edit_bones.new(renamed.get(i, name))
            bone.head = points[i]
            child = next((j for j, p in enumerate(parents) if p == i and (points[j] - points[i]).length > 1e-06), None)
            bone.tail = points[child] if child is not None else points[i] + Vector((0, 0, 0.01))
            if parents[i] >= 0:
                bone.parent = bones[parents[i]]
            bones.append(bone)
        bpy.ops.object.mode_set(mode='OBJECT')
        for obj, vertices in meshes:
            obj.parent = None
            obj.matrix_world = Matrix.Identity(4)
            for modifier in list(obj.modifiers):
                if modifier.type == 'ARMATURE':
                    obj.modifiers.remove(modifier)
            for v, point in zip(obj.data.vertices, vertices):
                v.co = point
            obj.data.normals_split_custom_set([(0, 0, 0)] * len(obj.data.loops))
            obj.data.update()
            for i, new in renamed.items():
                group = obj.vertex_groups.get(names[i])
                if group:
                    group.name = new
            modifier = obj.modifiers.new('Studio USD skeleton', 'ARMATURE')
            modifier.object = armature
        print('USD skeleton restored:', len(points), 'mapped:', len(renamed))
    used = {o.find_armature() for o in bpy.context.scene.objects if o.type == 'MESH'}
    for obj in list(bpy.context.scene.objects):
        if obj.type == 'ARMATURE' and obj not in used and (not obj.children):
            bpy.data.objects.remove(obj, do_unlink=True)


arguments = sys.argv[sys.argv.index('--') + 1:]
source, destination = arguments[:2]
limit = int(arguments[2]) if len(arguments) > 2 else 0
# Fremdmodelle ohne Blender-Startobjekte laden; BLEND bringt seine eigene Szene mit.
if not source.lower().endswith('.blend'):
    bpy.ops.wm.read_factory_settings(use_empty=True)
if source.lower().endswith('.blend'):
    bpy.ops.wm.open_mainfile(filepath=source, load_ui=False, use_scripts=False)
elif source.lower().endswith('.usdz'):
    bpy.ops.wm.usd_import(filepath=source, import_textures_mode='IMPORT_PACK')
    repair_static_usd(source)
elif source.lower().endswith(('.glb', '.gltf')):
    bpy.ops.import_scene.gltf(filepath=source)
elif source.lower().endswith('.obj'):
    bpy.ops.wm.obj_import(filepath=source)
else:
    raise RuntimeError('Unsupported input format')
objects = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
if not objects:
    raise RuntimeError('The model contains no mesh objects')
if limit:
    total = sum(sum(len(p.vertices) - 2 for p in obj.data.polygons) for obj in objects)
    for obj in objects:
        bpy.context.view_layer.objects.active = obj
        # Nur die Vorschaukopie vereinfachen, keine Quelldatei speichern.
        if obj.data.shape_keys:
            obj.shape_key_clear()
        if total > limit:
            modifier = obj.modifiers.new('Studio preview', 'DECIMATE')
            modifier.ratio = limit / total
            bpy.ops.object.modifier_apply(modifier=modifier.name)
bpy.ops.export_scene.gltf(filepath=destination, export_format='GLB', export_animations=not bool(limit))
if not os.path.isfile(destination):
    raise RuntimeError('Model conversion produced no output')

normalize_specular_slots(destination)
