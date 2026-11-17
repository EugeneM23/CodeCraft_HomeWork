using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// Interact (step 90, C15 — "the action node"): it runs the object's own scenario — the graph does not say what
    /// happens: a rock or a pile gives a weapon to throw (taken up from the world through the rack), a depot gives a box to
    /// carry, a turret is manned (its gun taken up, mounted, with its rounds) or loaded with the box in the hands, a
    /// cover, a perch or an AI Object is occupied facing the threat. It works with the object a wire brings into its
    /// Object pin; without one, or away from the object, it fails at once.
    /// </summary>
    public sealed class InteractTests
    {
        private const string OBJECT = "Object";
        private const string THREAT = "Threat";
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
        public void Rock_TakenAfterItsTakeTime_ItsWeaponFromTheWorldInTheHand()
        {
            GraphRig rig = Rig(Vector3.zero, out WorldRack rack);
            var rock = Make<PickableItem>("Rock", new Vector3(0.5f, 0f, 0f));
            rock.Weapon = Weapon();
            rock.Weapon.Uses = 1;
            rock.TakeTime = 0.4f;
            rig.Context.Blackboard.Set(OBJECT, rock.transform);
            var take = new InteractTask { Object = OBJECT, Interaction = Offer.Take };
            float took = 0f;

            TaskStatus answer = Run(rig, take, 2f, () => took += DT);

            Assert.AreEqual(TaskStatus.Success, answer, "Done");
            Assert.That(took, Is.EqualTo(rock.TakeTime).Within(2f * DT), "After the rock's own take time");
            Assert.AreEqual(1, rack.TakenUp.Count);
            Assert.AreSame(rock.Weapon, rack.TakenUp[0].Weapon, "Its weapon");
            Assert.AreSame(rock.transform, rack.TakenUp[0].Model, "The rock itself is the weapon's object");
            Assert.AreEqual(rock.Weapon.Uses, rack.TakenUp[0].Uses, "With its uses — the weapon's");
            Assert.IsFalse(rack.TakenUp[0].Mounted);
            Assert.AreSame(rock.Weapon, rig.Context.Weapon, "The weapon in the hand now");
        }

        [Test]
        public void Pile_HandsOutARock_TakenUpAsItsWeapon_OneLess()
        {
            GraphRig rig = Rig(Vector3.zero, out WorldRack rack);
            var pile = Make<ItemSource>("Pile", new Vector3(1f, 0f, 0f));
            var rock = Make<PickableItem>("Rock", new Vector3(50f, 0f, 0f));
            rock.Weapon = Weapon();
            pile.Item = rock.gameObject;
            pile.Stock = 3;
            pile.TakeTime = 0.2f;
            rig.Context.Blackboard.Set(OBJECT, pile.transform);

            Assert.AreEqual(TaskStatus.Success, Run(rig, new InteractTask { Object = OBJECT }, 2f));

            Assert.AreEqual(1, rack.TakenUp.Count, "A rock from the pile, taken up");
            Assert.AreNotSame(rock.transform, rack.TakenUp[0].Model, "A new one, not the pattern");
            _created.Add(rack.TakenUp[0].Model.gameObject);
            Assert.AreSame(rock.Weapon, rack.TakenUp[0].Weapon);
            Assert.AreEqual(2, pile.Stock, "One less in the pile");
        }

        [Test]
        public void Depot_GivesABoxToCarry_UntilItRunsOut()
        {
            GraphRig rig = Rig(Vector3.zero, out _);
            var depot = Make<ItemSource>("Depot", new Vector3(1f, 0f, 0f));
            depot.Stock = 1;
            depot.TakeTime = 0f;
            rig.Context.Blackboard.Set(OBJECT, depot.transform);

            Assert.AreEqual(TaskStatus.Success, Run(rig, new InteractTask { Object = OBJECT }, 1f));
            Transform box = rig.Agent.Item;
            Assert.IsNotNull(box, "A box in the hands");
            _created.Add(box.gameObject);
            Assert.AreEqual(0, depot.Stock);

            Items.PutDown(rig.Agent.LetGo(), Vector3.zero);
            Assert.AreEqual(TaskStatus.Failure, Run(rig, new InteractTask { Object = OBJECT }, 1f), "Run out: Failed");
        }

        [Test]
        public void Turret_Use_ItsGunTakenUpMounted_WithItsRounds_TheLoadPutDownFirst()
        {
            GraphRig rig = Rig(Vector3.zero, out WorldRack rack);
            Turret turret = NewTurret(rounds: 7, capacity: 10);
            rig.WakeUpAt(turret.SeatPosition);
            Transform box = Make<Transform>("Box", Vector3.zero);
            rig.Agent.Hold(box);
            rig.Context.Blackboard.Set(OBJECT, turret.transform);

            Assert.AreEqual(TaskStatus.Success, Run(rig, new InteractTask { Object = OBJECT, Interaction = Offer.Use }, 1f));

            Assert.AreEqual(1, rack.TakenUp.Count);
            Assert.AreSame(turret.Weapon, rack.TakenUp[0].Weapon, "The turret's gun");
            Assert.AreSame(turret.GunModel, rack.TakenUp[0].Model);
            Assert.IsTrue(rack.TakenUp[0].Mounted, "On its mount: the gun stays where it stands");
            Assert.AreEqual(7, rack.TakenUp[0].Uses, "Its rounds are the gun's uses");
            Assert.IsNull(rig.Agent.Item, "Both hands on the gun: the box was put down");
            Assert.AreSame(turret.transform, rig.Context.Occupied, "It mans the turret (Occupying)");

            rack.SpendUse();
            rack.SpendUse();
            Assert.AreEqual(5, turret.Ammo, "Two rounds fired from the gun");
            rack.LetGoFromWorld();
            Assert.AreEqual(5, turret.Ammo, "Left with rounds in the gun: they are the turret's again");
            Assert.IsNull(turret.Gunner);
            Assert.IsNull(rig.Context.Occupied);
        }

        [Test]
        public void Turret_ItsGunFiredEmpty_TheGunnerLetsGo_TheTurretIsEmpty_TheRoundsAreFired()
        {
            GraphRig rig = Rig(Vector3.zero, out WorldRack rack);
            Turret turret = NewTurret(rounds: 3, capacity: 10);
            rig.WakeUpAt(turret.SeatPosition);
            rig.Context.Blackboard.Set(OBJECT, turret.transform);
            int fired = 0;
            turret.Fired += (_, _, _) => fired++;
            Assert.AreEqual(TaskStatus.Success, Run(rig, new InteractTask { Object = OBJECT, Interaction = Offer.Use }, 1f));

            for (int round = 0; round < 3; round++)
            {
                rack.SpendUse();                                        // a round fired (by the gunner's Attack)
                Assert.AreEqual(2 - round, turret.Ammo, "The turret looks at its gun as it does every frame");
            }

            Assert.IsNull(rack.FromWorld, "Spent: the gun is let go, the gunner's own weapon is back");
            Assert.IsTrue(turret.IsEmpty, "Empty: nothing came back");
            Assert.AreEqual(3, fired, "Every round was fired (the Fired event, for a tracer)");
            Assert.IsNull(turret.Gunner);
            Assert.IsFalse(turret.Offers(Offer.Use, rig.Context), "Nothing to fire");
            Assert.IsTrue(turret.Offers(Offer.Load, rig.Context), "…a box is wanted");
        }

        [Test]
        public void Turret_TheGunnerWalksOffTheSeat_TheGunStaysOnItsMount_ItsRoundsComeBack()
        {
            GraphRig rig = Rig(Vector3.zero, out WorldRack rack);
            Turret turret = NewTurret(rounds: 8, capacity: 10);
            rig.WakeUpAt(turret.SeatPosition);
            rig.Context.Blackboard.Set(OBJECT, turret.transform);
            Assert.AreEqual(TaskStatus.Success, Run(rig, new InteractTask { Object = OBJECT, Interaction = Offer.Use }, 1f));
            rack.SpendUse();
            Assert.AreEqual(7, turret.Ammo, "Set-up: a round fired");

            rig.Actuator.Position = turret.SeatPosition + new Vector3(-4f, 0f, 0f);    // off to something else

            Assert.AreEqual(7, turret.Ammo, "The rounds in the gun are the turret's again");
            Assert.IsNull(rack.FromWorld, "Its rack let the gun go: the gun stays on its mount");
            Assert.IsNull(turret.Gunner);
            Assert.IsNull(rig.Context.Occupied, "It mans no turret");
            Assert.IsTrue(turret.Offers(Offer.Use, rig.Context), "Free for the next gunner");
        }

        [Test]
        public void Turret_Load_WithABoxInTheHands_FullAndTheBoxUsedUp_EmptyHands_Failed()
        {
            GraphRig rig = Rig(Vector3.zero, out _);
            Turret turret = NewTurret(rounds: 0, capacity: 10);
            turret.LoadTime = 0.3f;
            rig.WakeUpAt(turret.SeatPosition);
            rig.Context.Blackboard.Set(OBJECT, turret.transform);
            var load = new InteractTask { Object = OBJECT, Interaction = Offer.Load };

            Assert.AreEqual(TaskStatus.Failure, Run(rig, load, 1f), "Nothing to load it with");
            Assert.AreEqual(0, turret.Ammo);

            Transform box = Make<Transform>("Box", Vector3.zero);
            rig.Agent.Hold(box);
            float took = 0f;
            Assert.AreEqual(TaskStatus.Success, Run(rig, load, 2f, () => took += DT));
            Assert.That(took, Is.EqualTo(turret.LoadTime).Within(2f * DT), "After the turret's load time");
            Assert.IsTrue(turret.IsFull, "Full");
            Assert.IsNull(rig.Agent.Item, "The box is used up");
        }

        [Test]
        public void Cover_Occupied_FacingTheThreat_InCover_TheThreatWalksRound_NotAnyMore()
        {
            GraphRig rig = Rig(Vector3.zero, out _);
            Transform threat = Make<Transform>(THREAT, new Vector3(0f, 0f, 12f));
            var cover = Make<Cover>("Crate", new Vector3(0f, 0f, 3f));
            rig.WakeUpAt(cover.PlaceFor(Offer.Occupy, rig.Context, threat));
            rig.Context.Blackboard.Set(OBJECT, cover.transform);
            rig.Context.Blackboard.Set(THREAT, threat);

            Assert.AreEqual(TaskStatus.Success,
                Run(rig, new InteractTask { Object = OBJECT, Interaction = Offer.Occupy, Target = THREAT }, 1f));

            Assert.AreSame(cover.transform, rig.Context.Occupied, "Occupying");
            Assert.IsTrue(rig.Context.InCover, "In cover: the crate between it and the threat");
            Assert.Less(Vector3.Angle(rig.Actuator.Facing, threat.position - rig.Actuator.Position), 1f, "Facing the threat");

            threat.position = new Vector3(12f, 0f, 0f);                 // walked round the crate
            Assert.IsFalse(rig.Context.InCover, "The crate hides it no more");
            Assert.AreSame(cover.transform, rig.Context.Occupied, "…though it still stands at it");
        }

        [Test]
        public void PerchAndAIObject_Occupied_PerchOnlyFromItsTop()
        {
            GraphRig rig = Rig(new Vector3(5f, 0f, 0f), out _);
            var perch = Make<Perch>("Perch", Vector3.zero);
            perch.Height = 2.5f;
            rig.Context.Blackboard.Set(OBJECT, perch.transform);
            var occupy = new InteractTask { Object = OBJECT, Interaction = Offer.Occupy };

            Assert.AreEqual(TaskStatus.Failure, Run(rig, occupy, 0.5f), "On the ground, away from it: not there");

            rig.WakeUpAt(perch.TopCenter);
            Assert.AreEqual(TaskStatus.Success, Run(rig, occupy, 0.5f), "Up there");
            Assert.AreSame(perch.transform, rig.Context.Occupied);
            Assert.IsFalse(rig.Context.InCover, "A perch is no cover");

            var well = Make<AIObject>("Well", new Vector3(20f, 0f, 0f));
            well.transform.rotation = Quaternion.LookRotation(Vector3.left);
            rig.WakeUpAt(well.transform.position);
            rig.Context.Blackboard.Set(OBJECT, well.transform);
            Assert.AreEqual(TaskStatus.Success, Run(rig, occupy, 0.5f));
            Assert.AreSame(well.transform, rig.Context.Occupied, "One object at a time: the well now");
            Assert.IsFalse(perch.IsOccupiedBy(rig.Context), "The perch is let go");
            Assert.Less(Vector3.Angle(rig.Actuator.Facing, Vector3.left), 1f, "Facing the way the place looks");
        }

        [Test]
        public void NoObjectWired_OrTheVariableEmpty_FailsAtOnce()
        {
            GraphRig rig = Rig(Vector3.zero, out WorldRack rack);

            Assert.AreEqual(TaskStatus.Failure, new InteractTask().Tick(rig.Context, DT), "Nothing wired in");
            Assert.AreEqual(TaskStatus.Failure, new InteractTask { Object = OBJECT }.Tick(rig.Context, DT),
                "The variable holds no object");
            Assert.IsEmpty(rack.TakenUp);
        }

        // ---------- helpers ----------

        private GraphRig Rig(Vector3 at, out WorldRack rack)
        {
            var rig = new GraphRig(trace: false);
            rig.WakeUpAt(at);
            rig.UseObjects(_registry);
            rack = rig.UseWorldRack();
            return rig;
        }

        private Turret NewTurret(int rounds, int capacity)
        {
            var turret = Make<Turret>("Turret", new Vector3(10f, 0f, 0f));
            turret.transform.rotation = Quaternion.LookRotation(Vector3.right);
            turret.Weapon = Weapon();
            turret.Capacity = capacity;
            turret.Ammo = rounds;
            return turret;
        }

        private WeaponAsset Weapon()
        {
            WeaponAsset weapon = WeaponAsset.Create(new WeaponMove { Name = "Attack 1", Reach = 12f });
            weapon.Uses = 1;                                    // a rock: one throw (the weapon owns its uses)
            _created.Add(weapon);
            return weapon;
        }

        private T Make<T>(string name, Vector3 at) where T : Component
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            go.transform.position = at;
            return typeof(T) == typeof(Transform) ? go.transform as T : go.AddComponent<T>();
        }

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
