using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class DiscardAllBuffBehaviourTests
    {
        private sealed class SpyBuff : BuffBase
        {
            public bool Discarded { get; private set; }

            // DiscardAll never reads the config, and a ScriptableObject is not needed to prove it.
            public SpyBuff() : base(null) { }

            public override bool Apply(IEntity entity) => true;

            public override void Discard(IEntity entity) => this.Discarded = true;
        }

        /// <summary>
        /// The order every character prefab has: the core installer's DeathBehaviour handles
        /// OnHealthEmpty first — it raises the death event, then disposes the entity — and
        /// CharacterBuffInstaller, which adds DiscardAllBuffBehaviour, comes later.
        /// </summary>
        private static IEntity CreateCharacter(out Health health)
        {
            Health hp = new Health(10, 10);
            IEntity character = new Entity();
            character.AddHealth(hp);
            character.AddDeathTakenEvent(new Event<TakeDamageArgs>());
            character.AddBuffsEffects(new ReactiveList<BuffBase>());

            hp.OnHealthEmpty += () =>
            {
                character.GetDeathTakenEvent().Invoke(new TakeDamageArgs(character));
                character.Dispose();
            };

            character.AddBehaviour<DiscardAllBuffBehaviour>();
            character.Init();

            health = hp;
            return character;
        }

        [Test]
        public void Death_DiscardsBuffs_ThoughTheDeathHandlerDisposesTheEntityFirst()
        {
            IEntity character = CreateCharacter(out Health health);
            var buff = new SpyBuff();
            character.GetBuffsEffects().Add(buff);

            health.Reduce(10);

            Assert.That(buff.Discarded, Is.True);
        }

        [Test]
        public void DamageThatDoesNotKill_KeepsBuffs()
        {
            IEntity character = CreateCharacter(out Health health);
            var buff = new SpyBuff();
            character.GetBuffsEffects().Add(buff);

            health.Reduce(5);

            Assert.That(buff.Discarded, Is.False);
        }
    }
}
