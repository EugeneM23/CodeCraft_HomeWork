using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using RayFire;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// Blocks a quarter of a metre across hanging under an anchored piece. With the component's
    /// defaults a seam holds 0.3 m of the material hanging from it: one block (0.25 m tall)
    /// hangs, two do not.
    /// </summary>
    public sealed class CollapseUnderOwnWeightTests
    {
        private const float CELL = 0.25f;
        private const float AREA = CELL * CELL;

        private readonly List<Object> _created = new();
        private CollapseUnderOwnWeight _collapse;
        private RayfireConnectivity _wall;
        private Mesh _block;

        [SetUp]
        public void SetUp()
        {
            var wall = new GameObject("Wall");
            _created.Add(wall);
            _collapse = wall.AddComponent<CollapseUnderOwnWeight>();
            _wall = wall.GetComponent<RayfireConnectivity>();
            _wall.type = ConnectivityType.ByTriangles;
            typeof(CollapseUnderOwnWeight)
                .GetField("_connectivity", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_collapse, _wall);

            _block = Cube(CELL, Vector3.zero);
            _created.Add(_block);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created)
                if (created != null)
                    Object.DestroyImmediate(created);
            _created.Clear();
        }

        [Test]
        public void OneBlockHangingUnderTheAnchor_Holds()
        {
            RFShard anchor = Piece(0, anchored: true);
            RFShard block = Piece(-1);
            Link(anchor, block);

            bool collapsed = _collapse.TearOverloadedSeams(new List<RFShard> { anchor, block });

            Assert.That(collapsed, Is.False);
            Assert.That(anchor.neibShards, Does.Contain(block));
        }

        [Test]
        public void TwoBlocksHangingUnderTheAnchor_TearOffAtTheTop()
        {
            RFShard anchor = Piece(0, anchored: true);
            RFShard upper = Piece(-1);
            RFShard lower = Piece(-2);
            Link(anchor, upper);
            Link(upper, lower);

            bool collapsed = _collapse.TearOverloadedSeams(new List<RFShard> { anchor, upper, lower });

            Assert.That(collapsed, Is.True);
            Assert.That(anchor.neibShards, Has.No.Member(upper));
            Assert.That(upper.neibShards, Is.EqualTo(new[] { lower }), "the two blocks fall together");
        }

        [Test]
        public void KnockedOutPiece_IsNotWeighed()
        {
            RFShard anchor = Piece(0, anchored: true);
            RFShard upper = Piece(-1);
            RFShard lower = Piece(-2);
            Link(anchor, upper);
            Link(upper, lower);
            lower.rigid.lim.demolished = true;

            bool collapsed = _collapse.TearOverloadedSeams(new List<RFShard> { anchor, upper, lower });

            Assert.That(collapsed, Is.False);
            Assert.That(anchor.neibShards, Does.Contain(upper));
        }

        [Test]
        public void MeshVolume_OfACube_IsItsSideCubed()
        {
            Assert.That(MeshVolume(Track(Cube(2f, Vector3.zero))), Is.EqualTo(8f).Within(1e-4f));
        }

        [Test]
        public void MeshVolume_DoesNotDependOnWhereTheMeshSitsAroundItsPivot()
        {
            Assert.That(MeshVolume(Track(Cube(1f, new Vector3(3f, -2f, 5f)))), Is.EqualTo(1f).Within(1e-4f));
        }

        private float MeshVolume(Mesh mesh) =>
            (float)typeof(CollapseUnderOwnWeight)
                .GetMethod("MeshVolume", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_collapse, new object[] { mesh });

        private Mesh Track(Mesh mesh)
        {
            _created.Add(mesh);
            return mesh;
        }

        // A block of the wall `row` cells above the anchor's row.
        private RFShard Piece(int row, bool anchored = false)
        {
            var gameObject = new GameObject("Piece");
            _created.Add(gameObject);
            gameObject.transform.position = new Vector3(0f, row * CELL, 0f);
            MeshFilter filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = _block;
            RayfireRigid rigid = gameObject.AddComponent<RayfireRigid>();
            rigid.act.cnt = _wall;

            return new RFShard
            {
                uny = anchored,
                bnd = new Bounds(gameObject.transform.position, Vector3.one * CELL),
                tm = gameObject.transform,
                mf = filter,
                rigid = rigid,
                neibShards = new List<RFShard>(),
                nIds = new List<int>(),
                nArea = new List<float>()
            };
        }

        private static void Link(RFShard a, RFShard b)
        {
            a.neibShards.Add(b);
            a.nIds.Add(b.id);
            a.nArea.Add(AREA);
            b.neibShards.Add(a);
            b.nIds.Add(a.id);
            b.nArea.Add(AREA);
        }

        // A closed cube `side` across centred on `centre`, every triangle wound outwards.
        private static Mesh Cube(float side, Vector3 centre)
        {
            float h = side * 0.5f;
            var corners = new Vector3[8];
            for (int i = 0; i < 8; i++)
                corners[i] = new Vector3((i & 1) != 0 ? h : -h, (i & 2) != 0 ? h : -h, (i & 4) != 0 ? h : -h);

            int[][] faces =
            {
                new[] { 0, 2, 6, 4 }, new[] { 1, 3, 7, 5 }, // -x, +x
                new[] { 0, 1, 5, 4 }, new[] { 2, 3, 7, 6 }, // -y, +y
                new[] { 0, 1, 3, 2 }, new[] { 4, 5, 7, 6 }  // -z, +z
            };

            var triangles = new List<int>();
            foreach (int[] face in faces)
            {
                AddOutward(corners, triangles, face[0], face[1], face[2]);
                AddOutward(corners, triangles, face[0], face[2], face[3]);
            }

            var vertices = new Vector3[8];
            for (int i = 0; i < 8; i++)
                vertices[i] = corners[i] + centre;

            var mesh = new Mesh { vertices = vertices };
            mesh.SetTriangles(triangles, 0);
            return mesh;
        }

        private static void AddOutward(Vector3[] corners, List<int> triangles, int a, int b, int c)
        {
            Vector3 normal = Vector3.Cross(corners[b] - corners[a], corners[c] - corners[a]);
            bool outward = Vector3.Dot(normal, corners[a] + corners[b] + corners[c]) > 0f;
            triangles.Add(a);
            triangles.Add(outward ? b : c);
            triangles.Add(outward ? c : b);
        }
    }
}
