using System.Collections.Generic;
using System.Reflection;
using AIKit.NavMeshNavigation;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// A weapon taken from the world, at its corners (the step 90 recheck of the fight): a character that dies (its AI
    /// Character switched off, the lab's way) lets go of it — the rock lies there to be taken again, the turret's gun is
    /// left on its mount with its rounds; a rock in the air is offered to nobody; the rock that flies is the flight's
    /// whatever the launcher does with it; the clips shown are the weapon's in the hand — the rock's while it is held, the
    /// own weapon's again after; a rock of three moves throws two copies and then itself; uses count only a weapon taken
    /// from the world.
    /// </summary>
    public sealed class CombatWorldWeaponTests
    {
        private const string VICTIM = "Victim";
        private const float DT = 0.02f;

        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (ArcFlight flight in Object.FindObjectsByType<ArcFlight>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(flight.gameObject);
            foreach (Object created in _created)
            {
                if (created != null)
                    Object.DestroyImmediate(created);
            }

            _created.Clear();
            GraphRig.DestroyAll();
        }

        private static WeaponMove Throw() =>
            new() { Name = "Attack 1", Reach = 12f, WindUp = 0.5f, Recovery = 0.4f, Damage = 1f };

        // The lab's rock: one move, its own model flies in a lob, one use
        private WeaponAsset Rock(int uses = 1)
        {
            WeaponAsset rock = WeaponAsset.Create(Throw());
            rock.name = "Rock";
            rock.Shoots = true;
            rock.Uses = uses;
            rock.Shot.ThrowsModel = true;
            rock.Shot.Arc = 0.15f;
            rock.Shot.Speed = 14f;
            rock.Shot.HitRadius = 1f;
            rock.Shot.Pool = "";
            _created.Add(rock);
            return rock;
        }

        private Transform Thing(string name, Vector3 at = default)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            go.transform.position = at;
            return go.transform;
        }

        // A rock lying about that gives this weapon
        private PickableItem Stone(WeaponAsset rock)
        {
            PickableItem item = Thing("Stone").gameObject.AddComponent<PickableItem>();
            item.Weapon = rock;
            return item;
        }

        // A character with a sword in the hand (its models: in the hand — under the hand's bone — and on the back)
        private GraphRig Armed(out WeaponAsset sword, out GameObject inHand)
        {
            sword = WeaponAsset.Create(new WeaponMove { Name = "Attack 1", Reach = 2f, WindUp = 0.3f, Recovery = 0.3f });
            sword.name = "Sword";
            var rig = new GraphRig(trace: false);
            Transform hand = Thing("Hand");
            hand.SetParent(rig.Agent.transform, false);
            inHand = Thing("Sword In Hand").gameObject;
            inHand.transform.SetParent(hand, false);
            GameObject onBack = Thing("Sword On Back").gameObject;
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

        // The character switched off, the lab's way of dying (LabLife.Fall). Edit mode sends a component no OnDisable (the
        // AI Character is no ExecuteAlways script): the engine's message is sent here by hand, as Play sends it
        internal static void SwitchOff(AICharacter character)
        {
            Assume.That(character.gameObject.activeInHierarchy, "Set-up: its object stays in the world");
            character.enabled = false;
            typeof(AICharacter).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(character, null);
        }

        // The Use scenario of the turret, run to its end (as Interact runs it)
        private static bool Man(GraphRig rig, Turret turret)
        {
            var interaction = new Interaction();
            interaction.Reset(rig.Context, Offer.Use, null);
            interaction.MayFace = true;
            if (!turret.Begin(interaction))
                return false;

            TaskStatus status = turret.Tick(interaction, DT);
            turret.End(interaction, ExitReason.Finished);
            return status == TaskStatus.Success;
        }

        // ---------- the holder dies ----------

        [Test]
        public void WorldWeapon_TheCharacterIsDisabled_LetsGoOfTheRock()
        {
            GraphRig rig = Armed(out WeaponAsset sword, out GameObject inHand);
            rig.Agent.gameObject.SetActive(true);
            PickableItem stone = Stone(Rock());
            Assert.IsTrue(stone.GiveTo(rig.Context), "Set-up: the rock is in the hand");
            var other = new GraphRig(trace: false);

            SwitchOff(rig.Agent);                               // the lab's death (LabLife.Fall)

            Assert.IsNull(rig.Agent.FromWorld, "A dead hand holds no rock");
            Assert.AreEqual(-1, rig.Agent.UsesLeft);
            Assert.IsNull(stone.transform.parent, "Put down where the character fell");
            Assert.AreSame(sword, rig.Context.Weapon, "The own weapon is the character's again");
            Assert.IsTrue(inHand.activeSelf);
            Assert.IsNull(stone.Holder, "Nobody's");
            Assert.IsTrue(stone.Offers(Offer.Take, other.Context), "Free to be taken by another");
        }

        [Test]
        public void WorldWeapon_TheGunnerIsDisabled_TheGunIsLeft_ItsRoundsBack_FreeForAnother()
        {
            const int ROUNDS = 3;
            GraphRig rig = Armed(out WeaponAsset sword, out _);
            var turret = Thing("Turret", new Vector3(0f, 0f, 5f)).gameObject.AddComponent<Turret>();
            turret.Weapon = WeaponAsset.Create(new WeaponMove { Name = "Attack 1", Reach = 20f, WindUp = 0.1f, Recovery = 0.1f });
            turret.Weapon.Shoots = true;
            turret.Weapon.Shot.Pool = "";
            _created.Add(turret.Weapon);
            turret.Capacity = ROUNDS;
            turret.Ammo = ROUNDS;
            rig.WakeUpAt(turret.SeatPosition);
            rig.Agent.gameObject.SetActive(true);
            Assert.IsTrue(Man(rig, turret), "Set-up: manned");
            Assert.AreSame(rig.Agent, turret.Gunner);
            var other = new GraphRig(trace: false);
            other.WakeUpAt(turret.SeatPosition + Vector3.left);

            SwitchOff(rig.Agent);                               // the gunner dies at its seat

            Assert.IsNull(turret.Gunner, "A dead gunner mans nothing");
            Assert.AreEqual(ROUNDS, turret.Ammo, "The rounds in its gun come back to the turret");
            Assert.IsTrue(turret.Offers(Offer.Use, other.Context), "Another may man it");
            Assert.AreSame(sword, rig.Context.Weapon);
        }

        // ---------- the rock in the air ----------

        [Test]
        public void Rock_InTheAir_IsNotOfferedForTaking()
        {
            GraphRig rig = Armed(out _, out _);
            rig.Context.Launcher = ArrowLauncher.Shared;
            PickableItem stone = Stone(Rock());
            Assert.IsTrue(stone.GiveTo(rig.Context));
            Victim(rig, new Vector3(0f, 0f, 8f));
            var other = new GraphRig(trace: false);
            Attack(rig);

            rig.StepSeconds(Throw().WindUp + 0.05f);

            var flight = stone.GetComponent<ArcFlight>();
            Assert.IsNotNull(flight, "Set-up: the rock itself flies");
            Assert.IsTrue(flight.IsFlying, "Set-up: it is in the air");
            Assert.IsFalse(stone.Offers(Offer.Take, other.Context), "Nobody may take it until it lands");
        }

        [Test]
        public void Rock_ALauncherThatLeavesItInTheHand_TheThrownRockIsNotDestroyed()
        {
            GraphRig rig = Armed(out WeaponAsset sword, out _);
            Victim(rig, new Vector3(0f, 0f, 8f));
            Transform stone = Thing("Stone");
            rig.Agent.TakeUpFromWorld(Rock(), stone, 0, false);
            var launcher = new CombatCaptureLauncher();           // it only writes the launch down
            rig.Context.Launcher = launcher;
            Attack(rig);

            rig.StepSeconds(Throw().WindUp + 0.05f);

            Assert.AreEqual(1, launcher.Launches.Count, "Set-up: thrown");
            Assert.AreSame(sword, rig.Context.Weapon, "Set-up: spent, the own weapon is back");
            Assert.IsTrue(stone != null, "The rock that was let go is the flight's: the spent weapon does not use it up");
            Assert.IsTrue(launcher.Launches[0].Carried != null, "The launcher is handed a live model");
            Assert.IsNull(stone.parent, "It left the hand when it was let go");
        }

        // ---------- the moves of a weapon ----------

        [Test]
        public void Rock_ThreeUses_TwoCopiesFly_ThenTheRockItself_OwnWeaponBack()
        {
            GraphRig rig = Armed(out WeaponAsset sword, out _);
            Victim(rig, new Vector3(0f, 0f, 8f));
            Transform stone = Thing("Stone");
            rig.Agent.TakeUpFromWorld(Rock(uses: 3), stone, 0, false);
            Transform hand = stone.parent;
            var launcher = new CombatCaptureLauncher { TakeOut = true };    // takes the model out, as the 3D launcher
            rig.Context.Launcher = launcher;
            Attack(rig);

            var usesAfter = new List<int>();
            for (int i = 0; i < Mathf.CeilToInt(3f / DT) && launcher.Launches.Count < 3; i++)
            {
                int before = launcher.Launches.Count;
                rig.Step(DT);
                if (launcher.Launches.Count == before)
                    continue;

                usesAfter.Add(rig.Agent.UsesLeft);
                if (launcher.Launches.Count < 3)
                    Assert.AreSame(hand, stone.parent, $"After throw {launcher.Launches.Count} the rock is still in the hand");
            }

            foreach (ProjectileLaunch launch in launcher.Launches)
            {
                if (launch.Carried != null && launch.Carried != stone)
                    _created.Add(launch.Carried.gameObject);
            }

            Assert.AreEqual(3, launcher.Launches.Count, "Three moves");
            for (int i = 0; i < 2; i++)
            {
                Assert.AreNotSame(stone, launcher.Launches[i].Carried, $"Throw {i + 1}: a copy flies");
                Assert.AreEqual(stone.name, launcher.Launches[i].Carried.name, "…of the rock");
            }

            Assert.AreSame(stone, launcher.Launches[2].Carried, "The last throw: the rock itself");
            CollectionAssert.AreEqual(new[] { 2, 1, -1 }, usesAfter, "Counted down on the character, none left at the end");
            Assert.AreSame(sword, rig.Context.Weapon, "Spent: the own weapon is back");
            Assert.IsTrue(stone != null, "The rock that flew is the flight's");
        }

        [Test]
        public void OwnWeaponWithUses_IsNeverSpent()
        {
            var rig = new GraphRig(trace: false);
            WeaponAsset sword = WeaponAsset.Create(new WeaponMove { Name = "Attack 1", Reach = 2f, WindUp = 0.2f, Recovery = 0.2f });
            sword.Uses = 1;
            rig.GiveWeapon(sword);
            HitProbe hits = Victim(rig, new Vector3(0f, 0f, 1f));
            Attack(rig);

            rig.StepSeconds(1.5f);

            Assert.GreaterOrEqual(hits.Hits, 3, "Blow after blow");
            Assert.AreSame(sword, rig.Context.Weapon, "Uses count only a weapon taken from the world");
            Assert.AreEqual(-1, rig.Agent.UsesLeft);
        }

        // ---------- the clips of the weapon in the hand ----------

        [Test]
        public void WorldWeapon_TakenUp_ItsMotionsAreTheCharacters_LetGo_TheOwnWeaponsAgain()
        {
            GraphRig rig = Armed(out WeaponAsset sword, out _);
            sword.Motions = new AnimatorOverrideController();
            _created.Add(sword.Motions);
            WeaponAsset rock = Rock();
            rock.Motions = new AnimatorOverrideController();
            _created.Add(rock.Motions);
            Assert.AreSame(sword, rig.Agent.MotionsWeapon, "Set-up: the sword's clips");

            rig.Agent.TakeUpFromWorld(rock, Thing("Stone"), 0, false);
            Assert.AreSame(rock, rig.Agent.MotionsWeapon, "The throw is shown with the rock's clips");
            rig.Agent.LetGoFromWorld();
            Assert.AreSame(sword, rig.Agent.MotionsWeapon, "Let go: the sword's clips again");

            rig.Agent.TakeUpFromWorld(rock, Thing("Stone 2"), 0, false);
            Assert.AreSame(rock, rig.Agent.MotionsWeapon);
            rig.Agent.SpendUse();
            Assert.AreSame(sword, rig.Agent.MotionsWeapon, "Spent: the sword's clips again");
        }

        [Test]
        public void WorldWeapon_TheOwnWeaponSwitchedBehindTheRock_TheRocksClipsStay_TheNewOnesComeBack()
        {
            GraphRig rig = Armed(out WeaponAsset sword, out _);
            WeaponAsset bow = WeaponAsset.Create(new WeaponMove { Name = "Attack 1", Reach = 14f });
            bow.name = "Bow";
            _created.Add(bow);
            rig.Carry(bow);
            WeaponAsset rock = Rock();
            rig.Agent.TakeUpFromWorld(rock, Thing("Stone"), 0, false);

            rig.Agent.TakeUp(bow);                              // what Switch Weapon does with a rock in the hand

            Assert.AreSame(rock, rig.Agent.MotionsWeapon, "The rock is still in the hand: its clips stay");
            rig.Agent.LetGoFromWorld();
            Assert.AreSame(bow, rig.Agent.MotionsWeapon, "The own weapon back in the hand is the bow: its clips");
        }
    }
}
