using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Voxels.Tests
{
    /// <summary>
    /// A Victorian house bearing its own weight (VoxelBody.SolveStructure): it stands whole,
    /// sheds a cluster over a knocked-out corner, folds when its ground floor goes, does not
    /// hang on a post, loses a corner over a crater, and a floor breaks under a heavy fall.
    /// </summary>
    public sealed class VoxelHouseStructureTests
    {
        private readonly List<GameObject> _spawned = new();

        /// <summary>One storey: 6.4 × 7.8 × 5.4 m, about 56 t.</summary>
        private static readonly Vector3Int OneStorey = new(64, 78, 54);

        [SetUp]
        public void SetUp() => VoxelBody.LogStructure = true;

        /// <summary>Twice, a frame apart: a solve queued this frame still lets pieces fall in its LateUpdate.</summary>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            VoxelBody.LogStructure = false;
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (GameObject go in _spawned)
                    if (go != null)
                        Object.Destroy(go);
                foreach (VoxelBody body in Object.FindObjectsByType<VoxelBody>(FindObjectsSortMode.None))
                    Object.Destroy(body.gameObject);
                yield return null;
            }

            _spawned.Clear();
        }

        [UnityTest]
        public IEnumerator WholeHouses_Stand([Values(78, 106, 134)] int height)
        {
            SpawnFlatGround();
            VoxelBody house = SpawnHouse(new Vector3Int(64, height, 54), Vector3.zero);
            yield return null;

            Assert.IsTrue(house.HasStrength);
            // Timed without the log: the best of a few solves (the first may wait for Burst).
            VoxelBody.LogStructure = false;
            float best = float.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                house.MarkStructureDirty();
                yield return null;
                // The solve runs the frame after the one it was asked in.
                yield return null;
                best = Mathf.Min(best, VoxelWorld.Instance.LastStructureMs);
                Debug.Log($"[Structure] split blocks {VoxelBody.LastBlocksMs:F2} load {VoxelBody.LastLoadMs:F2} all {VoxelWorld.Instance.LastStructureMs:F2}");
            }

            Debug.Log($"[Structure] whole {height}: islands {house.LastSolveIslands} solve ms {best:F2}");
            Assert.AreEqual(0, house.LastSolveIslands, "an undamaged house holds itself up");
            Assert.AreEqual(0, Pieces().Count);
        }

        [UnityTest]
        public IEnumerator House_OnAThinPost_DoesNotHang()
        {
            SpawnFlatGround();
            VoxelBody house = SpawnHouse(OneStorey, Vector3.zero);
            yield return null;

            Bounds b = house.WorldBounds;
            // Everything under 60 cm goes but a post about 20 cm square at one corner: a brick or two.
            Vector3 post = new Vector3(b.min.x + 0.5f, 0f, b.min.z + 0.9f);
            DigBox(house, new Vector3(b.min.x - 0.2f, 0f, b.min.z - 0.2f), new Vector3(b.max.x + 0.2f, 0.6f, b.max.z + 0.2f),
                p => Mathf.Abs(p.x - post.x) < 0.3f && Mathf.Abs(p.z - post.z) < 0.3f);
            for (int i = 0; i < 20 && !Pieces().Any(r => r.mass > 20000f); i++)
                yield return null;

            List<Rigidbody> pieces = Pieces();
            Debug.Log($"[Structure] post: islands {house.LastSolveIslands} pieces {pieces.Count} " +
                      $"biggest {(pieces.Count > 0 ? pieces.Max(r => r.mass) : 0f):F0} left {(house != null ? house.SolidCount : 0)}");
            Assert.That(pieces.Any(r => r.mass > 20000f), "the house came off its post");
        }

        [UnityTest]
        public IEnumerator CornerKnockedOut_HouseStands()
        {
            SpawnFlatGround();
            VoxelBody house = SpawnHouse(OneStorey, Vector3.zero);
            yield return null;
            int before = house.SolidCount;

            Bounds b = house.WorldBounds;
            // The front-left corner of the ground floor, 1.5 × 1.5 m, from the ground to 3 m (the whole storey under the cornice).
            Vector3 lo = new Vector3(b.min.x - 0.2f, 0f, b.min.z - 0.2f);
            Vector3 hi = new Vector3(b.min.x + 1.5f + 0.3f, 3.0f, b.min.z + 0.6f + 1.5f);
            DigBox(house, lo, hi, null);
            for (int i = 0; i < 20; i++)
                yield return null;

            List<Rigidbody> pieces = Pieces();
            Debug.Log($"[Structure] corner: pieces {pieces.Count} masses {string.Join(",", pieces.Select(r => r.mass.ToString("F0")))} " +
                      $"left {house.SolidCount}/{before}");
            Assert.IsNotNull(house, "the house stands");
            Assert.That(house.SolidCount, Is.GreaterThan(before * 0.85f), "most of the house stands");
        }

        /// <summary>A tank shell (a 1.6 m blast) into the middle of a wall: the wall breaks there, the house stands.</summary>
        [UnityTest]
        public IEnumerator ShellIntoOneWall_HouseStands([Values(78, 106)] int height)
        {
            SpawnFlatGround();
            VoxelBody house = SpawnHouse(new Vector3Int(64, height, 54), Vector3.zero);
            yield return null;
            int before = house.SolidCount;

            Bounds b = house.WorldBounds;
            VoxelDamage.Blast(new Vector3(b.min.x + 0.4f, 1.5f, b.center.z), 1.6f, 9f);
            for (int i = 0; i < 30; i++)
                yield return null;

            List<Rigidbody> pieces = Pieces();
            Debug.Log($"[Structure] shell {height}: pieces {pieces.Count} biggest {(pieces.Count > 0 ? pieces.Max(r => r.mass) : 0f):F0} left {house.SolidCount}/{before}");
            Assert.That(house.SolidCount, Is.GreaterThan(before * 0.85f), "one shell does not bring the house down");
            Assert.That(pieces.Where(r => r.mass > 3000f), Is.Empty, "nothing big falls off");
        }

        /// <summary>
        /// A tank ramming the front at full speed, as VoxelRammer bites (a 1.3 m blast and two at the
        /// sides every 10 cm of 2 m): a hole the tank's width, the house stands.
        /// </summary>
        [UnityTest]
        public IEnumerator Ram_HouseStands()
        {
            SpawnFlatGround();
            VoxelBody house = SpawnHouse(new Vector3Int(64, 106, 54), Vector3.zero);
            yield return null;
            int before = house.SolidCount;

            Bounds b = house.WorldBounds;
            for (float z = b.min.z; z < b.min.z + 2.6f; z += 0.1f)
            {
                var centre = new Vector3(b.center.x - 1.2f, 1.1f, z);
                VoxelDamage.Blast(centre, 1.3f, 3f);
                VoxelDamage.Blast(centre + Vector3.right * 1.04f, 1.04f, 3f);
                VoxelDamage.Blast(centre - Vector3.right * 1.04f, 1.04f, 3f);
                yield return null;
            }

            for (int i = 0; i < 30; i++)
                yield return null;

            List<Rigidbody> pieces = Pieces();
            Debug.Log($"[Structure] ram: pieces {pieces.Count} biggest {(pieces.Count > 0 ? pieces.Max(r => r.mass) : 0f):F0} left {house.SolidCount}/{before}");
            Assert.That(house.SolidCount, Is.GreaterThan(before * 0.7f), "one ram does not bring the house down");
        }

        /// <summary>
        /// A three-storey house loses its middle storey but for two corners: the top storey and the
        /// roof come down on the ground floor, press it in and break up — no heap of whole storeys.
        /// </summary>
        [UnityTest]
        public IEnumerator UpperStoreysFall_CrushTheOneBelow()
        {
            SpawnFlatGround();
            VoxelBody house = SpawnHouse(new Vector3Int(64, 134, 54), Vector3.zero);
            yield return null;

            Bounds b = house.WorldBounds;
            Vector3 a = new Vector3(b.min.x + 0.6f, 0f, b.min.z + 0.9f);
            Vector3 c = new Vector3(b.max.x - 0.5f, 0f, b.max.z - 0.5f);
            // The first floor, from its floor to the second, but for two corners.
            DigBox(house, new Vector3(b.min.x - 0.2f, 3.35f, b.min.z - 0.2f), new Vector3(b.max.x + 0.2f, 6.0f, b.max.z + 0.2f),
                p => Near(p, a, 0.45f) || Near(p, c, 0.45f));
            for (int i = 0; i < 6; i++)
                yield return null;
            int ground = house.SolidCount;

            float end = Time.time + 5f;
            while (Time.time < end)
                yield return null;

            List<Rigidbody> pieces = Pieces();
            float biggest = pieces.Count > 0 ? pieces.Max(r => r.mass) : 0f;
            Debug.Log($"[Structure] upper: ground floor {ground} -> {(house != null ? house.SolidCount : 0)}, pieces {pieces.Count} biggest {biggest:F0}");
            // PhysX lands them a little differently each run: 11–24 % of the ground floor goes (20–29 % before the
            // skeleton: its ring beam and floor edges take part of the crush, VoxelStructure), the biggest piece is
            // 2–12 t of the ~50 t that fell.
            Assert.That(house == null || house.SolidCount < ground * 0.92f, "the falling storeys pressed the ground floor in");
            Assert.That(biggest, Is.LessThan(15000f), "the storeys broke up as they landed");
        }

        /// <summary>A building's piece falls at twice g: from 5 m it is down in well under the second it would take.</summary>
        [UnityTest]
        public IEnumerator BuildingPiece_FallsHeavy()
        {
            SpawnFlatGround();
            VoxelBody block = SpawnBlock(new Vector3(0f, 5.3f, 0f), new int3(10, 6, 8));
            var strength = new NativeArray<float3>(256, Allocator.Persistent);
            strength[1] = new float3(2500f, 40f, 2f);
            block.SetStrength(strength);
            block.Launch(Vector3.zero, Vector3.zero);
            yield return null;

            float start = Time.time;
            Rigidbody rigidbody = block.GetComponent<Rigidbody>();
            while (rigidbody != null && rigidbody.worldCenterOfMass.y > 0.6f && Time.time - start < 2f)
                yield return new WaitForFixedUpdate();

            float took = Time.time - start;
            Debug.Log($"[Structure] heavy fall: {took:F2} s, mass {(rigidbody != null ? rigidbody.mass : 0f):F0}");
            Assert.That(took, Is.LessThan(0.85f), "it falls heavy, not like cardboard");
            Assert.That(rigidbody.mass, Is.InRange(800f, 1000f), "480 brick voxels weigh 864 kg");
        }

        [UnityTest]
        public IEnumerator GroundFloorGone_HouseFolds()
        {
            SpawnFlatGround();
            VoxelBody house = SpawnHouse(new Vector3Int(64, 106, 54), Vector3.zero);
            yield return null;
            int before = house.SolidCount;

            Bounds b = house.WorldBounds;
            // The ground floor goes from above the plinth to the first floor, but for two corners.
            Vector3 a = new Vector3(b.min.x + 0.6f, 0f, b.min.z + 0.9f);
            Vector3 c = new Vector3(b.max.x - 0.5f, 0f, b.max.z - 0.5f);
            DigBox(house, new Vector3(b.min.x - 0.2f, 0.55f, b.min.z - 0.2f), new Vector3(b.max.x + 0.2f, 3.2f, b.max.z + 0.2f),
                p => Near(p, a, 0.45f) || Near(p, c, 0.45f));

            float end = Time.time + 3f;
            int most = 0;
            while (Time.time < end)
            {
                yield return null;
                most = Mathf.Max(most, Pieces().Count(r => r.mass > 100f));
            }

            Debug.Log($"[Structure] folds: pieces {most} left {(house != null ? house.SolidCount : 0)}/{before}");
            Assert.That(most, Is.GreaterThanOrEqualTo(3), "the house came down in pieces");
            Assert.That(house == null || house.SolidCount < before * 0.4f, "little of it stands");
            // And it came down a storey: nothing heavy is left up on the stumps of the corners, held
            // by their colliders (the ridge stood at about b.max.y).
            foreach (VoxelBody piece in Pieces().Where(r => r.mass > 2000f).Select(r => r.GetComponent<VoxelBody>()))
                Assert.That(piece.WorldBounds.max.y, Is.LessThan(b.max.y - 2f), $"a {piece.SolidCount}-voxel piece still stands up high");
        }

        /// <summary>A brick wall 6 × 3 m under a concrete cap: the wall over a 1 m doorway and a 3.2 m gap stands.</summary>
        [UnityTest]
        public IEnumerator Opening_WallOverItHolds([Values(1f, 3.2f)] float width)
        {
            const float top = 2.2f;
            SpawnFlatGround();
            VoxelBody wall = SpawnStructure(VoxelStructure.Shape.Wall, new Vector3Int(60, 30, 3));
            yield return null;
            Assert.IsTrue(wall.HasStrength);

            // The opening through the middle.
            DigBox(wall, new Vector3(-width * 0.5f, -0.05f, -0.4f), new Vector3(width * 0.5f, top, 0.4f), null);
            for (int i = 0; i < 20; i++)
                yield return null;

            List<Rigidbody> fallen = Pieces().Where(r => r.mass > 100f).ToList();
            Debug.Log($"[Structure] opening {width}: pieces {string.Join(",", fallen.Select(r => r.mass.ToString("F0")))}");
            Assert.That(fallen, Is.Empty, "the wall over the opening holds");
        }

        [UnityTest]
        public IEnumerator CraterUnderCorner_CornerLosesItsGround()
        {
            VoxelBody ground = SpawnVoxelGround();
            VoxelBody house = SpawnHouse(OneStorey, Vector3.zero);
            yield return null;
            yield return null;

            house.MarkStructureDirty();
            yield return null;
            // The solve runs the frame after the one it was asked in.
            yield return null;
            Assert.AreEqual(0, house.LastSolveIslands, "on whole ground the house stands");

            // The ground under the back-right corner goes 3.5 × 3.5 m, a metre deep: an air bomb's crater (4 m round)
            // at the corner. The skeleton's piers and beams (VoxelStructure) hold a corner over a smaller hole.
            Bounds b = house.WorldBounds;
            int standing = house.SolidCount;
            for (float x = b.max.x - 3.5f; x <= b.max.x + 0.4f; x += 0.2f)
            for (float zz = b.max.z - 3.5f; zz <= b.max.z + 0.4f; zz += 0.2f)
            for (float y = -0.9f; y <= -0.05f; y += 0.2f)
                ground.Dig(new Vector3(x, y, zz), 0.2f, Vector3.zero, 0);
            house.MarkStructureDirty(new Vector3(b.max.x - 1.7f, 0f, b.max.z - 1.7f), 3.5f);
            for (int i = 0; i < 20; i++)
                yield return null;

            List<Rigidbody> pieces = Pieces();
            int lost = standing - (house != null ? house.SolidCount : 0);
            Debug.Log($"[Structure] crater: pieces {string.Join(",", pieces.Select(r => r.mass.ToString("F0")))} lost {lost}");
            // It falls as a piece, or (a scrap under VoxelBody.RUBBLE_BODY_MIN voxels) as crumbs where it was.
            Assert.That(pieces.Any(r => r.mass > 150f) || lost > 100, "the corner over the crater fell");
        }

        [UnityTest]
        public IEnumerator HeavyFall_BreaksAWoodenFloor()
        {
            SpawnFlatGround();
            // Two brick piers 0.8 m square and 2 m tall, 4 m apart, a wooden floor across them.
            VoxelBody bridge = SpawnBridge();
            yield return null;
            yield return null;
            bridge.MarkStructureDirty();
            yield return null;
            // The solve runs the frame after the one it was asked in.
            yield return null;
            Assert.AreEqual(0, bridge.LastSolveIslands, "the floor bears itself");

            int floor = bridge.SolidCount;
            // A two-tonne block of brick dropped from 3 m onto the middle of the floor.
            VoxelBody block = SpawnBlock(new Vector3(2.8f, 5.2f, 0.4f), new int3(10, 6, 8));
            block.Launch(Vector3.down * 2f, Vector3.zero);
            float end = Time.time + 3f;
            while (Time.time < end)
                yield return null;

            int pieces = Object.FindObjectsByType<VoxelBody>(FindObjectsSortMode.None)
                .Count(v => v != block && v != bridge && v.GetComponent<Rigidbody>() != null);
            int left = bridge != null ? bridge.SolidCount : 0;
            Debug.Log($"[Structure] fall: bridge pieces {pieces} voxels {floor} -> {left}");
            // It broke off in pieces, or was crushed through (the hit crumbles what it lands on).
            Assert.That(pieces > 0 || left < floor - 100, "the floor gave way under the hit");
        }

        private static bool Near(Vector3 p, Vector3 c, float half) => Mathf.Abs(p.x - c.x) < half && Mathf.Abs(p.z - c.z) < half;

        private static List<Rigidbody> Pieces() =>
            Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None).Where(r => r.GetComponent<VoxelBody>() != null).ToList();

        /// <summary>Takes out every voxel of the box but those <paramref name="keep"/> spares (by x, z).</summary>
        private static void DigBox(VoxelBody body, Vector3 lo, Vector3 hi, System.Func<Vector3, bool> keep)
        {
            const float step = 0.2f;
            for (float x = lo.x; x <= hi.x; x += step)
            for (float y = lo.y + 0.05f; y <= hi.y; y += step)
            for (float z = lo.z; z <= hi.z; z += step)
            {
                var p = new Vector3(x, y, z);
                if (keep != null && keep(p))
                    continue;
                body.Dig(p, 0.19f, Vector3.zero, 0);
            }

            // As a blast would: the load is worked out again round the damage.
            body.MarkStructureDirty((lo + hi) * 0.5f, (hi - lo).magnitude * 0.5f);
        }

        private void SpawnFlatGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(60f, 1f, 60f);
            _spawned.Add(ground);
        }

        /// <summary>A flat voxel ground 16 × 1.2 × 16 m, its top at 0, marked as terrain.</summary>
        private VoxelBody SpawnVoxelGround()
        {
            var size = new int3(160, 12, 160);
            var voxels = new NativeArray<byte>(size.x * size.y * size.z, Allocator.Persistent);
            for (int i = 0; i < voxels.Length; i++)
                voxels[i] = 1;
            var palette = new NativeArray<Color32>(256, Allocator.Persistent);
            palette[1] = new Color32(90, 70, 50, 255);

            var go = new GameObject("Ground");
            _spawned.Add(go);
            VoxelBodySettings settings = VoxelBodySettings.Default(0.1f);
            settings.Origin = new float3(-8f, -1.2f, -8f);
            settings.Anchor = VoxelAnchor.Ground;
            settings.Terrain = true;
            settings.Split = false;
            settings.ChunkSize = 32;
            VoxelBody body = go.AddComponent<VoxelBody>();
            body.Init(size, voxels, palette, settings);
            return body;
        }

        /// <summary>Two brick piers with a 10 cm wooden floor across, standing on the ground.</summary>
        private VoxelBody SpawnBridge()
        {
            var size = new int3(56, 22, 8);
            var voxels = new NativeArray<byte>(size.x * size.y * size.z, Allocator.Persistent);
            for (int z = 0; z < size.z; z++)
            for (int y = 0; y < size.y; y++)
            for (int x = 0; x < size.x; x++)
            {
                bool pier = (x < 8 || x >= 48) && y < 20;
                bool floor = y >= 20;
                if (pier || floor)
                    voxels[x + size.x * (y + size.y * z)] = (byte)(pier ? 1 : 2);
            }

            var palette = new NativeArray<Color32>(256, Allocator.Persistent);
            palette[1] = new Color32(150, 60, 40, 255);
            palette[2] = new Color32(120, 90, 60, 255);
            var strength = new NativeArray<float3>(256, Allocator.Persistent);
            strength[1] = new float3(1000f, 10f, 2f);
            strength[2] = new float3(600f, 400f, 4f);

            var go = new GameObject("Bridge");
            _spawned.Add(go);
            VoxelBodySettings settings = VoxelBodySettings.Default(0.1f);
            settings.Anchor = VoxelAnchor.Ground;
            VoxelBody body = go.AddComponent<VoxelBody>();
            body.Init(size, voxels, palette, settings);
            body.SetStrength(strength);
            return body;
        }

        private VoxelBody SpawnBlock(Vector3 at, int3 size)
        {
            var voxels = new NativeArray<byte>(size.x * size.y * size.z, Allocator.Persistent);
            for (int i = 0; i < voxels.Length; i++)
                voxels[i] = 1;
            var palette = new NativeArray<Color32>(256, Allocator.Persistent);
            palette[1] = new Color32(100, 100, 100, 255);

            var go = new GameObject("Block");
            go.transform.position = at;
            _spawned.Add(go);
            VoxelBodySettings settings = VoxelBodySettings.Default(0.1f);
            settings.Origin = -(float3)size * 0.05f;
            settings.Density = 4000f;
            VoxelBody body = go.AddComponent<VoxelBody>();
            body.Init(size, voxels, palette, settings);
            return body;
        }

        private VoxelBody SpawnStructure(VoxelStructure.Shape shape, Vector3Int size)
        {
            var go = new GameObject(shape.ToString());
            go.SetActive(false);
            VoxelStructure structure = go.AddComponent<VoxelStructure>();
            Set(structure, "_shape", shape);
            Set(structure, "_size", size);
            go.SetActive(true);
            _spawned.Add(go);
            structure.Build();
            return go.GetComponentInChildren<VoxelBody>();
        }

        private VoxelBody SpawnHouse(Vector3Int size, Vector3 position)
        {
            var go = new GameObject("House");
            go.SetActive(false);
            go.transform.position = position;
            VoxelStructure structure = go.AddComponent<VoxelStructure>();
            Set(structure, "_shape", VoxelStructure.Shape.Victorian);
            Set(structure, "_size", size);
            go.SetActive(true);
            _spawned.Add(go);
            structure.Build();
            return go.GetComponentInChildren<VoxelBody>();
        }

        private static void Set<T>(Object target, string field, T value)
        {
            var info = target.GetType().GetField(field,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            info.SetValue(target, value);
        }
    }
}
