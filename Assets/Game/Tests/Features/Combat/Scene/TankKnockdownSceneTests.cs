using System.Collections;
using System.Reflection;
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
    /// The tank runs a sandbox zombie down (Zombie.prefab, VoxelSandbox): driving into it knocks it down
    /// (KnockdownBehaviour: its fall plays, it lies where it fell) and it is popped only once the hull is over it
    /// (TankCrushBehaviour), not at the first touch.
    /// </summary>
    public sealed class TankKnockdownSceneTests
    {
        private const string SCENE = "Assets/Game/Voxels/Scenes/VoxelSandbox.unity";
        private const float SPEED = 6f;

        private Rigidbody _body;
        private Vector3 _forward;
        private IReactiveVariable<Vector2> _drive;

        [TearDown]
        public void TearDown() => Application.onBeforeRender -= Drive;

        /// <summary>Full throttle, and the tank kept at <see cref="SPEED"/> along its way (no ramp-up to wait for).</summary>
        private void Drive()
        {
            // Nothing in the way: a level-up offer or the window's focus would pause the game loop (the ticks).
            if (GameTicks.Loop != null)
                foreach (object source in new System.Collections.Generic.List<object>(GameTicks.Loop.PauseSources))
                    GameTicks.Loop.Resume(source);
            if (_drive != null)
                _drive.Value = new Vector2(0f, 1f);
            if (_body == null)
                return;
            Vector3 v = _body.linearVelocity;
            float along = Vector3.Dot(v, _forward);
            if (along < SPEED)
                _body.linearVelocity = v + _forward * (SPEED - along);
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator ATankDrivingIntoAZombie_KnocksItDown_ThenPopsItUnderTheHull()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(SCENE, new LoadSceneParameters(LoadSceneMode.Single));
            for (int i = 0; i < 30; i++)
                yield return new WaitForFixedUpdate();
            FieldInfo on = typeof(VoxelAirStrike).GetField("_on", BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (VoxelAirStrike strike in Object.FindObjectsByType<VoxelAirStrike>(FindObjectsSortMode.None))
                on?.SetValue(strike, false);

            GameObject tank = GameObject.Find("Tank_01");
            Assert.That(tank, Is.Not.Null, "the sandbox's tank");
            _body = tank.GetComponent<Rigidbody>();
            _forward = Vector3.ProjectOnPlane(tank.transform.forward, Vector3.up).normalized;
            Assert.That(((IEntity)tank.GetComponent<MonoEntity>()).TryGetDriveInput(out _drive));

            KnockdownInstaller installer = Object.FindFirstObjectByType<KnockdownInstaller>();
            Assert.That(installer, Is.Not.Null, "a sandbox zombie that can be knocked down");
            IEntity zombie = installer.GetComponent<MonoEntity>();
            Assert.That(zombie.TryGetBehaviour(out KnockdownBehaviour knockdown));
            zombie.GetMoveSpeed().Value = 0f; // it stands: only the tank moves
            Animator animator = zombie.GetAnimator();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);

            Vector3 at = tank.transform.position + _forward * 8f;
            if (Physics.Raycast(at + Vector3.up * 20f, Vector3.down, out RaycastHit ground, 40f, 1 << PhysicsLayers.Ground))
                at.y = ground.point.y;
            installer.transform.position = at;
            Physics.SyncTransforms();
            float standing = head.position.y - at.y;

            Application.onBeforeRender += Drive;
            float knockedAhead = float.NaN, poppedAhead = float.NaN, lowest = standing, drift = 0f;
            float until = Time.time + 6f;
            while (Time.time < until)
            {
                yield return null;
                float ahead = Vector3.Dot(at - tank.transform.position, _forward);
                if (knockdown.IsDown && float.IsNaN(knockedAhead))
                    knockedAhead = ahead;
                if (knockdown.IsDown)
                {
                    lowest = Mathf.Min(lowest, head.position.y - at.y);
                    drift = Mathf.Max(drift, Vector3.Distance(knockdown.Position, at));
                }
                // Dead: the death disposes it (no health left at all) or its health is empty.
                if (!zombie.TryGetHealth(out Health health) || health.IsEmpty())
                {
                    poppedAhead = ahead;
                    break;
                }
            }

            Application.onBeforeRender -= Drive;
            Debug.Log($"[Knockdown] knocked {knockedAhead:F2} m ahead of the tank's centre, popped {poppedAhead:F2} m; head {standing:F2} -> {lowest:F2} m");
            Assert.IsFalse(float.IsNaN(knockedAhead), "driving into it knocks it down");
            Assert.IsFalse(float.IsNaN(poppedAhead), "the hull over it pops it");
            Assert.Greater(knockedAhead - poppedAhead, 0.5f, "popped only after the tank rolled on, not at the first touch");
            Assert.Less(Mathf.Abs(poppedAhead), 1.4f, "popped under the hull");
            Assert.Less(drift, 0.2f, "it lay where it fell: the tank rolled onto it");
            Assert.Less(lowest, standing - 0.1f, "it was falling (its head going down) when the hull reached it");
        }
    }
}
