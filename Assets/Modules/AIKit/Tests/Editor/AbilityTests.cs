using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    public class AbilityTests
    {
        private GraphRig _rig;

        [SetUp]
        public void SetUp()
        {
            _rig = new GraphRig();
            _rig.CreateTargetAt(new Vector3(1f, 0f, 0f));
        }

        [TearDown]
        public void TearDown() => _rig.DestroyTarget();

        [Test]
        public void Ability_OutOfRange_IsNotAvailable()
        {
            _rig.AddAbility("Melee", maxRange: 2f);

            Assert.IsTrue(_rig.Abilities.IsReady("Melee", _rig.Context));

            _rig.MoveTargetTo(new Vector3(6f, 0f, 0f));

            Assert.IsFalse(_rig.Abilities.IsReady("Melee", _rig.Context));
        }

        [Test]
        public void Ability_WithoutTarget_IsNotAvailable()
        {
            _rig.AddAbility("Melee");
            _rig.DestroyTarget();

            Assert.IsFalse(_rig.Abilities.IsReady("Melee", _rig.Context));
        }

        [Test]
        public void Cooldown_CountsFromTheEndOfTheAbility()
        {
            TestAbilityRunner runner = _rig.AddAbility("Swing", duration: 0.3f, cooldown: 0.5f);

            Assert.IsTrue(_rig.Abilities.TryUse("Swing", _rig.Context));

            _rig.Tick(0.3f);                           // the swing is over, the pause starts now
            Assert.IsFalse(_rig.Abilities.IsReady("Swing", _rig.Context), "The pause starts when the ability ends");

            _rig.Tick(0.4f);
            Assert.IsFalse(_rig.Abilities.IsReady("Swing", _rig.Context));

            _rig.Tick(0.2f);
            Assert.IsTrue(_rig.Abilities.IsReady("Swing", _rig.Context));
            Assert.AreEqual(1, runner.Starts);
        }

        [Test]
        public void StartDelay_KeepsTheAbilityQuietAtFirst()
        {
            _rig.Abilities.Add(new AbilityDefinition
            {
                Id = "Slow",
                StartDelay = 0.5f,
                MaxRange = 5f,
                RequiresGround = false
            });

            Assert.IsFalse(_rig.Abilities.IsReady("Slow", _rig.Context));

            _rig.Tick(0.6f);

            Assert.IsTrue(_rig.Abilities.IsReady("Slow", _rig.Context));
        }

        [Test]
        public void Select_PicksTheHeavierOption()
        {
            _rig.AddAbility("Weak", cooldown: 0f, maxRange: 5f, weight: 1f);
            _rig.AddAbility("Strong", cooldown: 0f, maxRange: 5f, weight: 50f);

            int strong = 0;
            for (int i = 0; i < 40; i++)
            {
                if (_rig.Abilities.Select(_rig.Context) == "Strong")
                    strong++;
            }

            Assert.Greater(strong, 25, "The heavier option must come up far more often");
        }

        [Test]
        public void Select_SkipsWhatIsOutOfRangeOrOnCooldown()
        {
            _rig.AddAbility("Melee", cooldown: 10f, maxRange: 2f);
            _rig.AddAbility("Ranged", cooldown: 0f, maxRange: 20f);

            _rig.Abilities.TryUse("Melee", _rig.Context);
            _rig.Tick(0.02f);

            Assert.AreEqual("Ranged", _rig.Abilities.Select(_rig.Context));
        }

        [Test]
        public void Select_AntiRepeat_PullsTheLastUsedDown()
        {
            _rig.Abilities.Add(new AbilityDefinition
            {
                Id = "A",
                Cooldown = 0f,
                MaxRange = 5f,
                Weight = 1f,
                RepeatPenalty = 1f,
                RequiresGround = false
            });

            _rig.Abilities.Add(new AbilityDefinition
            {
                Id = "B",
                Cooldown = 0f,
                MaxRange = 5f,
                Weight = 1f,
                RequiresGround = false
            });

            _rig.Abilities.TryUse("A", _rig.Context);

            Assert.AreEqual("B", _rig.Abilities.Select(_rig.Context),
                "A full repeat penalty takes the last used ability out of the choice");
        }

        [Test]
        public void Select_SameSeed_GivesTheSameChoices()
        {
            string First() => Choices(new GraphRig(777));
            Assert.AreEqual(First(), First());
        }

        [Test]
        public void UseAbilityTask_PlaysAndWaitsForTheEnd()
        {
            TestAbilityRunner runner = _rig.AddAbility("Swing", duration: 0.2f, maxRange: 5f);
            var task = new UseAbilityTask(GraphRig.Named("Swing"));

            Assert.AreEqual(TaskStatus.Running, task.Tick(_rig.Context, 0.02f));
            Assert.AreEqual(1, runner.Starts);

            _rig.Tick(0.25f);

            Assert.AreEqual(TaskStatus.Success, task.Tick(_rig.Context, 0.02f));
        }

        [Test]
        public void UseAbilityTask_NoAbilityOfItsOwn_NoDecisionsAbility_Fails()
        {
            _rig.AddAbility("Swing", duration: 0.2f, maxRange: 5f);
            var task = new UseAbilityTask();

            Assert.AreEqual(TaskStatus.Failure, task.Tick(_rig.Context, 0.02f), "It chooses nothing itself (step 90)");
        }

        [Test]
        public void UseAbilityTask_Interrupted_CancelsTheAbility()
        {
            TestAbilityRunner runner = _rig.AddAbility("Swing", duration: 1f, maxRange: 5f);
            var task = new UseAbilityTask(GraphRig.Named("Swing"));

            task.Tick(_rig.Context, 0.02f);
            task.Interrupt(_rig.Context);

            Assert.AreEqual(1, runner.Cancels);
        }

        private static string Choices(GraphRig rig)
        {
            rig.CreateTargetAt(new Vector3(1f, 0f, 0f));
            rig.AddAbility("A", cooldown: 0f, maxRange: 5f);
            rig.AddAbility("B", cooldown: 0f, maxRange: 5f);
            rig.AddAbility("C", cooldown: 0f, maxRange: 5f);

            string result = string.Empty;
            for (int i = 0; i < 10; i++)
                result += rig.Abilities.Select(rig.Context) + ",";

            rig.DestroyTarget();
            return result;
        }
    }
}
