import sys
sys.path.insert(0, r'C:/!!!Work/CodeCraft_HomeWork_ATOMIC/Tools/Maya/RegaxGibs')
import importlib, gibs_common; importlib.reload(gibs_common)
from gibs_common import *

SIDE = {'L': 'L', 'R': 'R'}
PIECES = {
    'Head': ('Rapax_ Neck', ['Rapax_ Neck', 'Rapax_ Head', 'Rapax_JawLeft', 'Rapax_JawMiddle', 'Rapax_JawRight']),
}
for s in 'LR':
    PIECES['Arm_' + s] = ('Rapax_ %s UpperArm' % s, ['Rapax_ %s %s' % (s, b) for b in
                                                     ('UpperArm', 'Forearm', 'Hand', 'Finger0', 'Finger1', 'Finger2')])
    PIECES['Leg_' + s] = ('Rapax_ %s Thigh' % s, ['Rapax_ %s %s' % (s, b) for b in
                                                  ('Thigh', 'Calf', 'Foot', 'Toe0', 'Toe1', 'Toe2')])
    PIECES['Torso_' + s] = ('Rapax_ Pelvis', ['Rapax_ Pelvis', 'Rapax_ Spine', 'Rapax_ Spine1', 'Rapax_ Spine2',
                                              'Rapax_ %s Clavicle' % s, 'Rapax_ Tail', 'Rapax_ Tail1', 'Rapax_ Tail2'])

# Source skeleton: short name -> long path, parent short name
src_joints = {short(j): j for j in cmds.ls('root', dag=True, type='joint', long=True)}
src_parent = {}
for n, j in src_joints.items():
    p = cmds.listRelatives(j, parent=True, fullPath=True)
    src_parent[n] = short(p[0]) if p and cmds.nodeType(p[0]) == 'joint' else None

sc, infl, W = skin_data(SRC_MESH)
infl_short = [short(i) for i in infl]
src_path = dag('SK_RapaxShape')
src_fn = om.MFnMesh(src_path)
src_pts = src_fn.getPoints(om.MSpace.kWorld)
intersector = om.MMeshIntersector()
intersector.create(src_path.node(), src_path.inclusiveMatrix())


def build_piece(piece):
    root_name, keep = PIECES[piece]
    grp = 'Gib_' + piece
    if cmds.objExists(grp):
        cmds.delete(grp)
    grp = cmds.group(empty=True, name=grp, parent='Gibs')
    geo = cmds.parent('Gibs|Gib_%s_Geo' % piece, grp)[0]

    # Copy of the whole skeleton; only the piece's root chain survives.
    dup_root = cmds.duplicate(src_joints['root'], name='tmp_root')[0]
    dup = {short(j): j for j in cmds.ls(dup_root, dag=True, type='joint', long=True)}
    # FBX left 0.9999999 scales on the bones; Maya reads that as non-uniform and would slip a
    # compensating transform above the piece root when it is re-parented.
    for j in dup.values():
        cmds.setAttr(j + '.scale', 1, 1, 1)
    cmds.parent(dup[root_name], grp)
    cmds.delete(dup_root)
    piece_root = cmds.listRelatives(grp, children=True, type='joint', fullPath=True)[0]
    for j in cmds.ls(piece_root, dag=True, type='joint', long=True)[::-1]:
        if short(j) not in keep:
            cmds.delete(j)
    joints = cmds.ls(piece_root, dag=True, type='joint', long=True)
    names = [short(j) for j in joints]
    assert sorted(names) == sorted(keep), (piece, names)

    sk = cmds.skinCluster(joints, geo, toSelectedBones=True, bindMethod=0, maximumInfluences=4,
                          normalizeWeights=1, name='skin_' + piece)[0]
    sl = om.MSelectionList(); sl.add(sk)
    fn = oma.MFnSkinCluster(sl.getDependNode(0))
    new_infl = [short(p.fullPathName()) for p in fn.influenceObjects()]

    # Every source bone the piece lacks hands its weight to its nearest ancestor in the piece,
    # or to the piece root when the chain leads elsewhere (a limb's clavicle, the head's spine).
    def target(bone):
        b = bone
        while b is not None:
            if b in new_infl:
                return new_infl.index(b)
            b = src_parent.get(b)
        return new_infl.index(root_name)
    remap = [target(b) for b in infl_short]

    geo_path = dag(geo)
    pts = om.MFnMesh(geo_path).getPoints(om.MSpace.kWorld)
    ni = len(new_infl)
    out = om.MDoubleArray(len(pts) * ni, 0.0)
    for vi, p in enumerate(pts):
        hit = intersector.getClosestPoint(p)
        a, b = hit.barycentricCoords
        c = 1.0 - a - b
        tri = src_fn.getPolygonTriangleVertices(hit.face, hit.triangle)
        acc = [0.0] * ni
        for tv, f in zip(tri, (a, b, c)):
            for k, w in enumerate(W[tv]):
                if w > 0:
                    acc[remap[k]] += w * f
        # keep the four strongest, normalised
        top = sorted(range(ni), key=lambda k: -acc[k])[:4]
        tot = sum(acc[k] for k in top) or 1.0
        for k in top:
            out[vi * ni + k] = acc[k] / tot
    comp = om.MFnSingleIndexedComponent().create(om.MFn.kMeshVertComponent)
    om.MFnSingleIndexedComponent(comp).addElements(list(range(len(pts))))
    fn.setWeights(geo_path, comp, om.MIntArray(list(range(ni))), out, False)
    return piece, len(pts), names


# Run after 02_cut.py: it leaves the seven capped *_Geo meshes directly under Gibs.
for piece in PIECES:
    print(build_piece(piece))
print(cmds.listRelatives('Gibs', children=True))
