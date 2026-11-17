using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using RayFire;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// The queue's guards: a job whose piece is gone by its turn, or a brick that is not flying,
    /// is dropped without touching RayFire. Actual demolition needs the RayFire runtime.
    /// </summary>
    public sealed class DemolitionQueueTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly Vector3 Far = new(5000f, 5000f, 5000f);

        private readonly List<GameObject> _created = new();

        [SetUp]
        public void SetUp() => DestroyInstance();

        [TearDown]
        public void TearDown()
        {
            DestroyInstance();
            foreach (GameObject gameObject in _created)
                if (gameObject != null)
                    Object.DestroyImmediate(gameObject);
            _created.Clear();
        }

        [Test]
        public void Enqueue_RemembersTheDamageAndHowFarThePieceIsFromTheBlast()
        {
            RayfireRigid pane = Piece(Far + new Vector3(3f, 0f, 4f));

            DemolitionQueue.Enqueue(pane, 40f, Far, 6f, 2f);

            object job = Jobs()[0];
            Assert.That(Field<float>(job, "damage"), Is.EqualTo(40f));
            Assert.That(Field<float>(job, "distance"), Is.EqualTo(5f).Within(1e-4f));
            Assert.That(Field<bool>(job, "split"), Is.False);
        }

        [Test]
        public void PiecesGoneByTheirTurn_AreDroppedUndamaged()
        {
            RayfireRigid inactive = Piece(Far);
            RayfireRigid demolished = Piece(Far);
            RayfireRigid destroyed = Piece(Far);
            DemolitionQueue.Enqueue(inactive, 40f, Far, 6f, 2f);
            DemolitionQueue.Enqueue(demolished, 40f, Far, 6f, 2f);
            DemolitionQueue.Enqueue(destroyed, 40f, Far, 6f, 2f);
            inactive.gameObject.SetActive(false);
            demolished.lim.demolished = true;
            Object.DestroyImmediate(destroyed.gameObject);

            Tick();

            Assert.That(Jobs(), Is.Empty);
            Assert.That(inactive.damage.cur, Is.Zero);
            Assert.That(demolished.damage.cur, Is.Zero);
        }

        [Test]
        public void BrickStillStandingInTheWall_IsNotSplit()
        {
            RayfireRigid brick = Piece(Far);
            brick.physics.rb = brick.gameObject.AddComponent<Rigidbody>();
            brick.physics.rb.isKinematic = true;
            brick.mshDemol.am = 15;

            DemolitionQueue.EnqueueSplit(brick, Far);
            Tick();

            Assert.That(Jobs(), Is.Empty);
            Assert.That(brick.lim.demolished, Is.False);
            Assert.That(brick.mshDemol.am, Is.EqualTo(15), "not set up for a split either");
        }

        [Test]
        public void BrickWithoutABody_IsNotSplit()
        {
            RayfireRigid brick = Piece(Far);
            brick.mshDemol.am = 15;

            DemolitionQueue.EnqueueSplit(brick, Far);
            Tick();

            Assert.That(brick.lim.demolished, Is.False);
            Assert.That(brick.mshDemol.am, Is.EqualTo(15));
        }

        private RayfireRigid Piece(Vector3 position)
        {
            var gameObject = new GameObject("Piece");
            _created.Add(gameObject);
            gameObject.transform.position = position;
            RayfireRigid rigid = gameObject.AddComponent<RayfireRigid>();
            rigid.damage.en = true;
            rigid.damage.max = 100f;
            return rigid;
        }

        private static T Field<T>(object job, string name) =>
            (T)job.GetType().GetField(name).GetValue(job);

        private static FieldInfo InstanceField =>
            typeof(DemolitionQueue).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);

        private static DemolitionQueue Instance()
        {
            var instance = (DemolitionQueue)InstanceField.GetValue(null);
            return instance != null ? instance : null;
        }

        private static IList Jobs() =>
            (IList)typeof(DemolitionQueue).GetField("_jobs", Private).GetValue(Instance());

        private static void Tick() =>
            typeof(DemolitionQueue).GetMethod("Update", Private).Invoke(Instance(), null);

        // In edit mode OnDestroy does not run, so the static is cleared by hand.
        private static void DestroyInstance()
        {
            DemolitionQueue instance = Instance();
            if (instance != null)
                Object.DestroyImmediate(instance.gameObject);
            InstanceField.SetValue(null, null);
        }
    }
}
