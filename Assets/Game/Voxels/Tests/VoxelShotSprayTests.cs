using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Voxels.Tests
{
    /// <summary>
    /// The goo of a body a shell hit (VoxelSpray.ShotGoo): most of it flies on along the shot in a widening cone and lands
    /// about twice as far as the plain upward goo (VoxelSpray.Goo) rises, some goes up, a little splashes to the sides;
    /// with no way along the ground it is the plain upward goo. Green goo, so it lies as cubes instead of soaking in.
    /// </summary>
    public sealed class VoxelShotSprayTests
    {
        private static readonly Color32[] Green = { new(40, 190, 10, 255), new(60, 170, 20, 255) };

        private const int DROPS = 48;
        private const float SPEED = 6.5f;
        private const float SIZE = 0.08f;

        private readonly List<GameObject> _spawned = new();
        private readonly List<Vector3> _flying = new();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned)
                if (go != null)
                    Object.Destroy(go);
            _spawned.Clear();
        }

        private void Floor()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = new Vector3(100f, 1f, 100f);
            _spawned.Add(floor);
            Physics.SyncTransforms();
        }

        [UnityTest]
        public IEnumerator ShotGoo_FliesOnAlongTheShot_TwiceAsFarAsTheUpwardGooRises_InAWideningCone()
        {
            Floor();
            VoxelDebris debris = VoxelWorld.Instance.Debris;
            var up = new Vector3(-150f, 1f, 150f);
            var shot = new Vector3(150f, 1f, 150f);
            HashSet<int> before = LyingNear(debris, shot, 10f).Select(p => p.GetHashCode()).ToHashSet();

            int plain = VoxelSpray.Goo(up, Vector3.up, DROPS, SPEED, 0.5f, Green, SIZE, default, 0.3f);
            // A shell coming down a little, as a tank's gun aims at a body: only its way along the ground counts.
            int along = VoxelSpray.ShotGoo(shot, new Vector3(1f, -0.15f, 0f), DROPS, SPEED, 0.5f, Green, SIZE, default, 0.3f);
            Assert.Greater(plain, 0);
            Assert.That(along, Is.InRange(plain * 0.85f, plain * 1.35f), "about the same goo, only thrown another way");

            float top = up.y;
            float until = Time.time + 6f;
            while (Time.time < until)
            {
                debris.GetClusterPositions(_flying);
                bool left = false;
                foreach (Vector3 p in _flying)
                {
                    if (Flat(p - up) < 4f)
                    {
                        top = Mathf.Max(top, p.y);
                        left = true;
                    }
                    else if (Flat(p - shot) < 10f)
                    {
                        left = true;
                    }
                }

                if (!left)
                    break;
                yield return null;
            }

            float rise = top - up.y;
            Assert.That(rise, Is.InRange(0.9f, 2.2f), "the plain goo rises as it always did");

            List<Vector3> landed = LyingNear(debris, shot, 10f).Where(p => !before.Contains(p.GetHashCode()))
                .Select(p => p - shot).ToList();
            Assert.Greater(landed.Count, DROPS / 2, "the shot's goo lies on the floor");

            List<float> reach = landed.Select(p => p.x).OrderBy(x => x).ToList();
            float far = reach[(int)(reach.Count * 0.9f)];
            float mean = reach.Average();
            float side = landed.Average(p => p.z);
            Debug.Log($"[ShotGoo] rise {rise:F2} m, along: p90 {far:F2} m ({far / rise:F2}x), mean {mean:F2} m, " +
                      $"mean side {side:F2} m, {landed.Count} cubes");

            Assert.That(far / rise, Is.InRange(1.5f, 4.5f), "along the shot it reaches well past how far the goo rises (shells throw it twice as hard)");
            Assert.Greater(mean, 0.6f, "the spray goes the shell's way");
            Assert.Less(Mathf.Abs(side), 0.35f * mean, "straight on, not off to one side");

            float near = landed.Where(p => p.x > 0.6f && p.x < far * 0.5f).Select(p => Mathf.Abs(p.z)).DefaultIfEmpty(0f).Average();
            float wide = landed.Where(p => p.x > far * 0.7f).Select(p => Mathf.Abs(p.z)).DefaultIfEmpty(0f).Average();
            Assert.Greater(wide, near, "a cone: narrow by the body, broad further out");

            Assert.Greater(landed.Count(p => p.z > 0.45f && p.x < 0.8f), 0, "a few splashes out to one side");
            Assert.Greater(landed.Count(p => p.z < -0.45f && p.x < 0.8f), 0, "and to the other");
            Assert.Greater(landed.Count(p => p.x < 0.6f), landed.Count / 8, "some goes up and comes down by the body");
        }

        [UnityTest]
        public IEnumerator ShotGoo_WithNoWayAlongTheGround_IsThePlainUpwardGoo()
        {
            Floor();
            VoxelDebris debris = VoxelWorld.Instance.Debris;
            var at = new Vector3(-150f, 1f, -150f);
            HashSet<int> before = LyingNear(debris, at, 8f).Select(p => p.GetHashCode()).ToHashSet();

            Assert.Greater(VoxelSpray.ShotGoo(at, Vector3.down, DROPS, SPEED, 0.5f, Green, SIZE, default, 0.3f), 0);
            yield return WaitLanded(debris, at, 8f);

            List<Vector3> landed = LyingNear(debris, at, 8f).Where(p => !before.Contains(p.GetHashCode()))
                .Select(p => p - at).ToList();
            Assert.Greater(landed.Count, DROPS / 2);
            Assert.Less(Mathf.Abs(landed.Average(p => p.x)), 0.4f, "a bomb's, a squash's: straight up, no way");
            Assert.Less(Mathf.Abs(landed.Average(p => p.z)), 0.4f);
        }

        [UnityTest]
        public IEnumerator TornPieces_GoTheShotsWay_LessFarThanItsGoo()
        {
            Floor();
            VoxelDebris debris = VoxelWorld.Instance.Debris;
            var feet = new Vector3(150f, 0.05f, -150f);
            HashSet<int> before = LyingNear(debris, feet, 10f).Select(p => p.GetHashCode()).ToHashSet();

            Assert.AreEqual(6, VoxelSpray.Chunks(feet, 1.8f, Vector3.right, Green, Green, null, VoxelSpray.SHOT_PUSH));
            yield return WaitLanded(debris, feet, 10f);

            List<Vector3> landed = LyingNear(debris, feet, 10f).Where(p => !before.Contains(p.GetHashCode()))
                .Select(p => p - feet).ToList();
            Assert.Greater(landed.Count, 50, "the parts lie whole");
            float mean = landed.Average(p => p.x);
            Debug.Log($"[ShotChunks] mean along {mean:F2} m, {landed.Count} cubes");
            Assert.Greater(mean, 0.8f, "the pieces are carried on the shell's way");
            Assert.Less(mean, 3f, "less far than the goo");
        }

        private IEnumerator WaitLanded(VoxelDebris debris, Vector3 at, float radius)
        {
            float until = Time.time + 6f;
            while (Time.time < until)
            {
                debris.GetClusterPositions(_flying);
                if (!_flying.Any(p => Flat(p - at) < radius))
                    yield break;
                yield return null;
            }
        }

        private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        /// <summary>Lying cubes near a point (other tests' cubes lie elsewhere).</summary>
        private static List<Vector3> LyingNear(VoxelDebris debris, Vector3 at, float radius)
        {
            var near = new List<Vector3>();
            for (int slot = 0; slot < debris.LyingCount; slot++)
            {
                if (!debris.TryGetLying(slot, out Matrix4x4 m, out _))
                    continue;
                Vector3 p = m.GetPosition();
                if (Flat(p - at) < radius)
                    near.Add(p);
            }

            return near;
        }
    }
}
