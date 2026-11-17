using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// Find (step 90, C15 — "how to search"): what to look for comes from its What, the found object goes into its
    /// variable. Nearest — of the objects the world knows; Look Around — of those the eyes see; Wander — runs about until
    /// the eyes see one, gives up at its limit. It skips what another holds and what the character could not reach a
    /// moment ago, prefers its own objects, keeps the one it holds while it still fits, and claims what it finds.
    /// </summary>
    public sealed class FindTests
    {
        private const string FOUND = "Found";
        private const float DT = 0.02f;

        private readonly List<Object> _created = new();
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
        public void Nearest_TheNearestThatFits_AtOnce_IntoItsVariable_AndClaimsIt()
        {
            GraphRig rig = Rig(Vector3.zero);
            Rock(new Vector3(8f, 0f, 0f));
            PickableItem near = Rock(new Vector3(3f, 0f, 1f));
            Rock(new Vector3(-50f, 0f, 0f));                            // out of the radius
            var find = new FindTask { What = new WhatValue { Offer = Offer.Take, Radius = 20f }, StoreIn = FOUND };

            Assert.IsTrue(find.IsInstant, "Nearest is at once");
            Assert.AreEqual(TaskStatus.Success, find.Tick(rig.Context, DT), "Found");
            Assert.AreSame(near.transform, rig.Context.Blackboard.Get<Transform>(FOUND), "The nearest, into its variable");
            Assert.AreSame(rig.Agent, _registry.HolderOf(near), "Claimed: the others look for another");
        }

        [Test]
        public void Nearest_WhatAnotherHolds_OrIsUnreachableForMe_IsSkipped_NoneLeft_NothingFound()
        {
            GraphRig rig = Rig(Vector3.zero);
            GraphRig other = Rig(Vector3.zero);
            PickableItem near = Rock(new Vector3(2f, 0f, 0f));
            PickableItem far = Rock(new Vector3(6f, 0f, 0f));
            var find = new FindTask { What = new WhatValue { Offer = Offer.Take }, StoreIn = FOUND };
            _registry.TryClaim(near, other.Agent);

            Assert.AreEqual(TaskStatus.Success, find.Tick(rig.Context, DT));
            Assert.AreSame(far.transform, rig.Context.Blackboard.Get<Transform>(FOUND), "The near one is another's");

            _registry.ReportUnreachable(rig.Agent, far);
            rig.Context.Advance(1f);
            Assert.AreEqual(TaskStatus.Failure, find.Tick(rig.Context, DT), "Nothing Found");
            Assert.IsNull(rig.Context.Blackboard.Get<Transform>(FOUND), "The variable is emptied");
            Assert.AreEqual(rig.Context.LocalTime, rig.Context.NothingFoundAt, 1e-5f, "…and the search found nothing now");

            Assert.AreEqual(TaskStatus.Failure, new FindTask { StoreIn = FOUND }.Tick(rig.Context, DT),
                "No What wired in: nothing to search for");
        }

        [Test]
        public void Nearest_MineBeforePublic_OneItHoldsIsKeptWhileItFits()
        {
            GraphRig rig = Rig(Vector3.zero);
            var near = Place("Near Bench", new Vector3(1f, 0f, 0f), "Bench");
            var mine = Place("My Bench", new Vector3(9f, 0f, 0f), "Bench");
            mine.Owners.Add(rig.Agent);
            var find = new FindTask
            {
                What = new WhatValue { ObjectKind = "Bench", Offer = Offer.Occupy, Radius = 0f }, StoreIn = FOUND
            };

            find.Tick(rig.Context, DT);
            Assert.AreSame(mine.transform, rig.Context.Blackboard.Get<Transform>(FOUND), "Its own, though farther");

            mine.Owners.Clear();
            find.Tick(rig.Context, DT);
            Assert.AreSame(mine.transform, rig.Context.Blackboard.Get<Transform>(FOUND),
                "Both public now: the one it holds is kept, not swapped for a nearer");
            Assert.That(near, Is.Not.Null);
        }

        [Test]
        public void Nearest_ABandFromTheTarget_PlacesInIt_TheOneHeldKeepsItWithTheSlack()
        {
            GraphRig rig = Rig(Vector3.zero);
            Transform threat = Thing("Threat", new Vector3(0f, 0f, 20f));
            rig.Context.BrainTarget = threat;
            var tooNear = Make<Cover>("Too Near", new Vector3(0f, 0f, 17f));    // its place 4.2 m from the threat
            var inBand = Make<Cover>("In Band", new Vector3(4f, 0f, 8f));
            _registry.Register(tooNear);
            _registry.Register(inBand);
            var what = new WhatValue
            {
                Component = typeof(Cover).FullName, Offer = Offer.Occupy, Radius = 0f, FromTarget = true,
                MinDistance = 6f, MaxDistance = 16f, Slack = 3f
            };
            var find = new FindTask { What = what, StoreIn = FOUND };

            Assert.AreEqual(TaskStatus.Success, find.Tick(rig.Context, DT));
            Assert.AreSame(inBand.transform, rig.Context.Blackboard.Get<Transform>(FOUND), "Only a place in the band");

            threat.position = new Vector3(4f, 0f, 24.5f);                // its place now ~17.7 m off: out of 16, in 16 + 3
            Assert.AreEqual(TaskStatus.Success, find.Tick(rig.Context, DT), "The one it holds keeps it with the slack");
            Assert.AreSame(inBand.transform, rig.Context.Blackboard.Get<Transform>(FOUND));

            rig.Context.Blackboard.Set<Transform>(FOUND, null);
            _registry.Unregister(tooNear);                              // (from the new side it would fit the band)
            Assert.AreEqual(TaskStatus.Failure, find.Tick(rig.Context, DT), "Not held: no slack — out of the band");

            rig.Context.BrainTarget = null;
            what.MaxDistance = 0f;
            Assert.AreEqual(TaskStatus.Failure, find.Tick(rig.Context, DT), "A band and no target: nothing fits");
        }

        [Test]
        public void LookAround_OnlyWhatTheEyesSee()
        {
            GraphRig rig = Rig(Vector3.zero);
            rig.Context.Sight = new WallsSight().Wall(1.5f, -1f, 2f, 1f);   // a wall between it and the near rock
            Rock(new Vector3(3f, 0f, 0f));
            PickableItem seen = Rock(new Vector3(0f, 0f, 6f));
            var find = new FindTask
            {
                What = new WhatValue { Offer = Offer.Take, Radius = 20f }, How = FindTask.Way.LookAround, StoreIn = FOUND
            };

            Assert.AreEqual(TaskStatus.Success, find.Tick(rig.Context, DT));
            Assert.AreSame(seen.transform, rig.Context.Blackboard.Get<Transform>(FOUND), "Not the one behind the wall");

            var nearest = new FindTask { What = new WhatValue { Offer = Offer.Take, Radius = 20f }, StoreIn = "Known" };
            rig.Context.Blackboard.Set<Transform>("Known", null);
            _registry.Release(seen, rig.Agent);
            Assert.AreEqual(TaskStatus.Success, nearest.Tick(rig.Context, DT));
            Assert.AreNotSame(seen.transform, rig.Context.Blackboard.Get<Transform>("Known"),
                "Nearest knows the one behind the wall too");
        }

        [Test]
        public void Wander_RunsAboutUntilItSeesOne_ThenFound()
        {
            GraphRig rig = Rig(Vector3.zero);
            rig.Context.Sight = new WallsSight().Wall(-12f, 4f, 12f, 5f);   // a wall across the yard, the rock behind it
            PickableItem rock = Rock(new Vector3(0f, 0f, 8f));
            rock.transform.position = new Vector3(11f, 0f, 8f);
            var find = new FindTask
            {
                What = new WhatValue { Offer = Offer.Take, Radius = 30f }, How = FindTask.Way.Wander,
                WanderRadius = 20f, GiveUpAfter = 60f, StoreIn = FOUND
            };

            Assert.IsFalse(find.IsInstant, "Wander takes time");
            TaskStatus answer = Run(rig, find, 60f);

            Assert.AreEqual(TaskStatus.Success, answer, "It saw the rock at last");
            Assert.AreSame(rock.transform, rig.Context.Blackboard.Get<Transform>(FOUND));
            Assert.Greater(Vector3.Distance(rig.Actuator.Position, Vector3.zero), 1f, "It ran about to see it");
        }

        [Test]
        public void Wander_SeesNothing_GivesUpAtItsLimit()
        {
            GraphRig rig = Rig(Vector3.zero);
            var find = new FindTask
            {
                What = new WhatValue { Offer = Offer.Take, Radius = 30f }, How = FindTask.Way.Wander,
                WanderRadius = 5f, GiveUpAfter = 3f, StoreIn = FOUND
            };
            float limit = find.GiveUpAfter.Get(rig.Context);
            float took = 0f;

            TaskStatus answer = Run(rig, find, 10f, () => took += DT);

            Assert.AreEqual(TaskStatus.Failure, answer, "Nothing Found");
            Assert.That(took, Is.EqualTo(limit).Within(2f * DT + FindTask.LOOK_EVERY), "At Give Up After");
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

        private PickableItem Rock(Vector3 at)
        {
            var rock = Make<PickableItem>("Rock", at);
            rock.Weapon = Weapon();
            _registry.Register(rock);
            return rock;
        }

        private AIObject Place(string name, Vector3 at, string kind)
        {
            var place = Make<AIObject>(name, at);
            place.Kind = kind;
            _registry.Register(place);
            return place;
        }

        private WeaponAsset Weapon()
        {
            WeaponAsset weapon = WeaponAsset.Create(new WeaponMove { Name = "Attack 1", Reach = 12f });
            weapon.Uses = 1;                                    // a rock: one throw (the weapon owns its uses)
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

        // The task ticked the way its node ticks it, the body moving as told in between, until it ends or time is out
        private static TaskStatus Run(GraphRig rig, AgentTask task, float seconds, System.Action eachTick = null)
        {
            TaskStatus answer = TaskStatus.Running;
            int steps = Mathf.CeilToInt(seconds / DT);
            for (int i = 0; i < steps && answer == TaskStatus.Running; i++)
            {
                rig.Context.Advance(DT);
                answer = task.Tick(rig.Context, DT);
                eachTick?.Invoke();
                rig.Actuator.Apply(DT);
            }

            return answer;
        }
    }
}
