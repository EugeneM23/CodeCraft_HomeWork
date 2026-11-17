using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Game.Voxels
{
    /// <summary>
    /// Rebuilds a model made of mesh renderers as voxels when it wakes up, keeping its moving parts
    /// apart: every part root becomes its own <see cref="VoxelBody"/> parented under that root, so a
    /// turret still turns and wheels still spin. The original renderers are hidden, colliders and
    /// gameplay are left as they were — the voxels are a skin that blasts can chew off.
    ///
    /// Over the voxels it can lay a camouflage (patches over one paint material, in the model's own
    /// space so they run on across parts) and decals (pixel pictures painted on a part's side).
    /// </summary>
    /// <remarks>
    /// The defaults fit Tank_01: hull, turret and gun as parts; each wheel and idler as a part of its
    /// own (they spin); the thin track belt left as it is (its links slide along the track).
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class VoxelizedModel : MonoBehaviour
    {
        /// <summary>Patches of other colours over the voxels of one paint material.</summary>
        [Serializable]
        public struct Camouflage
        {
            [Tooltip("Voxels made from renderers with this material get the pattern. None: no camouflage.")]
            public Material Paint;
            public Color Patch;
            [Tooltip("The heart of the largest patches.")]
            public Color Dark;
            [Tooltip("Sun-bleached patches between the others.")]
            public Color Light;
            [Tooltip("Patches per metre, about.")]
            public float Frequency;
            [Tooltip("Noise level above which the paint is a patch: lower is more patch.")]
            [Range(-1f, 1f)] public float Threshold;
        }

        /// <summary>A pixel picture on a part's ±X side: a number, an emblem.</summary>
        [Serializable]
        public struct Decal
        {
            [Tooltip("The part root it is painted on, as a path from this object.")]
            public string Part;
            [Tooltip("+1 the part's +X side, -1 its -X side. Rows read left to right as seen from that side.")]
            public int Side;
            [Tooltip("Centre of the picture, part space, m: x along Z, y along Y.")]
            public Vector2 Center;
            [Tooltip("The picture, top row first, one character per voxel.")]
            public string[] Rows;
            [Tooltip("Characters that paint; each takes the colour at the same index in Colors.")]
            public string Keys;
            public Color[] Colors;
        }

        [Tooltip("Part roots, as paths from this object. '*' and '?' match any name in that step. A renderer belongs to the nearest part root above it.")]
        [SerializeField] private string[] _parts =
        {
            "HullVisual",
            "HullVisual/TurretVisual",
            "HullVisual/TurretVisual/Gun",
            "HullVisual/Wheel_*",
            "HullVisual/Track?_Front",
            "HullVisual/Track?_Rear"
        };

        [Tooltip("Renderers under these paths stay as they are and are not voxelized.")]
        [SerializeField] private string[] _keep =
        {
            "HullVisual/Track?_Link*",
            "HullVisual/Track?_Upper"
        };

        [Tooltip("Edge of one voxel, m. Teardown uses 0.1; a small vehicle needs finer.")]
        [SerializeField] private float _voxelSize = 0.05f;

        [Tooltip("How far each voxel's shade strays from its material's colour.")]
        [Range(0f, 0.3f)]
        [SerializeField] private float _colorNoise = 0.08f;

        [Tooltip("Blast radius multiplier on this model: below 1 is armour.")]
        [Range(0.1f, 1f)]
        [SerializeField] private float _toughness = 0.45f;

        [SerializeField] private float _density = 7800f;

        [SerializeField] private Camouflage _camouflage;

        [SerializeField] private Decal[] _decals = Array.Empty<Decal>();

        private readonly List<VoxelBody> _bodies = new();

        public IReadOnlyList<VoxelBody> Bodies => _bodies;

        /// <summary>For builders: what is kept as meshes, the voxel size and shading, the look.</summary>
        public void Configure(string[] keep, float voxelSize, float colorNoise, Camouflage camouflage, Decal[] decals)
        {
            _keep = keep;
            _voxelSize = voxelSize;
            _colorNoise = colorNoise;
            _camouflage = camouflage;
            _decals = decals ?? Array.Empty<Decal>();
        }

        private void Start() => Build();

        public void Build()
        {
            if (_bodies.Count > 0)
                return;

            foreach (Part part in Voxelize())
            {
                MeshVoxelizer.Result grid = part.Grid;
                var go = new GameObject("Voxels") { layer = part.Root.gameObject.layer };
                go.transform.SetParent(part.Root, false);

                VoxelBodySettings settings = VoxelBodySettings.Default(_voxelSize);
                settings.Origin = grid.Origin;
                settings.Anchor = VoxelAnchor.Largest;
                settings.Colliders = false;
                settings.Layer = part.Root.gameObject.layer;
                settings.Toughness = _toughness;
                settings.Density = _density;

                VoxelBody body = go.AddComponent<VoxelBody>();
                body.Init(grid.Size, grid.Voxels, grid.Palette, settings);
                _bodies.Add(body);

                foreach (MeshRenderer renderer in part.Renderers)
                    renderer.enabled = false;
            }
        }

        /// <summary>A part root, the renderers it owns and their voxels.</summary>
        public struct Part
        {
            public Transform Root;
            public List<MeshRenderer> Renderers;
            public MeshVoxelizer.Result Grid;
        }

        /// <summary>
        /// Voxelizes every part with its camouflage and decals, and touches nothing else. The caller
        /// owns the grids (<see cref="Build"/> hands them to the bodies; an editor preview disposes them).
        /// </summary>
        public List<Part> Voxelize()
        {
            HashSet<Transform> parts = Resolve(_parts);
            HashSet<Transform> keep = Resolve(_keep);
            var owned = new Dictionary<Transform, List<MeshRenderer>>();

            foreach (MeshRenderer renderer in GetComponentsInChildren<MeshRenderer>(true))
            {
                Transform owner = FindOwner(renderer.transform, parts, keep);
                if (owner == null || !renderer.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null)
                    continue;

                if (!owned.TryGetValue(owner, out List<MeshRenderer> list))
                    owned[owner] = list = new List<MeshRenderer>();
                list.Add(renderer);
            }

            var result = new List<Part>();
            var sources = new List<MeshVoxelizer.Source>();
            uint seed = 1;

            foreach (var pair in owned)
            {
                Transform part = pair.Key;
                sources.Clear();

                foreach (MeshRenderer renderer in pair.Value)
                {
                    Material material = renderer.sharedMaterial;
                    sources.Add(new MeshVoxelizer.Source
                    {
                        Mesh = renderer.GetComponent<MeshFilter>().sharedMesh,
                        ToSpace = part.worldToLocalMatrix * renderer.transform.localToWorldMatrix,
                        Color = material != null && material.HasProperty("_Color") ? material.color : Color.gray
                    });
                }

                if (!MeshVoxelizer.Voxelize(sources, _voxelSize, _colorNoise, seed++, out MeshVoxelizer.Result grid))
                    continue;

                PaintCamouflage(part, grid);
                PaintDecals(part, grid);
                result.Add(new Part { Root = part, Renderers = pair.Value, Grid = grid });
            }

            return result;
        }

        // ---------------------------------------------------------------- look

        private void PaintCamouflage(Transform part, MeshVoxelizer.Result grid)
        {
            Camouflage camo = _camouflage;
            if (camo.Paint == null || !camo.Paint.HasProperty("_Color"))
                return;

            int paint = MeshVoxelizer.FindShades(grid.Palette, camo.Paint.color, _colorNoise);
            if (paint < 0)
                return;

            VoxelPaint.Camouflage(grid.Voxels, grid.Size, grid.Origin, _voxelSize,
                transform.worldToLocalMatrix * part.localToWorldMatrix, grid.Palette, paint, MeshVoxelizer.VARIANTS, camo, _colorNoise);
        }

        private void PaintDecals(Transform part, MeshVoxelizer.Result grid)
        {
            if (_decals == null)
                return;

            foreach (Decal decal in _decals)
                if (transform.Find(decal.Part) == part)
                    VoxelPaint.Decal(grid.Voxels, grid.Size, grid.Origin, _voxelSize, grid.Palette, MeshVoxelizer.VARIANTS, decal);
        }

        // ---------------------------------------------------------------- parts

        private Transform FindOwner(Transform from, HashSet<Transform> parts, HashSet<Transform> keep)
        {
            for (Transform t = from; t != null && t != transform.parent; t = t.parent)
            {
                if (keep.Contains(t))
                    return null;
                if (parts.Contains(t))
                    return t;
            }

            return null;
        }

        private HashSet<Transform> Resolve(string[] paths)
        {
            var found = new HashSet<Transform>();
            var level = new List<Transform>();
            var next = new List<Transform>();

            foreach (string path in paths)
            {
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                level.Clear();
                level.Add(transform);

                foreach (string step in path.Split('/'))
                {
                    var pattern = new Regex("^" + Regex.Escape(step).Replace("\\*", ".*").Replace("\\?", ".") + "$");
                    next.Clear();
                    foreach (Transform parent in level)
                    foreach (Transform child in parent)
                        if (pattern.IsMatch(child.name))
                            next.Add(child);

                    level.Clear();
                    level.AddRange(next);
                }

                found.UnionWith(level);
            }

            return found;
        }
    }
}
