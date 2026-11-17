using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The blow at the frame the presentation tells (handbook §20.3: the moment of a blow comes from the clip; the model
    /// only listens): a presentation that tells the "Hit" of a swing makes the blow wait for it — not the weapon's
    /// wind-up; a swing that never reaches its frame (cut off the screen) lands no blow; a presentation that tells
    /// nothing leaves the numbers as they were.
    /// </summary>
    public sealed class AttackMomentTests
    {
        private const string VICTIM = "Victim";
        private const float WIND_UP = 0.3f;

        [TearDown]
        public void TearDown() => GraphRig.DestroyAll();

        private sealed class View : IActionView, IActionMoments
        {
            public bool TellsHit = true;
            public bool Showing = true;

            public bool Has(string action) => action == "Slash";

            public bool IsShowing(int serial) => Showing;

            public bool Tells(string action, string moment) =>
                TellsHit && moment == ActionState.HIT && (action == "Slash" || action == ActionState.SWING);
        }

        private static GraphRig Rig(View view, out HitProbe victim, string action = "Slash")
        {
            var rig = new GraphRig();
            rig.GiveWeapon(WeaponAsset.Create(new WeaponMove
            {
                Name = "Attack 1", Action = action, Reach = 3f, WindUp = WIND_UP, Recovery = 0.4f
            }));
            rig.Context.Actions.View = view;
            Transform target = rig.CreateTargetAt(new Vector3(0f, 0f, 1.5f));
            victim = target.gameObject.AddComponent<HitProbe>();
            rig.Context.Blackboard.Set(VICTIM, target);
            rig.SetGraph(GraphRig.OneState(new AttackTask { Target = VICTIM }));
            rig.Instance.Start(rig.Context);
            return rig;
        }

        [Test]
        public void Attack_ThePresentationTellsTheHit_TheBlowWaitsForIt_NotForTheWindUp()
        {
            GraphRig rig = Rig(new View(), out HitProbe victim);
            rig.Step(0.05f, (int)(WIND_UP / 0.05f) + 2);
            Assert.AreEqual(0, victim.Hits, "past the wind-up, but the frame of the blow has not come");

            ActionState actions = rig.Context.Actions;
            actions.Tell(actions.Serial, ActionState.HIT);
            rig.Step(0.05f, 1);
            Assert.AreEqual(1, victim.Hits, "the blow lands when its frame is told");
        }

        [Test]
        public void Attack_ThePlainSwingsHitIsTold_Too()
        {
            GraphRig rig = Rig(new View(), out HitProbe victim, action: null);
            rig.Step(0.05f, 2);
            Assert.AreEqual(ActionState.Kind.Swing, rig.Context.Actions.Current);

            rig.Context.Actions.Tell(rig.Context.Actions.Serial, ActionState.HIT);
            rig.Step(0.05f, 1);
            Assert.AreEqual(1, victim.Hits);
        }

        [Test]
        public void Attack_TheSwingIsCutOffTheScreenBeforeItsFrame_NoBlow()
        {
            var view = new View();
            GraphRig rig = Rig(view, out HitProbe victim);
            rig.Step(0.05f, 2);

            view.Showing = false;
            rig.Step(0.05f, 20);
            Assert.AreEqual(0, victim.Hits, "the swing never reached its frame");
        }

        [Test]
        public void Attack_AMomentOfAnOlderRequest_IsNotHeard()
        {
            GraphRig rig = Rig(new View(), out HitProbe victim);
            rig.Step(0.05f, 2);
            ActionState actions = rig.Context.Actions;
            actions.Tell(actions.Serial - 1, ActionState.HIT);
            rig.Step(0.05f, 2);
            Assert.AreEqual(0, victim.Hits);
        }

        [Test]
        public void Attack_APresentationThatTellsNothing_TheWindUpAsBefore()
        {
            GraphRig rig = Rig(new View { TellsHit = false }, out HitProbe victim);
            rig.Step(0.05f, (int)(WIND_UP / 0.05f) + 2);
            Assert.AreEqual(1, victim.Hits);
        }
    }
}
