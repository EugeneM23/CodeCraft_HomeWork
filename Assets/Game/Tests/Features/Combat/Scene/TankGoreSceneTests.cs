using System.Collections;
using System.Reflection;
using Atomic.Elements;
using Atomic.Entities;
using Game.Voxels;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>
    /// Gore and the tank in VoxelSandbox: a body part lying as debris (a shell's torn body, VoxelSpray.Chunks) bursts under
    /// a track and bloodies it (TankSquashBehaviour), and goo coming down anywhere on the tank stains it there
    /// (TankBloodBehaviour): the deck, the rear, the fenders, the turret.
    /// </summary>
    public sealed class TankGoreSceneTests
    {
        private const string SCENE = "Assets/Game/Voxels/Scenes/VoxelSandbox.unity";

        private static readonly Color32[] Goo = { new Color(0.6f, 0.04f, 0.03f) };

        private GameObject _tank;
        private IReactiveVariable<Vector2> _drive;
        private Vector2 _throttle;

        private IEnumerator Load()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(SCENE, new LoadSceneParameters(LoadSceneMode.Single));
            for (int i = 0; i < 30; i++)
                yield return new WaitForFixedUpdate();

            FieldInfo on = typeof(VoxelAirStrike).GetField("_on", BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (VoxelAirStrike strike in Object.FindObjectsByType<VoxelAirStrike>(FindObjectsSortMode.None))
                on?.SetValue(strike, false);

            _tank = GameObject.Find("Tank_01");
            Assert.That(_tank, Is.Not.Null, "the sandbox's tank");
        }

        private T Behaviour<T>() where T : class
        {
            foreach (object behaviour in ((IEntity)_tank.GetComponent<MonoEntity>()).GetBehaviours())
                if (behaviour is T found)
                    return found;
            return null;
        }

        [TearDown]
        public void TearDown() => Application.onBeforeRender -= WriteDrive;

        /// <summary>The controller writes the input each frame: the throttle goes in last, before the frame renders.</summary>
        private void WriteDrive()
        {
            if (_drive != null)
                _drive.Value = _throttle;
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator ABodyPartLyingAsDebris_BurstsUnderATrack_AndThatTrackIsBloody()
        {
            yield return Load();
            var squash = Behaviour<TankSquashBehaviour>();
            Assert.That(squash, Is.Not.Null, "the tank has a TankSquashBehaviour");
            var ruts = _tank.GetComponent<VoxelTrackRuts>();
            Assert.That(((IEntity)_tank.GetComponent<MonoEntity>()).TryGetDriveInput(out _drive), "the tank has DriveInput");

            // A torn body part (as VoxelSpray.Chunks throws it) lying 4 m ahead, under the right track.
            Transform frame = _tank.GetComponent<Rigidbody>().transform;
            VoxelDebris debris = VoxelWorld.Instance.Debris;
            Vector3 at = frame.position + frame.right * 0.8f + frame.forward * 4f + Vector3.up * 0.5f;
            int part = debris.BeginGoreCluster(at, Vector3.zero, 0f);
            for (int i = 0; i < 27; i++)
                debris.AddCube(part, at + new Vector3(i % 3, i / 9, i / 3 % 3) * 0.12f, 0.12f, new Color(0.55f, 0.6f, 0.3f));
            debris.SetGoo(part, true);
            for (int i = 0; i < 90; i++)
                yield return new WaitForFixedUpdate();
            int lying = debris.LyingCount;
            Assert.That(lying, Is.GreaterThanOrEqualTo(27), "the part lies");

            int before = squash.BurstCount;
            _throttle = new Vector2(0f, 0.8f);
            Application.onBeforeRender += WriteDrive;
            for (int i = 0; i < 400 && squash.BurstCount == before; i++)
                yield return new WaitForFixedUpdate();
            for (int i = 0; i < 25; i++)
                yield return new WaitForFixedUpdate();
            _throttle = Vector2.zero;

            Assert.That(squash.BurstCount - before, Is.GreaterThanOrEqualTo(1), "the part burst under the track");
            Assert.That(ruts.TrackBlood(1), Is.GreaterThan(0.3f), "the right track is bloody");
            Debug.Log($"[TankGoreTest] bursts {squash.BurstCount - before}, right track blood {ruts.TrackBlood(1):F2}");
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator GooComingDownOnTheTank_StainsItEverywhere()
        {
            yield return Load();
            var blood = Behaviour<TankBloodBehaviour>();
            Assert.That(blood, Is.Not.Null, "the tank has a TankBloodBehaviour");
            var skin = _tank.GetComponent<VoxelizedModel>();
            Transform frame = _tank.GetComponent<Rigidbody>().transform;

            // The hull's top in the tank's frame (the turret and gun turn: left out).
            Bounds hull = default;
            bool any = false;
            foreach (VoxelBody part in skin.Bodies)
            {
                if (part == null || !part.IsCreated || part.transform.parent == null ||
                    part.transform.parent.name.Contains("Turret") || part.transform.parent.name.Contains("Gun"))
                    continue;
                Bounds world = part.WorldBounds;
                for (int k = 0; k < 8; k++)
                {
                    Vector3 corner = world.center + Vector3.Scale(world.extents,
                        new Vector3((k & 1) != 0 ? 1 : -1, (k & 2) != 0 ? 1 : -1, (k & 4) != 0 ? 1 : -1));
                    Vector3 local = frame.InverseTransformPoint(corner);
                    if (!any)
                        hull = new Bounds(local, Vector3.zero);
                    else
                        hull.Encapsulate(local);
                    any = true;
                }
            }

            Assert.IsTrue(any, "the tank's skin is built");

            // Goo comes down on a grid over all of it, front to rear, fender to fender, in half a second: a fountain's worth.
            const int X = 5, Z = 7;
            var spots = new Vector3[X * Z];
            int thrown = 0;
            for (int xi = 0; xi < X; xi++)
            for (int zi = 0; zi < Z; zi++)
            {
                Vector3 local = new(hull.center.x + hull.extents.x * 0.85f * (xi / 2f - 1f), 0f,
                    hull.center.z + hull.extents.z * 0.88f * (zi / 3f - 1f));
                spots[xi * Z + zi] = local;
                thrown += VoxelSpray.Goo(frame.TransformPoint(local) + Vector3.up * 4.5f, Vector3.down, 6, 1f, 0.05f, Goo, 0.08f,
                    default, 0.05f);
            }

            Assert.That(thrown, Is.GreaterThan(200));
            for (int i = 0; i < 200; i++)
                yield return new WaitForFixedUpdate();

            int stained = 0, rear = 0, front = 0, fenders = 0;
            for (int s = 0; s < spots.Length; s++)
            {
                if (!Bloody(skin, frame, spots[s]))
                    continue;
                stained++;
                int zi = s % Z, xi = s / Z;
                if (zi == 0)
                    rear++;
                if (zi == Z - 1)
                    front++;
                if (xi == 0 || xi == X - 1)
                    fenders++;
            }

            Debug.Log($"[TankGoreTest] stained {stained}/{spots.Length}, rear {rear}, front {front}, fenders {fenders}, laid {blood.Laid}");
            Assert.That(stained, Is.GreaterThanOrEqualTo(spots.Length * 2 / 3), "goo stained the tank wherever it came down");
            Assert.That(rear, Is.GreaterThanOrEqualTo(1), "the rear (none before: the box was made once, gun forward)");
            Assert.That(front, Is.GreaterThanOrEqualTo(2), "the front");
            Assert.That(fenders, Is.GreaterThanOrEqualTo(6), "the fenders");
        }

        /// <summary>Blood on the topmost skin voxel round a spot of the tank's frame (a 30 cm square: the stains are speckled).</summary>
        private static bool Bloody(VoxelizedModel skin, Transform frame, Vector3 local)
        {
            for (int ox = -1; ox <= 1; ox++)
            for (int oz = -1; oz <= 1; oz++)
            {
                Vector3 p = frame.TransformPoint(local + new Vector3(ox * 0.1f, 0f, oz * 0.1f));
                float best = float.MinValue;
                Color32 top = default;
                foreach (VoxelBody part in skin.Bodies)
                {
                    if (part == null || !part.IsCreated)
                        continue;
                    Bounds b = part.WorldBounds;
                    if (p.x < b.min.x || p.x > b.max.x || p.z < b.min.z || p.z > b.max.z)
                        continue;
                    float step = part.VoxelSize * Mathf.Abs(part.transform.lossyScale.x) * 0.5f;
                    for (float y = b.max.y; y > b.min.y; y -= step)
                    {
                        if (!part.TryGetColor(new Vector3(p.x, y, p.z), out Color32 colour))
                            continue;
                        if (y > best)
                        {
                            best = y;
                            top = colour;
                        }

                        break;
                    }
                }

                // Clearly red (as VoxelBlood.IsBlood has it): the skin's sand, grey and green are not.
                if (best > float.MinValue && top.r > 10 && top.r > top.g * 2.2f && top.r > top.b * 2.2f)
                    return true;
            }

            return false;
        }
    }
}
