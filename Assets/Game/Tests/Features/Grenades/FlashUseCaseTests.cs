using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// The grenade here carries no effect and no shake, so the flash needs neither the factory nor
    /// GameContext. Strength runs from 1 at the centre to 0.3 at the edge of the radius.
    /// </summary>
    public sealed class FlashUseCaseTests
    {
        private static readonly Vector3 Origin = new(-3200f, 800f, 3200f);
        private const float Radius = 8f;

        private readonly List<GameObject> _objects = new();
        private BlindBuffConfig _blind;

        [SetUp]
        public void SetUp() => _blind = ScriptableObject.CreateInstance<BlindBuffConfig>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject gameObject in _objects)
                if (gameObject != null)
                    Object.DestroyImmediate(gameObject);

            _objects.Clear();
            Object.DestroyImmediate(_blind);
        }

        private IEntity Grenade()
        {
            IEntity grenade = new Entity();
            grenade.AddFlashRadius(Radius);
            grenade.AddFlashBlindBuff(_blind);
            return grenade;
        }

        private MonoEntity Body(string name, Vector3 offset)
        {
            MonoEntity entity = MonoEntity.Create(new MonoEntity.CreateArgs { name = name });
            _objects.Add(entity.gameObject);
            entity.transform.position = Origin + offset;
            entity.gameObject.AddComponent<BoxCollider>();
            entity.AddDamageableTag();
            return entity;
        }

        private List<float> Player(Vector3 offset)
        {
            var strengths = new List<float>();
            var flashed = new Event<float>();
            flashed.OnEvent += strengths.Add;

            MonoEntity player = Body("Player", offset);
            player.AddPlayerTag();
            player.AddFlashedEvent(flashed);
            return strengths;
        }

        private MonoEntity Enemy(Vector3 offset, IEntity target)
        {
            MonoEntity enemy = Body("Enemy", offset);
            enemy.AddEnemyTag();
            enemy.AddBuffsEffects(new ReactiveList<BuffBase>());
            enemy.AddTarget(new ReactiveVariable<IEntity>(target));
            enemy.AddMoveDirection(new ReactiveVariable<Vector3>());
            enemy.AddRotateDirection(new ReactiveVariable<Vector3>());
            return enemy;
        }

        private void Wall(Vector3 offset)
        {
            GameObject wall = new("Wall");
            _objects.Add(wall);
            wall.transform.position = Origin + offset;
            wall.AddComponent<BoxCollider>().size = new Vector3(0.2f, 4f, 4f);
        }

        private static void Flash(IEntity grenade)
        {
            Physics.SyncTransforms();
            FlashUseCase.Flash(grenade, Origin, null);
        }

        [Test]
        public void IsFlash_OnlyWithAFlashRadius()
        {
            Assert.That(FlashUseCase.IsFlash(Grenade()), Is.True);
            Assert.That(FlashUseCase.IsFlash(new Entity()), Is.False);
        }

        [Test]
        public void PlayerInSight_IsDazzled_WeakerFurtherOut()
        {
            // Near face 2.5 m out of 8: 1 - 0.7 * 0.3125.
            List<float> strengths = Player(new Vector3(3f, 0f, 0f));

            Flash(Grenade());

            Assert.That(strengths, Has.Count.EqualTo(1));
            Assert.That(strengths[0], Is.EqualTo(0.78125f).Within(1e-3f));
        }

        [Test]
        public void PlayerAtTheCentre_GetsFullStrength()
        {
            List<float> strengths = Player(Vector3.zero);

            Flash(Grenade());

            Assert.That(strengths, Has.Count.EqualTo(1));
            Assert.That(strengths[0], Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void PlayerBehindAWall_IsShielded()
        {
            List<float> strengths = Player(new Vector3(3f, 0f, 0f));
            Wall(new Vector3(1.5f, 0f, 0f));

            Flash(Grenade());

            Assert.That(strengths, Is.Empty);
        }

        [Test]
        public void PlayerOutOfRange_IsLeftAlone()
        {
            List<float> strengths = Player(new Vector3(12f, 0f, 0f));

            Flash(Grenade());

            Assert.That(strengths, Is.Empty);
        }

        [Test]
        public void EnemyInSight_IsBlinded_AndLosesItsTarget()
        {
            MonoEntity enemy = Enemy(new Vector3(0f, 0f, 3f), new Entity());

            Flash(Grenade());

            Assert.That(enemy.GetBuffsEffects(), Has.Count.EqualTo(1));
            Assert.That(enemy.GetBuffsEffects(), Has.All.InstanceOf<BlindBuff>());
            Assert.That(enemy.GetTarget().Value, Is.Null);
        }

        [Test]
        public void EnemyBehindAWall_KeepsItsSight()
        {
            IEntity target = new Entity();
            MonoEntity enemy = Enemy(new Vector3(3f, 0f, 0f), target);
            Wall(new Vector3(1.5f, 0f, 0f));

            Flash(Grenade());

            Assert.That(enemy.GetBuffsEffects(), Is.Empty);
            Assert.That(enemy.GetTarget().Value, Is.SameAs(target));
        }

        [Test]
        public void FlashWithoutABlindBuff_EnemyIsUnaffected()
        {
            MonoEntity enemy = Enemy(new Vector3(0f, 0f, 3f), new Entity());
            IEntity grenade = new Entity();
            grenade.AddFlashRadius(Radius);

            Flash(grenade);

            Assert.That(enemy.GetBuffsEffects(), Is.Empty);
        }
    }
}
