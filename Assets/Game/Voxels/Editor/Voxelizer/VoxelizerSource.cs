using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Voxels.EditorTools
{
    /// <summary>Collects what the voxelizer needs from a part's renderers, in the part's space.</summary>
    public static class VoxelizerSource
    {
        private static readonly string[] TextureNames = { "_BaseMap", "_MainTex", "_BaseColorMap" };
        private static readonly string[] ColorNames = { "_BaseColor", "_Color" };

        public static VoxelizerTriangles Collect(Transform part, IReadOnlyList<Renderer> renderers)
        {
            var corners = new List<float3>();
            var uvs = new List<float2>();
            var materialOf = new List<int>();
            var materials = new List<Material>();
            float3 min = new float3(float.MaxValue);
            float3 max = new float3(float.MinValue);
            Matrix4x4 toPart = part.worldToLocalMatrix;

            foreach (Renderer renderer in renderers)
            {
                Mesh mesh = MeshOf(renderer, out Matrix4x4 meshToWorld);
                if (mesh == null)
                    continue;

                Matrix4x4 m = toPart * meshToWorld;
                // A mirroring transform flips the winding: swap two corners so cross(b − a, c − a) still
                // points out of the body (the voxelizer reads the inside of boundary faces from it).
                int second = m.determinant < 0f ? 2 : 1, third = 3 - second;
                Vector3[] vertices = mesh.vertices;
                Vector2[] meshUvs = mesh.uv;
                bool hasUv = meshUvs.Length == vertices.Length;
                Material[] shared = renderer.sharedMaterials;

                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    if (mesh.GetTopology(sub) != MeshTopology.Triangles)
                        continue;
                    Material material = shared.Length > 0 ? shared[math.min(sub, shared.Length - 1)] : null;
                    int index = materials.IndexOf(material);
                    if (index < 0)
                    {
                        index = materials.Count;
                        materials.Add(material);
                    }

                    int[] indices = mesh.GetTriangles(sub);
                    for (int i = 0; i + 2 < indices.Length; i += 3)
                    {
                        for (int k = 0; k < 3; k++)
                        {
                            int vertex = indices[i + (k == 0 ? 0 : k == 1 ? second : third)];
                            float3 p = m.MultiplyPoint3x4(vertices[vertex]);
                            corners.Add(p);
                            uvs.Add(hasUv ? (float2)meshUvs[vertex] : float2.zero);
                            min = math.min(min, p);
                            max = math.max(max, p);
                        }

                        materialOf.Add(index);
                    }
                }

                if (renderer is SkinnedMeshRenderer)
                    Object.DestroyImmediate(mesh);
            }

            var result = new VoxelizerTriangles
            {
                Corners = new NativeArray<float3>(corners.ToArray(), Allocator.Persistent),
                Uvs = new NativeArray<float2>(uvs.ToArray(), Allocator.Persistent),
                Material = new NativeArray<int>(materialOf.ToArray(), Allocator.Persistent),
                Tints = new NativeArray<Color32>(materials.Count, Allocator.Persistent),
                Textures = new NativeArray<int4>(materials.Count, Allocator.Persistent),
                Min = min,
                Max = max
            };

            var pixels = new List<Color32>();
            for (int i = 0; i < materials.Count; i++)
            {
                result.Tints[i] = TintOf(materials[i]);
                Color32[] texels = ReadTexture(TextureOf(materials[i]), out int width, out int height);
                result.Textures[i] = new int4(pixels.Count, width, height, texels != null ? 1 : 0);
                if (texels != null)
                    pixels.AddRange(texels);
            }

            result.Pixels = new NativeArray<Color32>(math.max(1, pixels.Count), Allocator.Persistent);
            for (int i = 0; i < pixels.Count; i++)
                result.Pixels[i] = pixels[i];
            return result;
        }

        private static Mesh MeshOf(Renderer renderer, out Matrix4x4 meshToWorld)
        {
            meshToWorld = renderer.transform.localToWorldMatrix;
            if (renderer is MeshRenderer && renderer.TryGetComponent(out MeshFilter filter))
                return filter.sharedMesh;
            if (renderer is SkinnedMeshRenderer skin && skin.sharedMesh != null)
            {
                var baked = new Mesh();
                skin.BakeMesh(baked, true);
                meshToWorld = Matrix4x4.TRS(renderer.transform.position, renderer.transform.rotation, Vector3.one);
                return baked;
            }

            return null;
        }

        /// <summary>A material's colour as the voxelizer sees it, sRGB: its tint times its texture's mean.</summary>
        public static Color32 ColourOf(Material material)
        {
            Color32 tint = TintOf(material);
            Color32[] texels = ReadTexture(TextureOf(material), out _, out _);
            if (texels == null || texels.Length == 0)
                return new Color32(tint.r, tint.g, tint.b, 255);

            float3 sum = float3.zero;
            foreach (Color32 t in texels)
                sum += new float3(t.r, t.g, t.b);
            float3 mean = sum / texels.Length / 255f;
            return new Color32((byte)(tint.r * mean.x), (byte)(tint.g * mean.y), (byte)(tint.b * mean.z), 255);
        }

        private static Color32 TintOf(Material material)
        {
            if (material == null)
                return new Color32(128, 128, 128, 255);
            foreach (string name in ColorNames)
                if (material.HasProperty(name))
                    return material.GetColor(name);
            return new Color32(255, 255, 255, 255);
        }

        private static Texture TextureOf(Material material)
        {
            if (material == null)
                return null;
            foreach (string name in TextureNames)
                if (material.HasProperty(name) && material.GetTexture(name) != null)
                    return material.GetTexture(name);
            return null;
        }

        /// <summary>The texture's pixels, row by row from the bottom, as stored (sRGB).</summary>
        private static Color32[] ReadTexture(Texture texture, out int width, out int height)
        {
            width = height = 0;
            if (texture == null)
                return null;
            width = texture.width;
            height = texture.height;
            RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(texture, rt);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var copy = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            copy.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            copy.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            Color32[] pixels = copy.GetPixels32();
            Object.DestroyImmediate(copy);
            return pixels;
        }
    }
}
