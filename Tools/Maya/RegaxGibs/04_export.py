import maya.cmds as cmds
import maya.mel as mel
import os

OUT = r'C:/!!!Work/CodeCraft_HomeWork_ATOMIC/Assets/Game/Models/Regax/SK_Rapax_Gibs.fbx'
os.makedirs(os.path.dirname(OUT), exist_ok=True)

pieces = cmds.listRelatives('Gibs', children=True, fullPath=True)
pieces = cmds.parent(pieces, world=True)
cmds.select(pieces, replace=True)

mel.eval('FBXResetExport')
mel.eval('FBXExportSkins -v true')
mel.eval('FBXExportShapes -v false')
mel.eval('FBXExportAnimationOnly -v false')
mel.eval('FBXExportBakeComplexAnimation -v false')
mel.eval('FBXExportCameras -v false')
mel.eval('FBXExportLights -v false')
mel.eval('FBXExportEmbeddedTextures -v false')
mel.eval('FBXExportSmoothingGroups -v true')
mel.eval('FBXExportInputConnections -v false')
mel.eval('FBXExportUpAxis y')
mel.eval('FBXExportInAscii -v false')
mel.eval('FBXExport -f "%s" -s' % OUT)

cmds.parent(pieces, 'Gibs')
print('exported', OUT, os.path.getsize(OUT))
