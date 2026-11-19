using System.Collections.Generic;
using Game.Voxels;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game
{
    /// <summary>
    /// The armour map (the lab's debug view): the tank's outside sampled by a grid of rays from its six sides, each
    /// point classified as a shell would see it (TankArmour) and drawn as a small tile just over the surface — thin
    /// green through yellow to thick red, spaced armour blue. One mesh under the tank, so it turns with it.
    /// </summary>
    public static class ArmourMapBuilder
    {
        /// <summary>Distance (m) between the rays, and the tiles' size.</summary>
        public const float STEP = 0.07f;

        /// <summary>Thickness (mm) drawn green, and the one drawn red.</summary>
        public const float THIN = 25f;

        public const float THICK = 130f;

        private static Material s_material;

        public static GameObject Build(TankArmour armour)
        {
            Transform root = armour.transform;
            Bounds local = LocalBounds(armour);
            var vertices = new List<Vector3>();
            var colors = new List<Color32>();
            var normals = new List<Vector3>();
            var indices = new List<int>();
            local.Expand(0.4f);

            for (int axis = 0; axis < 3; axis++)
            for (int sign = -1; sign <= 1; sign += 2)
            {
                // Rays along -axis*sign from beyond that side, on a grid over the other two axes.
                int u = (axis + 1) % 3, v = (axis + 2) % 3;
                Vector3 dirLocal = Vector3.zero;
                dirLocal[axis] = -sign;
                for (float a = local.min[u]; a <= local.max[u]; a += STEP)
                for (float b = local.min[v]; b <= local.max[v]; b += STEP)
                {
                    Vector3 startLocal = Vector3.zero;
                    startLocal[axis] = sign > 0 ? local.max[axis] : local.min[axis];
                    startLocal[u] = a;
                    startLocal[v] = b;
                    Vector3 from = root.TransformPoint(startLocal);
                    Vector3 dir = root.TransformDirection(dirLocal);
                    if (!Physics.Raycast(from, dir, out RaycastHit hit, local.size[axis] + 1f, ~0, QueryTriggerInteraction.Ignore))
                        continue;
                    if (TankArmour.Find(hit.collider) != armour)
                        continue;
                    Vector3 normal = ArmourSurfaceUseCase.SmoothNormal(hit, dir);
                    if (!armour.Classify(hit.collider, hit.point, normal, out _, out float thickness, out ArmourKind kind) ||
                        kind == ArmourKind.Module)
                        continue;
                    Tile(root, hit.point + hit.normal * 0.03f, hit.normal, ColorOf(thickness, kind), vertices, colors,
                        normals, indices);
                }
            }

            var mesh = new Mesh { name = "ArmourMap", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetNormals(normals);
            mesh.SetTriangles(indices, 0);
            var go = new GameObject("ArmourMap");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Material();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        /// <summary>Green (thin) → yellow → red (thick); spaced armour blue.</summary>
        public static Color32 ColorOf(float thickness, ArmourKind kind)
        {
            if (kind == ArmourKind.Spaced)
                return new Color32(60, 140, 255, 255);
            float t = Mathf.InverseLerp(THIN, THICK, thickness);
            Color c = t < 0.5f
                ? Color.Lerp(new Color(0.1f, 0.85f, 0.15f), new Color(1f, 0.9f, 0.1f), t * 2f)
                : Color.Lerp(new Color(1f, 0.9f, 0.1f), new Color(0.9f, 0.08f, 0.05f), t * 2f - 1f);
            return c;
        }

        private static void Tile(Transform root, Vector3 point, Vector3 normal, Color32 color, List<Vector3> vertices,
            List<Color32> colors, List<Vector3> normals, List<int> indices)
        {
            Vector3 n = root.InverseTransformDirection(normal).normalized;
            Vector3 p = root.InverseTransformPoint(point);
            Vector3 t1 = Vector3.Cross(n, Mathf.Abs(n.y) < 0.9f ? Vector3.up : Vector3.right).normalized * (STEP * 0.5f);
            Vector3 t2 = Vector3.Cross(n, t1);
            int at = vertices.Count;
            vertices.Add(p - t1 - t2);
            vertices.Add(p + t1 - t2);
            vertices.Add(p + t1 + t2);
            vertices.Add(p - t1 + t2);
            for (int i = 0; i < 4; i++)
            {
                colors.Add(color);
                normals.Add(n);
            }

            // Both windings: the tile shows from either side whatever the cross products made of it.
            indices.Add(at); indices.Add(at + 1); indices.Add(at + 2);
            indices.Add(at); indices.Add(at + 2); indices.Add(at + 3);
            indices.Add(at); indices.Add(at + 2); indices.Add(at + 1);
            indices.Add(at); indices.Add(at + 3); indices.Add(at + 2);
        }

        /// <summary>The tank's voxel bodies' grids, bounded in the tank's own frame.</summary>
        private static Bounds LocalBounds(TankArmour armour)
        {
            Transform root = armour.transform;
            bool any = false;
            var bounds = new Bounds();
            foreach (VoxelBody body in armour.GetComponentsInChildren<VoxelBody>())
            {
                Vector3 min = body.Origin;
                Vector3 max = (Vector3)body.Origin + (Vector3)(Unity.Mathematics.float3)body.Size * body.VoxelSize;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) != 0 ? max.x : min.x, (i & 2) != 0 ? max.y : min.y, (i & 4) != 0 ? max.z : min.z);
                    Vector3 p = root.InverseTransformPoint(body.transform.TransformPoint(corner));
                    if (!any)
                        bounds = new Bounds(p, Vector3.zero);
                    else
                        bounds.Encapsulate(p);
                    any = true;
                }
            }

            return bounds;
        }

        private static Material Material()
        {
            if (s_material != null)
                return s_material;
            Shader shader = Shader.Find("Game/Voxel") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");
            s_material = new Material(shader) { name = "ArmourMap" };
            if (s_material.HasProperty("_Color"))
                s_material.SetColor("_Color", Color.white);
            if (s_material.HasProperty("_InstColor"))
                s_material.SetColor("_InstColor", Color.white);
            return s_material;
        }
    }
}
