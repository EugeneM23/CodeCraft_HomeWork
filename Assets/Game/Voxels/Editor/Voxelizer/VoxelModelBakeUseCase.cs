using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Game.Voxels.EditorTools
{
    /// <summary>Bakes a config's model into VX_&lt;Name&gt;_Data.asset and VX_&lt;Name&gt;.prefab.</summary>
    public static class VoxelModelBakeUseCase
    {
        public static VoxelModelAsset ToAsset(VoxelizerResult result, VoxelizerConfig config)
        {
            var asset = ScriptableObject.CreateInstance<VoxelModelAsset>();
            bool vehicle = config.kind == VoxelizerConfig.ModelKind.Vehicle;
            asset.voxelSize = result.ClassVoxel;
            asset.toughness = vehicle ? 0.45f : 1f;
            asset.density = vehicle ? 7800f : 1800f;
            asset.anchor = vehicle ? VoxelAnchor.Largest : VoxelAnchor.Ground;
            asset.colliders = !vehicle;
            asset.colliderCell = vehicle ? 0 : 2;
            asset.palette = (Color32[])result.Palette.Clone();
            asset.parts = new VoxelModelAsset.Part[result.Parts.Count];
            for (int i = 0; i < result.Parts.Count; i++)
            {
                VoxelizerResultPart part = result.Parts[i];
                asset.parts[i] = new VoxelModelAsset.Part
                {
                    path = part.Path,
                    size = new Vector3Int(part.Size.x, part.Size.y, part.Size.z),
                    origin = part.Origin,
                    voxelSize = part.VoxelSize,
                    runs = VoxelModelAsset.Encode(part.Voxels)
                };
            }

            return asset;
        }

        /// <summary>Where Bake puts the config's data asset.</summary>
        public static string DataPath(VoxelizerConfig config, string folder) =>
            Path.Combine(folder, $"VX_{config.model.name}_Data.asset").Replace('\\', '/');

        /// <summary>
        /// Bakes the prefab and its data. An existing data asset is overwritten in place, so its GUID (and every
        /// reference to it) stays; whatever was imported into it from .vox is replaced.
        /// </summary>
        public static GameObject Bake(VoxelizerConfig config, string folder)
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Voxelizer: baking is off in Play mode.");

            string name = config.model.name;
            // An isolated scene: the open scene is never touched, so it never turns dirty.
            Scene scene = EditorSceneManager.NewPreviewScene();
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(config.model, scene);
            if (instance == null)
            {
                instance = Object.Instantiate(config.model);
                SceneManager.MoveGameObjectToScene(instance, scene);
            }

            try
            {
                using VoxelizerResult result = VoxelizeUseCase.Run(config, instance);
                VoxelModelAsset data = Save(ToAsset(result, config), DataPath(config, folder));

                if (PrefabUtility.IsPartOfPrefabInstance(instance))
                    PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

                // The voxels take the place of every renderer a part owns (the voxelizable ones only: particles
                // and the like stay); the mesh filter goes first, while the renderer still exists to find it by.
                foreach (VoxelizerResultPart part in result.Parts)
                    foreach (Renderer r in part.Root.GetComponentsInChildren<Renderer>(true))
                    {
                        if (!VoxelizerSource.IsVoxelizable(r) || Owner(r.transform, instance.transform, result) != part.Root) continue;
                        if (r.TryGetComponent(out MeshFilter filter)) Object.DestroyImmediate(filter);
                        Object.DestroyImmediate(r);
                    }

                // The grids are in each part's own space, sized for the world after this scale (see VoxelizeUseCase.Run):
                // the root's scale brings the whole model, skipped parts too, to its real size.
                instance.transform.localScale *= result.Scale;
                instance.AddComponent<VoxelModel>().SetData(data);
                instance.name = $"VX_{name}";
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, Path.Combine(folder, $"VX_{name}.prefab").Replace('\\', '/'));
                AssetDatabase.SaveAssets();
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(instance);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        /// <summary>A new asset at the path, or the fresh data copied into the one already there (same GUID).</summary>
        private static VoxelModelAsset Save(VoxelModelAsset fresh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<VoxelModelAsset>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(fresh, path);
                return fresh;
            }

            string assetName = existing.name;
            EditorUtility.CopySerialized(fresh, existing);
            existing.name = assetName;
            Object.DestroyImmediate(fresh);
            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssetIfDirty(existing);
            return existing;
        }

        /// <summary>The nearest rule root at or above a transform (a part, or a kept one, as Run saw it), or null.</summary>
        private static Transform Owner(Transform from, Transform root, VoxelizerResult result)
        {
            for (Transform t = from; t != null; t = t == root ? null : t.parent)
            {
                foreach (VoxelizerResultPart part in result.Parts)
                    if (part.Root == t) return t;
                if (result.KeptRoots.Contains(t)) return t;
            }

            return null;
        }
    }
}
