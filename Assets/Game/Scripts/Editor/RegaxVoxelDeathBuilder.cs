using Atomic.Entities;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools/Regax/Use Voxel Death: Regax dies into voxel flesh and bleeds voxel blood in place of
    /// the Maya gibs and the pack's blood (Docs/superpowers/specs/2026-11-16-regax-voxel-death.md).
    /// Tools/Regax/Build calls it last, so a rebuild from the zombie keeps the voxel death.
    /// </summary>
    public static class RegaxVoxelDeathBuilder
    {
        private const string LOG = "[REGAX VOXEL DEATH]";
        private const string REGAX_PREFAB = "Assets/Game/Prefabs/Characters/Regax.prefab";
        private const string MODEL = "Assets/3rdParty/Rapax/FBX Files/SK_Rapax.fbx";
        private const string CHEST_BONE = "Rapax_ Spine2";

        [MenuItem("Tools/Regax/Use Voxel Death")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError($"{LOG} leave Play first: the prefab and the model's import change");
                return;
            }

            MakeModelReadable();
            WireRegax();
        }

        /// <summary>BakeMesh skins on the CPU: a build keeps a mesh's vertices only when it is readable.</summary>
        private static void MakeModelReadable()
        {
            if (AssetImporter.GetAtPath(MODEL) is not ModelImporter importer)
            {
                Debug.LogError($"{LOG} no model importer at {MODEL}");
                return;
            }

            if (importer.isReadable)
                return;

            importer.isReadable = true;
            importer.SaveAndReimport();
            Debug.Log($"{LOG} {MODEL}: Read/Write on");
        }

        private static void WireRegax()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(REGAX_PREFAB);
            try
            {
                Transform skeleton = root.transform.Find("<Skeleton>");
                Transform skinObject = skeleton != null ? skeleton.Find("<Skin>") : null;
                SkinnedMeshRenderer skin = skinObject != null ? skinObject.GetComponent<SkinnedMeshRenderer>() : null;
                Transform colliderObject = root.transform.Find("<Collider>");
                Collider body = colliderObject != null ? colliderObject.GetComponent<Collider>() : null;
                Transform vfx = root.transform.Find("<VFX>");
                Transform chest = skeleton != null ? RegaxGibsBuilder.FindDeep(skeleton, CHEST_BONE) : null;
                MonoEntity entity = root.GetComponent<MonoEntity>();

                if (skin == null || body == null || vfx == null || chest == null || entity == null)
                {
                    Debug.LogError($"{LOG} {REGAX_PREFAB} lacks <Skeleton>/<Skin>, <Collider>, <VFX>, {CHEST_BONE} or MonoEntity");
                    return;
                }

                // The Maya gibs come off.
                foreach (CharacterGibsInstaller old in root.GetComponentsInChildren<CharacterGibsInstaller>(true))
                    Remove(entity, old);

                // The pack's hit blood: the installer is shared with the zombies, so only its effects go.
                CharacterVfxInstaller effects = root.GetComponentInChildren<CharacterVfxInstaller>(true);
                if (effects != null)
                {
                    var serialized = new SerializedObject(effects);
                    serialized.FindProperty("_bulletBlood").objectReferenceValue = null;
                    serialized.FindProperty("_meleeBlood").objectReferenceValue = null;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }

                CharacterVoxelGibsInstaller gibs = root.GetComponent<CharacterVoxelGibsInstaller>();
                if (gibs == null)
                    gibs = root.AddComponent<CharacterVoxelGibsInstaller>();
                var gibsSettings = new SerializedObject(gibs);
                gibsSettings.FindProperty("_skin").objectReferenceValue = skin;
                gibsSettings.FindProperty("_skeleton").objectReferenceValue = skeleton;
                SerializedProperty hide = gibsSettings.FindProperty("_hide");
                hide.arraySize = 1;
                hide.GetArrayElementAtIndex(0).objectReferenceValue = skin;
                gibsSettings.ApplyModifiedPropertiesWithoutUndo();

                VoxelBloodInstaller blood = vfx.GetComponent<VoxelBloodInstaller>();
                if (blood == null)
                    blood = vfx.gameObject.AddComponent<VoxelBloodInstaller>();
                var bloodSettings = new SerializedObject(blood);
                bloodSettings.FindProperty("_body").objectReferenceValue = body;
                bloodSettings.FindProperty("_chest").objectReferenceValue = chest;
                bloodSettings.ApplyModifiedPropertiesWithoutUndo();

                List(entity, gibs);
                List(entity, blood);

                PrefabUtility.SaveAsPrefabAsset(root, REGAX_PREFAB);
                Debug.Log($"{LOG} {REGAX_PREFAB} now dies into voxels and bleeds voxel blood");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void Remove(MonoEntity entity, Component installer)
        {
            var serialized = new SerializedObject(entity);
            SerializedProperty list = serialized.FindProperty("sceneInstallers");
            for (int i = list.arraySize - 1; i >= 0; i--)
            {
                SerializedProperty element = list.GetArrayElementAtIndex(i);
                if (element.objectReferenceValue != installer)
                    continue;
                element.objectReferenceValue = null;
                list.DeleteArrayElementAtIndex(i);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            Object.DestroyImmediate(installer);
        }

        private static void List(MonoEntity entity, Component installer)
        {
            var serialized = new SerializedObject(entity);
            SerializedProperty list = serialized.FindProperty("sceneInstallers");
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == installer)
                    return;

            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = installer;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
