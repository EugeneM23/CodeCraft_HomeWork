using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// Stage 8: the attack tokens of a target. Up to the capacity, one per holder, released and taken again; requests
    /// of one window ranked by priority, reach and distance — never by who asked first; out of reach refused; a
    /// started wind-up never preempted; a resting pool after it empties; the tokens of the gone come back.
    /// </summary>
    public class AttackTokensTests
    {
        private const string POOL = "Melee";
        private const float DT = 0.02f;

        private sealed class Capacity : MonoBehaviour, ITokenCapacity
        {
            public int Value = 1;

            public int CapacityOf(string pool) => Value;
        }

        private readonly List<GraphRig> _rigs = new();
        private readonly List<Object> _created = new();
        private AgentRoster _roster;
        private Transform _target;
        private Capacity _capacity;

        [SetUp]
        public void SetUp()
        {
            _roster = new AgentRoster();
            var target = new GameObject("Target") { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(target);
            _target = target.transform;
            _capacity = target.AddComponent<Capacity>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GraphRig rig in _rigs)
                Object.DestroyImmediate(rig.Agent.gameObject);
            foreach (Object created in _created)
                Object.DestroyImmediate(created);
            _rigs.Clear();
            _created.Clear();
        }

        /// <summary>An awake agent in the roster (the rig's agent object is woken up so the roster counts it).</summary>
        private AICharacter Agent()
        {
            var rig = new GraphRig(trace: false);
            _rigs.Add(rig);
            rig.Agent.gameObject.SetActive(true);
            _roster.Add(rig.Agent);
            return rig.Agent;
        }

        private AttackTokens Tokens => _roster.EngagementOf(_target).Tokens;

        private TokenResult Ask(AICharacter agent, float distance = 1f, float priority = 0f, int cost = 1) =>
            Tokens.Request(agent, POOL, cost, priority, distance);

        // ---------- capacity ----------

        [Test]
        public void UpToCapacity_ThenWaiting()
        {
            AICharacter a = Agent(), b = Agent();

            Assert.AreEqual(TokenResult.Pending, Ask(a));
            Assert.AreEqual(TokenResult.Pending, Ask(b));
            _roster.Step(DT);

            Assert.AreEqual(1, Tokens.Used(POOL));
            Assert.AreEqual(TokenResult.Granted, Ask(a), "The first in the roster's order of an equal batch");
            Assert.AreEqual(TokenResult.Pending, Ask(b), "Full: waits");
        }

        [Test]
        public void SameAgentAskingAgain_HoldsOneToken()
        {
            AICharacter a = Agent();
            Ask(a);
            _roster.Step(DT);

            Ask(a);
            Ask(a);
            _roster.Step(DT);

            Assert.AreEqual(1, Tokens.Used(POOL));
            Assert.IsTrue(Tokens.Holds(a, POOL));
        }

        [Test]
        public void Release_FreesTheToken_ForTheNext()
        {
            AICharacter a = Agent(), b = Agent();
            Ask(a);
            Ask(b);
            _roster.Step(DT);

            Tokens.Release(a, POOL);
            Ask(b);
            _roster.Step(DT);

            Assert.IsFalse(Tokens.Holds(a, POOL));
            Assert.IsTrue(Tokens.Holds(b, POOL));
        }

        [Test]
        public void CouldWin_HolderAlways_OthersNotWhenFull()
        {
            AICharacter a = Agent(), b = Agent();
            Ask(a);
            _roster.Step(DT);

            Assert.IsTrue(Tokens.CouldWin(a, POOL));
            Assert.IsFalse(Tokens.CouldWin(b, POOL));
        }

        [Test]
        public void CapacityTwo_AllowsTwo_FromTheTarget()
        {
            _capacity.Value = 2;
            AICharacter a = Agent(), b = Agent(), c = Agent();
            Ask(a);
            Ask(b);
            Ask(c);

            _roster.Step(DT);

            Assert.AreEqual(2, Tokens.Used(POOL));
            Assert.IsFalse(Tokens.Holds(c, POOL));
        }

        [Test]
        public void GoneHolder_TokenComesBack()
        {
            AICharacter a = Agent(), b = Agent();
            Ask(a);
            Ask(b);
            _roster.Step(DT);

            a.enabled = false;                                      // switched off (or dead, or destroyed)
            Ask(b);
            _roster.Step(DT);
            _roster.Step(DT);

            Assert.IsTrue(Tokens.Holds(b, POOL));
        }

        [Test]
        public void NoAgent_OrNoPool_Refused()
        {
            Assert.AreEqual(TokenResult.Refused, Tokens.Request(null, POOL, 1, 0f, 1f));
            Assert.AreEqual(TokenResult.Refused, Tokens.Request(Agent(), "", 1, 0f, 1f));
        }

        // ---------- ranking ----------

        [Test]
        public void OneWindow_TheOneInReachFirst_ThenTheNearer_NotWhoAskedFirst()
        {
            Tokens.CloseRange = 1.5f;
            AICharacter far = Agent(), near = Agent(), inReach = Agent();
            Ask(far, distance: 5f, priority: 3f);
            Ask(near, distance: 3f, priority: 3f);
            Ask(inReach, distance: 1f, priority: 0f);              // asked last, the lowest priority, but in reach

            _roster.Step(DT);
            Assert.IsTrue(Tokens.Holds(inReach, POOL), "In reach goes first");

            Tokens.Release(inReach, POOL);
            Ask(far, distance: 5f, priority: 3f);
            Ask(near, distance: 3f, priority: 3f);
            _roster.Step(DT);
            Assert.IsTrue(Tokens.Holds(near, POOL), "Equal priority: the nearer");
        }

        [Test]
        public void MaturationWindow_RequestsOfTheWindowCompete()
        {
            Tokens.MaturationWindow = 0.1f;
            AICharacter early = Agent(), better = Agent();
            Ask(early, distance: 4f);
            _roster.Step(DT);                                        // the early one is not granted yet
            Ask(early, distance: 4f);
            Ask(better, distance: 2f);

            for (int i = 0; i < 6; i++)
            {
                Ask(early, distance: 4f);
                Ask(better, distance: 2f);
                _roster.Step(DT);
            }

            Assert.IsTrue(Tokens.Holds(better, POOL), "Asked later, nearer: wins the window");
            Assert.IsFalse(Tokens.Holds(early, POOL));
        }

        [Test]
        public void OutOfReach_RefusedAtOnce()
        {
            Tokens.WinRadius = 6f;

            Assert.AreEqual(TokenResult.Refused, Ask(Agent(), distance: 9f));
        }

        // ---------- preemption and rest ----------

        [Test]
        public void BetterRequest_TakesTheTokenFromAWalker_NeverFromAStartedSwing()
        {
            AICharacter walker = Agent(), better = Agent();
            Ask(walker, distance: 4f);
            _roster.Step(DT);
            Assert.IsTrue(Tokens.Holds(walker, POOL));

            Ask(better, distance: 4f, priority: 5f);
            _roster.Step(DT);
            Assert.IsTrue(Tokens.Holds(better, POOL), "Only walking up: the token goes to the better one");
            Assert.IsFalse(Tokens.Holds(walker, POOL));

            // The new holder starts its swing: nobody takes the token now
            var runner = new TestAbilityRunner("Swing", duration: 1f);
            better.Abilities.Add(new AbilityDefinition { Id = "Swing", RequiresTarget = false, RequiresGround = false });
            better.Abilities.Register(runner);
            Assert.IsTrue(better.Abilities.TryUse("Swing", better.Context));
            Ask(walker, distance: 1f, priority: 50f);
            _roster.Step(DT);

            Assert.IsTrue(Tokens.Holds(better, POOL), "A started wind-up runs to the end");
        }

        [Test]
        public void EmptiedPool_RestsBeforeTheNext()
        {
            Tokens.RestAfterEmpty = 0.1f;
            AICharacter a = Agent(), b = Agent();
            Ask(a);
            _roster.Step(DT);
            Tokens.Release(a, POOL);

            Ask(b);
            _roster.Step(DT);
            Assert.IsFalse(Tokens.Holds(b, POOL), "The pool rests");
            Assert.IsTrue(Tokens.IsResting(POOL));

            for (int i = 0; i < 6; i++)
            {
                Ask(b);
                _roster.Step(DT);
            }

            Assert.IsTrue(Tokens.Holds(b, POOL), "Then the next gets in");
        }

        [Test]
        public void DeadTarget_EndsTheEngagement()
        {
            AICharacter a = Agent();
            Ask(a);
            _roster.Step(DT);

            _target.gameObject.SetActive(false);
            _roster.Step(DT);

            CollectionAssert.IsEmpty(_roster.Engagements);
        }

        [Test]
        public void Roster_RemovedAgent_LetsGoOfEverything()
        {
            AICharacter a = Agent();
            Ask(a);
            _roster.Step(DT);

            _roster.Remove(a);

            Assert.AreEqual(0, Tokens.Used(POOL));
            Assert.AreEqual(-1, _roster.OrderOf(a));
        }

        // ---------- the roster as allies ----------

        [Test]
        public void Roster_AllyAhead_ByPositions_NotByPhysics()
        {
            AICharacter self = Agent(), ally = Agent();
            ((TestActuator)self.Context.Actuator).Position = Vector3.zero;
            ((TestActuator)ally.Context.Actuator).Position = new Vector3(1f, 0f, 0f);
            IAllyProbe probe = _roster.ProbeFor(self);

            Assert.IsTrue(probe.AllyAhead(Vector3.zero, Vector3.right, 1.5f));
            Assert.IsFalse(probe.AllyAhead(Vector3.zero, Vector3.left, 1.5f), "Behind is not ahead");
            Assert.IsFalse(_roster.ProbeFor(ally).AllyAhead(new Vector3(1f, 0f, 0f), Vector3.right, 1.5f),
                "Itself is not its ally");
        }

        [Test]
        public void RoleAssignment_AThirdAtLeastOne()
        {
            Assert.AreEqual(1, RoleAssignment.AttackersFor(1));
            Assert.AreEqual(1, RoleAssignment.AttackersFor(3));
            Assert.AreEqual(2, RoleAssignment.AttackersFor(4));
        }
    }
}
