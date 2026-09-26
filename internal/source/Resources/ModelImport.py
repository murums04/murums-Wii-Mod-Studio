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


arguments = sys.argv[sys.argv.index('--') + 1:]
source, destination = arguments[:2]
limit = int(arguments[2]) if len(arguments) > 2 else 0
if source.lower().endswith('.blend'):
    bpy.ops.wm.open_mainfile(filepath=source, load_ui=False, use_scripts=False)
elif source.lower().endswith('.usdz'):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.wm.usd_import(filepath=source, import_textures_mode='IMPORT_PACK')
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
