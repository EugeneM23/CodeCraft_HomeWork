using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using RayFire;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// How a knocked-out area is split into rubble and chunks, and which seams are cut around them.
    /// The release itself (RayFire's connectivity check) is left out: it needs the RayFire runtime.
    /// </summary>
    public sealed class BreakInChunksTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        private GameObject _wallObject;
        private BreakInChunks _wall;

        [SetUp]
        public void SetUp()
        {
            _wallObject = new GameObject("Wall");
            _wall = _wallObject.AddComponent<BreakInChunks>();
            Set("_minPieces", 3);
            Set("_maxPieces", 3);
            Set("_rubble", 0.25f);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_wallObject);

        [Test]
        public void PiecesNearestTheImpact_ComeOffOneByOne_TheRestInChunksOfNeighbours()
        {
            // A row of eight knocked-out bricks hit at its left end: a quarter of them (two) is
            // rubble, the other six make two chunks of three.
            RFShard[] row = Row(8);
            KnockOut(row);

            Group(Vector3.zero);

            Assert.That(ChunkOf(row[0]), Is.Not.EqualTo(ChunkOf(row[1])));
            Assert.That(ChunkOf(row[2]), Is.EqualTo(ChunkOf(row[3])).And.EqualTo(ChunkOf(row[4])));
            Assert.That(ChunkOf(row[5]), Is.EqualTo(ChunkOf(row[6])).And.EqualTo(ChunkOf(row[7])));
            Assert.That(ChunkOf(row[4]), Is.Not.EqualTo(ChunkOf(row[5])));
            Assert.That(ChunkOf(row[2]), Is.Not.EqualTo(ChunkOf(row[1])));
        }

        [Test]
        public void Rubble_IsTakenFromAroundTheImpact()
        {
            // The same row hit at its right end: there the last two bricks are rubble.
            RFShard[] row = Row(8);
            KnockOut(row);

            Group(new Vector3(7f, 0f, 0f));

            Assert.That(ChunkOf(row[7]), Is.Not.EqualTo(ChunkOf(row[6])));
            Assert.That(ChunkOf(row[5]), Is.EqualTo(ChunkOf(row[4])).And.EqualTo(ChunkOf(row[3])));
            Assert.That(ChunkOf(row[6]), Is.Not.EqualTo(ChunkOf(row[5])));
        }

        [Test]
        public void Chunk_NeverTakesPiecesThatWereNotKnockedOut()
        {
            // Only bricks 2-3 were reached; brick 4 next to them stays in the wall.
            Set("_rubble", 0f);
            RFShard[] row = Row(6);
            KnockOut(row[2], row[3]);

            Group(new Vector3(2f, 0f, 0f));

            Assert.That(ChunkOf(row[2]), Is.EqualTo(ChunkOf(row[3])));
            Assert.That(ChunkMap().ContainsKey(row[4]), Is.False);
            Assert.That(ChunkMap().ContainsKey(row[1]), Is.False);
        }

        [Test]
        public void Cut_SeparatesEveryChunkFromTheWallAndFromTheOtherChunks_KeepingItWhole()
        {
            // Bricks 1-6 knocked out, 0 and 7 stay: rubble is brick 1 and 2 (nearest the impact
            // at 1), chunks are 3-5 and 6.
            RFShard[] row = Row(8);
            KnockOut(row[1], row[2], row[3], row[4], row[5], row[6]);
            Set("_rubble", 0.34f);

            Group(new Vector3(1f, 0f, 0f));
            Invoke("CutAroundChunks");

            Assert.That(row[0].neibShards, Is.Empty, "the wall lets go of the rubble");
            Assert.That(row[2].neibShards, Is.Empty, "rubble flies alone");
            Assert.That(row[3].neibShards, Is.EqualTo(new[] { row[4] }));
            Assert.That(row[4].neibShards, Is.EquivalentTo(new[] { row[3], row[5] }));
            Assert.That(row[5].neibShards, Is.EqualTo(new[] { row[4] }));
            Assert.That(row[7].neibShards, Is.Empty, "the wall lets go of the last chunk");
        }

        // Bricks 1 m apart along X, each joined to the next.
        private static RFShard[] Row(int count)
        {
            var row = new RFShard[count];
            for (int i = 0; i < count; i++)
            {
                row[i] = new RFShard
                {
                    id = i,
                    bnd = new Bounds(new Vector3(i, 0f, 0f), Vector3.one),
                    neibShards = new List<RFShard>(),
                    nIds = new List<int>(),
                    nArea = new List<float>()
                };

                if (i > 0)
                {
                    row[i - 1].neibShards.Add(row[i]);
                    row[i - 1].nIds.Add(i);
                    row[i - 1].nArea.Add(1f);
                    row[i].neibShards.Add(row[i - 1]);
                    row[i].nIds.Add(i - 1);
                    row[i].nArea.Add(1f);
                }
            }

            return row;
        }

        private void KnockOut(params RFShard[] pieces)
        {
            var list = (List<RFShard>)typeof(BreakInChunks).GetField("_pieces", Private).GetValue(_wall);
            list.Clear();
            list.AddRange(pieces);
        }

        private void Group(Vector3 impact) => Invoke("Group", impact);

        private int ChunkOf(RFShard piece) => ChunkMap()[piece];

        private Dictionary<RFShard, int> ChunkMap() =>
            (Dictionary<RFShard, int>)typeof(BreakInChunks).GetField("_chunkOf", Private).GetValue(_wall);

        private void Invoke(string method, params object[] args) =>
            typeof(BreakInChunks).GetMethod(method, Private).Invoke(_wall, args);

        private void Set(string field, object value) =>
            typeof(BreakInChunks).GetField(field, Private).SetValue(_wall, value);
    }
}
