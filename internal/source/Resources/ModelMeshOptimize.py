import ctypes
import os
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform


def _surface_locks(data, texture_folder):
    import bpy
    import numpy

    images = {}
    for index, material in enumerate(data['Materials']):
        if not material.get('Texture'):
            continue
        image = bpy.data.images.load(os.path.join(texture_folder, material['Texture']), check_existing=True)
        pixels = numpy.empty(len(image.pixels), dtype=numpy.float32)
        image.pixels.foreach_get(pixels)
        width, height = image.size
        images[index] = pixels.reshape(height, width, 4)

    def color(material, uv):
        pixels = images.get(material)
        if pixels is None:
            return Vector(data['Materials'][material]['Color'][:3])
        height, width = pixels.shape[:2]
        return Vector(pixels[int(uv[1] % 1 * height), int(uv[0] % 1 * width), :3])

    vertex_materials = [set() for _ in data['Points']]
    for face, material in zip(data['Faces'], data['FaceMaterials']):
        for vertex in face:
            vertex_materials[vertex].add(material)
    surface = BVHTree.FromPolygons(data['Points'], data['Faces'], all_triangles=True)
    height = max(p[1] for p in data['Points']) - min(p[1] for p in data['Points'])
    distance = max(height * .002, 1e-7)
    locks = [0] * len(data['Points'])
    # Eng anliegende, verschiedenfarbige Schichten duerfen nicht ineinander fallen.
    for vertex, (point, normal) in enumerate(zip(data['Points'], data['Normals'])):
        if not vertex_materials[vertex]:
            continue
        point, normal = Vector(point), Vector(normal).normalized()
        for direction in (normal, -normal):
            hit, hit_normal, face_index, _ = surface.ray_cast(point + direction * (distance * .0005), direction, distance)
            if hit is None or normal.dot(hit_normal) <= .65:
                continue
            face = data['Faces'][face_index]
            uv = barycentric_transform(hit, *[Vector(data['Points'][v]) for v in face],
                                       *[Vector(data['Uvs'][v] + [0]) for v in face])
            other = color(data['FaceMaterials'][face_index], uv)
            if any((color(material, data['Uvs'][vertex]) - other).length > .18
                   for material in vertex_materials[vertex]):
                locks[vertex] = 1
                break
    return locks


def simplify(data, limit, texture_folder, distant=False):
    if limit <= 0 or len(data['Faces']) <= limit:
        return
    library = ctypes.CDLL(os.path.join(os.environ['STUDIO_MODEL_RUNTIME'], 'meshoptimizer.dll'))
    function = library.meshopt_simplifyWithAttributes
    uint_pointer = ctypes.POINTER(ctypes.c_uint)
    float_pointer = ctypes.POINTER(ctypes.c_float)
    function.restype = ctypes.c_size_t
    function.argtypes = [uint_pointer, uint_pointer, ctypes.c_size_t, float_pointer, ctypes.c_size_t,
                        ctypes.c_size_t, float_pointer, ctypes.c_size_t, float_pointer, ctypes.c_size_t,
                        ctypes.POINTER(ctypes.c_ubyte), ctypes.c_size_t, ctypes.c_float, ctypes.c_uint,
                        float_pointer]
    locks = [0] * len(data['Points']) if distant else _surface_locks(data, texture_folder)
    grouped = {}
    for face, material in zip(data['Faces'], data['FaceMaterials']):
        grouped.setdefault(material, []).append(face)
    sloppy = library.meshopt_simplifySloppy
    sloppy.restype = ctypes.c_size_t
    sloppy.argtypes = [uint_pointer, uint_pointer, ctypes.c_size_t, float_pointer, ctypes.c_size_t,
                       ctypes.c_size_t, ctypes.POINTER(ctypes.c_ubyte), ctypes.c_size_t,
                       ctypes.c_float, float_pointer]
    result_faces, result_materials = [], []
    for material, faces in grouped.items():
        welded, originals, mapping, local_locks = {}, [], {}, []
        for vertex in sorted({v for face in faces for v in face}):
            key = (tuple(round(n, 5) for n in data['Points'][vertex]),
                   tuple(round(n, 6) for n in data['Uvs'][vertex]),
                   tuple(round(n, 4) for n in data['Normals'][vertex]),
                   tuple(zip(data['BoneIndices'][vertex], (round(n, 4) for n in data['BoneWeights'][vertex]))))
            if key not in welded:
                welded[key] = len(originals)
                originals.append(vertex)
                local_locks.append(0)
            mapping[vertex] = welded[key]
            local_locks[welded[key]] |= locks[vertex]
        vertices = (ctypes.c_float * (len(originals) * 3))(*(n for v in originals for n in data['Points'][v]))
        attributes = (ctypes.c_float * (len(originals) * 5))(*(n for v in originals for n in data['Normals'][v] + data['Uvs'][v]))
        indices = (ctypes.c_uint * (len(faces) * 3))(*(mapping[v] for face in faces for v in face))
        destination = (ctypes.c_uint * len(indices))()
        weights = (ctypes.c_float * 5)(.5, .5, .5, 1, 1)
        protected = (ctypes.c_ubyte * len(originals))(*local_locks)
        error = ctypes.c_float()
        budget = max(48, int(limit * len(faces) / len(data['Faces'])))
        count = function(destination, indices, len(indices), vertices, len(originals), 12,
                         attributes, 20, weights, 5, protected, min(len(indices), budget * 3),
                         .003, 0, ctypes.byref(error))
        if distant and count > budget * 3:
            reduced = (ctypes.c_uint * len(indices))()
            reduced_count = sloppy(reduced, indices, len(indices), vertices, len(originals), 12,
                                   None, min(len(indices), budget * 3), 1.0, ctypes.byref(error))
            if reduced_count > 0:
                destination, count = reduced, reduced_count
        if count == 0 or count % 3 or count > len(indices):
            raise ValueError('Invalid simplified model surface.')
        result_faces.extend([[originals[destination[i + j]] for j in range(3)] for i in range(0, count, 3)])
        result_materials.extend([material] * (count // 3))
    print('Surface optimization:', len(data['Faces']), '->', len(result_faces), 'triangles;', sum(locks), 'protected vertices')
    data['Faces'], data['FaceMaterials'] = result_faces, result_materials
    used = sorted({v for face in result_faces for v in face})
    remap = {vertex: index for index, vertex in enumerate(used)}
    data['Faces'] = [[remap[v] for v in face] for face in result_faces]
    for key in ('Points', 'Normals', 'Uvs', 'BoneIndices', 'BoneWeights', 'SourceBoneIndices', 'SourceBoneWeights'):
        if data.get(key) is not None:
            data[key] = [data[key][v] for v in used]
