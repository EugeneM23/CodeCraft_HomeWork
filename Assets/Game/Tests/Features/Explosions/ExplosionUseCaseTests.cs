using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// The damage pass of an explosion. The projectile here carries no bomb, breach, effects or
    /// shake, so nothing reaches for GameContext or the factory.
    /// </summary>
    public sealed class ExplosionUseCaseTests
    {
        private static readonly Vector3 Origin = new(-3100f, 800f, 3100f);
        private const int Damage = 100;

        private readonly List<GameObject> _objects = new();
        private bool _areaWas;

        /// <summary>The damage pass is the shell's area damage: on for these tests (it is off in the game for now).</summary>
        [SetUp]
        public void SetUp()
        {
            _areaWas = CombatConfig.Load().Weapons.shellAreaDamage;
            CombatConfig.Load().Weapons.shellAreaDamage = true;
        }

        [TearDown]
        public void TearDown()
        {
            CombatConfig.Load().Weapons.shellAreaDamage = _areaWas;
            foreach (GameObject gameObject in _objects)
                if (gameObject != null)
                    Object.DestroyImmediate(gameObject);

            _objects.Clear();
        }

        private static IEntity Shell(float? minDamageFactor = 0.25f)
        {
            IEntity shell = new Entity();
            shell.AddDamage(new ReactiveInt(Damage));
            shell.AddExplosionRadius(4f);
            if (minDamageFactor.HasValue)
                shell.AddExplosionMinDamageFactor(minDamageFactor.Value);
            return shell;
        }

        private Health Target(Vector3 offset, List<TakeDamageArgs> hits = null)
        {
            MonoEntity target = MonoEntity.Create(new MonoEntity.CreateArgs { name = "Target" });
            _objects.Add(target.gameObject);
            target.transform.position = Origin + offset;
            target.gameObject.AddComponent<BoxCollider>();

            var health = new Health(1000, 1000);
            var damageTaken = new Event<TakeDamageArgs>();
            if (hits != null)
                damageTaken.OnEvent += hits.Add;
            target.AddDamageableTag();
            target.AddHealth(health);
            target.AddDamageTakenEvent(damageTaken);
            return health;
        }

        private static void Explode(IEntity shell)
        {
            Physics.SyncTransforms();
            ExplosionUseCase.Explode(shell, Origin, null, null);
        }

        [Test]
        public void Explode_RaisesShellExploded_BeforeExplodedAtTheSamePoint()
        {
            var order = new List<string>();
            Vector3 shellPoint = Vector3.zero;
            System.Action<Vector3> onShell = point => { order.Add("shell"); shellPoint = point; };
            System.Action<Vector3, float> onBlast = (_, _) => order.Add("blast");
            GameSignals.ShellExploded += onShell;
            ExplosionEvents.Exploded += onBlast;
            try
            {
                Explode(Shell());
            }
            finally
            {
                GameSignals.ShellExploded -= onShell;
                ExplosionEvents.Exploded -= onBlast;
            }

            Assert.That(order, Is.EqualTo(new[] { "shell", "blast" }));
            Assert.That(shellPoint, Is.EqualTo(Origin));
        }

        [Test]
        public void IsExplosive_OnlyWithARadius()
        {
            Assert.That(ExplosionUseCase.IsExplosive(Shell()), Is.True);
            Assert.That(ExplosionUseCase.IsExplosive(new Entity()), Is.False);
        }

        [Test]
        public void TargetAtTheCentre_TakesFullDamage()
        {
            Health health = Target(Vector3.zero);

            Explode(Shell());

            Assert.That(health.GetCurrent(), Is.EqualTo(1000 - Damage));
        }

        [Test]
        public void TargetFurtherOut_TakesLess()
        {
            // Near face 2.5 m out of 4: 1 - 0.75 * 0.625 = 0.53125 of the damage, rounded.
            Health health = Target(new Vector3(3f, 0f, 0f));

            Explode(Shell());

            Assert.That(health.GetCurrent(), Is.EqualTo(1000 - 53));
        }

        [Test]
        public void NoMinimumFactorSet_FallsOffToAQuarter()
        {
            Health withDefault = Target(new Vector3(3f, 0f, 0f));

            Explode(Shell(minDamageFactor: null));

            Assert.That(withDefault.GetCurrent(), Is.EqualTo(1000 - 53));
        }

        [Test]
        public void AreaDamageOff_TheBlastHurtsNoOne()
        {
            CombatConfig.Load().Weapons.shellAreaDamage = false;
            var hits = new List<TakeDamageArgs>();
            Health health = Target(Vector3.zero, hits);

            Explode(Shell());

            Assert.That(health.GetCurrent(), Is.EqualTo(1000), "only the enemy a shell hits takes its damage (BulletCollisionBehaviour)");
            Assert.That(hits, Is.Empty);
        }

        [Test]
        public void TargetOutOfRange_IsUntouched()
        {
            var hits = new List<TakeDamageArgs>();
            Health health = Target(new Vector3(8f, 0f, 0f), hits);

            Explode(Shell());

            Assert.That(health.GetCurrent(), Is.EqualTo(1000));
            Assert.That(hits, Is.Empty);
        }

        [Test]
        public void Hit_NamesTheShellAsItsSource()
        {
            var hits = new List<TakeDamageArgs>();
            Target(Vector3.zero, hits);
            IEntity shell = Shell();

            Explode(shell);

            Assert.That(hits, Has.Count.EqualTo(1));
            Assert.That(hits[0].source, Is.SameAs(shell));
        }
    }
}
