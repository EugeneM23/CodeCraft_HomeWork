using System.Collections.Generic;
using System.Text.RegularExpressions;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Voxels.EditorTools
{
    /// <summary>The whole voxelizer: a model instance and its config in, one byte grid per part and the model's palette out.</summary>
    public static class VoxelizeUseCase
    {
        public static VoxelizerResult Run(VoxelizerConfig config, GameObject instance)
        {
            Transform root = instance.transform;
            float scale = Scale(config, instance);
            float voxel = config.VoxelSize / scale;
            int k = math.clamp(config.colours, 4, 32);

            var rules = new List<(Transform part, VoxelizerConfig.PartRule rule)>();
            foreach (VoxelizerConfig.PartRule rule in config.parts)
                foreach (Transform t in Resolve(root, rule.path))
                    rules.Add((t, rule));
            if (rules.Count == 0)
                rules.Add((root, new VoxelizerConfig.PartRule { path = "", mark = VoxelizerConfig.PartMark.Auto }));

            var owned = new Dictionary<Transform, List<Renderer>>();
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                Transform owner = null;
                for (Transform t = renderer.transform; t != null && owner == null; t = t == root ? null : t.parent)
                    foreach (var (part, _) in rules)
                        if (part == t) { owner = t; break; }
                if (owner == null) continue;
                if (!owned.TryGetValue(owner, out List<Renderer> list)) owned[owner] = list = new List<Renderer>();
                list.Add(renderer);
            }

            var grids = new List<VoxelizerPartGrid>();
            var result = new VoxelizerResult { VoxelSize = voxel, Scale = scale };
            try
            {
                foreach (var (part, rule) in rules)
                {
                    if (rule.mark == VoxelizerConfig.PartMark.Skip || !owned.TryGetValue(part, out List<Renderer> renderers))
                        continue;

                    using VoxelizerTriangles triangles = VoxelizerSource.Collect(part, renderers);
                    if (triangles.Count == 0) continue;
                    if (rule.mark == VoxelizerConfig.PartMark.Auto)
                        VoxelizerStraighten.SnapPlanes(triangles, voxel);
                    VoxelizerPartGrid grid = VoxelizerSurface.Sample(triangles, voxel);
                    grids.Add(grid);
                    grid.Root = part;
                    grid.Path = PathOf(root, part);
                    switch (rule.mark)
                    {
                        case VoxelizerConfig.PartMark.Box: VoxelizerPrimitives.Box(grid); break;
                        case VoxelizerConfig.PartMark.Cylinder: VoxelizerPrimitives.Cylinder(grid, rule.axis); break;
                        default: VoxelizerSurface.Fill(grid); break;
                    }

                    if (rule.mark == VoxelizerConfig.PartMark.Auto)
                        VoxelizerStraighten.Open(grid, 2);
                }

                foreach (var (part, _) in rules)
                    if (!grids.Exists(g => g.Root == part) && !result.KeptRoots.Contains(part))
                        result.KeptRoots.Add(part);

                result.Palette = VoxelizerPalette.Build(grids, k, out Color32[] centres);
                foreach (VoxelizerPartGrid grid in grids)
                {
                    NativeArray<byte> voxels = VoxelizerPalette.Map(grid, centres, k, Allocator.Persistent);
                    VoxelizerPalette.Smooth(voxels, grid.Size, k);
                    result.Parts.Add(new VoxelizerResultPart
                    {
                        Path = grid.Path, Root = grid.Root, Size = grid.Size, Origin = grid.Origin, Voxels = voxels
                    });
                }
            }
            catch
            {
                result.Dispose();
                throw;
            }
            finally
            {
                foreach (VoxelizerPartGrid grid in grids) grid.Dispose();
            }

            return result;
        }

        /// <summary>Model metres to real metres: the config's length over the model's longest side.</summary>
        private static float Scale(VoxelizerConfig config, GameObject instance)
        {
            if (config.length <= 0f) return 1f;
            var bounds = new Bounds(instance.transform.position, Vector3.zero);
            foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true)) bounds.Encapsulate(r.bounds);
            float longest = math.cmax((float3)bounds.size);
            return longest > 1e-4f ? config.length / longest : 1f;
        }

        private static string PathOf(Transform root, Transform t)
        {
            if (t == root) return "";
            string path = t.name;
            for (Transform p = t.parent; p != null && p != root; p = p.parent) path = p.name + "/" + path;
            return path;
        }

        /// <summary>Same matching as VoxelizedModel.Resolve: '*' and '?' within one step.</summary>
        private static List<Transform> Resolve(Transform root, string path)
        {
            var level = new List<Transform> { root };
            if (string.IsNullOrWhiteSpace(path)) return level;
            foreach (string step in path.Split('/'))
            {
                var pattern = new Regex("^" + Regex.Escape(step).Replace("\\*", ".*").Replace("\\?", ".") + "$");
                var next = new List<Transform>();
                foreach (Transform parent in level)
                    foreach (Transform child in parent)
                        if (pattern.IsMatch(child.name)) next.Add(child);
                level = next;
            }

            return level;
        }
    }
}
