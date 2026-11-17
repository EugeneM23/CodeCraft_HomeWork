using UnityEngine;

namespace Game.Voxels.Tests
{
    /// <summary>Meshes and textured objects built in code for the voxelizer tests.</summary>
    public static class TestMeshes
    {
        /// <summary>An axis-aligned box from min to max, 12 triangles, outward winding, UVs 0..1 per face.</summary>
        public static Mesh Box(Vector3 min, Vector3 max)
        {
            Vector3[] c =
            {
                new(min.x, min.y, min.z), new(max.x, min.y, min.z), new(max.x, max.y, min.z), new(min.x, max.y, min.z),
                new(min.x, min.y, max.z), new(max.x, min.y, max.z), new(max.x, max.y, max.z), new(min.x, max.y, max.z)
            };
            int[][] faces =
            {
                new[] { 0, 3, 2, 1 }, new[] { 4, 5, 6, 7 }, new[] { 0, 4, 7, 3 },
                new[] { 1, 2, 6, 5 }, new[] { 0, 1, 5, 4 }, new[] { 3, 7, 6, 2 }
            };
            var vertices = new Vector3[24];
            var uvs = new Vector2[24];
            var triangles = new int[36];
            Vector2[] corner = { new(0, 0), new(0, 1), new(1, 1), new(1, 0) };
            for (int f = 0; f < 6; f++)
            {
                for (int k = 0; k < 4; k++)
                {
                    vertices[f * 4 + k] = c[faces[f][k]];
                    uvs[f * 4 + k] = corner[k];
                }

                int b = f * 4;
                int t = f * 6;
                triangles[t] = b; triangles[t + 1] = b + 1; triangles[t + 2] = b + 2;
                triangles[t + 3] = b; triangles[t + 4] = b + 2; triangles[t + 5] = b + 3;
            }

            var mesh = new Mesh { vertices = vertices, uv = uvs, triangles = triangles };
            mesh.RecalculateNormals();
            return mesh;
        }

        /// <summary>A child with this mesh and a Standard material of this colour, optionally textured.</summary>
        public static MeshRenderer Add(Transform parent, string name, Mesh mesh, Color color, Texture2D texture = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var material = new Material(Shader.Find("Standard")) { color = color, mainTexture = texture };
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        /// <summary>A texture whose left half is one colour and right half another.</summary>
        public static Texture2D TwoColours(Color left, Color right)
        {
            var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
                texture.SetPixel(x, y, x < 4 ? left : right);
            texture.Apply();
            return texture;
        }
    }
}
