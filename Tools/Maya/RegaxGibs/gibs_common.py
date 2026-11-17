# Shared helpers for cutting SK_Rapax into gibs (run inside Maya).
import maya.cmds as cmds
import maya.mel as mel
import maya.api.OpenMaya as om
import maya.api.OpenMayaAnim as oma

SRC_MESH = 'SK_Rapax'


def short(name):
    return name.split('|')[-1].replace('FBXASC032', ' ')


def piece_of_bone(b):
    """Which group a source bone belongs to (Maya +X is Regax's left)."""
    n = short(b)
    for side in ('L', 'R'):
        if n.startswith('Rapax_ %s ' % side):
            part = n.split(' ')[-1]
            if part.startswith(('UpperArm', 'Forearm', 'Hand', 'Finger')):
                return 'Arm_' + side
            if part.startswith(('Thigh', 'Calf', 'Foot', 'Toe')):
                return 'Leg_' + side
            return 'Torso'          # clavicles
    if n in ('Rapax_ Neck', 'Rapax_ Head') or n.startswith('Rapax_Jaw'):
        return 'Head'
    return 'Torso'


def dag(name):
    sl = om.MSelectionList(); sl.add(name)
    return sl.getDagPath(0)


def skin_data(mesh):
    """(influence names, weights list per vertex as list of floats)."""
    sc = mel.eval('findRelatedSkinCluster "%s"' % mesh)
    sl = om.MSelectionList(); sl.add(sc)
    fn = oma.MFnSkinCluster(sl.getDependNode(0))
    path = dag(mesh)
    infl = [p.partialPathName() for p in fn.influenceObjects()]
    comp = om.MFnSingleIndexedComponent().create(om.MFn.kMeshVertComponent)
    w, n = fn.getWeights(path, comp)
    nv = om.MFnMesh(path).numVertices
    weights = [list(w[i * n:(i + 1) * n]) for i in range(nv)]
    return sc, infl, weights


def joint_pos(name):
    return om.MVector(cmds.xform(name, q=True, ws=True, t=True))


def jname(short_name):
    """Maya name of a source joint by its Unity short name."""
    return short_name.replace(' ', 'FBXASC032')
