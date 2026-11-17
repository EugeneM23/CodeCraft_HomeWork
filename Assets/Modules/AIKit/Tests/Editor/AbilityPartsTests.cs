using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// A character's runner of an ability made of parts (step 90): a blow lands on the target in reach and misses one out
    /// of it; a projectile and a lob go through the body's launcher — the same flight as a weapon's shot — and their effects
    /// happen where they come down, not at the release; an area damage hits everyone of its tag in its radius and only
    /// them, found through the agent's object finder; a cue shown while it flies ends where it comes down and when the
    /// ability is cancelled; the action is asked for once (never a swing on top); the phase of the blow is the agent's
    /// status; an ability on itself acts on the user; the side the target is on is a condition.
    /// </summary>
    public sealed class AbilityPartsTests
    {
        private const float DT = 0.02f;
        private const string FOE = "Foe";

        private GraphRig _rig;
        private Transform _target;
        private HitProbe _hits;
        private readonly List<Object> _created = new();

        [SetUp]
        public void SetUp()
        {
            _rig = new GraphRig();
            _target = _rig.CreateTargetAt(new Vector3(0f, 0f, 1.5f));
            _hits = _target.gameObject.AddComponent<HitProbe>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created)
            {
                if (created != null)
                    Object.DestroyImmediate(created);
            }

            _created.Clear();
            GraphRig.DestroyAll();
        }

        private AbilityRunner Runner(AbilityAsset ability)
        {
            _created.Add(ability);
            var runner = new AbilityRunner(ability, _rig.Agent.transform);
            _rig.Abilities.Add(runner.Definition);
            _rig.Abilities.Register(runner);
            return runner;
        }

        private static AbilityAsset Ability(string name, float windUp, float recovery, params AbilityPart[] parts)
        {
            AbilityAsset ability = AbilityAsset.Create(name, parts);
            ability.Timing = new AbilityTiming { WindUp = windUp, Recovery = recovery };
            ability.Queue.AtOnce = 0;
            return ability;
        }

        private Transform Thing(string name, Vector3 at)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            go.transform.position = at;
            _created.Add(go);
            return go.transform;
        }

        // ---------- the delivery ----------

        [Test]
        public void Strike_InReachAtTheRelease_Hits_OutOfReach_Misses()
        {
            const float REACH = 2f;
            Runner(Ability("Smash", 0.2f, 0.1f, new StrikeDelivery { Reach = REACH }, new DamageEffect { Damage = 3f }));

            Assert.IsTrue(_rig.Abilities.TryUse("Smash", _rig.Context));
            _rig.Tick(0.1f);
            Assert.AreEqual(0, _hits.Hits, "Not before the release");
            _rig.Tick(0.15f);
            Assert.AreEqual(1, _hits.Hits);
            Assert.AreEqual(3f, _hits.LastDamage);

            _rig.Tick(1f);
            Assert.IsTrue(_rig.Abilities.TryUse("Smash", _rig.Context));
            _rig.MoveTargetTo(new Vector3(0f, 0f, REACH + 0.5f));       // out of reach while it swings
            _rig.Tick(0.3f);
            Assert.AreEqual(1, _hits.Hits, "Out of reach at the release: it misses");
        }

        [Test]
        public void Projectile_GoesThroughTheBodysLauncher_TheEffectsWhereItLands()
        {
            var launcher = new Capture();
            _rig.Context.Launcher = launcher;
            Runner(Ability("Bolt", 0.2f, 0.1f, new ProjectileDelivery { Arc = 0f, Speed = 20f },
                new DamageEffect { Damage = 2f }));

            _rig.Abilities.TryUse("Bolt", _rig.Context);
            _rig.Tick(0.25f);

            Assert.AreEqual(1, launcher.Launches.Count, "The same launcher as a weapon's shot");
            ProjectileLaunch launch = launcher.Launches[0];
            Assert.AreSame(_target, launch.Target);
            Assert.AreEqual(0f, launch.Damage, "The flight hits nobody itself: the effects do, where it lands");
            Assert.AreEqual(0, _hits.Hits);

            launch.Landing.Landed(launch, _target.position, _target);
            Assert.AreEqual(1, _hits.Hits);
            Assert.AreEqual(2f, _hits.LastDamage);
        }

        [Test]
        public void Lob_TheEffectsHappenWhereItLands_NotAtTheRelease()
        {
            var launcher = new Capture();
            _rig.Context.Launcher = launcher;
            var finder = new ListFinder();
            finder.Add(FOE, _target);
            _rig.Context.Objects = finder;
            var lob = new LobDelivery { Flight = 1f, Arc = 2f, Hand = 1.5f };
            Runner(Ability("Grenade", 0.3f, 0.2f, lob, new AreaDamageEffect { Radius = 2f, Damage = 1f, Tag = FOE }));
            Vector3 stood = _target.position;

            _rig.Abilities.TryUse("Grenade", _rig.Context);
            _rig.Tick(0.35f);

            Assert.AreEqual(1, launcher.Launches.Count);
            ProjectileLaunch launch = launcher.Launches[0];
            Assert.IsNull(launch.Target, "A lob to a place");
            Assert.AreEqual(stood, launch.Point, "Where the target stood at the release");
            Assert.AreEqual(lob.Flight, launch.Flight, "In its own time in the air");
            Assert.That(launch.Arc, Is.EqualTo(lob.Arc / Vector3.Distance(Vector3.up * lob.Hand, stood)).Within(1e-4f),
                "Its arc as high as the part says, over its length");
            Assert.AreEqual(0, _hits.Hits, "Nothing at the release");

            launch.Landing.Landed(launch, launch.Point, null);
            Assert.AreEqual(1, _hits.Hits, "The blast where it came down");
        }

        [Test]
        public void AreaDamage_HitsEveryoneOfItsTagInTheRadius_AndOnlyThem()
        {
            _rig.Context.Launcher = null;                           // no launcher: it lands at once at the target's place
            var finder = new ListFinder();
            _rig.Context.Objects = finder;
            const float RADIUS = 2f;
            Transform near = Thing("Near", _target.position + new Vector3(RADIUS * 0.8f, 0f, 0f));
            Transform far = Thing("Far", _target.position + new Vector3(RADIUS * 1.5f, 0f, 0f));
            Transform other = Thing("Other Kind", _target.position + new Vector3(0.5f, 0f, 0f));
            HitProbe nearHits = near.gameObject.AddComponent<HitProbe>();
            HitProbe farHits = far.gameObject.AddComponent<HitProbe>();
            HitProbe otherHits = other.gameObject.AddComponent<HitProbe>();
            HitProbe selfHits = _rig.Agent.gameObject.AddComponent<HitProbe>();
            _rig.Agent.gameObject.SetActive(true);                 // found by the finder if it were not left out
            finder.Add(FOE, _target);
            finder.Add(FOE, near);
            finder.Add(FOE, far);
            finder.Add(FOE, _rig.Agent.transform);
            finder.Add("Friend", other);
            Runner(Ability("Grenade", 0.1f, 0.1f, new LobDelivery(),
                new AreaDamageEffect { Radius = RADIUS, Damage = 1f, Tag = FOE }));

            _rig.Abilities.TryUse("Grenade", _rig.Context);
            _rig.Tick(0.15f);

            Assert.AreEqual(1, _hits.Hits);
            Assert.AreEqual(1, nearHits.Hits);
            Assert.AreEqual(0, farHits.Hits, "Beyond the radius");
            Assert.AreEqual(0, otherHits.Hits, "Of another tag");
            Assert.AreEqual(0, selfHits.Hits, "The user is never its own victim");
        }

        [Test]
        public void Self_ActsOnTheUser()
        {
            HitProbe self = _rig.Agent.gameObject.AddComponent<HitProbe>();
            _rig.Agent.gameObject.SetActive(true);                 // a hit reaches an active object
            Runner(Ability("Sacrifice", 0.1f, 0.1f, new SelfDelivery(), new DamageEffect { Damage = 5f }));

            _rig.Abilities.TryUse("Sacrifice", _rig.Context);
            _rig.Tick(0.15f);

            Assert.AreEqual(1, self.Hits);
            Assert.AreEqual(0, _hits.Hits);
        }

        // ---------- the cues ----------

        [Test]
        public void Cue_BegunAtTheRelease_EndsWhereItComesDown()
        {
            var launcher = new Capture();
            _rig.Context.Launcher = launcher;
            var cue = new CountingCue();
            Runner(Ability("Grenade", 0.1f, 0.1f, new LobDelivery(), cue));

            _rig.Abilities.TryUse("Grenade", _rig.Context);
            _rig.Tick(0.05f);
            Assert.AreEqual(0, cue.Begun, "Not in the wind-up");
            _rig.Tick(0.1f);
            Assert.AreEqual(1, cue.Begun, "At the release, while it flies");
            Assert.AreEqual(_target.position, cue.At, "Where it will come down");
            Assert.AreEqual(0, cue.Ended);

            ProjectileLaunch launch = launcher.Launches[0];
            launch.Landing.Landed(launch, launch.Point, null);
            Assert.AreEqual(1, cue.Ended, "Ended where it came down");
            launch.Landing.Landed(launch, launch.Point, null);
            Assert.AreEqual(1, cue.Ended, "…once");
        }

        [Test]
        public void Cue_TheAbilityCancelled_ItEnds_AndTheLandingDoesNotEndItAgain()
        {
            var launcher = new Capture();
            _rig.Context.Launcher = launcher;
            var cue = new CountingCue();
            AbilityRunner runner = Runner(Ability("Grenade", 0.1f, 0.5f, new LobDelivery(), cue));
            _rig.Abilities.TryUse("Grenade", _rig.Context);
            _rig.Tick(0.15f);
            Assert.AreEqual(1, cue.Begun);

            runner.Cancel();

            Assert.AreEqual(1, cue.Ended, "End is mandatory on a cancel (handbook §20.4)");
            ProjectileLaunch launch = launcher.Launches[0];
            launch.Landing.Landed(launch, launch.Point, null);
            Assert.AreEqual(1, cue.Ended);
        }

        [Test]
        public void GroundMark_AMarkWhereItWillComeDown_GoneWhenItLands()
        {
            var launcher = new Capture();
            _rig.Context.Launcher = launcher;
            Runner(Ability("Grenade", 0.1f, 0.1f, new LobDelivery(), new GroundMarkCue { Radius = 2f }));

            _rig.Abilities.TryUse("Grenade", _rig.Context);
            _rig.Tick(0.15f);
            GameObject mark = GameObject.Find("Ground Mark");

            Assert.IsNotNull(mark);
            Assert.AreEqual(_target.position.x, mark.transform.position.x, 1e-4f);
            Assert.AreEqual(_target.position.z, mark.transform.position.z, 1e-4f);
            Assert.AreEqual(4f, mark.transform.localScale.x, 1e-4f, "As big as the blast");

            ProjectileLaunch launch = launcher.Launches[0];
            launch.Landing.Landed(launch, launch.Point, null);
            Assert.IsTrue(mark == null, "Gone where it came down");
        }

        // ---------- the action and the phase ----------

        [Test]
        public void Action_ThePreviousStillShowing_TheAbilitysActionIsAskedOnce_NoSwingOnTop()
        {
            var shown = new ActionLog(_rig.Context) { AlwaysShowing = true };
            Runner(Ability("Roar", 0.2f, 0.1f, new SelfDelivery()));

            _rig.Abilities.TryUse("Roar", _rig.Context);

            CollectionAssert.AreEqual(new[] { "Roar" }, shown.Played);
            Assert.AreEqual(0, shown.Swings, "One request, never the swing on top of it");
        }

        [Test]
        public void Action_ThePresentationHasNone_ThePlainSwingIsAsked_Instead()
        {
            var view = new View();
            _rig.Context.Actions.View = view;
            Runner(Ability("Roar", 0.2f, 0.1f, new SelfDelivery()));

            _rig.Abilities.TryUse("Roar", _rig.Context);

            Assert.AreEqual(ActionState.Kind.Swing, _rig.Context.Actions.Current, "No such action: the plain swing");
            view.Known.Add("Roar");
            _rig.Tick(1f);
            _rig.Abilities.TryUse("Roar", _rig.Context);
            Assert.AreEqual(ActionState.Kind.Action, _rig.Context.Actions.Current);
            Assert.AreEqual("Roar", _rig.Context.Actions.Name);
        }

        [Test]
        public void Phase_TheAgentsStatus_WindUp_Recovery_None()
        {
            Runner(Ability("Smash", 0.2f, 0.2f, new StrikeDelivery { Reach = 2f }));

            _rig.Abilities.TryUse("Smash", _rig.Context);
            Assert.AreEqual(AbilityPhase.WindUp, _rig.Context.AttackPhase);
            _rig.Tick(0.25f);
            Assert.AreEqual(AbilityPhase.Recovery, _rig.Context.AttackPhase);
            _rig.Tick(0.2f);
            Assert.AreEqual(AbilityPhase.None, _rig.Context.AttackPhase);
        }

        // ---------- the conditions ----------

        [Test]
        public void Angle_TheTargetBehind_ABackstabIsReady_OneInFrontIsNot()
        {
            Runner(Ability("Backstab", 0.1f, 0.1f, new AngleCondition { Front = false, Side = false, Back = true },
                new StrikeDelivery()));
            _rig.Agent.transform.rotation = Quaternion.LookRotation(Vector3.forward);

            _rig.MoveTargetTo(new Vector3(0f, 0f, 1.5f));                     // in front
            Assert.AreEqual(AbilityBlock.WrongSide, _rig.Abilities.BlockOf("Backstab", _rig.Context));

            _rig.MoveTargetTo(new Vector3(0f, 0f, -1.5f));                    // behind
            Assert.AreEqual(AbilityBlock.None, _rig.Abilities.BlockOf("Backstab", _rig.Context));
        }

        [Test]
        public void Range_AndCooldownFromTheEnd_AreTheConditionsTheAgentChecks()
        {
            Runner(Ability("Smash", 0.2f, 0.3f, new RangeCondition { Min = 1f, Max = 2f },
                new CooldownCondition { Seconds = 1f }, new StrikeDelivery()));

            _rig.MoveTargetTo(new Vector3(0f, 0f, 0.5f));
            Assert.AreEqual(AbilityBlock.TooNear, _rig.Abilities.BlockOf("Smash", _rig.Context));
            _rig.MoveTargetTo(new Vector3(0f, 0f, 1.5f));
            Assert.IsTrue(_rig.Abilities.TryUse("Smash", _rig.Context));

            _rig.Tick(0.45f);                                                 // over at 0.5: still playing
            Assert.AreEqual(AbilityBlock.Playing, _rig.Abilities.BlockOf("Smash", _rig.Context));
            _rig.Tick(0.1f);
            _rig.Tick(0.9f);
            Assert.AreEqual(AbilityBlock.Resting, _rig.Abilities.BlockOf("Smash", _rig.Context), "From its end, not its start");
            _rig.Tick(0.2f);
            Assert.AreEqual(AbilityBlock.None, _rig.Abilities.BlockOf("Smash", _rig.Context));
        }

        // ---------- fakes ----------

        private sealed class Capture : IProjectileLauncher
        {
            public readonly List<ProjectileLaunch> Launches = new();

            public void Launch(in ProjectileLaunch launch) => Launches.Add(launch);
        }

        private sealed class CountingCue : AbilityCue
        {
            public int Begun;
            public int Ended;
            public Vector3 At;

            public override object Begin(in AbilityImpact at)
            {
                Begun++;
                At = at.Point;
                return this;
            }

            public override void End(object made) => Ended++;
        }


        private sealed class View : IActionView
        {
            public readonly HashSet<string> Known = new();

            public bool Has(string action) => Known.Contains(action);

            public bool IsShowing(int serial) => true;
        }
    }
}
