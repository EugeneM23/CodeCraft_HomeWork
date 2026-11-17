using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// A weapon taken from the world for a while (step 90, C14): a rock in the hand is the weapon the Attack node strikes
    /// with — the character's own weapon on the back meanwhile, as for a box in the hands, and back in the hand when the
    /// rock is spent; its moves are counted on the character; the rock itself is what flies; a move cut short spends
    /// nothing; the Switch / Change Weapon nodes never see it; a gun on a stand shoots from its model and stays where it
    /// stands when it is empty.
    /// </summary>
    public sealed class WorldWeaponTests
    {
        private const string VICTIM = "Victim";
        private const float DT = 0.02f;

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

        private static WeaponMove Throw() => new() { Name = "Attack 1", Reach = 12f, WindUp = 0.5f, Recovery = 0.4f, Damage = 1f };

        // The lab's rock: one move, its own model flies in a lob, one use
        private WeaponAsset Rock()
        {
            WeaponAsset rock = WeaponAsset.Create(Throw());
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

        private Transform Thing(string name)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            return go.transform;
        }

        // A character with a sword in the hand (its models: in the hand — under the hand's bone — and on the back)
        private GraphRig Armed(out WeaponAsset sword, out GameObject inHand, out GameObject onBack, out Transform hand)
        {
            sword = WeaponAsset.Create(new WeaponMove { Name = "Attack 1", Reach = 2f, WindUp = 0.3f, Recovery = 0.3f });
            sword.name = "Sword";
            var rig = new GraphRig();
            hand = Thing("Hand");
            hand.SetParent(rig.Agent.transform, false);
            inHand = Thing("Sword In Hand").gameObject;
            inHand.transform.SetParent(hand, false);
            onBack = Thing("Sword On Back").gameObject;
            rig.GiveWeapon(sword);
            rig.Carry(sword, inHand, onBack);
            return rig;
        }

        private static HitProbe Victim(GraphRig rig, Vector3 at)
        {
            Transform victim = rig.CreateTargetAt(at);
            rig.Context.Blackboard.Set(VICTIM, victim);
            return victim.gameObject.AddComponent<HitProbe>();
        }

        private static void Attack(GraphRig rig)
        {
            rig.SetGraph(GraphRig.OneState(new AttackTask { Target = VICTIM }));
            rig.Instance.Start(rig.Context);
        }

        [Test]
        public void Rock_InTheHand_TheOwnWeaponIsOnTheBack_TheRockIsInTheHand()
        {
            GraphRig rig = Armed(out WeaponAsset sword, out GameObject inHand, out GameObject onBack, out Transform hand);
            WeaponAsset rock = Rock();
            Transform stone = Thing("Stone");

            Assert.IsTrue(rig.Agent.TakeUpFromWorld(rock, stone, 0, false));

            Assert.AreSame(rock, rig.Context.Weapon, "The Attack node strikes with the rock now");
            Assert.AreSame(sword, rig.Agent.Weapon, "…the own weapon is still the character's, on the back");
            Assert.IsFalse(inHand.activeSelf, "The sword's model is not in the hand");
            Assert.IsTrue(onBack.activeSelf, "…it is on the back, as for a box in the hands");
            Assert.AreSame(hand, stone.parent, "The rock is in the hand that held the sword");
            Assert.AreEqual(rock.Uses, rig.Agent.UsesLeft, "Its moves are counted on the character");
            Assert.IsFalse(rig.Agent.TakeUpFromWorld(Rock(), Thing("Another"), 0, false), "One weapon from the world at a time");
        }

        [Test]
        public void Rock_ItsMoveDelivered_ItIsSpent_TheRockFliesAndIsGone_TheOwnWeaponIsBack()
        {
            GraphRig rig = Armed(out WeaponAsset sword, out GameObject inHand, out _, out _);
            HitProbe victim = Victim(rig, new Vector3(0f, 0f, 8f));
            WeaponAsset rock = Rock();
            Transform stone = Thing("Stone");
            rig.Agent.TakeUpFromWorld(rock, stone, 0, false);
            var launcher = new Capture();
            rig.Context.Launcher = launcher;
            Attack(rig);

            rig.StepSeconds(Throw().WindUp - 0.05f);
            Assert.IsEmpty(launcher.Launches, "Not before the wind-up");
            rig.StepSeconds(0.1f);

            Assert.AreEqual(1, launcher.Launches.Count, "Thrown at the end of the wind-up");
            Assert.AreSame(stone, launcher.Launches[0].Carried, "The rock itself flies (its last move)");
            Assert.AreEqual(rock.Shot.Arc, launcher.Launches[0].Arc, "…in the rock's lob");
            Assert.AreSame(sword, rig.Context.Weapon, "Spent: the own weapon is back in the hand");
            Assert.IsTrue(inHand.activeSelf);
            Assert.AreEqual(-1, rig.Agent.UsesLeft);
            Assert.IsNull(rig.Agent.FromWorld);
            Assert.IsTrue(stone != null, "A rock that flew is the flight's: it is gone where it lands, not in the hand");
            Assert.AreEqual(0, victim.Hits, "The fake flight has not come down");
        }

        [Test]
        public void Rock_NoLauncher_LandsAtOnce_HitsTheTarget_AndIsUsedUp()
        {
            GraphRig rig = Armed(out WeaponAsset sword, out _, out _, out _);
            HitProbe victim = Victim(rig, new Vector3(0f, 0f, 8f));
            WeaponAsset rock = Rock();
            Transform stone = Thing("Stone");
            rig.Agent.TakeUpFromWorld(rock, stone, 0, false);
            rig.Context.Launcher = null;
            Attack(rig);

            rig.StepSeconds(Throw().WindUp + 0.05f);

            Assert.AreEqual(1, victim.Hits);
            Assert.AreEqual(Throw().Damage, victim.LastDamage);
            Assert.IsTrue(stone == null, "Thrown, the rock is gone (C14): it does not lie about to be picked up again");
            Assert.AreSame(sword, rig.Context.Weapon);
        }

        [Test]
        public void Rock_AMoveCutShort_SpendsNothing()
        {
            GraphRig rig = Armed(out _, out _, out _, out Transform hand);
            Victim(rig, new Vector3(0f, 0f, 8f));
            WeaponAsset rock = Rock();
            Transform stone = Thing("Stone");
            rig.Agent.TakeUpFromWorld(rock, stone, 0, false);
            var launcher = new Capture();
            rig.Context.Launcher = launcher;
            Attack(rig);

            rig.StepSeconds(Throw().WindUp * 0.5f);
            rig.Instance.Stop(rig.Context);                     // taken away in the middle of the swing

            Assert.IsEmpty(launcher.Launches);
            Assert.AreSame(rock, rig.Context.Weapon, "Nothing delivered: the rock is still in the hand");
            Assert.AreEqual(1, rig.Agent.UsesLeft);
            Assert.AreSame(hand, stone.parent);
        }

        [Test]
        public void Rock_SwitchWeapon_ChangesTheOwnWeaponBehindIt_AtOnce_TheRockStaysInTheHand()
        {
            GraphRig rig = Armed(out WeaponAsset sword, out _, out _, out _);
            WeaponAsset bow = WeaponAsset.Create(new WeaponMove { Name = "Attack 1", Reach = 14f });
            bow.name = "Bow";
            bow.DrawAction = "Bow Draw";
            _created.Add(bow);
            rig.Carry(bow);
            WeaponAsset rock = Rock();
            rig.Agent.TakeUpFromWorld(rock, Thing("Stone"), 0, false);
            var switching = new SwitchWeaponTask { Weapon = SwitchWeaponTask.NEXT };
            rig.SetGraph(GraphRig.OneState(switching));
            rig.Instance.Start(rig.Context);

            rig.Step(DT, 3);

            Assert.AreEqual(TaskStatus.Success, switching.StatusOf(rig.Context), "Done at once: the hand is the rock's");
            Assert.AreSame(rock, rig.Context.Weapon, "The rock is no weapon of the character's: it stays in the hand");
            Assert.AreSame(bow, rig.Agent.Weapon, "The own weapon after the sword is the bow now, on the back");
            CollectionAssert.AreEquivalent(new[] { sword, bow }, rig.Agent.Carried, "The rock is not carried");

            rig.Agent.LetGoFromWorld();
            Assert.AreSame(bow, rig.Context.Weapon, "Let go: the own weapon comes back — the bow");
        }

        [Test]
        public void Rock_ChangeWeapon_SeesOnlyTheCharactersOwnWeapons()
        {
            GraphRig rig = Armed(out WeaponAsset sword, out _, out _, out _);
            rig.Agent.TakeUpFromWorld(Rock(), Thing("Stone"), 0, false);
            var change = new ChangeWeaponReasoner { BackWhenCalm = true };

            bool holds = change.Holds(new ReasonerQuery(rig.Context, TargetFacts.None, false), out string why);

            Assert.IsFalse(holds, "One weapon of its own: nothing to change, and the rock in the hand is no reason to");
            Assert.AreEqual("one weapon only", why);
            Assert.AreEqual(1, rig.Agent.Carried.Count);
            Assert.AreEqual(0, rig.Agent.WeaponIndex, "The animator's weapon is the own one's");
        }

        [Test]
        public void Rock_LetGo_ItIsPutDown_TheOwnWeaponIsBack()
        {
            GraphRig rig = Armed(out WeaponAsset sword, out GameObject inHand, out _, out _);
            Transform stone = Thing("Stone");
            stone.gameObject.AddComponent<PickableItem>();
            rig.Agent.TakeUpFromWorld(Rock(), stone, 0, false);

            Assert.AreSame(stone, rig.Agent.LetGoFromWorld());

            Assert.IsNull(stone.parent, "Put down where the character stands");
            Assert.AreSame(sword, rig.Context.Weapon);
            Assert.IsTrue(inHand.activeSelf);
            Assert.IsNull(rig.Agent.LetGoFromWorld(), "Nothing held: nothing to let go");
        }

        [Test]
        public void Rock_WithABoxInTheHands_IsNotTaken()
        {
            GraphRig rig = Armed(out WeaponAsset sword, out _, out _, out _);
            rig.Agent.Hold(Thing("Box"));

            Assert.IsFalse(rig.Agent.TakeUpFromWorld(Rock(), Thing("Stone"), 0, false), "The hands are full: put it down first");
            Assert.AreSame(sword, rig.Context.Weapon);
        }

        [Test]
        public void Turret_Mounted_ShootsFromItsModel_AndEmpty_StaysWhereItStands()
        {
            GraphRig rig = Armed(out WeaponAsset sword, out _, out _, out _);
            Victim(rig, new Vector3(0f, 0f, 10f));
            WeaponAsset gun = WeaponAsset.Create(new WeaponMove { Name = "Attack 1", Reach = 20f, WindUp = 0.1f, Recovery = 0.1f });
            gun.Shoots = true;
            gun.Shot.Pool = "";
            _created.Add(gun);
            Transform head = Thing("Turret Head");
            head.position = new Vector3(1f, 1f, 0f);
            const int ROUNDS = 2;
            rig.Agent.TakeUpFromWorld(gun, head, ROUNDS, true);
            var launcher = new Capture();
            rig.Context.Launcher = launcher;
            Attack(rig);

            Assert.IsTrue(rig.StepUntil(() => launcher.Launches.Count == ROUNDS, 3f));

            Assert.AreSame(head, launcher.Launches[0].Origin, "The shot leaves from the gun's model");
            Assert.IsNull(launcher.Launches[0].Carried, "Nothing leaves the hand");
            Assert.IsNull(head.parent, "A mounted gun is never in the hand");
            Assert.IsTrue(head != null, "Empty, it stays where it stands — only let go");
            Assert.AreSame(sword, rig.Context.Weapon, "Its rounds spent: the own weapon is back");
        }

        /// <summary>A launcher that only writes the launches down.</summary>
        private sealed class Capture : IProjectileLauncher
        {
            public readonly List<ProjectileLaunch> Launches = new();

            public void Launch(in ProjectileLaunch launch) => Launches.Add(launch);
        }
    }
}
