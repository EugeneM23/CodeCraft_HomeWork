using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The Switch Weapon node: the weapon in the hand goes on the back, the new one comes into the hand at the moments
    /// the weapons give (read from their clips); the character strikes with the new one only once it is in the hand;
    /// the models follow (the one not in the hand is on the back); a switch that has started is not taken away, and one
    /// cut short anyway never leaves the hand and the weapon apart.
    /// </summary>
    public sealed class SwitchWeaponTests
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

        private static WeaponAsset Sword()
        {
            var sword = WeaponAsset.Create<WeaponAsset>(new WeaponMove { Name = "Normal" });
            sword.name = "Sword";
            sword.SheatheAction = "Sword Sheathe";
            sword.SheatheAt = 0.3f;
            sword.SheatheTime = 0.6f;
            sword.DrawAction = "Sword Draw";
            sword.DrawAt = 0.2f;
            sword.DrawTime = 0.5f;
            return sword;
        }

        private static WeaponAsset Bow()
        {
            var bow = WeaponAsset.Create(new WeaponMove { Name = "Shot", Reach = 14f });
            bow.Shoots = true;
            bow.name = "Bow";
            bow.Shot.Pool = "";                            // no group round the target here: it shoots without asking
            bow.DrawAction = "Bow Draw";
            bow.DrawAt = 0.25f;
            bow.DrawTime = 0.5f;
            bow.SheatheAction = "Bow Sheathe";
            return bow;
        }

        private GameObject Model(string name)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            return go;
        }

        /// <summary>A character with a sword in the hand and a bow on the back, each with both models.</summary>
        private GraphRig Armed(out WeaponAsset sword, out WeaponAsset bow, out CharacterWeapon swordModels,
            out CharacterWeapon bowModels)
        {
            sword = Sword();
            bow = Bow();
            var rig = new GraphRig();
            rig.GiveWeapon(sword);
            swordModels = rig.Carry(sword, Model("Sword In Hand"), Model("Sword On Back"));
            bowModels = rig.Carry(bow, Model("Bow In Hand"), Model("Bow On Back"));
            return rig;
        }

        // The graph is started in its Brain at once, so the Brain takes its decision on the first step and the chain runs
        // from that step on — the timings below count from it
        private static void Run(GraphRig rig, BehaviorGraph graph)
        {
            rig.SetGraph(graph);
            rig.Instance.Start(rig.Context);
        }

        [Test]
        public void Character_TheWeaponInTheHandShowsInTheHand_TheOthersOnTheBack()
        {
            Armed(out _, out _, out CharacterWeapon sword, out CharacterWeapon bow);

            Assert.That(sword.InHand.activeSelf, Is.True);
            Assert.That(sword.OnBack.activeSelf, Is.False);
            Assert.That(bow.InHand.activeSelf, Is.False);
            Assert.That(bow.OnBack.activeSelf, Is.True);
        }

        [Test]
        public void SwitchWeapon_TheWeaponIsAlreadyInTheHand_DoneAtOnce_NothingPlayed()
        {
            GraphRig rig = Armed(out WeaponAsset sword, out _, out _, out _);
            var body = new ActionLog(rig.Context);
            var switching = new SwitchWeaponTask { Weapon = sword.name };
            Run(rig, GraphRig.OneState(switching));

            rig.Step();

            Assert.That(switching.StatusOf(rig.Context), Is.EqualTo(TaskStatus.Success));
            Assert.That(body.Played, Is.Empty);
        }

        [Test]
        public void SwitchWeapon_AWeaponNotCarried_NotCarried()
        {
            GraphRig rig = Armed(out WeaponAsset sword, out _, out _, out _);
            var switching = new SwitchWeaponTask { Weapon = "Spear" };
            Run(rig, GraphRig.OneState(switching));

            rig.Step();

            Assert.That(switching.StatusOf(rig.Context), Is.EqualTo(TaskStatus.Failure));
            Assert.That(rig.Context.Weapon, Is.SameAs(sword));
        }

        [Test]
        public void SwitchWeapon_PutsTheOldAwayThenTakesTheNewUp_AtTheWeaponsMoments()
        {
            GraphRig rig = Armed(out WeaponAsset sword, out WeaponAsset bow, out CharacterWeapon swordModels,
                out CharacterWeapon bowModels);
            var body = new ActionLog(rig.Context);
            var switching = new SwitchWeaponTask { Weapon = bow.name };
            Run(rig, GraphRig.OneState(switching));

            rig.StepSeconds(sword.SheatheAt - 0.05f);
            Assert.That(swordModels.InHand.activeSelf, Is.True, "the sword is still being put away");

            rig.StepSeconds(0.1f);
            Assert.That(swordModels.OnBack.activeSelf, Is.True, "the sword is on the back");
            Assert.That(swordModels.InHand.activeSelf, Is.False);
            Assert.That(rig.Context.Weapon, Is.SameAs(sword), "nothing new in the hand yet");

            rig.StepSeconds(sword.SheatheTime - sword.SheatheAt + bow.DrawAt - 0.1f);
            Assert.That(rig.Context.Weapon, Is.SameAs(sword), "the bow is still on its way into the hand");
            Assert.That(bowModels.OnBack.activeSelf, Is.True);

            rig.StepSeconds(0.1f);
            Assert.That(rig.Context.Weapon, Is.SameAs(bow), "the bow is in the hand: the moves are its own");
            Assert.That(bowModels.InHand.activeSelf, Is.True);
            Assert.That(bowModels.OnBack.activeSelf, Is.False);
            Assert.That(rig.Agent.WeaponIndex, Is.EqualTo(1), "the animator is told: the second weapon");

            rig.StepSeconds(bow.DrawTime - bow.DrawAt + 0.05f);
            Assert.That(switching.StatusOf(rig.Context), Is.EqualTo(TaskStatus.Success));
            Assert.That(body.Played, Is.EqualTo(new[] { sword.SheatheAction, bow.DrawAction }));
        }

        [Test]
        public void SwitchWeapon_InTheMiddle_CommitsTheAgent_NotBeforeNorAfter()
        {
            GraphRig rig = Armed(out WeaponAsset sword, out WeaponAsset bow, out _, out _);
            var switching = new SwitchWeaponTask { Weapon = bow.name };
            Run(rig, GraphRig.OneState(switching));

            rig.Step();
            Assert.That(switching.CommitsFor(rig.Context), Is.True, "a switch that has started runs to its end");

            rig.StepSeconds(sword.SheatheTime + bow.DrawTime + 0.1f);
            Assert.That(switching.CommitsFor(rig.Context), Is.False);
        }

        [Test]
        public void SwitchWeapon_CutShortOnceTheOldIsAway_TheNewOneIsInTheHandAtOnce()
        {
            GraphRig rig = Armed(out WeaponAsset sword, out WeaponAsset bow, out CharacterWeapon swordModels,
                out CharacterWeapon bowModels);
            var body = new ActionLog(rig.Context);
            var switching = new SwitchWeaponTask { Weapon = bow.name };
            Run(rig, GraphRig.OneState(switching));
            rig.StepSeconds(sword.SheatheAt + 0.1f);

            switching.Interrupt(rig.Context);

            Assert.That(rig.Context.Weapon, Is.SameAs(bow), "the hand is never left empty");
            Assert.That(bowModels.InHand.activeSelf, Is.True);
            Assert.That(swordModels.OnBack.activeSelf, Is.True);
            Assert.That(body.Drops, Is.EqualTo(1), "the action is dropped at once");
        }

        [Test]
        public void SwitchWeapon_CutShortBeforeTheOldIsAway_TheOldOneStays()
        {
            GraphRig rig = Armed(out WeaponAsset sword, out WeaponAsset bow, out CharacterWeapon swordModels, out _);
            var switching = new SwitchWeaponTask { Weapon = bow.name };
            Run(rig, GraphRig.OneState(switching));
            rig.StepSeconds(sword.SheatheAt * 0.5f);

            switching.Interrupt(rig.Context);

            Assert.That(rig.Context.Weapon, Is.SameAs(sword));
            Assert.That(swordModels.InHand.activeSelf, Is.True);
        }

        [Test]
        public void SwitchWeapon_ThenAttack_StrikesWithTheNewWeaponsMove()
        {
            GraphRig rig = Armed(out WeaponAsset sword, out WeaponAsset bow, out _, out _);
            Transform target = rig.CreateTargetAt(new Vector3(0f, 0f, 8f));
            rig.Context.Blackboard.Set("Victim", target);
            var attack = new AttackTask { Target = "Victim", Move = "Shot" };
            var g = new BrainGraphBuilder();
            g.Chain(g.Always(), new SwitchWeaponTask { Weapon = bow.name }, attack);    // the switch's Done → Strike
            Run(rig, g.Build());

            Assert.That(rig.StepUntil(() => attack.IsRunningFor(rig.Context), sword.SheatheTime + bow.DrawTime + 0.5f),
                Is.True, "after the switch the chain goes on");
            Assert.That(attack.StatusOf(rig.Context), Is.EqualTo(TaskStatus.Running), "Shot is a move of the bow in the hand");
        }

        [Test]
        public void SwitchWeapon_Next_TakesUpTheWeaponAfterTheOneInTheHand_RoundToTheFirst()
        {
            GraphRig rig = Armed(out WeaponAsset sword, out WeaponAsset bow, out _, out _);
            var switching = new SwitchWeaponTask { Weapon = SwitchWeaponTask.NEXT };
            Run(rig, GraphRig.OneState(switching));

            Assert.That(rig.StepUntil(() => switching.StatusOf(rig.Context) == TaskStatus.Success,
                sword.SheatheTime + bow.DrawTime + 0.5f), Is.True);
            Assert.That(rig.Context.Weapon, Is.SameAs(bow), "the bow comes after the sword");

            var again = new SwitchWeaponTask { Weapon = SwitchWeaponTask.NEXT };
            Run(rig, GraphRig.OneState(again));
            Assert.That(rig.StepUntil(() => again.StatusOf(rig.Context) == TaskStatus.Success,
                bow.SheatheTime + sword.DrawTime + 0.5f), Is.True);
            Assert.That(rig.Context.Weapon, Is.SameAs(sword), "after the last weapon — the first again");
        }

        [Test]
        public void SwitchWeapon_TheNewWeaponsClipsAreShownBeforeItIsTakenUp_NotBeforeTheOldIsAway()
        {
            GraphRig rig = Armed(out WeaponAsset sword, out WeaponAsset bow, out _, out _);
            var rack = new WatchedRack(rig.Agent);
            rig.Context.Rack = rack;
            var body = new ActionLog(rig.Context);
            Run(rig, GraphRig.OneState(new SwitchWeaponTask { Weapon = bow.name }));

            rig.StepSeconds(sword.SheatheTime - 0.05f);
            Assert.That(rack.Shown, Is.Empty, "the sword is put away with its own clips");

            rig.StepSeconds(0.1f);
            Assert.That(rack.Shown, Is.EqualTo(new[] { bow }), "the bow's clips, then its draw");
            Assert.That(body.Played, Is.EqualTo(new[] { sword.SheatheAction, bow.DrawAction }));
        }

        /// <summary>The character's rack, with the clip sets it was asked to show written down.</summary>
        private sealed class WatchedRack : IWeaponRack
        {
            private readonly AICharacter _character;
            public readonly List<WeaponAsset> Shown = new();

            public WatchedRack(AICharacter character) => _character = character;

            public IReadOnlyList<WeaponAsset> Carried => _character.Carried;
            public WeaponAsset Find(string weapon) => _character.Find(weapon);
            public void PutAway(WeaponAsset weapon) => _character.PutAway(weapon);
            public void TakeUp(WeaponAsset weapon) => _character.TakeUp(weapon);
            public WeaponAsset After(WeaponAsset weapon) => _character.After(weapon);

            public void ShowMotionsOf(WeaponAsset weapon)
            {
                Shown.Add(weapon);
                _character.ShowMotionsOf(weapon);
            }

            public bool TakeUpFromWorld(WeaponAsset weapon, Transform model, int uses, bool mounted) =>
                _character.TakeUpFromWorld(weapon, model, uses, mounted);

            public Transform FromWorld => _character.FromWorld;
            public int UsesLeft => _character.UsesLeft;
            public void SpendUse() => _character.SpendUse();
            public Transform LetGoFromWorld() => _character.LetGoFromWorld();
        }

    }
}
