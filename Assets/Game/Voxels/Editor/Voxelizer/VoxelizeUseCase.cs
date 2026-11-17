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

        /// <summary>How far apart (relative) a world scale's axes may be and still count as uniform.</summary>
        private const float UNIFORM_TOLERANCE = 1e-3f;

        /// <exception cref="System.InvalidOperationException">
        /// The root or a part has a non-uniform scale, or a part does not fit the grid's 512 cells an axis.
        /// </exception>
        public static VoxelizerResult Run(VoxelizerConfig config, GameObject instance)
        {
            // The unit chain. "World" is the instance as placed, its root's own scale R included. Real metres
            // are world × Scale: Bake makes that true by scaling the prefab root by Scale. The class voxel c is
            // in real metres, so it is c / Scale in the world, and c / Scale / s in a part's own space, where the
            // part's triangles are, s being the part's uniform world scale (R and every parent's in it). After the
            // bake the part's world scale is s × Scale, so its voxels are c in the world again.
            Transform root = instance.transform;
            float rootScale = UniformScale(root, root);
            float longest = LongestSide(instance);
            float scale = config.length > 0f && longest > 1e-4f ? config.length / longest : 1f;
            float classVoxel = config.VoxelSizeFor(config.length > 0f ? config.length : longest);
            float voxel = classVoxel / scale;
            int k = math.clamp(config.colours, 4, 32);

            List<(Transform part, VoxelizerConfig.PartRule rule)> rules = Rules(config, root);
            if (rules.Count == 0)
                rules.Add((root, new VoxelizerConfig.PartRule { path = "", mark = VoxelizerConfig.PartMark.Auto }));
            var partVoxel = new Dictionary<Transform, float>();
            foreach (var (part, rule) in rules)
                if (rule.mark != VoxelizerConfig.PartMark.Skip)
                    partVoxel[part] = voxel / UniformScale(part, root);

            var owned = new Dictionary<Transform, List<Renderer>>();
            foreach (Renderer renderer in VoxelizerSource.Voxelizable(instance))
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
            var result = new VoxelizerResult { VoxelSize = voxel, ClassVoxel = classVoxel, Scale = scale, RootScale = rootScale };
            try
            {
                foreach (var (part, rule) in rules)
                {
                    if (rule.mark == VoxelizerConfig.PartMark.Skip || !owned.TryGetValue(part, out List<Renderer> renderers))
                        continue;

                    using VoxelizerTriangles triangles = VoxelizerSource.Collect(part, renderers);
                    if (triangles.Count == 0) continue;
                    float size = partVoxel[part];
                    if (rule.mark == VoxelizerConfig.PartMark.Auto)
                        VoxelizerStraighten.SnapPlanes(triangles, size);
                    string path = PathOf(root, part);
                    VoxelizerPartGrid grid = VoxelizerSurface.Sample(triangles, size, path);
                    grids.Add(grid);
                    grid.Root = part;
                    grid.Path = path;
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
                        Path = grid.Path, Root = grid.Root, Size = grid.Size, Origin = grid.Origin, VoxelSize = grid.VoxelSize, Voxels = voxels
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
                        // Part space → the root's space (÷ R) → real metres (× R × Scale), so the noise runs on across parts.
                        float4x4 toModel = math.mul(float4x4.Scale(result.RootScale * result.Scale), root.worldToLocalMatrix * part.Root.localToWorldMatrix);
                        VoxelPaint.Camouflage(part.Voxels, part.Size, part.Origin, part.VoxelSize, toModel, palette, paint, 1, camo, 0f, 2 * k + 1);
                    }

                    if (decals)
                        VoxelPaint.Decals(part.Voxels, part.Size, part.Origin, part.VoxelSize, palette, 1, part.Path, config.decals, 2 * k + 1);
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

        /// <summary>The longest side of the voxelizable renderers' world bounds (the pivot not counted); 0 with none.</summary>
        private static float LongestSide(GameObject instance) =>
            VoxelizerSource.TryBounds(instance, out Bounds bounds) ? math.cmax((float3)bounds.size) : 0f;

        /// <summary>A transform's world scale, which must be the same on every axis (a mirror allowed).</summary>
        private static float UniformScale(Transform t, Transform root)
        {
            Vector3 s = t.lossyScale;
            float3 a = math.abs((float3)s);
            if (math.cmax(a) - math.cmin(a) > UNIFORM_TOLERANCE * math.cmax(a) || math.cmin(a) <= 0f)
                throw new System.InvalidOperationException(
                    $"Voxelizer: '{(t == root ? t.name : PathOf(root, t))}' has a non-uniform world scale {s}; a part's voxels " +
                    "must be cubes. Make the scale uniform on the root and the parts (put the stretch on a mesh child or into the mesh).");
            return math.cmax(a);
        }

        /// <summary>Each transform under its first matching rule; a later match of it is skipped, and a rule matching nothing warns.</summary>
        private static List<(Transform part, VoxelizerConfig.PartRule rule)> Rules(VoxelizerConfig config, Transform root)
        {
            var rules = new List<(Transform part, VoxelizerConfig.PartRule rule)>();
            var firstRule = new Dictionary<Transform, string>();
            foreach (VoxelizerConfig.PartRule rule in config.parts ?? System.Array.Empty<VoxelizerConfig.PartRule>())
            {
                List<Transform> matched = Resolve(root, rule.path);
                if (matched.Count == 0)
                    Debug.LogWarning($"Voxelizer: rule '{rule.path}' matches nothing in '{root.name}'.");
                foreach (Transform t in matched)
                {
                    if (firstRule.TryGetValue(t, out string first))
                    {
                        Debug.LogWarning($"Voxelizer: '{PathOf(root, t)}' is already under rule '{first}'; rule '{rule.path}' is skipped for it.");
                        continue;
                    }

                    firstRule[t] = rule.path;
                    rules.Add((t, rule));
                }
            }

            return rules;
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
