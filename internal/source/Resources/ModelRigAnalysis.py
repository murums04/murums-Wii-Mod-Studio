import math
import re


REQUIRED = ('skl_root', 'spin', 'face_1', 'arm_l1', 'arm_l2', 'wrist_l1',
            'arm_r1', 'arm_r2', 'wrist_r1', 'leg_l1', 'leg_l2', 'ankle_l1',
            'leg_r1', 'leg_r2', 'ankle_r1')


def human_bone(name):
    if name in REQUIRED:
        return name
    text = re.sub(r'[^a-z]', '', name.lower())
    if any(word in text for word in ('adjust', 'twist', 'end', 'weapon')):
        return None
    for prefix in ('mixamorig', 'bip', 'def'):
        if text.startswith(prefix):
            text = text[len(prefix):]
    centers = {'hips': 'skl_root', 'hip': 'skl_root', 'pelvis': 'skl_root', 'spine': 'spin', 'head': 'face_1'}
    if text in centers:
        return centers[text]
    for side, longside in (('l', 'left'), ('r', 'right')):
        part = None
        if text.startswith(longside):
            part = text[len(longside):]
        elif text.endswith(side):
            part = text[:-1]
        elif text.startswith(side):
            part = text[1:]
        targets = {'upperarm': 'arm_'+side+'1', 'arm': 'arm_'+side+'1',
                   'forearm': 'arm_'+side+'2', 'lowerarm': 'arm_'+side+'2', 'elbow': 'arm_'+side+'2',
                   'hand': 'wrist_'+side+'1', 'thigh': 'leg_'+side+'1', 'upleg': 'leg_'+side+'1',
                   'calf': 'leg_'+side+'2', 'shin': 'leg_'+side+'2', 'lowerleg': 'leg_'+side+'2',
                   'knee': 'leg_'+side+'2', 'upperleg': 'leg_'+side+'1', 'leg': 'leg_'+side+'?',
                   'foot': 'ankle_'+side+'1', 'ankle': 'ankle_'+side+'1'}
        if part in targets:
            return targets[part]
    return None


def analyze(nodes):
    if len(nodes) > 4096:
        return {}, [], 'too_many_joints'
    children = [[] for _ in nodes]
    for i, node in enumerate(nodes):
        parent = node['Parent']
        if parent >= len(nodes) or parent == i or parent < -1:
            return {}, [], 'invalid_hierarchy'
        if parent >= 0:
            children[parent].append(i)

    def ancestors(index):
        result = []
        while index >= 0 and index not in result:
            result.append(index)
            index = nodes[index]['Parent']
        if index >= 0:
            raise ValueError('Cyclic skeleton')
        return result

    lineage = [ancestors(i) for i in range(len(nodes))]
    targets = [human_bone(node['Name']) for node in nodes]
    for i, target in enumerate(targets):
        if target and target.endswith('?'):
            parent = nodes[i]['Parent']
            upper, lower = target[:-1]+'1', target[:-1]+'2'
            targets[i] = (lower if parent >= 0 and targets[parent] == upper else
                          upper if any(targets[c] == lower for c in children[i]) else None)
    named = {}
    for i, node in enumerate(nodes):
        target = targets[i]
        if target:
            named.setdefault(target, []).append(i)
    direct = {i: target for target, ids in named.items() for i in ids}
    trusted = {target for target, ids in named.items() if len(ids) == 1}
    if 'spin' in named and all(a in lineage[b] or b in lineage[a] for a in named['spin'] for b in named['spin']):
        trusted.add('spin')
    # Gleiche Namen allein belegen noch keine anatomische Kette.
    chains = [('skl_root', 'spin', 'face_1')]
    for side in ('l', 'r'):
        chains.extend([('arm_'+side+'1', 'arm_'+side+'2', 'wrist_'+side+'1'),
                       ('leg_'+side+'1', 'leg_'+side+'2', 'ankle_'+side+'1'),
                       ('skl_root', 'spin', 'arm_'+side+'1'),
                       ('skl_root', 'leg_'+side+'1')])
    reverse = {target: ids[0] for target, ids in named.items()}
    for chain in chains:
        present = [name for name in chain if name in reverse]
        for a, b in zip(present, present[1:]):
            if reverse[a] not in lineage[reverse[b]][1:]:
                trusted.difference_update(chain)
    if len(trusted) == len(REQUIRED):
        return direct, sorted(trusted), 'semantic'

    def straight_chain(index):
        chain = [index]
        while len(children[chain[-1]]) == 1 and len(chain) < 12:
            chain.append(children[chain[-1]][0])
        return chain

    wrists = [i for i in range(len(nodes)) if len(children[i]) == 5
              and all(3 <= len(straight_chain(c)) <= 5 for c in children[i])]
    if len(wrists) != 2:
        return direct, sorted(trusted), 'partial' if direct else 'unresolved'
    common = next((i for i in lineage[wrists[0]] if i in lineage[wrists[1]]), -1)
    if common < 0:
        return direct, sorted(trusted), 'partial' if direct else 'unresolved'
    arms = [list(reversed(lineage[w][:3])) for w in wrists]
    if any(len(arm) != 3 or common in arm for arm in arms):
        return direct, sorted(trusted), 'partial' if direct else 'unresolved'

    def length(chain):
        return sum(math.dist(nodes[a]['Point'], nodes[b]['Point']) for a, b in zip(chain, chain[1:]))

    arm_length = sum(length(arm) for arm in arms) / 2
    candidates = []
    for pelvis in lineage[common][1:]:
        branches = [straight_chain(c) for c in children[pelvis] if c not in lineage[common]]
        legs = [chain for chain in branches if 3 <= len(chain) <= 6
                and arm_length * .8 < length(chain[:3]) < arm_length * 3.5]
        if len(legs) == 2:
            candidates.append((pelvis, legs))
    if len(candidates) != 1:
        return direct, sorted(trusted), 'partial' if direct else 'unresolved'
    pelvis, legs = candidates[0]
    proposals = {}
    # Die Seiten bleiben Vorschlaege: Topologie allein bestimmt keine Haendigkeit.
    for pair, labels in ((arms, ('arm_', 'arm_', 'wrist_')), (legs, ('leg_', 'leg_', 'ankle_'))):
        known_left = next((chain for chain in pair if any(direct.get(i, '').endswith('_l1') for i in chain)), None)
        if known_left is not None:
            pair = [known_left, next(chain for chain in pair if chain is not known_left)]
        for chain, side in zip(pair, ('l', 'r')):
            for index, prefix, suffix in zip(chain[:3], labels, ('1', '2', '1')):
                proposals[index] = prefix + side + suffix
    proposals[pelvis] = 'skl_root'
    torso = list(reversed(lineage[common][:lineage[common].index(pelvis)]))
    if torso:
        proposals[torso[0]] = 'spin'
    heads = [c for c in children[common] if all(c not in lineage[w] for w in wrists)
             and len(children[c]) == 1]
    if len(heads) == 1:
        proposals[children[heads[0]][0]] = 'face_1'
    for index, target in proposals.items():
        if index not in direct and target not in direct.values() and target not in named:
            direct[index] = target
    return direct, sorted(trusted), 'hierarchy'
