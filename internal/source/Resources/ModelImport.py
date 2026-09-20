import bpy
import os
import sys

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
