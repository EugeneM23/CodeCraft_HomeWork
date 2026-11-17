using System.Collections.Generic;
using AIKit.NavMeshNavigation;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// Things of the world in the hands (step 90, C14 — a rock is a weapon of one throw; handbook §24.1 — the thing says
    /// itself what it offers): a rock that flies offers nothing until it is gone where it lands (nobody runs for it in the
    /// air); a pile whose rock could not be taken after all keeps its stock; a load needs empty hands — a rock in the hand
    /// is no empty hand; a weapon taken from the world counts its uses on the character — the ones it is given, else the
    /// weapon's own, none — no end — and its model is held where a thing is carried when the character has no hand model.
    /// </summary>
    public sealed class WorldItemTests
    {
        private const string VICTIM = "Victim";
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
        public void Rock_InFlight_OffersNothing_FindSkipsIt_UntilItIsGone()
        {
            GraphRig thrower = Rig(Vector3.zero, worldRack: false);         // the character's own rack
            var rock = Make<PickableItem>("Rock", new Vector3(0.5f, 0f, 0f));
            rock.Weapon = RockWeapon();
            _registry.Register(rock);
            Assert.IsTrue(rock.GiveTo(thrower.Context), "Set-up: the rock is in the thrower's hand");
            thrower.Context.Launcher = ArrowLauncher.Shared;
            Transform victim = thrower.CreateTargetAt(new Vector3(0f, 0f, 8f));
            thrower.Context.Blackboard.Set(VICTIM, victim);
            thrower.SetGraph(GraphRig.OneState(new AttackTask { Target = VICTIM }));
            thrower.Instance.Start(thrower.Context);
            GraphRig other = Rig(new Vector3(4f, 0f, 4f), worldRack: true);    // 4 m off the flight path, empty hands

            thrower.StepSeconds(rock.Weapon.Find("").WindUp + 0.05f);       // thrown at the end of the wind-up

            var flight = rock.GetComponent<ArcFlight>();
            Assert.IsNotNull(flight, "Set-up: the rock itself flies");
            Assert.IsTrue(flight.IsFlying, "Set-up: in the air (a lob of 8 m at 14 m/s lasts about half a second)");
            Assert.IsNull(rock.Holder, "Set-up: nobody holds it");
            Assert.IsTrue(rock.IsFlying, "In the air: it says so");
            Assert.IsFalse(rock.Offers(Offer.Take, other.Context), "Nobody may take it in the air");
            var find = new FindTask { What = new WhatValue { Offer = Offer.Take, Radius = 30f }, StoreIn = FOUND };
            Assert.AreEqual(TaskStatus.Failure, find.Tick(other.Context, DT), "A search does not run for a rock in the air");
            Assert.IsNull(_registry.HolderOf(rock), "…nor claims it");

            flight.Fly(1f);

            Assert.IsTrue(flight.Ended, "Set-up: it came down");
            Assert.IsTrue(rock == null || !rock.Offers(Offer.Take, other.Context),
                "Came down: it is gone where it landed (C14; Play destroys it) — never offered");
        }

        [Test]
        public void Pile_ItsThingCannotBeTakenAfterAll_ItGoesBack_StockAsBefore()
        {
            GraphRig rig = Rig(Vector3.zero, worldRack: false);
            rig.Context.Rack = new RefusingRack();                          // a free hand that will not take it up
            var pile = Make<ItemSource>("Pile", new Vector3(0.5f, 0f, 0f));
            var pattern = Make<PickableItem>("Rock", new Vector3(50f, 0f, 0f));
            pattern.Weapon = RockWeapon();
            pile.Item = pattern.gameObject;
            pile.Stock = 2;
            pile.TakeTime = 0f;
            Assert.IsTrue(pile.Offers(Offer.Take, rig.Context), "Set-up: the hand looks free, the pile offers a rock");

            var interaction = new Interaction();
            interaction.Reset(rig.Context, Offer.Take, null);
            Assert.IsTrue(pile.Begin(interaction), "Set-up: begun");
            TaskStatus status = pile.Tick(interaction, DT);
            pile.End(interaction, ExitReason.Finished);

            Assert.AreEqual(TaskStatus.Failure, status, "Could not be taken after all: Failed");
            Assert.AreEqual(2, pile.Stock, "It went back into the pile: the stock is as before");
            Assert.IsTrue(interaction.Item == null, "The rock it handed out is gone (destroyed), not left lying about");
            Assert.IsTrue(pattern != null, "The pile's own pattern is untouched");
        }

        [Test]
        public void Box_WithARockInTheHand_IsNotTaken()
        {
            GraphRig rig = Rig(Vector3.zero, worldRack: false);
            Transform stone = Make<Transform>("Stone", Vector3.zero);
            Assert.IsTrue(rig.Agent.TakeUpFromWorld(RockWeapon(), stone, 0, false), "Set-up: a rock in the hand");
            var box = Make<PickableItem>("Box", new Vector3(0.3f, 0f, 0f));
            var depot = Make<ItemSource>("Depot", new Vector3(0.6f, 0f, 0f));

            Assert.IsFalse(box.Offers(Offer.Take, rig.Context), "A load needs empty hands: a rock in the hand is no empty hand");
            Assert.IsFalse(box.GiveTo(rig.Context));
            Assert.IsFalse(depot.Offers(Offer.Take, rig.Context), "…nor does a depot hand a box to it");
            Assert.IsNull(rig.Agent.Item, "Nothing in the hands");
            Assert.AreSame(stone, rig.Agent.FromWorld, "The rock is still in the hand");
        }

        [Test]
        public void TakeUpFromWorld_UsesFallBackToTheWeapons_NoneThere_NoEnd_NoHandModel_AtTheCarryOffset()
        {
            GraphRig rig = Rig(Vector3.zero, worldRack: false);
            WeaponAsset twice = RockWeapon();
            twice.Uses = 2;
            Transform first = Make<Transform>("First", new Vector3(3f, 0f, 3f));

            Assert.IsTrue(rig.Agent.TakeUpFromWorld(twice, first, 0, false));
            Assert.AreEqual(2, rig.Agent.UsesLeft, "Given none: the weapon's own Uses");
            Assert.AreSame(rig.Agent.transform, first.parent, "No hand model: held where a thing is carried");
            Assert.AreEqual(rig.Agent.CarryOffset, first.localPosition, "…at the carry offset");
            Assert.AreEqual(new Vector3(0f, 1.2f, 0.5f), rig.Agent.CarryOffset, "Set-up: the default offset");
            rig.Agent.LetGoFromWorld();

            WeaponAsset endless = RockWeapon();
            endless.Uses = 0;
            Assert.IsTrue(rig.Agent.TakeUpFromWorld(endless, Make<Transform>("Second", Vector3.zero), 0, false));
            Assert.AreEqual(-1, rig.Agent.UsesLeft, "None given and none of its own: no end");
            for (int i = 0; i < 5; i++)
                rig.Agent.SpendUse();
            Assert.AreSame(endless, rig.Context.Weapon, "Still in the hand after five moves");
            rig.Agent.LetGoFromWorld();

            Assert.IsTrue(rig.Agent.TakeUpFromWorld(twice, Make<Transform>("Third", Vector3.zero), 5, false));
            Assert.AreEqual(5, rig.Agent.UsesLeft, "Given uses win over the weapon's own");
        }

        // ---------- helpers ----------

        /// <summary>A rack whose hand looks free but takes nothing up from the world.</summary>
        private sealed class RefusingRack : IWeaponRack
        {
            public IReadOnlyList<WeaponAsset> Carried { get; } = new List<WeaponAsset>();

            public WeaponAsset Find(string weapon) => null;

            public void PutAway(WeaponAsset weapon)
            {
            }

            public void ShowMotionsOf(WeaponAsset weapon)
            {
            }

            public WeaponAsset After(WeaponAsset weapon) => null;

            public void TakeUp(WeaponAsset weapon)
            {
            }

            public bool TakeUpFromWorld(WeaponAsset weapon, Transform model, int uses, bool mounted) => false;

            public Transform FromWorld => null;

            public int UsesLeft => -1;

            public void SpendUse()
            {
            }

            public Transform LetGoFromWorld() => null;
        }

        // The lab's rock: one move thrown in a lob, its own model flies, one use
        private WeaponAsset RockWeapon()
        {
            WeaponAsset rock = WeaponAsset.Create(new WeaponMove
            {
                Name = "Attack 1", Reach = 12f, WindUp = 0.5f, Recovery = 0.4f, Damage = 1f
            });
            rock.name = "Rock";
            rock.Shoots = true;
            rock.Uses = 1;
            rock.Shot.ThrowsModel = true;
            rock.Shot.Arc = 0.15f;
            rock.Shot.Speed = 14f;
            rock.Shot.HitRadius = 1f;
            rock.Shot.Pool = "";
            _created.Add(rock);
            return rock;
        }

        private GraphRig Rig(Vector3 at, bool worldRack)
        {
            var rig = new GraphRig(trace: false);
            rig.WakeUpAt(at);
            rig.UseObjects(_registry);
            if (worldRack)
                rig.UseWorldRack();
            return rig;
        }

        private T Make<T>(string name, Vector3 at) where T : Component
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            go.transform.position = at;
            return typeof(T) == typeof(Transform) ? go.transform as T : go.AddComponent<T>();
        }
    }
}
