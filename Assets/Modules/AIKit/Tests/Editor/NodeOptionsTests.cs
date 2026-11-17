using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AIKit.Editor;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The lists a text setting of a node is picked from (step 90, C5; spec 4.4): taken from the project's assets, not from
    /// a character — so a graph opened on its own has them too; a value the project lacks stays and is said to be "not
    /// found"; an empty value says what it means; a character's weapons are marked against the list. Built here from
    /// assets made in memory (no lab asset is needed).
    /// </summary>
    public sealed class NodeOptionsTests
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
        }

        [Test]
        public void MovesOf_EveryMoveOfEveryWeapon_ByNameOnce()
        {
            WeaponAsset sword = Weapon("Sword", new WeaponMove { Name = "Attack 1" }, new WeaponMove { Name = "Heavy" });
            WeaponAsset bow = Weapon("Bow", new WeaponMove { Name = "Attack 1" }, new WeaponMove { Name = "Volley" });

            CollectionAssert.AreEqual(new[] { "Attack 1", "Heavy", "Volley" }, NodeOptions.MovesOf(new[] { sword, bow }));
            CollectionAssert.IsEmpty(NodeOptions.MovesOf(new WeaponAsset[0]), "No weapon in the project: nothing to pick");
        }

        [Test]
        public void WeaponsOf_NextFirst_ThenTheWeaponsByName()
        {
            WeaponAsset sword = Weapon("Sword");
            WeaponAsset bow = Weapon("Bow");

            CollectionAssert.AreEqual(new[] { SwitchWeaponTask.NEXT, "Sword", "Bow" }, NodeOptions.WeaponsOf(new[] { sword, bow }));
        }

        [Test]
        public void ActionsOf_TheActionStatesOfTheAnimators_TheMovesDashesDrawSheathe_TheAbilities()
        {
            var controller = new AnimatorController { name = "Agent" };
            _created.Add(controller);
            var machine = new AnimatorStateMachine { name = "Base" };
            _created.Add(machine);
            controller.layers = new[] { new AnimatorControllerLayer { name = "Base", stateMachine = machine } };
            AnimatorState kick = machine.AddState("Kick");
            kick.tag = NodeOptions.ACTION_TAG;
            machine.AddState("Idle");                   // no tag: locomotion, not an action
            WeaponAsset sword = Weapon("Sword", new WeaponMove { Name = "Heavy", Action = "Heavy Attack" });
            sword.Dashes.Add(new WeaponDash { Action = "Dodge Back" });
            sword.DrawAction = "Weapon Draw";
            var grenade = ScriptableObject.CreateInstance<AbilityAsset>();
            grenade.Name = "Grenade";
            _created.Add(grenade);

            List<string> actions = NodeOptions.ActionsOf(new[] { controller }, new[] { sword }, new[] { grenade });

            CollectionAssert.AreEquivalent(new[] { "Kick", "Heavy Attack", "Dodge Back", "Weapon Draw", "Grenade" }, actions);
            CollectionAssert.DoesNotContain(actions, "Idle");
        }

        [Test]
        public void PoolsOf_TheCommonGroupFirst_ThenTheShotsTheTargetsAndTheGraphsGroups()
        {
            WeaponAsset bow = Weapon("Bow");
            bow.Shoots = true;
            bow.Shot.Pool = "Ranged";
            WeaponAsset sword = Weapon("Sword");        // shoots nothing: its shot's group is not a group anybody asks

            List<string> pools = NodeOptions.PoolsOf(new[] { bow, sword }, new[] { "Siege", "Ranged" }, new[] { "Melee" });

            CollectionAssert.AreEqual(new[] { "", "Melee", "Ranged", "Siege" }, pools);
        }

        [Test]
        public void AbilitiesOf_TheAbilityAssets_ByNameOnce()
        {
            AbilityAsset grenade = Ability("Grenade");
            AbilityAsset kick = Ability("Kick");
            AbilityAsset again = Ability("Kick");

            CollectionAssert.AreEqual(new[] { "Grenade", "Kick" }, NodeOptions.AbilitiesOf(new[] { grenade, kick, again }));
        }

        [Test]
        public void OptionWords_ANameNotInTheList_StaysMarked_TheEmptyValueSaysWhatItMeans()
        {
            var options = new List<string> { "Attack 1", "Heavy" };

            List<string> words = ElementInspector.OptionWords(options, "Slash", "(first move)", null, out int selected,
                out List<string> values);

            Assert.AreEqual("(first move)", words[0], "The empty value, said");
            Assert.AreEqual("", values[0]);
            Assert.AreEqual("Slash (not found)", words[selected], "Kept as it is, and said");
            Assert.AreEqual("Slash", values[selected]);

            ElementInspector.OptionWords(options, "", NodeOptions.COMMON, null, out selected, out _);
            Assert.AreEqual(0, selected, "Empty: the first entry");

            words = ElementInspector.OptionWords(options, "Heavy", "(first move)", new HashSet<string> { "Heavy" },
                out selected, out _);
            Assert.AreEqual("Heavy (not on its weapons)", words[selected], "A move the open character lacks is marked");
        }

        [Test]
        public void MissingFor_NoCharacter_Nothing_ACharacter_TheMovesItsWeaponsLack()
        {
            CollectionAssert.IsEmpty(NodeOptions.MissingFor(null, new[] { "Heavy" }), "No character: the list is the project's");

            var go = new GameObject("Skeleton");
            _created.Add(go);
            var character = go.AddComponent<AICharacter>();
            character.Weapons.Add(new CharacterWeapon { Weapon = Weapon("Sword", new WeaponMove { Name = "Attack 1" }) });

            HashSet<string> missing = NodeOptions.MissingFor(character, new[] { "Attack 1", "Heavy", "" });

            CollectionAssert.AreEquivalent(new[] { "Heavy" }, missing);
        }

        [Test]
        public void For_EveryKindOfPicker_HasItsList_FreeTextAndVariablesHaveNone()
        {
            FieldInfo move = typeof(AttackTask).GetField(nameof(AttackTask.Move));
            FieldInfo weapon = typeof(SwitchWeaponTask).GetField(nameof(SwitchWeaponTask.Weapon));
            FieldInfo action = typeof(TurnToTargetTask).GetField(nameof(TurnToTargetTask.Left));
            FieldInfo message = typeof(DebugLogTask).GetField(nameof(DebugLogTask.Message));
            FieldInfo target = typeof(AttackTask).GetField(nameof(AttackTask.Target));

            Assert.IsNotNull(NodeOptions.For(move));
            Assert.IsNotNull(NodeOptions.For(weapon));
            Assert.IsTrue(NodeOptions.For(weapon).Contains(SwitchWeaponTask.NEXT));
            Assert.IsNotNull(NodeOptions.For(action));
            Assert.IsNull(NodeOptions.For(message), "Free text: a text field");
            Assert.IsNull(NodeOptions.For(target), "A variable comes by a wire");
            Assert.AreEqual("(first move)", NodeOptions.EmptyMeaning(move));
            Assert.AreEqual(NodeOptions.COMMON, NodeOptions.EmptyMeaning(typeof(GuardedPool).GetField(nameof(GuardedPool.Pool))));
        }

        [Test]
        public void UnknownKeys_AKeyTheProjectLacks_IsNamed_AnEmptyOrAKnownOneIsNot()
        {
            var attack = new AttackTask { Move = "Slash" };
            List<string> Moves(FieldInfo field) => new() { "Attack 1", "Heavy" };

            List<string> unknown = NodeOptions.UnknownKeys(attack, Moves);

            Assert.AreEqual(1, unknown.Count, "A typo, or a move renamed: a warning on the node");
            StringAssert.Contains("'Slash'", unknown[0]);
            attack.Move = "Heavy";
            CollectionAssert.IsEmpty(NodeOptions.UnknownKeys(attack, Moves));
            attack.Move = "";
            CollectionAssert.IsEmpty(NodeOptions.UnknownKeys(attack, Moves), "Empty: the first move, not a key");
            CollectionAssert.IsEmpty(NodeOptions.UnknownKeys(new TurnToTargetTask(), _ => new List<string>()),
                "A project with no actions at all: nothing to check against");
        }

        [Test]
        public void TypesOf_TheNonAbstractTypesOfABase()
        {
            List<string> types = NodeOptions.TypesOf(typeof(SensorTask));

            Assert.Contains(typeof(RadiusSensorTask).FullName, types);
            Assert.IsFalse(types.Contains(typeof(SensorTask).FullName), "An abstract base is not a type to pick");
        }

        // A setting of a group of places, as a node of a lane names it
        private sealed class GuardedPool
        {
            [PoolName] public string Pool = "";
        }

        private WeaponAsset Weapon(string name, params WeaponMove[] moves)
        {
            WeaponAsset weapon = WeaponAsset.Create(moves);
            weapon.name = name;
            _created.Add(weapon);
            return weapon;
        }

        private AbilityAsset Ability(string name)
        {
            var asset = ScriptableObject.CreateInstance<AbilityAsset>();
            asset.Name = name;
            _created.Add(asset);
            return asset;
        }
    }
}
