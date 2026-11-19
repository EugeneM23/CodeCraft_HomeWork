using System.Collections;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>
    /// The player's real cannon in the sandbox fires real shells (the pooled TankShell_01 through BulletShotUseCase)
    /// at the voxel ground ahead: an armour-piercing shell grazing it glances off and flies on; a high-explosive one
    /// at the same graze bursts with its full blast; an armour-piercing one at a steep angle bursts with its small
    /// blast. No rendered frames are waited for (batchmode safe).
    /// </summary>
    public class SandboxShellAmmoTest
    {
        private const string SCENE = "Assets/Game/Voxels/Scenes/VoxelSandbox.unity";

        private IEntity _cannon;
        private Transform _tank;
        private GameFactory _factory;
        private GameObject _muzzle;
        private float _blastRadius;
        private Vector3 _blastAt;
        private int _blasts;

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
            tank.TryGetTransform(out _tank);
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
            _blastRadius = radius;
        }

        [UnityTest]
        public IEnumerator ArmourPiercing_GrazingTheGround_GlancesOff()
        {
            AmmoUseCase.GetOrAdd(_cannon).Value = ShellKind.ArmourPiercing;
            IEntity shell = null;
            yield return Fire(10f, s => shell = s);
            bool rose = false;
            for (int i = 0; i < 40 && !rose; i++)
            {
                yield return new WaitForFixedUpdate();
                rose = shell.GetMoveDirection().Value.y > 0f;
            }

            Assert.That(rose, Is.True, "the shell flies up off the ground");
            Assert.That(shell.GetRicochetCount(), Is.GreaterThanOrEqualTo(1));
            Assert.That(shell.GetMoveSpeed().Value, Is.LessThan(TankAmmoConfig.Load().Get(ShellKind.ArmourPiercing).speed));
        }

        [UnityTest]
        public IEnumerator HighExplosive_GrazingTheGround_Bursts()
        {
            AmmoUseCase.GetOrAdd(_cannon).Value = ShellKind.HighExplosive;
            IEntity shell = null;
            yield return Fire(10f, s => shell = s);
            bool rose = false;
            for (int i = 0; i < 40 && _blasts == 0; i++)
            {
                yield return new WaitForFixedUpdate();
                rose |= shell.GetMoveDirection().Value.y > 0f;
            }

            Assert.That(rose, Is.False, "a high-explosive shell never glances off");
            Assert.That(_blasts, Is.GreaterThan(0), "it burst on the ground");
            Assert.That(_blastRadius, Is.EqualTo(BaseRadius() * TankAmmoConfig.Load().Get(ShellKind.HighExplosive).radiusFactor).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator ArmourPiercing_SteepIntoTheGround_BurstsSmall()
        {
            AmmoUseCase.GetOrAdd(_cannon).Value = ShellKind.ArmourPiercing;
            IEntity shell = null;
            yield return Fire(55f, s => shell = s);
            bool rose = false;
            for (int i = 0; i < 40 && _blasts == 0; i++)
            {
                yield return new WaitForFixedUpdate();
                rose |= shell.GetMoveDirection().Value.y > 0f;
            }

            Assert.That(rose, Is.False, "a steep hit does not glance off");
            Assert.That(_blasts, Is.GreaterThan(0));
            Assert.That(_blastRadius, Is.EqualTo(BaseRadius() * TankAmmoConfig.Load().Get(ShellKind.ArmourPiercing).radiusFactor).Within(0.01f));
        }

        private float BaseRadius() =>
            _cannon.TryGetShellRadius(out IVariable<float> radius) && radius.Value > 0f ? radius.Value : AmmoUseCase.DEFAULT_RADIUS;

        /// <summary>A shell from a muzzle 1 m over the ground 8 m ahead of the tank, <paramref name="below"/>° down.</summary>
        private IEnumerator Fire(float below, System.Action<IEntity> fired)
        {
            Vector3 forward = Vector3.ProjectOnPlane(_tank.forward, Vector3.up).normalized;
            Vector3 over = _tank.position + forward * 8f + Vector3.up * 20f;
            Assert.That(Physics.Raycast(over, Vector3.down, out RaycastHit ground, 40f, ~0, QueryTriggerInteraction.Ignore), Is.True);
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 dir = Quaternion.AngleAxis(below, right) * forward;
            _muzzle.transform.SetPositionAndRotation(ground.point + Vector3.up * 1f, Quaternion.LookRotation(dir));
            _blasts = 0;
            IEntity shell = BulletShotUseCase.Fire(_cannon, _factory, _muzzle.transform);
            Assert.That(AmmoUseCase.IsShell(shell), Is.True, "the cannon's shell takes a type");
            fired(shell);
            yield return null;
        }
    }
}
