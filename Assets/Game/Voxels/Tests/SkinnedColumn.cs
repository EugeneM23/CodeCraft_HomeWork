using UnityEngine;

namespace Game.Voxels.Tests
{
    /// <summary>
    /// A 0.4 × 1 × 0.4 m column standing on <c>at</c>, made of two stacked boxes skinned to two bones:
    /// Lower (0.25 m up) holds the bottom box, Upper (0.75 m up, its child) the top box.
    /// </summary>
    public static class SkinnedColumn
    {
        public static SkinnedMeshRenderer Create(Vector3 at, out Transform root, out Transform lower, out Transform upper)
        {
            root = new GameObject("Column").transform;
            root.position = at;
            lower = new GameObject("Lower").transform;
            lower.SetParent(root, false);
            lower.localPosition = new Vector3(0f, 0.25f, 0f);
            upper = new GameObject("Upper").transform;
            upper.SetParent(lower, false);
            upper.localPosition = new Vector3(0f, 0.5f, 0f);

            var skinObject = new GameObject("Skin");
            skinObject.transform.SetParent(root, false);

            GameObject primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh cube = primitive.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(primitive);

            // Two separate closed boxes: a lone cube has vertices only at y 0 and 1, so moving a bone
            // would shear the whole column instead of moving one half.
            Vector3[] cubeVertices = cube.vertices;
            int[] cubeTriangles = cube.triangles;
            Vector3[] cubeNormals = cube.normals;
            int count = cubeVertices.Length;
            var vertices = new Vector3[count * 2];
            var normals = new Vector3[count * 2];
            var weights = new BoneWeight[count * 2];
            var triangles = new int[cubeTriangles.Length * 2];
            for (int box = 0; box < 2; box++)
            {
                var offset = new Vector3(0f, box == 0 ? 0.25f : 0.75f, 0f);
                for (int i = 0; i < count; i++)
                {
                    vertices[box * count + i] = Vector3.Scale(cubeVertices[i], new Vector3(0.4f, 0.5f, 0.4f)) + offset;
                    normals[box * count + i] = cubeNormals[i];
                    weights[box * count + i] = new BoneWeight { boneIndex0 = box, weight0 = 1f };
                }

                for (int i = 0; i < cubeTriangles.Length; i++)
                    triangles[box * cubeTriangles.Length + i] = cubeTriangles[i] + box * count;
            }

            var mesh = new Mesh
            {
                name = "Column",
                vertices = vertices,
                triangles = triangles,
                normals = normals,
                boneWeights = weights
            };
            Matrix4x4 skinToWorld = skinObject.transform.localToWorldMatrix;
            mesh.bindposes = new[] { lower.worldToLocalMatrix * skinToWorld, upper.worldToLocalMatrix * skinToWorld };
            mesh.RecalculateBounds();

            var skin = skinObject.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = mesh;
            skin.bones = new[] { lower, upper };
            skin.rootBone = lower;
            skin.updateWhenOffscreen = true;
            return skin;
        }
    }
}
