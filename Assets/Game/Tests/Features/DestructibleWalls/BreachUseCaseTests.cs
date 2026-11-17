using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using RayFire;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// Metre bricks far from the scene origin, out of the way of whatever the open scene holds.
    /// Bricks are dynamic pieces of a prefractured wall (their MeshRoot set), so a breach throws
    /// them without asking RayFire to activate anything.
    /// </summary>
    public sealed class BreachUseCaseTests
    {
        private static readonly Vector3 Far = new(5000f, 5000f, 5000f);

        private readonly List<GameObject> _created = new();
        private RayfireRigid _meshRoot;

        [SetUp]
        public void SetUp()
        {
            ResetSingletons();
            _meshRoot = Track(new GameObject("Wall")).AddComponent<RayfireRigid>();
        }

        [TearDown]
        public void TearDown()
        {
            ResetSingletons();
            foreach (GameObject gameObject in _created)
                if (gameObject != null)
                    Object.DestroyImmediate(gameObject);
            _created.Clear();
        }

        [Test]
        public void BrickAtTheBlast_IsThrownAtFullSpeed()
        {
            Rigidbody brick = Brick(Far);

            BreachUseCase.Breach(Far, 2f, 10f, 0f);

            Assert.That(brick.linearVelocity.magnitude, Is.EqualTo(10f).Within(1e-3f));
        }

        [Test]
        public void BrickAtTheEdgeOfTheBreach_IsThrownSlower()
        {
            // Its near face is 1 m out of a 2 m breach: three quarters of the speed.
            Rigidbody brick = Brick(Far + new Vector3(1.5f, 0f, 0f));

            BreachUseCase.Breach(Far, 2f, 10f, 0f);

            Assert.That(brick.linearVelocity.magnitude, Is.EqualTo(7.5f).Within(1e-3f));
        }

        [Test]
        public void BrickBelowTheBlast_IsNeverDrivenDown()
        {
            Rigidbody brick = Brick(Far);

            for (int i = 0; i < 30; i++)
            {
                BreachUseCase.Breach(Far + new Vector3(0f, 0.4f, 0f), 2f, 10f, 0f);
                Assert.That(brick.linearVelocity.y, Is.GreaterThanOrEqualTo(0f));
            }
        }

        [Test]
        public void BrickOutsideTheBreach_IsNotTouched()
        {
            Rigidbody brick = Brick(Far + new Vector3(4f, 0f, 0f));

            BreachUseCase.Breach(Far, 2f, 10f, 0f);

            Assert.That(brick.linearVelocity, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void BrickHeldInTheWall_StaysPut()
        {
            Rigidbody brick = Brick(Far);
            brick.isKinematic = true;

            BreachUseCase.Breach(Far, 2f, 10f, 0f);

            Assert.That(brick.linearVelocity, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void ThrownBrick_PassesThroughTheWallPiecesNextToIt_ButNotTheFloor()
        {
            Rigidbody brick = Brick(Far);
            GameObject neighbour = Track(new GameObject("Neighbour"));
            neighbour.transform.position = Far + new Vector3(1f, 0f, 0f);
            Collider neighbourCollider = neighbour.AddComponent<BoxCollider>();
            neighbour.AddComponent<Rigidbody>().isKinematic = true;
            GameObject floor = Track(new GameObject("Floor"));
            floor.transform.position = Far + new Vector3(0f, -1f, 0f);
            Collider floorCollider = floor.AddComponent<BoxCollider>();
            Physics.SyncTransforms();

            BreachUseCase.Breach(Far, 0.4f, 10f, 0f);

            Collider own = brick.GetComponent<Collider>();
            Assert.That(Physics.GetIgnoreCollision(own, neighbourCollider), Is.True);
            Assert.That(Physics.GetIgnoreCollision(own, floorCollider), Is.False);
        }

        [Test]
        public void StandaloneDestructibleInTheBreach_IsQueuedToBreakWithAllItCanTake()
        {
            RayfireRigid pane = Pane(Far);

            BreachUseCase.Breach(Far, 2f, 10f, 0f);

            IList jobs = QueuedJobs();
            Assert.That(jobs.Count, Is.EqualTo(1));
            Assert.That(JobField<RayfireRigid>(jobs[0], "rigid"), Is.SameAs(pane));
            Assert.That(JobField<float>(jobs[0], "damage"), Is.EqualTo(pane.damage.max));
            Assert.That(JobField<float>(jobs[0], "push"), Is.EqualTo(10f));
        }

        [Test]
        public void StandaloneObjectThatTakesNoDamage_IsLeftAlone()
        {
            RayfireRigid pane = Pane(Far);
            pane.damage.en = false;

            BreachUseCase.Breach(Far, 2f, 10f, 0f);

            Assert.That(QueuedJobs(), Is.Empty);
        }

        [Test]
        public void AlreadyDemolishedObject_IsLeftAlone()
        {
            RayfireRigid pane = Pane(Far);
            pane.lim.demolished = true;

            BreachUseCase.Breach(Far, 2f, 10f, 0f);

            Assert.That(QueuedJobs(), Is.Empty);
        }

        [Test]
        public void BreachOfNoSize_DoesNothing()
        {
            Rigidbody brick = Brick(Far);
            Pane(Far);

            BreachUseCase.Breach(Far, 0f, 10f, 0f);

            Assert.That(brick.linearVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(QueuedJobs(), Is.Empty);
        }

        private GameObject Track(GameObject gameObject)
        {
            _created.Add(gameObject);
            return gameObject;
        }

        // A dynamic piece of the prefractured wall, a metre across.
        private Rigidbody Brick(Vector3 position)
        {
            GameObject brick = Track(new GameObject("Brick"));
            brick.transform.position = position;
            brick.AddComponent<BoxCollider>();
            Rigidbody body = brick.AddComponent<Rigidbody>();
            body.useGravity = false;
            RayfireRigid rigid = brick.AddComponent<RayfireRigid>();
            rigid.simTp = SimType.Dynamic;
            rigid.meshRoot = _meshRoot;
            rigid.physics.rb = body;
            Physics.SyncTransforms();
            return body;
        }

        // A standalone destructible: a glass pane with no body until it breaks.
        private RayfireRigid Pane(Vector3 position)
        {
            GameObject pane = Track(new GameObject("Pane"));
            pane.transform.position = position;
            pane.AddComponent<BoxCollider>();
            RayfireRigid rigid = pane.AddComponent<RayfireRigid>();
            rigid.damage.en = true;
            rigid.damage.max = 50f;
            Physics.SyncTransforms();
            return rigid;
        }

        private static T JobField<T>(object job, string name) =>
            (T)job.GetType().GetField(name).GetValue(job);

        private static IList QueuedJobs()
        {
            var queue = (DemolitionQueue)QueueInstance.GetValue(null);
            if (queue == null)
                return new List<object>();

            return (IList)typeof(DemolitionQueue)
                .GetField("_jobs", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(queue);
        }

        private static FieldInfo QueueInstance =>
            typeof(DemolitionQueue).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);

        private static FieldInfo IgnoreInstance =>
            typeof(TemporaryCollisionIgnore).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);

        // In edit mode OnDestroy does not run, so the statics are cleared by hand.
        private static void ResetSingletons()
        {
            foreach (FieldInfo field in new[] { QueueInstance, IgnoreInstance })
            {
                var instance = (Component)field.GetValue(null);
                if (instance != null)
                    Object.DestroyImmediate(instance.gameObject);
                field.SetValue(null, null);
            }
        }
    }
}
