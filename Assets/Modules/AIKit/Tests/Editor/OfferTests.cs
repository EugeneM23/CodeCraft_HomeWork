using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// What each object of the world offers whom, as its own say at the moment of asking (handbook §24.1; step 90): a rock
    /// offers Take while it lies about and to a free hand; a pile offers Take to everybody until it runs out; a turret
    /// offers Use while it has rounds and nobody else mans it, Load while it is not full; a cover, a perch offer Occupy to
    /// one at a time; an AI Object up to its capacity.
    /// </summary>
    public sealed class OfferTests
    {
        private readonly List<Object> _created = new();

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
        public void Rock_OffersTakeWhileItLiesAbout_NotWhileHeld_NotToAHandHoldingAnotherOrALoad()
        {
            GraphRig me = Rig(Vector3.zero);
            GraphRig other = Rig(Vector3.zero);
            WorldRack rack = me.UseWorldRack();
            other.UseWorldRack();
            var rock = Make<PickableItem>("Rock", Vector3.zero);
            rock.Weapon = Weapon();
            var second = Make<PickableItem>("Rock 2", Vector3.zero);
            second.Weapon = rock.Weapon;

            Assert.IsTrue(rock.Offers(Offer.Take, me.Context), "A rock offers Take");
            Assert.IsFalse(rock.Offers(Offer.Occupy, me.Context), "…and nothing else");
            Assert.IsTrue(rock.OneAtATime(Offer.Take), "One goes for it at a time");

            Assert.IsTrue(rock.GiveTo(me.Context), "Taken");
            Assert.AreSame(rock.transform, rack.FromWorld);
            Assert.AreSame(me.Agent, rock.Holder);
            Assert.IsFalse(rock.Offers(Offer.Take, other.Context), "Held: nobody else's");
            Assert.IsFalse(second.Offers(Offer.Take, me.Context), "A hand that holds a rock takes no second one");

            Transform box = Make<Transform>("Box", Vector3.zero);
            other.Agent.Hold(box);
            Assert.IsFalse(second.Offers(Offer.Take, other.Context), "Hands full of a box: no rock");

            rack.SpendUse();                                    // thrown: used up
            Assert.IsNull(rock.Holder, "A thing its holder no longer holds is nobody's");
            Assert.IsFalse(rock.Offers(Offer.Take, other.Context), "Used up (gone): offers nothing");
        }

        [Test]
        public void Box_IsCarried_OffersTakeToEmptyHandsOnly()
        {
            GraphRig me = Rig(Vector3.zero);
            var box = Make<PickableItem>("Box", Vector3.zero);
            var other = Make<PickableItem>("Box 2", Vector3.zero);

            Assert.IsTrue(box.Offers(Offer.Take, me.Context));
            Assert.IsTrue(box.GiveTo(me.Context));
            Assert.AreSame(box.transform, me.Agent.Item, "Carried in the hands");
            Assert.IsFalse(other.Offers(Offer.Take, me.Context), "Full hands take nothing");

            Items.PutDown(me.Agent.LetGo(), Vector3.zero);
            Assert.IsTrue(box.Offers(Offer.Take, me.Context), "Put down: it may be taken again");
        }

        [Test]
        public void Pile_OffersTakeToEverybody_UntilItRunsOut()
        {
            GraphRig me = Rig(Vector3.zero);
            GraphRig other = Rig(Vector3.zero);
            me.UseWorldRack();
            other.UseWorldRack();
            var pile = Make<ItemSource>("Pile", Vector3.zero);
            var rock = Make<PickableItem>("Rock", Vector3.zero);
            rock.Weapon = Weapon();
            pile.Item = rock.gameObject;
            pile.Stock = 1;

            Assert.IsFalse(pile.OneAtATime(Offer.Take), "A pile serves everybody at once: nobody claims it");
            Assert.IsTrue(pile.Offers(Offer.Take, me.Context) && pile.Offers(Offer.Take, other.Context));

            PickableItem handed = pile.HandOut();
            _created.Add(handed.gameObject);
            Assert.AreSame(rock.Weapon, handed.Weapon, "It hands out a copy of its thing: a rock to throw");
            Assert.AreEqual(0, pile.Stock);
            Assert.IsFalse(pile.Offers(Offer.Take, other.Context), "Run out: offers nothing");

            pile.Stock = -1;
            Assert.IsTrue(pile.Offers(Offer.Take, other.Context), "Below 0: never runs out");
        }

        [Test]
        public void Turret_WithRounds_OffersUseAndLoad_Empty_LoadNotUse_Full_NoLoad_MannedByAnother_NoUse()
        {
            GraphRig gunner = Rig(Vector3.zero);
            GraphRig other = Rig(Vector3.zero);
            gunner.UseWorldRack();
            other.UseWorldRack();
            Turret turret = NewTurret(Vector3.zero, rounds: 5, capacity: 10);

            Assert.IsTrue(turret.Offers(Offer.Use, gunner.Context), "Rounds and nobody at it: Use");
            Assert.IsTrue(turret.Offers(Offer.Load, gunner.Context), "Not full: Load");
            Assert.IsTrue(turret.OneAtATime(Offer.Use), "One gunner goes for it");
            Assert.IsFalse(turret.OneAtATime(Offer.Load), "A box may be brought by anybody");

            turret.Ammo = 10;
            Assert.IsFalse(turret.Offers(Offer.Load, gunner.Context), "Full: no Load");

            turret.Ammo = 0;
            Assert.IsFalse(turret.Offers(Offer.Use, gunner.Context), "Empty: no Use");
            Assert.IsTrue(turret.Offers(Offer.Load, gunner.Context), "…but Load");

            turret.Ammo = 6;
            Assert.IsTrue(Interact(gunner, turret, Offer.Use), "Manned");
            Assert.AreSame(gunner.Agent, turret.Gunner);
            Assert.IsTrue(turret.Offers(Offer.Use, gunner.Context), "Its gunner still may");
            Assert.IsFalse(turret.Offers(Offer.Use, other.Context), "Manned by another: no Use");
            Assert.AreEqual(6, turret.Ammo, "The rounds are in the gun now — still the turret's rounds");
        }

        [Test]
        public void Cover_TakenByOne_OffersNothingToAnother_FreeAgainWhenItWalksOff()
        {
            GraphRig me = Rig(Vector3.zero);
            GraphRig other = Rig(new Vector3(3f, 0f, 0f));
            Transform threat = Make<Transform>("Threat", new Vector3(0f, 0f, 10f));
            var cover = Make<Cover>("Crate", new Vector3(0f, 0f, 2f));
            me.WakeUpAt(cover.PlaceFor(Offer.Occupy, me.Context, threat));

            Assert.IsTrue(cover.Offers(Offer.Occupy, me.Context) && cover.Offers(Offer.Occupy, other.Context));
            Assert.IsTrue(Interact(me, cover, Offer.Occupy, threat), "Taken");
            Assert.IsFalse(cover.Offers(Offer.Occupy, other.Context), "Taken by one: nothing for another");
            Assert.IsTrue(cover.Offers(Offer.Occupy, me.Context), "…still its own");

            me.Actuator.Position = new Vector3(15f, 0f, 0f);            // walked off
            Assert.IsTrue(cover.Offers(Offer.Occupy, other.Context), "Free again");
            Assert.IsNull(me.Context.Occupied, "And it occupies nothing");
        }

        [Test]
        public void Perch_OneAtATime_AIObject_UpToItsCapacity()
        {
            GraphRig first = Rig(Vector3.zero);
            GraphRig second = Rig(Vector3.zero);
            var perch = Make<Perch>("Perch", Vector3.zero);
            perch.Height = 0f;                                          // its top at the feet: no climbing in this test
            Assert.IsTrue(Interact(first, perch, Offer.Occupy));
            Assert.IsFalse(perch.Offers(Offer.Occupy, second.Context), "One on a perch");

            var well = Make<AIObject>("Well", Vector3.zero);
            well.Capacity = 2;
            GraphRig third = Rig(Vector3.zero);
            Assert.IsTrue(Interact(first, well, Offer.Occupy) && Interact(second, well, Offer.Occupy));
            Assert.AreEqual(2, well.OccupantCount);
            Assert.IsFalse(well.Offers(Offer.Occupy, third.Context), "Full at its capacity");

            well.Capacity = 0;
            Assert.IsTrue(well.Offers(Offer.Occupy, third.Context), "0 — any number");
            Assert.IsFalse(well.OneAtATime(Offer.Occupy), "Nobody claims a place for any number");
        }

        // ---------- helpers ----------

        private GraphRig Rig(Vector3 at)
        {
            var rig = new GraphRig(trace: false);
            rig.WakeUpAt(at);
            return rig;
        }

        // The object's scenario run to its end for the agent (an Interaction as Interact keeps it)
        private static bool Interact(GraphRig rig, WorldObject target, Offer offer, Transform threat = null)
        {
            var interaction = new Interaction();
            interaction.Reset(rig.Context, offer, threat);
            interaction.MayFace = true;
            if (!target.Begin(interaction))
                return false;

            TaskStatus status = TaskStatus.Running;
            for (int i = 0; i < 500 && status == TaskStatus.Running; i++)
            {
                status = target.Tick(interaction, 0.02f);
                interaction.Time += 0.02f;
            }

            target.End(interaction, ExitReason.Finished);
            return status == TaskStatus.Success;
        }

        private Turret NewTurret(Vector3 at, int rounds, int capacity)
        {
            var turret = Make<Turret>("Turret", at);
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
    }
}
