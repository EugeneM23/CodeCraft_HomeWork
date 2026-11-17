using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// Stage 8 in the core: the movement arbiter (a stronger writer wins, the loser stops writing, an ability freezes
    /// both axes), the nine response curves at three points each and the two decorators, the random stream does not
    /// depend on the graph's shape, ability phases.
    /// </summary>
    public class CombatCoreTests
    {
        private const float DT = 0.02f;
        private const string TARGET = "Target";
        private const string READY_IN = "Ready In";

        // ---------- the arbiter ----------

        [Test]
        public void Arbiter_StrongerClaimWins_EqualOneDoesNot()
        {
            var arbiter = new MovementArbiter();
            object path = new(), strafe = new(), other = new();

            Assert.IsTrue(arbiter.TryClaim(path, BodyAxis.Move, MoveAuthority.Path));
            Assert.IsFalse(arbiter.TryClaim(other, BodyAxis.Move, MoveAuthority.Path), "Equal: the first keeps it");
            Assert.IsTrue(arbiter.TryClaim(strafe, BodyAxis.Move, MoveAuthority.Strafe), "Stronger: takes it");
            Assert.IsFalse(arbiter.IsValid(path, BodyAxis.Move), "The loser's claim is no longer valid");
            Assert.IsTrue(arbiter.IsValid(strafe, BodyAxis.Move));

            arbiter.Release(strafe);
            Assert.AreEqual(MoveAuthority.None, arbiter.AuthorityOf(BodyAxis.Move));
            Assert.IsNull(arbiter.OwnerOf(BodyAxis.Move));
        }

        [Test]
        public void Arbiter_FrozenTakesBothAxes()
        {
            var arbiter = new MovementArbiter();
            object mover = new(), ability = new();
            arbiter.TryClaim(mover, BodyAxis.Move, MoveAuthority.Path);
            arbiter.TryClaim(mover, BodyAxis.Face, MoveAuthority.Steering);

            Assert.IsTrue(arbiter.TryClaimBoth(ability, MoveAuthority.Frozen));

            Assert.IsFalse(arbiter.IsValid(mover, BodyAxis.Move));
            Assert.IsFalse(arbiter.IsValid(mover, BodyAxis.Face));
            Assert.IsFalse(arbiter.TryClaim(mover, BodyAxis.Move, MoveAuthority.Traversal), "Nothing beats Frozen");
        }

        [Test]
        public void Arbiter_TwoMoversInParallel_OnlyTheStrongerMovesTheBody()
        {
            var rig = new GraphRig();
            Transform target = rig.CreateTargetAt(new Vector3(10f, 0f, 0f));
            rig.Context.Blackboard.Set(TARGET, target);
            var path = new MoveToTask(target.position, 0.5f);                          // a path to the target
            var keep = new KiteTask { Target = TARGET, MinDistance = 12f, MaxDistance = 14f, NeedsSight = false };
            ((DirectNavigator)rig.Context.Navigation).MaxTurnSpeed = 0f;   // no turning time: only who writes matters
            float start = rig.Actuator.Position.x;

            // Both write the body every step, as two blocks that ran at once would; the kite is too close: it backs off
            for (int i = 0; i < 25; i++)
            {
                path.Tick(rig.Context, DT);
                keep.Tick(rig.Context, DT);
                rig.Actuator.Apply(DT);
            }

            Assert.Less(rig.Actuator.Position.x, start, "The strafe (stronger) backs off; the path does not win");
            Assert.AreSame(keep, rig.Context.Arbiter.OwnerOf(BodyAxis.Move));
            rig.DestroyTarget();
        }

        [Test]
        public void UseAbility_FreezesTheBodyWhileItPlays()
        {
            var rig = new GraphRig();
            rig.CreateTargetAt(new Vector3(1f, 0f, 0f));
            rig.AddAbility("Swing", duration: 0.5f, maxRange: 3f);
            var use = new UseAbilityTask(GraphRig.Named("Swing"));
            use.Tick(rig.Context, DT);

            Assert.AreSame(use, rig.Context.Arbiter.OwnerOf(BodyAxis.Move));
            Assert.AreEqual(MoveAuthority.Frozen, rig.Context.Arbiter.AuthorityOf(BodyAxis.Face));

            use.Interrupt(rig.Context);
            Assert.IsNull(rig.Context.Arbiter.OwnerOf(BodyAxis.Move), "Released on exit");
            rig.DestroyTarget();
        }

        // ---------- curves ----------

        private static readonly object[] Curves =
        {
            new object[] { ResponseCurve.Linear(), 0f, 0.5f, 1f },
            new object[] { ResponseCurve.Inverse(), 1f, 0.5f, 0f },
            new object[] { new ResponseCurve { Type = CurveType.Polynomial, M = 1f, K = 2f }, 0f, 0.25f, 1f },
            new object[] { ResponseCurve.Logistic(), 0.0067f, 0.5f, 0.9933f },
            new object[] { new ResponseCurve { Type = CurveType.Logit, M = 1f, K = 1f }, 0f, 0.5f, 1f },
            new object[] { ResponseCurve.Band(), 0.0821f, 1f, 0.0821f },
            new object[] { new ResponseCurve { Type = CurveType.Sine, M = 1f, K = 1f }, 0f, 1f, 0f },
            new object[] { ResponseCurve.Hyperbolic(), 1f, 1f / 6f, 1f / 11f },
            new object[] { ResponseCurve.Constant(0.3f), 0.3f, 0.3f, 0.3f }
        };

        [TestCaseSource(nameof(Curves))]
        public void Curve_ThreePoints(ResponseCurve curve, float at0, float atHalf, float at1)
        {
            Assert.AreEqual(at0, curve.Evaluate(0f), 0.01f, $"{curve.Type} at 0");
            Assert.AreEqual(atHalf, curve.Evaluate(0.5f), 0.01f, $"{curve.Type} at 0.5");
            Assert.AreEqual(at1, curve.Evaluate(1f), 0.01f, $"{curve.Type} at 1");
        }

        [Test]
        public void Curve_Custom_FollowsItsShape_EmptyShapeIsLinear()
        {
            ResponseCurve custom = ResponseCurve.Custom(AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

            Assert.AreEqual(1f, custom.Evaluate(0f), 0.01f);
            Assert.AreEqual(0.5f, custom.Evaluate(0.5f), 0.01f);
            Assert.AreEqual(0f, custom.Evaluate(1f), 0.01f);
            Assert.AreEqual(0.4f, new ResponseCurve { Type = CurveType.Custom }.Evaluate(0.4f), 1e-5f);
        }

        [Test]
        public void Decorators_InvertAndMultiply()
        {
            var plain = new Consideration { Input = ConsiderationInput.Constant, Constant = 0.25f };
            var inverted = new Consideration { Input = ConsiderationInput.Constant, Constant = 0.25f, InvertOutput = true };
            var doubled = new Consideration { Input = ConsiderationInput.Constant, Constant = 0.25f, Multiply = 2f };
            var capped = new Consideration { Input = ConsiderationInput.Constant, Constant = 0.75f, Multiply = 2f };

            Assert.AreEqual(0.25f, plain.Evaluate(null), 1e-5f);
            Assert.AreEqual(0.75f, inverted.Evaluate(null), 1e-5f);
            Assert.AreEqual(0.5f, doubled.Evaluate(null), 1e-5f);
            Assert.AreEqual(1f, capped.Evaluate(null), 1e-5f, "Kept within 0..1");
        }

        // ---------- the random stream does not depend on the graph's shape ----------

        // A fighter: Use Any Ability takes whatever ability the roulette picks when it is taken (a draw from the agent's
        // random), its chain uses it and pauses. Extra — two more decisions, less important, that the Brain asks on every
        // choice whether they hold (are A and B ready for the target); they never win: their graph timer never runs out
        private static List<string> TraceOf(bool extraDecisions)
        {
            var rig = new GraphRig(seed: 31);
            rig.CreateTargetAt(new Vector3(1f, 0f, 0f));
            rig.AddAbility("A", duration: 0.1f, cooldown: 0.2f, maxRange: 3f, weight: 1f);
            rig.AddAbility("B", duration: 0.1f, cooldown: 0.2f, maxRange: 3f, weight: 2f);
            rig.Abilities.Strategy = new RandomWeightedSelection();

            var g = new BrainGraphBuilder();
            g.Sensor(new SensesSensorTask { StoreIn = TARGET });
            g.BrainTask.Target = TARGET;
            g.Variable(TARGET, VariableType.Transform);
            AgentState fight = g.Decision(new UseAnyAbilityReasoner(GraphRig.Named("A"), GraphRig.Named("B")));
            AgentState use = g.Block(new UseAbilityTask());
            g.Do(fight, use);
            g.OnEnd(use, g.Block(new WaitTask(0.25f)));
            if (extraDecisions)
            {
                g.Variable(READY_IN, VariableType.Float);
                foreach (string ability in new[] { "A", "B" })
                {
                    AgentState ready = g.Decision(new UseAbilityReasoner
                    {
                        Ability = GraphRig.Named(ability), Condition = new CompareCondition(READY_IN, NumberComparison.LessOrEqual, 0f)
                    });
                    g.Chain(ready, new WaitTask(0.25f));
                }

                rig.Agent.Blackboard.Set(READY_IN, 1000f);
            }

            rig.SetGraph(g.Build());
            var used = new List<string>();
            for (int i = 0; i < 300; i++)
            {
                rig.Step(DT);
                if (rig.Abilities.LastUsed != null && (used.Count == 0 || rig.Abilities.IsAnyRunning))
                    used.Add(rig.Abilities.LastUsed);
            }

            rig.DestroyTarget();
            return used;
        }

        [Test]
        public void RandomStream_SameWithExtraDecisions()
        {
            List<string> plain = TraceOf(false);

            Assert.IsNotEmpty(plain, "The fighter used its abilities");
            CollectionAssert.AreEqual(plain, TraceOf(true), "Asking a decision whether it holds draws nothing from the stream");
        }

        // ---------- phases ----------

        [Test]
        public void Phases_TimedRunner_WindUpActiveRecovery()
        {
            var rig = new GraphRig();
            rig.CreateTargetAt(new Vector3(1f, 0f, 0f));
            var definition = new AbilityDefinition
            {
                Id = "Slam", WindUp = 0.2f, Recovery = 0.3f, RequiresGround = false, MaxRange = 3f
            };
            rig.Abilities.Add(definition);
            var runner = new TimedAbilityRunner(definition, activeTime: 0.1f);
            rig.Abilities.Register(runner);

            Assert.IsTrue(rig.Abilities.TryUse("Slam", rig.Context));
            Assert.AreEqual(AbilityPhase.WindUp, rig.Abilities.RunningPhase);
            rig.Tick(0.25f);
            Assert.AreEqual(AbilityPhase.Active, rig.Abilities.PhaseOf("Slam"));
            rig.Tick(0.1f);
            Assert.AreEqual(AbilityPhase.Recovery, rig.Abilities.RunningPhase);
            rig.Tick(0.3f);
            Assert.AreEqual(AbilityPhase.None, rig.Abilities.RunningPhase);
            Assert.IsFalse(runner.IsRunning);
            rig.DestroyTarget();
        }

        [Test]
        public void Phases_ARunnerThatDoesNotKnow_IsActiveWhileRunning()
        {
            var runner = new TestAbilityRunner("Swing", 1f);
            IAbilityRunner contract = runner;

            Assert.AreEqual(AbilityPhase.None, contract.Phase);
            runner.Begin(null);
            Assert.AreEqual(AbilityPhase.Active, contract.Phase);
        }
    }
}
