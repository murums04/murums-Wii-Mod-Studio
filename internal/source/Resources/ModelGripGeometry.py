import math
import heapq
import numpy as np
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree


def proper_triangle_cross(first, second, epsilon):
    normals = [np.cross(p[1] - p[0], p[2] - p[0]) for p in (first, second)]
    lengths = [np.linalg.norm(n) for n in normals]
    if min(lengths) <= epsilon * epsilon:
        return False
    normals = [n / length for n, length in zip(normals, lengths)]
    distances = [(first - second[0]) @ normals[1], (second - first[0]) @ normals[0]]
    if any(min(d) >= -epsilon or max(d) <= epsilon for d in distances):
        return False
    axis = np.cross(*normals)
    length = np.linalg.norm(axis)
    if length < 1e-8:
        return False
    axis /= length
    intervals = []
    for triangle, distance in zip((first, second), distances):
        crossings = []
        for i in range(3):
            j = (i + 1) % 3
            if distance[i] * distance[j] < 0:
                crossings.append(triangle[i] + (triangle[j] - triangle[i]) * distance[i] / (distance[i] - distance[j]))
            elif abs(distance[i]) <= epsilon:
                crossings.append(triangle[i])
        projection = np.asarray(crossings) @ axis
        intervals.append((min(projection), max(projection)))
    return min(i[1] for i in intervals) - max(i[0] for i in intervals) > epsilon


def guard_hand_grips(points, normals, faces, grips, contacts, bone_indices, bone_weights, bones, materials, face_materials):
    """Verhindert neue Durchdringungen durch automatisch erzeugte Fingerhaltungen."""
    if not grips:
        return grips, contacts
    source = np.asarray(points, dtype=float)
    triangles = np.asarray(faces, dtype=np.int32)
    epsilon = np.ptp(source[:, 1]) * 1e-5
    if epsilon <= 0:
        return [], {}
    seam_points = np.round(source / epsilon).astype(np.int64)
    seams = [set(map(tuple, seam_points[face])) for face in triangles]
    visible = [materials[material]['Color'][3] > 0 for material in face_materials]
    grouped = {name: [] for name in contacts}
    wrist_indices = {i: bone['Name'] for i, bone in enumerate(bones) if bone['Name'] in contacts}
    for grip in grips:
        index = grip['Index']
        weights = [(weight, wrist_indices[bone]) for bone, weight in zip(bone_indices[index], bone_weights[index]) if bone in wrist_indices]
        if weights:
            grouped[max(weights)[1]].append(grip)
    posed = source.copy()
    safe_grips, safe_contacts = [], dict(contacts)
    for name, group in grouped.items():
        if not group:
            safe_contacts.pop(name, None)
            continue
        moved = {grip['Index'] for grip in group}
        affected = [i for i, face in enumerate(triangles) if visible[i] and any(v in moved for v in face)]
        if not affected:
            safe_contacts.pop(name, None)
            continue
        def crossings(vertices):
            whole = BVHTree.FromPolygons(vertices.tolist(), triangles.tolist(), all_triangles=True, epsilon=0)
            hand = BVHTree.FromPolygons(vertices.tolist(), triangles[affected].tolist(), all_triangles=True, epsilon=0)
            pairs = set()
            for local, other in hand.overlap(whole):
                first = affected[local]
                if first == other or not visible[other] or seams[first] & seams[other]:
                    continue
                pair = tuple(sorted((first, other)))
                if pair not in pairs and proper_triangle_cross(vertices[triangles[first]], vertices[triangles[other]], epsilon):
                    pairs.add(pair)
            return pairs
        baseline = crossings(source)
        accepted = None
        for strength in (1., .75, .5, .25, .125):
            candidate = posed.copy()
            for grip in group:
                index = grip['Index']
                candidate[index] = source[index] + (np.asarray(grip['Point']) - source[index]) * strength
            if crossings(candidate) - baseline:
                continue
            accepted = strength
            posed = candidate
            for grip in group:
                index = grip['Index']
                normal = Vector(normals[index]).lerp(Vector(grip['Normal']), strength)
                if normal.length < 1e-7:
                    normal = Vector(normals[index])
                normal.normalize()
                safe_grips.append(dict(grip, Point=candidate[index].tolist(), Normal=list(normal)))
            break
        if accepted is None:
            safe_contacts.pop(name, None)
        elif accepted < 1:
            average = sum((Vector(grip['Point']) - Vector(points[grip['Index']]) for grip in group), Vector()) / len(group)
            safe_contacts[name] = dict(contacts[name], Point=list(Vector(contacts[name]['Point']) - average * (1 - accepted)))
            print('Reduced automatic hand closure to avoid surface crossings:', name, accepted)
    return safe_grips, safe_contacts


def open_hand_grip(points, faces, wrist_weights, wrist, elbow, side):
    """Erkennt nur offene, topologisch getrennte Fuenffingerhaende."""
    result = _open_hand_grip(points, faces, wrist_weights, wrist, elbow, side, False)
    return result if result is not None else _open_hand_grip(points, faces, wrist_weights, wrist, elbow, side, True)


def _open_hand_grip(points, faces, wrist_weights, wrist, elbow, side, surface_distance):
    origin, elbow = Vector(wrist), Vector(elbow)
    forearm = origin - elbow
    if forearm.length < 1e-7 or side not in ('l', 'r'):
        return None
    along = forearm.normalized()
    scale = forearm.length
    candidates = {i for i, p in enumerate(points) if wrist_weights[i] >= .65
                  and 0 < (Vector(p) - origin).dot(along)
                  and (Vector(p) - origin).length < scale * .9}
    if len(candidates) < 40:
        return None
    # UV-/Normalennaehte gehoeren zur selben Handoberflaeche.
    tolerance = scale * 1e-5
    weld, canonical, positions, originals = {}, {}, {}, {}
    for i in sorted(candidates):
        p = Vector(points[i])
        key = tuple(round(v / tolerance) for v in p)
        c = weld.setdefault(key, i)
        canonical[i] = c
        positions[c] = p
        originals.setdefault(c, []).append(i)
    adjacent = {i: set() for i in positions}
    for face in faces:
        for a, b in zip(face, face[1:] + face[:1]):
            if a in canonical and b in canonical:
                a, b = canonical[a], canonical[b]
                if a != b:
                    adjacent[a].add(b)
                    adjacent[b].add(a)

    def components(ids):
        remaining, groups = set(ids), []
        while remaining:
            pending = [min(remaining)]
            remaining.remove(pending[0])
            group = []
            while pending:
                i = pending.pop()
                group.append(i)
                neighbors = adjacent[i] & remaining
                remaining.difference_update(neighbors)
                pending.extend(sorted(neighbors))
            groups.append(group)
        return groups

    # Getrennte Accessoires nicht als Teil der Hand verbiegen.
    patches = components(positions)
    patches = [p for p in patches if len(p) >= 40
               and min((positions[i] - origin).length for i in p) < scale * .18]
    if len(patches) != 1:
        return None
    patch = patches[0]
    radial = {i: (positions[i] - origin).length for i in patch}
    extent = max(radial.values())
    if extent < scale * .18:
        return None
    # Oberflaechenwege trennen auch gekruemmte Finger an ihrer eigenen Handbasis.
    roots = [i for i in patch if radial[i] <= min(radial.values()) + extent * .08]
    distance = {i: math.inf for i in patch}
    pending = []
    for i in roots:
        distance[i] = radial[i]
        heapq.heappush(pending, (distance[i], i))
    while pending:
        current_distance, i = heapq.heappop(pending)
        if current_distance != distance[i]:
            continue
        for neighbor in adjacent[i]:
            candidate_distance = current_distance + (positions[i] - positions[neighbor]).length
            if candidate_distance < distance[neighbor]:
                distance[neighbor] = candidate_distance
                heapq.heappush(pending, (candidate_distance, neighbor))
    if surface_distance:
        radial = distance
    reach = max(radial.values())
    cloud = np.asarray([list(positions[i] - origin) for i in patch])
    centered = cloud - cloud.mean(axis=0)
    eigenvalues, eigenvectors = np.linalg.eigh(centered.T @ centered)
    if eigenvalues[1] <= 1e-12 * scale * scale or (not surface_distance and eigenvalues[0] > eigenvalues[1] * .2):
        return None
    selected = None
    fractions = np.arange(.24, .89, .04) if surface_distance else (.32,.36,.40,.44,.48,.52,.56,.60,.64)
    sections = [(reach * fraction, [g for g in components(i for i in patch if radial[i] > reach * fraction)
                                   if not surface_distance or len(g) >= 8]) for fraction in fractions]
    for threshold, groups in sections:
        if len(groups) != 5 or min(map(len, groups)) < 8:
            continue
        # Der kurze Daumen trennt sich frueher als die langen Finger von der Handflaeche.
        tips = [set(group) for group in groups]
        branches_at_base = []
        for tip in tips:
            for branch_threshold, earlier in sections:
                if branch_threshold > threshold:
                    break
                branch = next((group for group in earlier if tip <= set(group)
                               and sum(bool(set(group) & other) for other in tips) == 1), None)
                if branch is not None:
                    branches_at_base.append((branch_threshold, branch))
                    break
        branches = []
        for branch_threshold, group in branches_at_base:
            boundary = [i for i in group if any(n in radial and radial[n] <= branch_threshold for n in adjacent[i])]
            if not boundary:
                break
            base = sum((positions[i] for i in boundary), Vector()) / len(boundary)
            tip_extent = max((positions[i] - base).length for i in group)
            tip_ids = [i for i in group if (positions[i] - base).length >= tip_extent * .94]
            tip = sum((positions[i] for i in tip_ids), Vector()) / len(tip_ids)
            axis = tip - base
            length = axis.length
            if length < extent * .15:
                break
            axis.normalize()
            width = max((positions[i] - base - axis * (positions[i] - base).dot(axis)).length for i in group)
            if width > length * .40:
                break
            branches.append((group, base, axis, length, width, branch_threshold))
        if len(branches) != 5:
            continue
        # Ein anliegender Daumen bleibt durch seine proximale Basis erkennbar.
        thumb = min(branches, key=lambda b: (b[5], b[2].dot(along))) if surface_distance else min(branches, key=lambda b: b[2].dot(along))
        fingers = [b for b in branches if b is not thumb]
        finger_direction = sum((b[2] for b in fingers), Vector()).normalized()
        thumb_alignment = thumb[2].dot(finger_direction)
        thumb_offset = (sum((b[1] for b in fingers), Vector()) / 4 - thumb[1]).dot(finger_direction)
        if (finger_direction.dot(along) < (.35 if surface_distance else .65) or thumb_alignment <= .15
                or (not surface_distance and thumb_alignment >= .88)
                or (thumb_alignment >= .88 and thumb_offset < extent * .12
                    and min(b[5] for b in fingers) - thumb[5] < extent * .12)
                or min(b[2].dot(finger_direction) for b in fingers) < .8):
            continue
        across = sum((b[1] for b in fingers), Vector()) / 4 - thumb[1]
        across -= finger_direction * across.dot(finger_direction)
        if across.length < extent * .12:
            continue
        across.normalize()
        if surface_distance:
            bases = np.asarray([list(b[1]) for b in fingers])
            bases -= bases.mean(axis=0)
            direction = np.asarray(finger_direction)
            bases -= (bases @ direction)[:, None] * direction
            _, base_axes = np.linalg.eigh(bases.T @ bases)
            fitted_across = Vector(base_axes[:, -1])
            across = fitted_across * (1 if fitted_across.dot(across) >= 0 else -1)
        lateral = sorted(b[1].dot(across) for b in fingers)
        if lateral[-1] - lateral[0] < extent * .22 or min(b-a for a, b in zip(lateral, lateral[1:])) < extent * .045:
            continue
        palm = finger_direction.cross(across).normalized() * (-1 if side == 'l' else 1)
        if not surface_distance:
            plane_normal = Vector(eigenvectors[:, 0])
            palm = plane_normal * (1 if plane_normal.dot(palm) >= 0 else -1)
        across = (across - palm * across.dot(palm)).normalized()
        # Keine dicken Faust-/Faeustlingsformen durch den Lueckenfinder akzeptieren.
        palm_points = ([positions[i] for branch in fingers for i in branch[0] if radial[i] <= branch[5] + extent * .06]
                       if surface_distance else [positions[i] for i in patch if radial[i] <= threshold])
        thickness = max((p - origin).dot(palm) for p in palm_points) - min((p - origin).dot(palm) for p in palm_points)
        if thickness > extent * .4:
            continue
        selected = (branches, thumb, across, palm, finger_direction)
        break
    if selected is None:
        return None
    branches, thumb, across, palm, finger_direction = selected
    deformations = {}
    grip_centers = []
    for group, base, axis, length, width, branch_threshold in branches:
        bend = palm - axis * palm.dot(axis)
        if bend.length < .9:
            return None
        bend.normalize()
        boundary = [i for i in group if any(n in radial and radial[n] <= branch_threshold for n in adjacent[i])]
        base += axis * max((positions[i] - base).dot(axis) for i in boundary)
        length = max((positions[i] - base).dot(axis) for i in group)
        if length <= extent * .1:
            return None
        angle = math.radians(60 if group is thumb[0] else 110)
        bend_width = max(abs((positions[i] - base).dot(bend)) for i in group)
        radius = max(length / angle, bend_width / .35 if surface_distance else width / .30)
        achieved_angle = length / radius
        if achieved_angle < math.radians(30 if group is thumb[0] else 60):
            return None
        if group is not thumb[0]:
            grip_centers.append(base + bend * radius)
        for i in group:
            p = positions[i]
            distance = max(0, min(length, (p - base).dot(axis)))
            theta = distance / radius
            rotation = Matrix.Rotation(theta, 3, axis.cross(bend))
            center = base + axis * (radius * math.sin(theta)) + bend * (radius * (1 - math.cos(theta)))
            offset = p - base - axis * distance
            posed = center + rotation @ offset
            tangent_scale = 1 - offset.dot(bend) / radius
            if tangent_scale <= .3:
                return None
            normal_scale = 1 / tangent_scale - 1 if distance > 0 else 0
            normal_matrix = rotation @ Matrix([[float(a == b) + normal_scale * axis[a] * axis[b]
                                                for b in range(3)] for a in range(3)])
            for original in originals[i]:
                deformations[original] = (posed, normal_matrix)
    for face in faces:
        for i, neighbor in zip(face, face[1:] + face[:1]):
            if i not in deformations and neighbor not in deformations:
                continue
            original, other = Vector(points[i]), Vector(points[neighbor])
            old_length = (original - other).length
            first = deformations.get(i, (original, None))[0]
            second = deformations.get(neighbor, (other, None))[0]
            if old_length > tolerance and not (.65 <= (first - second).length / old_length <= 1.35):
                return None
        if not any(i in deformations for i in face):
            continue
        for j in range(1, len(face)-1):
            triangle = (face[0], face[j], face[j+1])
            original = [Vector(points[i]) for i in triangle]
            posed = [deformations.get(i, (p, None))[0] for i, p in zip(triangle, original)]
            normal = (original[1]-original[0]).cross(original[2]-original[0])
            bent_normal = (posed[1]-posed[0]).cross(posed[2]-posed[0])
            if normal.length <= tolerance*tolerance:
                continue
            expected = sum((deformations[i][1] @ normal if i in deformations else normal for i in triangle), Vector())
            if (not (.4 <= bent_normal.length/normal.length <= 2.25)
                    or bent_normal.normalized().dot(expected.normalized()) < .2):
                return None
    return {'Vertices': deformations,
            'Contact': {'Point': list(sum(grip_centers, Vector()) / len(grip_centers)),
                        'Axis': list(across)}}
