import maya.cmds as cmds
import maya.mel as mel

cmds.loadPlugin('fbxmaya', quiet=True)
cmds.file(new=True, force=True)
cmds.file(r'C:/!!!Work/CodeCraft_HomeWork_ATOMIC/Assets/3rdParty/Rapax/FBX Files/SK_Rapax.fbx',
          i=True, type='FBX', ignoreVersion=True, mergeNamespacesOnClash=False, options='fbx')
print('units', cmds.currentUnit(q=True, linear=True), 'up', cmds.upAxis(q=True, axis=True))
for t in cmds.ls(type='transform'):
    if cmds.listRelatives(t, parent=True) is None:
        print('root', t)
for m in cmds.ls(type='mesh', noIntermediate=True):
    print('mesh', m, cmds.polyEvaluate(m, v=True, f=True), cmds.exactWorldBoundingBox(m))
    sc = mel.eval('findRelatedSkinCluster "%s"' % cmds.listRelatives(m, parent=True)[0])
    print('skin', sc, len(cmds.skinCluster(sc, q=True, inf=True)))
    print('shading', cmds.listConnections(cmds.listConnections(m, type='shadingEngine') or [], type='shadingEngine'))
j = cmds.ls(type='joint')
print('joints', len(j))
for x in j[:6]:
    print(x, cmds.xform(x, q=True, ws=True, t=True))
