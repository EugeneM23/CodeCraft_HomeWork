import sys
sys.path.insert(0, r'C:/!!!Work/CodeCraft_HomeWork_ATOMIC/Tools/Maya/RegaxGibs')
import importlib, gibs_common; importlib.reload(gibs_common)
from gibs_common import *
from collections import defaultdict

# Cap UVs land in a small red, fleshy patch of the base colour map.
CAP_UV_CENTER = (0.74, 0.81)
CAP_UV_SIZE = 0.05

if cmds.objExists('Gibs'):
    cmds.delete('Gibs')
cmds.group(empty=True, name='Gibs')

sc, infl, W = skin_data(SRC_MESH)
groups = [piece_of_bone(i) for i in infl]
path = dag(SRC_MESH)
it = om.MItMeshPolygon(path)
face_group = []
while not it.isDone():
    acc = defaultdict(float)
    for v in it.getVertices():
        for k, w in enumerate(W[v]):
            if w > 0:
                acc[groups[k]] += w
    face_group.append(max(acc, key=acc.get))
    it.next()
nf = len(face_group)


def plain_copy(name):
    dup = cmds.duplicate(SRC_MESH, name=name)[0]
    for s in cmds.listRelatives(dup, shapes=True, fullPath=True) or []:
        if cmds.getAttr(s + '.intermediateObject'):
            cmds.delete(s)
    cmds.parent(dup, 'Gibs')
    for a in 'trs':
        for x in 'xyz':
            cmds.setAttr('%s.%s%s' % (dup, a, x), lock=False)
    return dup


def cap_uvs(obj, faces):
    if not faces:
        return
    comps = ['%s.f[%d]' % (obj, f) for f in faces]
    cmds.polyAutoProjection(comps, planes=6, layoutMethod=0, percentageSpace=2, scaleMode=1, optimize=1)
    uvs = cmds.polyListComponentConversion(comps, toUV=True)
    us = cmds.polyEditUV(uvs, q=True)
    u = us[0::2]; v = us[1::2]
    cu, cv = (min(u) + max(u)) / 2, (min(v) + max(v)) / 2
    k = CAP_UV_SIZE / max(max(u) - min(u), max(v) - min(v), 1e-6)
    cmds.polyEditUV(uvs, pu=cu, pv=cv, su=k, sv=k)
    cmds.polyEditUV(uvs, u=CAP_UV_CENTER[0] - cu, v=CAP_UV_CENTER[1] - cv)


def close_holes(obj, fan=True):
    """Caps every open border; returns how many holes were closed."""
    before = cmds.polyEvaluate(obj, face=True)
    cmds.polyCloseBorder(obj + '.e[*]')
    after = cmds.polyEvaluate(obj, face=True)
    caps = list(range(before, after))
    if fan and caps:
        nv = cmds.polyEvaluate(obj, vertex=True)
        cmds.polyPoke(['%s.f[%d]' % (obj, f) for f in caps])
        # Each fan hangs off the one new vertex its poke added.
        it = om.MItMeshPolygon(dag(obj))
        caps = []
        while not it.isDone():
            if any(v >= nv for v in it.getVertices()):
                caps.append(it.index())
            it.next()
    cap_uvs(obj, caps)
    cmds.delete(obj, constructionHistory=True)
    return after - before


report = []
for g in ('Head', 'Arm_L', 'Arm_R', 'Leg_L', 'Leg_R', 'Torso'):
    obj = plain_copy('Gib_' + g + '_Geo')
    drop = ['%s.f[%d]' % (obj, i) for i in range(nf) if face_group[i] != g]
    cmds.delete(drop)
    holes = close_holes(obj)
    report.append((obj, holes, cmds.polyEvaluate(obj, face=True), cmds.polyEvaluate(obj, shell=True)))

# Torso halves: the source is symmetric with a centre loop, so plane X=0 runs along existing edges.
for side, rot in (('L', (0, 90, 0)), ('R', (0, -90, 0))):
    obj = cmds.duplicate('Gib_Torso_Geo', name='Gib_Torso_%s_Geo' % side)[0]
    cmds.polyCut(obj + '.f[*]', cutPlaneCenter=(0, 0, 0), cutPlaneRotate=rot, deleteFaces=True)
    cmds.delete(obj, constructionHistory=True)
    bb = cmds.exactWorldBoundingBox(obj)
    nf0 = cmds.polyEvaluate(obj, face=True)
    holes = close_holes(obj, fan=False)
    # The cross-section is one flat but ragged polygon; Unity's own triangulation gives up on it
    # as self-intersecting and drops it, so Maya triangulates it here.
    cmds.polyTriangulate(['%s.f[%d]' % (obj, f) for f in range(nf0, nf0 + holes)])
    cmds.delete(obj, constructionHistory=True)
    report.append((obj, holes, cmds.polyEvaluate(obj, face=True), cmds.polyEvaluate(obj, shell=True), 'x', round(bb[0], 2), round(bb[3], 2)))
cmds.delete('Gib_Torso_Geo')

for r in report:
    print(r)
