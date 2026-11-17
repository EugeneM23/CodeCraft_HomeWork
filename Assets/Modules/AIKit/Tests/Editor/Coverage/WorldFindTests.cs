using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The searches of the world (step 90, C15; handbook §24 — the author's list never falls through to "something near",
    /// §24.1 — the object says what it offers; one gateway, not a scan): a character that owns objects of the kind takes
    /// none of the others, even when its own lies beyond the search's radius; a pile serves everybody and is claimed by
    /// nobody, a place for one is claimed and the next one looks for another; a band from the target with no far end of
    /// its own reaches as far as the weapon in the hand; a query by a component that is no object of the world finds the
    /// objects that carry it; an object that goes takes its claim with it; a wander leaves the walk called off and the pace
    /// full on every way out.
    /// </summary>
    public sealed class WorldFindTests
    {
        private const string FOUND = "Found";
        private const float DT = 0.02f;

        private readonly List<Object> _created = new();
        private readonly List<WorldObject> _found = new();
        private WorldObjects _registry;

        [SetUp]
        public void SetUp() => _registry = new WorldObjects();

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

        [Test]
        public void Find_IOwnOneBeyondTheRadius_NeverTakesAPublicOne()
        {
            GraphRig me = Rig(Vector3.zero);
            AIObject mine = Place("My Bench", new Vector3(20f, 0f, 0f), "Bench");
            mine.Owners.Add(me.Agent);
            AIObject common = Place("Bench", new Vector3(2f, 0f, 0f), "Bench");
            var what = new WhatValue
            {
                ObjectKind = "Bench", Offer = Offer.Occupy, Whose = Ownership.MineOrPublic, Radius = 10f
            };
            var find = new FindTask { What = what, StoreIn = FOUND };

            Assert.AreEqual(TaskStatus.Failure, find.Tick(me.Context, DT),
                "My own bench is out of the radius: nothing found — not the public one near by");
            Assert.AreNotSame(common.transform, me.Context.Blackboard.Get<Transform>(FOUND));

            what.Radius = 0f;
            Assert.AreEqual(TaskStatus.Success, find.Tick(me.Context, DT), "Anywhere: my own");
            Assert.AreSame(mine.transform, me.Context.Blackboard.Get<Transform>(FOUND));

            GraphRig stranger = Rig(Vector3.zero);
            what.Radius = 10f;
            Assert.AreEqual(TaskStatus.Success, find.Tick(stranger.Context, DT), "One who owns none takes the public one");
            Assert.AreSame(common.transform, stranger.Context.Blackboard.Get<Transform>(FOUND));
        }

        [Test]
        public void Find_APileServesEverybody_ItIsNotClaimed()
        {
            GraphRig first = Rig(Vector3.zero);
            GraphRig second = Rig(new Vector3(1f, 0f, 0f));
            var pile = Make<ItemSource>("Pile", new Vector3(2f, 0f, 0f));
            var pattern = Make<PickableItem>("Rock", new Vector3(50f, 0f, 0f));
            pattern.Weapon = Weapon(12f);
            pile.Item = pattern.gameObject;
            pile.Stock = -1;
            _registry.Register(pile);
            var what = new WhatValue { Offer = Offer.Take };

            Assert.AreEqual(TaskStatus.Success, new FindTask { What = what, StoreIn = FOUND }.Tick(first.Context, DT));
            Assert.AreEqual(TaskStatus.Success, new FindTask { What = what, StoreIn = FOUND }.Tick(second.Context, DT),
                "The second finds it too");
            Assert.AreSame(pile.transform, first.Context.Blackboard.Get<Transform>(FOUND));
            Assert.AreSame(pile.transform, second.Context.Blackboard.Get<Transform>(FOUND));
            Assert.IsNull(_registry.HolderOf(pile), "Nobody claims a pile");
        }

        [Test]
        public void AIObject_CapacityOne_IsClaimedByFind_TheSecondLooksForAnother()
        {
            GraphRig a = Rig(Vector3.zero);
            GraphRig b = Rig(Vector3.zero);
            AIObject near = Place("Near Seat", new Vector3(2f, 0f, 0f), "Seat");
            AIObject far = Place("Far Seat", new Vector3(6f, 0f, 0f), "Seat");
            near.Capacity = far.Capacity = 1;
            var what = new WhatValue { ObjectKind = "Seat", Offer = Offer.Occupy };

            Assert.AreEqual(TaskStatus.Success, new FindTask { What = what, StoreIn = FOUND }.Tick(a.Context, DT));
            Assert.AreEqual(TaskStatus.Success, new FindTask { What = what, StoreIn = FOUND }.Tick(b.Context, DT));
            Assert.AreSame(near.transform, a.Context.Blackboard.Get<Transform>(FOUND), "The first: the nearer seat");
            Assert.AreSame(a.Agent, _registry.HolderOf(near), "…claimed: a seat for one");
            Assert.AreSame(far.transform, b.Context.Blackboard.Get<Transform>(FOUND), "The second looks for another");

            _registry.ReleaseAll(a.Agent);
            _registry.ReleaseAll(b.Agent);
            a.Context.Blackboard.Set(FOUND, (Transform)null);              // fresh searches (one held is kept, §18а.2)
            b.Context.Blackboard.Set(FOUND, (Transform)null);
            near.Capacity = far.Capacity = 0;
            Assert.AreEqual(TaskStatus.Success, new FindTask { What = what, StoreIn = FOUND }.Tick(a.Context, DT));
            Assert.AreEqual(TaskStatus.Success, new FindTask { What = what, StoreIn = FOUND }.Tick(b.Context, DT));
            Assert.AreSame(near.transform, a.Context.Blackboard.Get<Transform>(FOUND), "Room for any number: the nearer");
            Assert.AreSame(near.transform, b.Context.Blackboard.Get<Transform>(FOUND), "…for both");
            Assert.IsNull(_registry.HolderOf(near), "No claim on a place for any number");
        }

        [Test]
        public void Find_ABandWithMaxDistanceZero_IsTheReachOfTheWeaponInTheHand()
        {
            GraphRig rig = Rig(Vector3.zero);
            Transform threat = Thing("Threat", new Vector3(0f, 0f, 20f));
            rig.Context.BrainTarget = threat;
            var withinReach = Make<Cover>("Ten Off", new Vector3(0f, 0f, 11.2f));    // its place 10 m from the threat
            var beyondReach = Make<Cover>("Fourteen Off", new Vector3(0f, 0f, 7.2f)); // 14 m — and nearer to the rig
            _registry.Register(withinReach);
            _registry.Register(beyondReach);
            Assert.That(Flat(threat.position - withinReach.PlaceFor(Offer.Occupy, rig.Context, threat)).magnitude,
                Is.EqualTo(10f).Within(1e-3f), "Set-up");
            Assert.That(Flat(threat.position - beyondReach.PlaceFor(Offer.Occupy, rig.Context, threat)).magnitude,
                Is.EqualTo(14f).Within(1e-3f), "Set-up");
            WeaponAsset bow = WeaponAsset.Create(new WeaponMove { Name = "Attack 1", Reach = 15f },
                new WeaponMove { Name = "Attack 2", Reach = 12f });
            _created.Add(bow);
            rig.GiveWeapon(bow);
            Assert.AreEqual(12f, bow.ShortestReach, "Set-up: its shortest move");
            var find = new FindTask
            {
                What = new WhatValue
                {
                    Component = typeof(Cover).FullName, Offer = Offer.Occupy, FromTarget = true, MinDistance = 0f,
                    MaxDistance = 0f, Slack = 0f
                },
                StoreIn = FOUND
            };

            Assert.AreEqual(TaskStatus.Success, find.Tick(rig.Context, DT));
            Assert.AreSame(withinReach.transform, rig.Context.Blackboard.Get<Transform>(FOUND),
                "Max 0: as far as the weapon in the hand reaches (its shortest move, 12 m) — not the nearer one 14 m off");

            rig.GiveWeapon(null);
            Assert.AreEqual(TaskStatus.Failure, find.Tick(rig.Context, DT), "No weapon in the hand: it reaches nowhere");
        }

        [Test]
        public void ObjectQuery_AComponentThatIsNoWorldObject_MatchesObjectsCarryingIt_UntaggedIsAny()
        {
            var a = Make<Cover>("Crate With Collider", new Vector3(1f, 0f, 0f));
            a.gameObject.AddComponent<BoxCollider>();
            var b = Make<Cover>("Crate", new Vector3(2f, 0f, 0f));
            _registry.Register(a);
            _registry.Register(b);

            Assert.That(Find(new ObjectQuery { Component = typeof(BoxCollider) }), Is.EquivalentTo(new WorldObject[] { a }),
                "A component of its object: only the one carrying it");
            Assert.That(Find(new ObjectQuery { Component = typeof(IInteractable) }), Is.EquivalentTo(new WorldObject[] { a, b }),
                "An interface every object of the world has");
            Assert.That(Find(new ObjectQuery { Component = typeof(System.IDisposable) }), Is.Empty,
                "A type that is no component and that no object is: none");
            Assert.That(Find(new ObjectQuery { Tag = "Untagged" }), Is.EquivalentTo(new WorldObject[] { a, b }),
                "Untagged is any tag");
        }

        [Test]
        public void Registry_AnObjectThatGoes_ItsClaimGoesWithIt()
        {
            GraphRig a = Rig(Vector3.zero);
            GraphRig b = Rig(Vector3.zero);
            var rock = Make<PickableItem>("Rock", Vector3.zero);
            _registry.Register(rock);
            Assert.IsTrue(_registry.TryClaim(rock, a.Agent), "Set-up: claimed");

            _registry.Unregister(rock);
            Assert.IsNull(_registry.HolderOf(rock), "Gone: its claim went with it");
            _registry.Register(rock);
            Assert.IsNull(_registry.HolderOf(rock), "Back: nobody's — the old claim did not come back");
            Assert.IsTrue(_registry.TryClaim(rock, b.Agent), "Anybody may claim it now");
        }

        [Test]
        public void Wander_Left_FullPaceAndTheWalkCalledOff_TornAway_TheBodyStops()
        {
            GraphRig rig = Rig(Vector3.zero, out WorldWalker walker);
            var find = new FindTask
            {
                What = new WhatValue { Offer = Offer.Take }, How = FindTask.Way.Wander, Pace = 0.6f, GiveUpAfter = 20f,
                StoreIn = FOUND
            };

            Assert.AreEqual(TaskStatus.Running, Run(rig, find, 1f), "Set-up: nothing to find yet");
            Assert.IsNotEmpty(walker.Paces, "Set-up: it ran about");
            Assert.That(walker.Paces, Is.All.EqualTo(0.6f), "At its pace");
            int stops = rig.Actuator.Stops;

            find.Interrupt(rig.Context);

            Assert.AreEqual(1f, walker.Pace, "Torn away: the full pace again");
            Assert.AreEqual(NavStatus.Idle, walker.Status, "…the walk called off");
            Assert.AreEqual(stops + 1, rig.Actuator.Stops, "…and the body stops");

            Assert.AreEqual(TaskStatus.Running, Run(rig, find, 0.5f), "Set-up: wandering again");
            var rock = Make<PickableItem>("Rock", rig.Actuator.Position + new Vector3(3f, 0f, 0f));
            _registry.Register(rock);
            stops = rig.Actuator.Stops;

            Assert.AreEqual(TaskStatus.Success, Run(rig, find, 1f), "Found");
            Assert.AreEqual(1f, walker.Pace, "Found: the full pace again");
            Assert.AreEqual(NavStatus.Idle, walker.Status, "…the walk called off");
            Assert.AreEqual(stops, rig.Actuator.Stops, "…without a stop: what comes next walks on");
        }

        // ---------- helpers ----------

        private GraphRig Rig(Vector3 at)
        {
            var rig = new GraphRig(trace: false);
            rig.WakeUpAt(at);
            rig.UseObjects(_registry);
            rig.UseWorldRack();
            return rig;
        }

        private GraphRig Rig(Vector3 at, out WorldWalker walker)
        {
            var body = new TestActuator();
            walker = new WorldWalker(body);
            var rig = new GraphRig(trace: false, navigator: walker, actuator: body);
            rig.WakeUpAt(at);
            rig.UseObjects(_registry);
            rig.UseWorldRack();
            return rig;
        }

        private AIObject Place(string name, Vector3 at, string kind)
        {
            var place = Make<AIObject>(name, at);
            place.Kind = kind;
            _registry.Register(place);
            return place;
        }

        private List<WorldObject> Find(ObjectQuery query)
        {
            _registry.Find(query, _found);
            return new List<WorldObject>(_found);
        }

        private WeaponAsset Weapon(float reach)
        {
            WeaponAsset weapon = WeaponAsset.Create(new WeaponMove { Name = "Attack 1", Reach = reach });
            weapon.Uses = 1;
            _created.Add(weapon);
            return weapon;
        }

        private Transform Thing(string name, Vector3 at)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            go.transform.position = at;
            return go.transform;
        }

        private T Make<T>(string name, Vector3 at) where T : Component => Thing(name, at).gameObject.AddComponent<T>();

        private static TaskStatus Run(GraphRig rig, AgentTask task, float seconds)
        {
            TaskStatus answer = TaskStatus.Running;
            int steps = Mathf.CeilToInt(seconds / DT);
            for (int i = 0; i < steps && answer == TaskStatus.Running; i++)
            {
                rig.Context.Advance(DT);
                answer = task.Tick(rig.Context, DT);
                rig.Actuator.Apply(DT);
            }

            return answer;
        }

        private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
