# Regax gibs — Maya side

Cuts `SK_Rapax.fbx` into the seven pieces of `Assets/Game/Models/Regax/SK_Rapax_Gibs.fbx`:
head, left/right torso halves, arms and legs. Each piece is capped and gets a skeleton of its own
with Regax's bone names.

1. In Maya (Python tab): `cmds.commandPort(name=":7005", sourceType="python", echoOutput=False)`
2. From PowerShell, in order:

   ```
   .\run_in_maya.ps1 "<full path>\01_import.py"   # new scene, imports SK_Rapax.fbx
   .\run_in_maya.ps1 "<full path>\02_cut.py"      # pieces by skin weight, caps, torso split at X=0
   .\run_in_maya.ps1 "<full path>\03_rig.py"      # per-piece skeletons, weights from the source
   .\run_in_maya.ps1 "<full path>\04_export.py"   # writes the FBX into Assets
   ```

3. In Unity: **Tools/Regax/Build Gibs** rebuilds `Regax_Gibs.prefab` and wires `Regax.prefab`.

Maya's command port runs every command twice; `run_in_maya.ps1` tags each run so the second pass
does nothing. 01_import starts a new scene without asking.
