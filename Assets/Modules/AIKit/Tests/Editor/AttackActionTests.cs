using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The moves ask for their actions through the character's state (step 90, C22 — the model never commands the
    /// presentation): a move's action when the presentation has it, else the plain swing — one request; a move cut short
    /// drops its action; the switch of weapons asks for the draw and the sheathe the same way.
    /// </summary>
    public sealed class AttackActionTests
    {
        private const string VICTIM = "Victim";

        [TearDown]
        public void TearDown() => GraphRig.DestroyAll();

        private static GraphRig Rig(string action, View view)
        {
            var rig = new GraphRig();
            rig.GiveWeapon(WeaponAsset.Create(new WeaponMove
            {
                Name = "Attack 1", Action = action, Reach = 3f, WindUp = 0.4f, Recovery = 0.3f
            }));
            rig.Context.Actions.View = view;
            Transform victim = rig.CreateTargetAt(new Vector3(0f, 0f, 1.5f));
            rig.Context.Blackboard.Set(VICTIM, victim);
            rig.SetGraph(GraphRig.OneState(new AttackTask { Target = VICTIM }));
            rig.Instance.Start(rig.Context);
            return rig;
        }

        [Test]
        public void Attack_ThePresentationHasTheMovesAction_ItIsAsked_NoSwing()
        {
            var view = new View { Known = { "Slash" } };
            GraphRig rig = Rig("Slash", view);

            rig.Step(0.02f, 3);

            Assert.AreEqual(ActionState.Kind.Action, rig.Context.Actions.Current);
            Assert.AreEqual("Slash", rig.Context.Actions.Name);
        }

        [Test]
        public void Attack_ThePresentationHasNoSuchAction_ThePlainSwing_OneRequest()
        {
            var view = new View();
            GraphRig rig = Rig("Slash", view);
            int before = rig.Context.Actions.Serial;

            rig.Step(0.02f, 3);

            Assert.AreEqual(ActionState.Kind.Swing, rig.Context.Actions.Current, "No such action: its plain swing");
            Assert.LessOrEqual(rig.Context.Actions.Serial - before, 2, "The action asked, then the swing — once each");
        }

        [Test]
        public void Attack_CutShort_ItsActionIsDroppedAtOnce()
        {
            var view = new View { Known = { "Slash" } };
            GraphRig rig = Rig("Slash", view);
            rig.Step(0.02f, 3);

            rig.Instance.Stop(rig.Context);

            Assert.AreEqual(ActionState.Kind.None, rig.Context.Actions.Current);
            Assert.IsFalse(rig.Context.Actions.EndsGently, "Dropped, not put down");
        }

        private sealed class View : IActionView
        {
            public HashSet<string> Known { get; } = new();

            public bool Has(string action) => Known.Contains(action);

            public bool IsShowing(int serial) => true;
        }
    }
}
