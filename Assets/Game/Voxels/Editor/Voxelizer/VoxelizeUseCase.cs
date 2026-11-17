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
        /// <summary>How far (sRGB, 0..441) the paint material's colour may be from its surface slot.</summary>
        private const float CAMO_MATCH = 48f;

        public static VoxelizerResult Run(VoxelizerConfig config, GameObject instance)
        {
            Transform root = instance.transform;
            float longest = LongestSide(instance);
            float scale = config.length > 0f && longest > 1e-4f ? config.length / longest : 1f;
            float voxel = config.VoxelSizeFor(config.length > 0f ? config.length : longest) / scale;
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

                    if (rule.mark == VoxelizerConfig.PartMark.Auto && config.cutThinDetails)
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

                Paint(config, root, k, centres, result);
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

        /// <summary>
        /// The config's camouflage and decals over the mapped grids, one palette slot per new colour after
        /// the model's 2K. The camouflage repaints the surface slot nearest the paint material's colour
        /// (inside slots stay: the paint is a coat), its noise in the model's space in real metres.
        /// </summary>
        private static void Paint(VoxelizerConfig config, Transform root, int k, Color32[] centres, VoxelizerResult result)
        {
            VoxelizedModel.Camouflage camo = config.camouflage;
            int paint = camo.Paint != null ? PaintSlot(camo.Paint, centres) : -1;
            bool decals = config.decals != null && config.decals.Length > 0;
            if (paint < 0 && !decals)
                return;

            var palette = new NativeArray<Color32>(result.Palette, Allocator.TempJob);
            try
            {
                foreach (VoxelizerResultPart part in result.Parts)
                {
                    if (paint > 0)
                    {
                        float4x4 toModel = math.mul(float4x4.Scale(result.Scale), root.worldToLocalMatrix * part.Root.localToWorldMatrix);
                        VoxelPaint.Camouflage(part.Voxels, part.Size, part.Origin, result.VoxelSize, toModel, palette, paint, 1, camo, 0f, 2 * k + 1);
                    }

                    if (decals)
                        VoxelPaint.Decals(part.Voxels, part.Size, part.Origin, result.VoxelSize, palette, 1, part.Path, config.decals, 2 * k + 1);
                }

                palette.CopyTo(result.Palette);
            }
            finally
            {
                palette.Dispose();
            }
        }

        /// <summary>The surface slot nearest the paint material's colour, or -1 (with a warning) if none is near.</summary>
        private static int PaintSlot(Material paint, Color32[] centres)
        {
            Color32 colour = VoxelizerSource.ColourOf(paint);
            var p = new float3(colour.r, colour.g, colour.b);
            int best = -1;
            float bestD = float.MaxValue;
            for (int i = 0; i < centres.Length; i++)
            {
                float d = math.distance(p, new float3(centres[i].r, centres[i].g, centres[i].b));
                if (d < bestD) { bestD = d; best = i; }
            }

            if (best >= 0 && bestD <= CAMO_MATCH)
                return 1 + best;
            Debug.LogWarning($"Voxelizer: no surface colour near camouflage paint '{paint.name}' {colour} (nearest {bestD:F0} > {CAMO_MATCH}); camouflage skipped.");
            return -1;
        }

        /// <summary>Model metres to real metres: the config's length over the model's longest side.</summary>
        /// <summary>The model's longest bounds side, in the model's own units.</summary>
        private static float LongestSide(GameObject instance)
        {
            var bounds = new Bounds(instance.transform.position, Vector3.zero);
            foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true)) bounds.Encapsulate(r.bounds);
            return math.cmax((float3)bounds.size);
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
