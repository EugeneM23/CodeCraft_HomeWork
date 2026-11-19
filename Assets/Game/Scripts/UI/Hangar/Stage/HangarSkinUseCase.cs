using System.Collections.Generic;
using Game.Voxels;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game
{
    /// <summary>
    /// The tank's voxel skin for the hangar: the same grids, camouflage and decals the battle builds through
    /// <see cref="VoxelizedModel.Build"/>, but drawn as plain static meshes (exposed faces, colours in the
    /// vertices, the voxel chunk material). No <see cref="VoxelBody"/> and so no VoxelWorld: nothing can
    /// break, and the menu needs none of the voxel simulation.
    /// </summary>
    public static class HangarSkinUseCase
    {
        public const string SKIN_NAME = "VoxelSkin";

        private static readonly int3[] Directions =
        {
            new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0), new(0, 0, 1), new(0, 0, -1)
        };

        /// <summary>
        /// Voxelizes the model once, hangs one mesh per part under the part root and hides the renderers the
        /// voxels replace. The meshes are added to <paramref name="meshes"/>: the caller destroys them.
        /// One-off work when the stage starts or the tank changes, not per frame.
        /// </summary>
        public static void Build(VoxelizedModel model, Material material, float voxelSize, List<Mesh> meshes)
        {
            List<VoxelizedModel.Part> parts = model.Voxelize();
            var vertices = new List<Vector3>(4096);
            var normals = new List<Vector3>(4096);
            var colours = new List<Color32>(4096);
            var triangles = new List<int>(6144);

            foreach (VoxelizedModel.Part part in parts)
            {
                MeshVoxelizer.Result grid = part.Grid;
                try
                {
                    Mesh mesh = BuildMesh(grid, voxelSize, vertices, normals, colours, triangles);
                    meshes.Add(mesh);

                    var go = new GameObject(SKIN_NAME, typeof(MeshFilter), typeof(MeshRenderer))
                    {
                        layer = part.Root.gameObject.layer
                    };
                    go.transform.SetParent(part.Root, false);
                    go.GetComponent<MeshFilter>().sharedMesh = mesh;
                    MeshRenderer renderer = go.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;

                    foreach (MeshRenderer source in part.Renderers)
                        source.enabled = false;
                }
                finally
                {
                    if (grid.Voxels.IsCreated) grid.Voxels.Dispose();
                    if (grid.Palette.IsCreated) grid.Palette.Dispose();
                }
            }
        }

        /// <summary>Faces between a full voxel and an empty one (or the grid's edge), in the part's space.</summary>
        private static Mesh BuildMesh(MeshVoxelizer.Result grid, float voxelSize, List<Vector3> vertices,
            List<Vector3> normals, List<Color32> colours, List<int> triangles)
        {
            vertices.Clear();
            normals.Clear();
            colours.Clear();
            triangles.Clear();

            int3 size = grid.Size;
            NativeArray<byte> voxels = grid.Voxels;
            NativeArray<Color32> palette = grid.Palette;
            var origin = (Vector3)grid.Origin;
            float half = voxelSize * 0.5f;

            for (int z = 0; z < size.z; z++)
            for (int y = 0; y < size.y; y++)
            for (int x = 0; x < size.x; x++)
            {
                byte index = voxels[x + size.x * (y + size.y * z)];
                if (index == 0)
                    continue;

                Color32 colour = palette[index];
                for (int d = 0; d < Directions.Length; d++)
                {
                    int3 dir = Directions[d];
                    if (Solid(voxels, size, x + dir.x, y + dir.y, z + dir.z))
                        continue;

                    // a = up for the ±X faces, right otherwise; b = n × a: (0, 1, 2) faces outwards.
                    var n = new Vector3(dir.x, dir.y, dir.z);
                    Vector3 a = dir.x != 0 ? Vector3.up : Vector3.right;
                    Vector3 b = Vector3.Cross(n, a);
                    Vector3 centre = origin + (new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) + n * 0.5f) * voxelSize;
                    int s = vertices.Count;
                    vertices.Add(centre + (-a - b) * half);
                    vertices.Add(centre + (a - b) * half);
                    vertices.Add(centre + (a + b) * half);
                    vertices.Add(centre + (-a + b) * half);
                    for (int k = 0; k < 4; k++)
                    {
                        normals.Add(n);
                        colours.Add(colour);
                    }

                    triangles.Add(s);
                    triangles.Add(s + 1);
                    triangles.Add(s + 2);
                    triangles.Add(s);
                    triangles.Add(s + 2);
                    triangles.Add(s + 3);
                }
            }

            var mesh = new Mesh
            {
                name = "HangarVoxelSkin",
                indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colours);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }

        private static bool Solid(NativeArray<byte> voxels, int3 size, int x, int y, int z) =>
            x >= 0 && y >= 0 && z >= 0 && x < size.x && y < size.y && z < size.z && voxels[x + size.x * (y + size.y * z)] != 0;
    }
}
