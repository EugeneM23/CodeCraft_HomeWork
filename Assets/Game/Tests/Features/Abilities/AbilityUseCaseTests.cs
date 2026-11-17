using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class AbilityUseCaseTests
    {
        private static Ability BaseAbility(bool allowed, List<string> log)
        {
            var ability = new Ability("Base");
            ability.AddBaseAbilityTag();
            ability.AddBaseCondition(new InlineFunction<bool>(() => allowed));
            ability.AddBaseAction(new InlineAction(() => log.Add("action")));
            var used = new Atomic.Elements.Event();
            used.OnEvent += () => log.Add("event");
            ability.AddBaseEvent(used);
            return ability;
        }

        [Test]
        public void UseBase_ConditionHolds_RaisesEventThenRunsAction()
        {
            var log = new List<string>();

            AbilityUseCase.Use(BaseAbility(true, log));

            Assert.That(log, Is.EqualTo(new[] { "event", "action" }));
        }

        [Test]
        public void UseBase_ConditionFails_DoesNothing()
        {
            var log = new List<string>();

            AbilityUseCase.Use(BaseAbility(false, log));

            Assert.That(log, Is.Empty);
        }

        [Test]
        public void UseBase_WithoutBaseTag_DoesNothing()
        {
            var log = new List<string>();
            Ability ability = BaseAbility(true, log);
            ability.DelBaseAbilityTag();

            AbilityUseCase.Use(ability);

            Assert.That(log, Is.Empty);
        }

        [Test]
        public void UsePoint_PassesThePointToConditionEventAndAction()
        {
            var seen = new List<Vector3>();
            var point = new Vector3(1f, 2f, 3f);
            var ability = new Ability("Point");
            ability.AddPointAbilityTag();
            ability.AddPointCondition(new InlineFunction<Vector3, bool>(p => { seen.Add(p); return true; }));
            ability.AddPointAction(new InlineAction<Vector3>(p => seen.Add(p)));
            var used = new Event<Vector3>();
            used.OnEvent += p => seen.Add(p);
            ability.AddPointEvent(used);

            AbilityUseCase.Use(ability, point);

            Assert.That(seen, Is.EqualTo(new[] { point, point, point }));
        }

        [Test]
        public void UsePoint_ConditionRejectsThePoint_ActionNotRun()
        {
            bool acted = false;
            var ability = new Ability("Point");
            ability.AddPointAbilityTag();
            ability.AddPointCondition(new InlineFunction<Vector3, bool>(p => p.x < 5f));
            ability.AddPointAction(new InlineAction<Vector3>(_ => acted = true));
            ability.AddPointEvent(new Event<Vector3>());

            AbilityUseCase.Use(ability, new Vector3(10f, 0f, 0f));

            Assert.That(acted, Is.False);
        }

        [Test]
        public void UsePoint_OnABaseAbility_DoesNothing()
        {
            var log = new List<string>();

            Assert.DoesNotThrow(() => AbilityUseCase.Use(BaseAbility(true, log), Vector3.zero));
            Assert.That(log, Is.Empty);
        }

        [Test]
        public void UseTarget_ConditionHolds_ActsOnThatTarget()
        {
            IEntity target = new Entity();
            IEntity acted = null;
            var ability = new Ability("Target");
            ability.AddTargetAbilityTag();
            ability.AddTargetCondition(new InlineFunction<IEntity, bool>(t => t != null));
            ability.AddTargetAction(new InlineAction<IEntity>(t => acted = t));
            ability.AddTargetEvent(new Event<IEntity>());

            AbilityUseCase.Use(ability, target);

            Assert.That(acted, Is.SameAs(target));
        }

        [Test]
        public void UseTarget_ConditionFails_NeitherEventNorAction()
        {
            int calls = 0;
            var ability = new Ability("Target");
            ability.AddTargetAbilityTag();
            ability.AddTargetCondition(new InlineFunction<IEntity, bool>(t => t != null));
            ability.AddTargetAction(new InlineAction<IEntity>(_ => calls++));
            var used = new Event<IEntity>();
            used.OnEvent += _ => calls++;
            ability.AddTargetEvent(used);

            AbilityUseCase.Use(ability, (IEntity)null);

            Assert.That(calls, Is.Zero);
        }
    }
}
