using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Voxels.EditorTools
{
    /// <summary>Bakes a config's model into VX_&lt;Name&gt;_Data.asset and VX_&lt;Name&gt;.prefab.</summary>
    public static class VoxelModelBakeUseCase
    {
        public static VoxelModelAsset ToAsset(VoxelizerResult result, VoxelizerConfig config)
        {
            var asset = ScriptableObject.CreateInstance<VoxelModelAsset>();
            bool vehicle = config.kind == VoxelizerConfig.ModelKind.Vehicle;
            asset.voxelSize = result.VoxelSize;
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
                    runs = VoxelModelAsset.Encode(part.Voxels)
                };
            }

            return asset;
        }

        public static GameObject Bake(VoxelizerConfig config, string folder)
        {
            string name = config.model.name;
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(config.model);
            if (instance == null) instance = Object.Instantiate(config.model);
            try
            {
                using VoxelizerResult result = VoxelizeUseCase.Run(config, instance);
                VoxelModelAsset data = ToAsset(result, config);
                AssetDatabase.CreateAsset(data, Path.Combine(folder, $"VX_{name}_Data.asset").Replace('\\', '/'));

                if (PrefabUtility.IsPartOfPrefabInstance(instance))
                    PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

                // The voxels take the place of every renderer a part owns; the mesh filter goes first,
                // while the renderer still exists to find it by.
                foreach (VoxelizerResultPart part in result.Parts)
                    foreach (Renderer r in part.Root.GetComponentsInChildren<Renderer>(true))
                    {
                        if (Owner(r.transform, result) != part.Root) continue;
                        if (r.TryGetComponent(out MeshFilter filter)) Object.DestroyImmediate(filter);
                        Object.DestroyImmediate(r);
                    }

                // The grids are in the model's own units (voxel = real voxel / Scale); the root's
                // scale brings the whole model, skipped parts too, to its real size.
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
            }
        }

        private static Transform Owner(Transform from, VoxelizerResult result)
        {
            for (Transform t = from; t != null; t = t.parent)
                foreach (VoxelizerResultPart part in result.Parts)
                    if (part.Root == t) return t;
            return null;
        }
    }
}
