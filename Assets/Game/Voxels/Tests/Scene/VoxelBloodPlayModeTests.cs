using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Voxels.Tests
{
    public sealed class VoxelBloodPlayModeTests
    {
        private readonly List<GameObject> _created = new();
        private IEntity _character;

        [SetUp]
        public void SetUp()
        {
            var body = new GameObject("Body");
            _created.Add(body);
            body.transform.position = new Vector3(0f, 1f, 0f);
            CapsuleCollider capsule = body.AddComponent<CapsuleCollider>();
            capsule.height = 2f;
            capsule.radius = 0.5f;

            var chest = new GameObject("Chest");
            _created.Add(chest);
            chest.transform.position = new Vector3(0f, 1.4f, 0f);
            Physics.SyncTransforms();

            _character = new Entity();
            _character.AddDamageTakenEvent(new Event<TakeDamageArgs>());
            new VoxelBloodBehaviour(capsule, chest.transform, Config).Init(_character);
        }

        /// <summary>The defaults Regax ships with; the counts below follow them.</summary>
        private static readonly VoxelBloodConfig Config = new();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _created)
                if (go != null)
                    Object.Destroy(go);
            _created.Clear();
        }

        private IEntity Source(Vector3 at)
        {
            var go = new GameObject("Source");
            _created.Add(go);
            go.transform.position = at;
            IEntity source = new Entity();
            source.AddTransform(go.transform);
            source.AddDamage(new ReactiveInt(10));
            return source;
        }

        private static int Drops() => VoxelWorld.Instance.Debris.Count + VoxelWorld.Instance.Debris.LyingCount;

        private int SprayFrom(IEntity source)
        {
            int before = Drops();
            _character.GetDamageTakenEvent().Invoke(new TakeDamageArgs(source));
            return Drops() - before;
        }

        [Test]
        public void ABullet_SpraysItsRangeOfDrops()
        {
            IEntity bullet = Source(new Vector3(0f, 1f, -3f));
            bullet.AddRangeWeaponTag();
            Assert.That(SprayFrom(bullet), Is.InRange(Config.bulletDrops.x, Config.bulletDrops.y));
        }

        [Test]
        public void AClaw_SpraysItsRangeOfDrops()
        {
            IEntity claw = Source(new Vector3(0f, 1f, -1f));
            claw.AddMeleeWeaponTag();
            Assert.That(SprayFrom(claw), Is.InRange(Config.meleeDrops.x, Config.meleeDrops.y));
        }

        [Test]
        public void ABlast_SpraysItsDrops()
        {
            IEntity shell = Source(new Vector3(0f, 0f, -2f));
            shell.AddExplosionRadius(4f);
            Assert.AreEqual(Config.blastDrops, SprayFrom(shell));
        }

        [Test]
        public void Dispose_WithoutInit_DoesNotThrow()
        {
            var behaviour = new VoxelBloodBehaviour(null, null, new VoxelBloodConfig());
            Assert.DoesNotThrow(() => behaviour.Dispose(new Entity()));
        }

        [Test]
        public void NoSource_NoBlood()
        {
            int before = Drops();
            _character.GetDamageTakenEvent().Invoke(new TakeDamageArgs(null));
            Assert.AreEqual(before, Drops());
        }
    }
}
