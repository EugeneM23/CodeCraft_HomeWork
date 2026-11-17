using System.Collections.Generic;
using NUnit.Framework;
using RayFire;
using UnityEngine;

namespace Game.Tests
{
    public sealed class WallSeamsTests
    {
        private readonly List<GameObject> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject gameObject in _created)
                if (gameObject != null)
                    Object.DestroyImmediate(gameObject);
            _created.Clear();
        }

        private static RFShard Shard(Bounds bounds) => new()
        {
            bnd = bounds,
            neibShards = new List<RFShard>(),
            nIds = new List<int>(),
            nArea = new List<float>()
        };

        private static void Link(RFShard a, RFShard b, float area)
        {
            a.neibShards.Add(b);
            a.nIds.Add(b.id);
            a.nArea.Add(area);
            b.neibShards.Add(a);
            b.nIds.Add(a.id);
            b.nArea.Add(area);
        }

        private RayfireConnectivity Wall(ConnectivityType type, float expand)
        {
            var gameObject = new GameObject("Wall");
            _created.Add(gameObject);
            RayfireConnectivity wall = gameObject.AddComponent<RayfireConnectivity>();
            wall.type = type;
            wall.expand = expand;
            return wall;
        }

        private RFShard StandingShard(RayfireConnectivity wall)
        {
            var gameObject = new GameObject("Piece");
            _created.Add(gameObject);
            RayfireRigid rigid = gameObject.AddComponent<RayfireRigid>();
            rigid.act.cnt = wall;

            RFShard shard = Shard(new Bounds(Vector3.zero, Vector3.one));
            shard.tm = gameObject.transform;
            shard.rigid = rigid;
            return shard;
        }

        [Test]
        public void Cut_SeparatesBothPieces()
        {
            RFShard a = Shard(default), b = Shard(default), c = Shard(default);
            Link(a, b, 0.1f);
            Link(a, c, 0.2f);

            WallSeams.Cut(a, b);

            Assert.That(a.neibShards, Is.EqualTo(new[] { c }));
            Assert.That(a.nArea, Is.EqualTo(new[] { 0.2f }));
            Assert.That(b.neibShards, Is.Empty);
            Assert.That(c.neibShards, Is.EqualTo(new[] { a }));
        }

        [Test]
        public void Cut_PiecesThatWereNotJoined_ChangesNothing()
        {
            RFShard a = Shard(default), b = Shard(default), c = Shard(default);
            Link(a, b, 0.1f);

            WallSeams.Cut(a, c);

            Assert.That(a.neibShards, Is.EqualTo(new[] { b }));
            Assert.That(c.neibShards, Is.Empty);
        }

        [Test]
        public void ContactArea_OnAMeshConnectedWall_IsWhatRayFireMeasured()
        {
            RFShard a = Shard(default), b = Shard(default), c = Shard(default);
            Link(a, b, 0.1f);
            Link(a, c, 0.35f);

            Assert.That(WallSeams.ContactArea(Wall(ConnectivityType.ByTriangles, 0.05f), a, 1), Is.EqualTo(0.35f));
        }

        [Test]
        public void ContactArea_OnABoundingBoxWall_IsTheOverlapAlongTheSeamLessTheExpand()
        {
            // Two 1 x 0.5 x 0.2 bricks side by side, each grown by 0.05 towards the other: they
            // overlap 0.1 across the seam, the seam itself is 0.5 by 0.2.
            RFShard a = Shard(new Bounds(new Vector3(0.525f, 0.25f, 0.1f), new Vector3(1.05f, 0.5f, 0.2f)));
            RFShard b = Shard(new Bounds(new Vector3(1.475f, 0.25f, 0.1f), new Vector3(1.05f, 0.5f, 0.2f)));
            Link(a, b, 0.4f);

            float area = WallSeams.ContactArea(Wall(ConnectivityType.ByBoundingBox, 0.05f), a, 0);

            Assert.That(area, Is.EqualTo((0.2f - 0.05f) * (0.5f - 0.05f)).Within(1e-5f));
        }

        [Test]
        public void ContactArea_OfBoxesTouchingAlongAnEdgeOnly_IsNothing()
        {
            // The overlap is no wider than the expand in two directions: no face is shared.
            RFShard a = Shard(new Bounds(new Vector3(0f, 0f, 0f), Vector3.one));
            RFShard b = Shard(new Bounds(new Vector3(0.95f, 0.95f, 0f), Vector3.one));
            Link(a, b, 0.4f);

            Assert.That(WallSeams.ContactArea(Wall(ConnectivityType.ByBoundingBox, 0.05f), a, 0), Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void IsStanding_PieceInTheWall_IsStanding()
        {
            Assert.That(WallSeams.IsStanding(StandingShard(Wall(ConnectivityType.ByTriangles, 0f))), Is.True);
        }

        [Test]
        public void IsStanding_DemolishedPiece_IsNotStanding()
        {
            RFShard shard = StandingShard(Wall(ConnectivityType.ByTriangles, 0f));
            shard.rigid.lim.demolished = true;

            Assert.That(WallSeams.IsStanding(shard), Is.False);
        }

        [Test]
        public void IsStanding_PieceNoLongerConnectedToTheWall_IsNotStanding()
        {
            RFShard shard = StandingShard(Wall(ConnectivityType.ByTriangles, 0f));
            shard.rigid.act.cnt = null;

            Assert.That(WallSeams.IsStanding(shard), Is.False);
        }

        [Test]
        public void IsStanding_PieceWhoseObjectWasDestroyed_IsNotStanding()
        {
            RFShard shard = StandingShard(Wall(ConnectivityType.ByTriangles, 0f));
            Object.DestroyImmediate(shard.tm.gameObject);

            Assert.That(WallSeams.IsStanding(shard), Is.False);
        }

        [Test]
        public void IsStanding_PieceWithoutNeighbourList_IsNotStanding()
        {
            RFShard shard = StandingShard(Wall(ConnectivityType.ByTriangles, 0f));
            shard.neibShards = null;

            Assert.That(WallSeams.IsStanding(shard), Is.False);
        }
    }
}
