using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace AIKit.Tests.SensesCoverage
{
    /// <summary>
    /// Get's relations (step-90 recheck): the angles are measured where the agent looks — a body in 3D along its
    /// Forward over the ground, a side-view body along its facing in the plane of the view (the Senses' rule,
    /// AgentPerception.LookOf) — not along the transform's forward on an XZ ground; no angle to measure is no data; the
    /// health of another AI character; Debug Log of no data; and thinks that allocate nothing.
    /// </summary>
    public sealed class SensesGetTests
    {
        private const string TARGET = "Target";

        private readonly Things _things = new();

        [TearDown]
        public void TearDown()
        {
            _things.DestroyAll();
            GraphRig.DestroyAll();
        }

        [Test]
        public void Get_ASideViewBody_MeasuresAnglesAlongItsFacing()
        {
            AICharacter me = SideViewCharacter(Vector3.right);
            var foe = new GraphRig(trace: false);                  // a side-view character at +X looking back at me
            foe.Actuator.Position = new Vector3(5f, 0f, 0f);
            foe.Agent.transform.position = foe.Actuator.Position;
            foe.Actuator.Face(Vector3.left);
            me.Blackboard.Set(TARGET, foe.Agent.transform);

            Assert.IsTrue(new GetValue(GetParameter.AngleFromMyFacing, TARGET).TryGetNumber(me.Context, out float ahead));
            Assert.AreEqual(0f, ahead, 1e-3f, "Straight ahead of where the side-view body looks");
            Assert.IsTrue(new GetValue(GetParameter.FacesMe, TARGET).TryGetNumber(me.Context, out float facing));
            Assert.AreEqual(0f, facing, 1e-3f, "It looks straight at me (its own facing, not its transform's forward)");

            Transform above = _things.Make("Above", new Vector3(0f, 6f, 0f));
            me.Blackboard.Set(TARGET, above);
            Assert.IsTrue(new GetValue(GetParameter.AngleFromMyFacing, TARGET).TryGetNumber(me.Context, out float up));
            Assert.AreEqual(90f, up, 1e-3f, "Straight above: a quarter turn off the facing, in the plane of the view");
        }

        [Test]
        public void Get_TheHealthOfAnotherAICharacter_IsItsGamesHealth()
        {
            var me = new GraphRig(trace: false);
            var other = new GraphRig(trace: false);
            StatusTests.Health health = other.Agent.gameObject.AddComponent<StatusTests.Health>();
            health.Current = 3f;
            health.Max = 4f;
            me.Agent.Blackboard.Set(TARGET, other.Agent.transform);

            Assert.IsTrue(new GetValue(GetParameter.Health, TARGET).TryGetNumber(me.Context, out float current));
            Assert.AreEqual(3f, current, "Its game's health, through the character's own say");
            Assert.IsTrue(new GetValue(GetParameter.HealthPercent, TARGET).TryGetNumber(me.Context, out float percent));
            Assert.AreEqual(75f, percent, 1e-4f);
            var dead = new IsCondition(CharacterState.Dead, TARGET);
            Assert.AreEqual(Truth.No, dead.Ask(me.Context));

            health.Current = 0f;
            Assert.AreEqual(Truth.Yes, dead.Ask(me.Context), "At 0 it is dead (asked again: read live)");
        }

        [Test]
        public void Get_AnglesOfAnObjectStraightAboveOrLookingStraightUp_NoData()
        {
            var me = new GraphRig(trace: false);
            me.Actuator.LooksIn3D = true;                           // a body in 3D looking along +Z: angles over the ground
            me.Actuator.Face(Vector3.forward);
            Transform above = _things.Make("Above", new Vector3(0f, 5f, 0f));
            me.Agent.Blackboard.Set(TARGET, above);
            Assert.IsFalse(new GetValue(GetParameter.AngleFromMyFacing, TARGET).TryGetNumber(me.Context, out _),
                "Straight above: no way over the ground to measure — no data, not 0");

            Transform upward = _things.Make("Looking Up", new Vector3(3f, 0f, 4f));
            upward.rotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
            me.Agent.Blackboard.Set(TARGET, upward);
            Assert.IsFalse(new GetValue(GetParameter.FacesMe, TARGET).TryGetNumber(me.Context, out _),
                "Looking straight up: it faces no way over the ground — no data");
        }

        [Test]
        public void DebugLog_AGetWithoutData_WritesNoData()
        {
            var me = new GraphRig(trace: false);
            Transform foe = _things.Make("Foe", new Vector3(3f, 0f, 0f));
            foe.gameObject.AddComponent<GetTests.Status>();         // it says nothing of its health
            me.Agent.Blackboard.Set(TARGET, foe);
            var log = new DebugLogTask { Message = "Seen:", Number = new GetValue(GetParameter.Health, TARGET) };

            LogAssert.Expect(LogType.Log, new Regex("Seen: health of Target = no data"));
            Assert.AreEqual(TaskStatus.Success, log.Tick(me.Context, 0.02f));
        }

        [Test]
        public void Get_ThinkAfterThink_AllocatesNothing_ByUnitysCounter()
        {
            var me = new GraphRig(trace: false);
            Transform foe = _things.Make("Foe", new Vector3(3f, 0f, 4f));
            foe.gameObject.AddComponent<GetTests.Status>().Say(CharacterStat.Health, 5f);
            me.Agent.Blackboard.Set(TARGET, foe);
            var health = new GetValue(GetParameter.Health, TARGET);
            var distance = new GetValue(GetParameter.Distance, TARGET);
            var angle = new GetValue(GetParameter.AngleFromMyFacing, TARGET);
            var faces = new GetValue(GetParameter.FacesMe, TARGET);
            var compare = new CompareCondition(health, NumberComparison.Greater, 1f, slack: 0.5f);

            void Think()
            {
                me.Context.BeginThink();
                health.TryGetNumber(me.Context, out _);
                distance.TryGetNumber(me.Context, out _);
                angle.TryGetNumber(me.Context, out _);
                faces.TryGetNumber(me.Context, out _);
                compare.Ask(me.Context);
                me.Context.EndThink();
            }

            for (int i = 0; i < 3; i++)
                Think();                                            // the first asks find the status and make the records

            // Unity's own GC.Alloc counter (GC.GetAllocatedBytesForCurrentThread says 0 in the editor, whatever is made)
            Assert.That(() =>
            {
                for (int i = 0; i < 100; i++)
                    Think();
            }, Is.Not.AllocatingGCMemory(), "A think reads the numbers without allocating");
        }

        // A character with the module's side-view body (a SimpleBody in the XY plane), looking along `facing`
        private AICharacter SideViewCharacter(Vector3 facing)
        {
            var go = _things.Keep(new GameObject("Side View") { hideFlags = HideFlags.HideAndDontSave });
            go.SetActive(false);
            var character = go.AddComponent<AICharacter>();
            character.Simulation = AICharacter.SimulationMode.Manual;
            character.Body = new SimpleBody { PlanarXY = true };
            character.Initialize();
            character.Context.Actuator.Face(facing);
            return character;
        }
    }
}
