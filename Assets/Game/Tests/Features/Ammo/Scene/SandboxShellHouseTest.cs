using System.Collections;
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
    /// The player's real cannon in the sandbox fires an armour-piercing shell square into one of its town houses,
    /// on a line that meets no load-bearing pier: it goes through the house and out of the far side, still flying,
    /// with no blast, a hole where it went in and one where it came out. A high-explosive shell on the same line
    /// bursts on the first wall. No rendered frames are waited for (batchmode safe).
    /// </summary>
    public class SandboxShellHouseTest
    {
        private const string SCENE = "Assets/Game/Voxels/Scenes/VoxelSandbox.unity";

        private IEntity _cannon;
        private GameFactory _factory;
        private GameObject _muzzle;
        private int _blasts;
        private Vector3 _blastAt;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(SCENE, new LoadSceneParameters(LoadSceneMode.Single));
            for (int i = 0; i < 60; i++)
                yield return new WaitForFixedUpdate();

            var on = typeof(VoxelAirStrike).GetField("_on", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            foreach (VoxelAirStrike strike in Object.FindObjectsByType<VoxelAirStrike>(FindObjectsSortMode.None))
                on?.SetValue(strike, false);

            GameContext context = GameContext.Instance;
            Assert.That(context, Is.Not.Null);
            _factory = context.GetGameFactory();
            IEntity tank = null;
            if (context.TryGetPlayerContext(out PlayerContext player) && player != null)
                tank = player.GetCharacter().Value;
            Assert.That(tank, Is.Not.Null, "the player's tank");
            Assert.That(tank.TryGetWeapon(out IReactiveVariable<IEntity> weapon) && weapon.Value != null, "the tank's cannon");
            _cannon = weapon.Value;
            _muzzle = new GameObject("TestMuzzle");
            ExplosionEvents.Exploded += OnExploded;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            ExplosionEvents.Exploded -= OnExploded;
            if (_muzzle != null)
                Object.Destroy(_muzzle);
            yield return null;
        }

        private void OnExploded(Vector3 at, float radius)
        {
            _blasts++;
            _blastAt = at;
        }

        [UnityTest]
        public IEnumerator ArmourPiercing_GoesThroughAHouse_AndFliesOn()
        {
            Assert.That(FindLine(out VoxelBody house, out Vector3 start, out Vector3 dir, out Vector3 entry, out Vector3 exit,
                out int walls), "a house with a line through it that meets no pier");
            Debug.Log($"[ShellHouse] {house.name}: {walls} walls, entry {entry}, exit {exit}");

            Vector3 side = Vector3.Cross(dir, Vector3.up).normalized * 0.25f;
            Vector3 beside = entry + dir * 0.05f + side;
            bool besideSoft = Probe(house, beside) && !house.IsHard(Material(house, beside));

            AmmoUseCase.GetOrAdd(_cannon).Value = ShellKind.ArmourPiercing;
            IEntity shell = Fire(start, dir);
            float beyond = Vector3.Dot(exit - start, dir) + 1.5f;
            bool out_ = false;
            for (int i = 0; i < 120 && !out_ && _blasts == 0; i++)
            {
                yield return new WaitForFixedUpdate();
                out_ = shell.GetGameObject().activeInHierarchy &&
                       Vector3.Dot(shell.GetTransform().position - start, dir) > beyond;
            }

            Assert.That(_blasts, Is.Zero, $"no blast in the house (one at {_blastAt})");
            Assert.That(out_, Is.True, "the shell came out of the far side");
            Assert.That(shell.GetMoveSpeed().Value, Is.GreaterThan(ShellHouseUseCase.MIN_SPEED));
            Assert.That(shell.GetMoveSpeed().Value, Is.LessThan(TankAmmoConfig.Load().Get(ShellKind.ArmourPiercing).speed),
                "the walls slowed it");
            Assert.That(Probe(house, entry + dir * 0.05f), Is.False, "a hole where it went in");
            Assert.That(Probe(house, exit - dir * 0.05f), Is.False, "a hole where it came out");
            // Wide: 25 cm off the line, the wall (not a pier) is gone too.
            if (besideSoft)
                Assert.That(Probe(house, beside), Is.False, "the hole is about half a metre across");
        }

        [UnityTest]
        public IEnumerator HighExplosive_BurstsOnTheFirstWall()
        {
            Assert.That(FindLine(out VoxelBody _, out Vector3 start, out Vector3 dir, out Vector3 entry, out Vector3 _, out int _));
            AmmoUseCase.GetOrAdd(_cannon).Value = ShellKind.HighExplosive;
            Fire(start, dir);
            for (int i = 0; i < 120 && _blasts == 0; i++)
                yield return new WaitForFixedUpdate();

            Assert.That(_blasts, Is.GreaterThan(0));
            Assert.That(Vector3.Distance(_blastAt, entry), Is.LessThan(1f), "at the first wall");
        }

        private IEntity Fire(Vector3 from, Vector3 dir)
        {
            _muzzle.transform.SetPositionAndRotation(from, Quaternion.LookRotation(dir));
            _blasts = 0;
            IEntity shell = BulletShotUseCase.Fire(_cannon, _factory, _muzzle.transform);
            Assert.That(AmmoUseCase.IsShell(shell), Is.True);
            return shell;
        }

        private static bool Probe(VoxelBody body, Vector3 at) => Material(body, at) != 0;

        private static byte Material(VoxelBody body, Vector3 at)
        {
            Vector3 local = body.transform.InverseTransformPoint(at) - (Vector3)body.Origin;
            var p = new Unity.Mathematics.int3(
                Mathf.FloorToInt(local.x / body.VoxelSize), Mathf.FloorToInt(local.y / body.VoxelSize), Mathf.FloorToInt(local.z / body.VoxelSize));
            return body.VoxelAt(p);
        }

        /// <summary>
        /// A level line through a standing house along its depth, 0.8 m up, under the ground floor's windows, that
        /// meets two walls at least, no pier, and costs a worst-rolled shell less than it has.
        /// </summary>
        internal static bool FindLine(out VoxelBody house, out Vector3 start, out Vector3 dir, out Vector3 entry,
            out Vector3 exit, out int walls)
        {
            house = null;
            start = dir = entry = exit = Vector3.zero;
            walls = 0;
            ShellAmmo ap = TankAmmoConfig.Load().Get(ShellKind.ArmourPiercing);
            float budget = ap.penetration * 0.92f;
            foreach (VoxelBody body in Object.FindObjectsByType<VoxelBody>(FindObjectsSortMode.None))
            {
                if (!ShellHouseUseCase.IsHouse(body) || !body.IsStatic || body.SolidCount == 0)
                    continue;
                Bounds b = body.WorldBounds;
                if (b.size.y < 4f)
                    continue;
                Vector3 along = body.transform.forward;
                Vector3 across = body.transform.right;
                float voxel = body.VoxelSize;
                for (float u = -0.3f; u <= 0.3f; u += 0.05f)
                {
                    Vector3 mid = b.center + across * (b.extents.x * u * 2f);
                    mid.y = b.min.y + 0.8f;
                    Vector3 from = mid - along * (b.extents.z + 1f);
                    float length = b.size.z + 2f, cost = 0f;
                    int count = 0;
                    bool ok = true;
                    Vector3 first = Vector3.zero, last = Vector3.zero;
                    for (float s = 0f; s < length && ok; s += voxel * 0.5f)
                    {
                        Vector3 p = from + along * s;
                        if (!Probe(body, p + along * voxel * 0.25f))
                            continue;
                        ShellHouseUseCase.Wall wall = ShellHouseUseCase.Measure(body, p, along);
                        ok = wall.Through && wall.Hard <= 0f && wall.Thickness <= 0.45f;
                        cost += ShellHouseUseCase.Cost(wall, ap);
                        if (count++ == 0)
                            first = p;
                        last = p + along * wall.Depth;
                        s += wall.Depth;
                    }

                    if (!ok || count < 2 || cost >= budget)
                        continue;
                    house = body;
                    dir = along;
                    start = from - along * 6f;
                    entry = first;
                    exit = last;
                    walls = count;
                    return true;
                }
            }

            return false;
        }
    }
}
