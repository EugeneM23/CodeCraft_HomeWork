using System.Collections.Generic;
using AIKit.Editor;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    public sealed class CharacterComponentTests
    {
        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
                Object.DestroyImmediate(_go);
        }

        private AICharacter Make(bool withBody = true)
        {
            _go = new GameObject("Character") { hideFlags = HideFlags.HideAndDontSave };
            _go.SetActive(false);
            var character = _go.AddComponent<AICharacter>();
            if (withBody)
                character.Body = new SimpleBody();
            return character;
        }

        [Test]
        public void AICharacter_GraphAssetSet_RunsThatGraph()
        {
            AICharacter character = Make();
            var asset = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
            GraphSerializer.Write(GraphRig.OneState(new WaitTask(10f), "Idle"), asset);
            character.GraphAsset = asset;

            character.Initialize();

            Assert.That(character.Graph, Is.Not.Null);
            character.Step(character.ThinkInterval);        // the first think: the graph starts in its Brain
            character.Step(character.ThinkInterval);        // the next: the Brain takes its decision, the chain leads on
            Assert.That(character.State.DisplayName, Is.EqualTo("Idle"));
            Object.DestroyImmediate(asset);
        }

        [Test]
        public void AICharacter_KeepsNoSceneObjects_ALegacyBindingReachesNothing()
        {
            // Step 90 (C18): the graph finds the objects of the world and its sensors see the target — the character
            // gives the graph no objects of the scene
            Assert.IsNull(typeof(AICharacter).GetMethod("Bind"), "No binding of objects on the character");
            Assert.IsNull(typeof(AICharacter).GetMethod("BoundTo"));
            Assert.IsNull(typeof(AICharacter).GetProperty("Bindings"));
            System.Reflection.FieldInfo legacy = typeof(AICharacter).GetField("_bindings",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(legacy, "The old data stays readable for the migration");
            Assert.IsTrue(System.Attribute.IsDefined(legacy, typeof(HideInInspector)), "…and is not shown");

            AICharacter character = Make();
            var target = new GameObject("Target") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var fields = new UnityEditor.SerializedObject(character);
                UnityEditor.SerializedProperty bindings = fields.FindProperty("_bindings");
                bindings.arraySize = 1;
                bindings.GetArrayElementAtIndex(0).FindPropertyRelative("Variable").stringValue = "Target";
                bindings.GetArrayElementAtIndex(0).FindPropertyRelative("Object").objectReferenceValue = target.transform;
                fields.ApplyModifiedPropertiesWithoutUndo();

                character.Initialize();

                Assert.IsNull(character.Blackboard.Get<Transform>("Target"), "A binding of an old scene reaches nothing");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void AICharacter_SimpleBody_MovesTransform()
        {
            _go = new GameObject("Character") { hideFlags = HideFlags.HideAndDontSave };
            _go.SetActive(false);
            AICharacter character = _go.AddComponent<AICharacter>();
            var body = new SimpleBody { Speed = 2f };
            character.Body = body;
            character.Initialize();

            character.Context.Actuator.Move(Vector3.forward);
            character.Context.Actuator.Apply(0.5f);

            Assert.That(_go.transform.position.z, Is.EqualTo(body.Speed * 0.5f).Within(1e-4f));
            Assert.That(character.Context.Actuator, Is.InstanceOf<TransformMover>());
        }

        private sealed class StubScanner : ITargetScanner
        {
            public int Scan(Vector3 center, float radius, System.Collections.Generic.List<Transform> results)
            {
                results.Clear();
                return 0;
            }
        }

        private sealed class ProbeBody : AgentBody
        {
            public readonly StubScanner ScannerPart = new();
            private readonly TransformMover _mover;
            public int Attached;
            public float Ticked;

            public ProbeBody(Transform owner) => _mover = new TransformMover(owner, new SimpleBody());

            public override void Attach(Transform owner) => Attached++;

            public override IAgentActuator Actuator => _mover;

            public override ITargetScanner Scanner => ScannerPart;

            public override void FrameTick(float dt) => Ticked += dt;
        }

        [Test]
        public void AICharacter_BodyWithScanner_AttachesOnce_TheEyesAreForASensesSensor_NoSensesOfItsOwn()
        {
            _go = new GameObject("Character") { hideFlags = HideFlags.HideAndDontSave };
            _go.SetActive(false);
            AICharacter character = _go.AddComponent<AICharacter>();
            var body = new ProbeBody(_go.transform);
            character.Body = body;

            character.Initialize();
            character.Initialize();

            Assert.That(body.Attached, Is.EqualTo(1));
            Assert.That(character.Context.Actuator, Is.SameAs(body.Actuator));
            Assert.That(character.Context.Scanner, Is.SameAs(body.ScannerPart), "the body's eyes, for a Senses sensor");
            Assert.That(character.Context.Perception, Is.Null,
                "step 90 (C21): the senses are a Senses sensor's of the graph — none in it, none at all");
        }

        [Test]
        public void AICharacter_HasNoPictureOfItsOwn_ItsActionsAreItsState_NothingShowsThemWithoutAView()
        {
            AICharacter character = Make();
            character.Initialize();

            Assert.That(character.Context.Actions.HasView, Is.False, "no AI Animator: nobody shows it");
            character.Context.Actions.Swing();
            Assert.That(character.Context.Actions.Current, Is.EqualTo(ActionState.Kind.Swing),
                "the swing is the character's state, asked for all the same");
            Assert.That(character.Context.Actions.IsShown, Is.False, "…and not on any screen");
            Assert.That(character.Context.Actions.CanShow("Attack 1"), Is.False);
        }

        /// <summary>
        /// The character's own serialized data, section by section (step 90: no Animation, no Senses — C21, C22; their old
        /// data only hidden, for the migration). A field another lane adds or removes changes this list.
        /// </summary>
        private static readonly string[] CharacterFields =
        {
            // Advanced
            "_simulation", "_thinkInterval", "_seed", "_trace", "_faction",
            // Body
            "_body",
            // Combat
            "_weapon", "_weapons", "_abilityAssets",
            // Behavior (and its debug drawing)
            "_graph", "_bindings", "_drawSensors", "_showState", "_stateHeight", "_showName",
            // Hands (where a carried thing is held)
            "_carryPoint", "_carryOffset"
        };

        /// <summary>The old sections' data, hidden, read only by the migration (Migrate - Animation To AI Animator).</summary>
        private static readonly string[] LegacyFields = { "_animated", "_animation", "_perception" };

        [Test]
        public void AICharacter_SerializedFields_OnlyItsSections_NoAnimationNoSenses_TheOldDataHidden()
        {
            var unknown = new List<string>();
            var shownLegacy = new List<string>();
            var pictureTypes = new List<string>();
            const System.Reflection.BindingFlags FIELDS = System.Reflection.BindingFlags.Instance |
                                                          System.Reflection.BindingFlags.Public |
                                                          System.Reflection.BindingFlags.NonPublic;
            foreach (System.Reflection.FieldInfo field in typeof(AICharacter).GetFields(FIELDS))
            {
                bool serialized = field.IsPublic && !field.IsNotSerialized ||
                                  field.IsDefined(typeof(SerializeField), false) ||
                                  field.IsDefined(typeof(SerializeReference), false);
                if (!serialized)
                    continue;

                System.Type type = field.FieldType.IsGenericType ? field.FieldType.GetGenericArguments()[0] : field.FieldType;
                if (typeof(Animator).IsAssignableFrom(type) || typeof(RuntimeAnimatorController).IsAssignableFrom(type) ||
                    typeof(AnimationClip).IsAssignableFrom(type) || type == typeof(PerceptionProfile))
                    pictureTypes.Add(field.Name);

                if (System.Array.IndexOf(LegacyFields, field.Name) >= 0)
                {
                    if (!field.IsDefined(typeof(HideInInspector), false))
                        shownLegacy.Add(field.Name);
                    continue;
                }

                if (System.Array.IndexOf(CharacterFields, field.Name) < 0)
                    unknown.Add(field.Name);
            }

            Assert.That(unknown, Is.Empty, "fields of no section of the character");
            Assert.That(shownLegacy, Is.Empty, "the old sections' data is hidden");
            Assert.That(pictureTypes, Is.Empty, "the character holds no animator, clip or perception profile");
        }

        [Test]
        public void AICharacter_SetPartsAndBody_SetPartsWin()
        {
            _go = new GameObject("Character") { hideFlags = HideFlags.HideAndDontSave };
            _go.SetActive(false);
            AICharacter character = _go.AddComponent<AICharacter>();
            character.Body = new SimpleBody();
            var own = new TransformMover(_go.transform, new SimpleBody());
            character.SetParts(own);

            character.Initialize();

            Assert.That(character.Context.Actuator, Is.SameAs(own));
        }

        [Test]
        public void AICharacter_BodySetAfterInitialize_Throws()
        {
            AICharacter character = Make();
            character.Initialize();

            Assert.Throws<System.InvalidOperationException>(() => character.Body = new SimpleBody());
        }

        // ---------- the inspector's logic: body kinds, required components, problems ----------

        // A graph asset whose one decision's chain is this block
        private BehaviorGraphAsset GraphWith(AgentTask task)
        {
            var asset = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
            GraphSerializer.Write(GraphRig.OneState(task), asset);
            return asset;
        }

        private static WeaponAsset WeaponWith(string move)
        {
            return WeaponAsset.Create<WeaponAsset>(new WeaponMove { Name = move });
        }

        [Test]
        public void AICharacterEditor_BodyKinds_ListsSimpleAndNavMesh()
        {
            List<System.Type> kinds = AICharacterEditor.BodyKinds();
            Assert.That(kinds, Has.Some.EqualTo(typeof(SimpleBody)));
            Assert.That(kinds, Has.Some.EqualTo(typeof(AIKit.NavMeshNavigation.NavMeshBody)));
            Assert.That(kinds, Has.None.EqualTo(typeof(AgentBody)));
        }

        [Test]
        public void AICharacterEditor_NavMeshBodyChosen_AddsNavMeshAgent()
        {
            _go = new GameObject("Character") { hideFlags = HideFlags.HideAndDontSave };
            AICharacter character = _go.AddComponent<AICharacter>();
            character.Body = new AIKit.NavMeshNavigation.NavMeshBody();

            AICharacterEditor.EnsureRequired(character);

            Assert.That(_go.GetComponent<UnityEngine.AI.NavMeshAgent>(), Is.Not.Null);
        }

        [Test]
        public void AICharacterEditor_ComponentAlreadyThere_IsNotAddedTwice()
        {
            _go = new GameObject("Character") { hideFlags = HideFlags.HideAndDontSave };
            var existing = _go.AddComponent<UnityEngine.AI.NavMeshAgent>();
            AICharacter character = _go.AddComponent<AICharacter>();
            character.Body = new AIKit.NavMeshNavigation.NavMeshBody();

            AICharacterEditor.EnsureRequired(character);

            Assert.That(_go.GetComponents<UnityEngine.AI.NavMeshAgent>(), Is.EqualTo(new[] { existing }));
        }

        [Test]
        public void AICharacterEditor_NoBody_ReportsIt()
        {
            AICharacter character = Make(withBody: false);

            Assert.That(AICharacterEditor.Problems(character), Has.Some.Contains("body"));
        }

        [Test]
        public void AICharacterEditor_NoGraph_ReportsIt()
        {
            AICharacter character = Make();

            Assert.That(AICharacterEditor.Problems(character), Is.EqualTo(new[] { AICharacterEditor.NO_GRAPH }),
                "a body and nothing else wrong: only the missing graph");

            BehaviorGraphAsset asset = GraphWith(new WaitTask(1f));
            character.GraphAsset = asset;
            Assert.That(AICharacterEditor.Problems(character), Is.Empty);
            Object.DestroyImmediate(asset);
        }

        [Test]
        public void AICharacterEditor_BodyNeedsMissingComponent_ReportsIt()
        {
            AICharacter character = Make();
            character.Body = new AIKit.NavMeshNavigation.NavMeshBody();

            Assert.That(AICharacterEditor.Problems(character), Has.Some.Contains(nameof(UnityEngine.AI.NavMeshAgent)));

            AICharacterEditor.EnsureRequired(character);
            Assert.That(AICharacterEditor.Problems(character), Has.None.Contains(nameof(UnityEngine.AI.NavMeshAgent)));
        }

        [Test]
        public void AICharacterEditor_GraphAttacksWithoutWeapon_ReportsIt()
        {
            AICharacter character = Make();
            BehaviorGraphAsset asset = GraphWith(new AttackTask { Target = "Victim" });
            character.GraphAsset = asset;

            Assert.That(AICharacterEditor.Problems(character), Has.Some.Contains("weapon"));
            Object.DestroyImmediate(asset);
        }

        [Test]
        public void AICharacterEditor_AttackMoveNotOnWeapon_ReportsTheMove()
        {
            AICharacter character = Make();
            BehaviorGraphAsset asset = GraphWith(new AttackTask { Target = "Victim", Move = "Smash" });
            WeaponAsset weapon = WeaponWith("Slash");
            character.GraphAsset = asset;
            character.Weapon = weapon;

            Assert.That(AICharacterEditor.Problems(character), Has.Some.Contains("Smash"));
            Object.DestroyImmediate(asset);
            Object.DestroyImmediate(weapon);
        }

        [Test]
        public void AICharacterEditor_AttackMoveOnWeapon_ReportsNoWeaponProblem()
        {
            AICharacter character = Make();
            BehaviorGraphAsset asset = GraphWith(new AttackTask { Target = "Victim", Move = "Slash" });
            WeaponAsset weapon = WeaponWith("Slash");
            character.GraphAsset = asset;
            character.Weapon = weapon;

            Assert.That(AICharacterEditor.Problems(character), Has.None.Contains("eapon"));
            Object.DestroyImmediate(asset);
            Object.DestroyImmediate(weapon);
        }

        [Test]
        public void AICharacterEditor_AnAbilityProblemOfItsGraph_IsShown_TheCharacterHasNoListOfItsOwn()
        {
            AICharacter character = Make();
            BehaviorGraphAsset asset = GraphWith(new UseAbilityTask());     // no ability, in no ability decision's chain
            character.GraphAsset = asset;

            Assert.That(AICharacterEditor.Problems(character), Has.Some.Contains("names no ability"));

            var ability = AbilityAsset.Create("Fireball");
            BehaviorGraphAsset named = GraphWith(new UseAbilityTask(ability));
            character.GraphAsset = named;
            Assert.That(AICharacterEditor.Problems(character), Has.None.Contains("ability"),
                "The graph names its ability: nothing to put on the character");
            Object.DestroyImmediate(asset);
            Object.DestroyImmediate(named);
            Object.DestroyImmediate(ability);
        }

        [Test]
        public void ValidateAll_CharacterInOpenScene_ReportsItsProblems()
        {
            AICharacter character = Make(withBody: false);

            Assert.That(ValidateAll.CharacterIssues(character).ConvertAll(issue => issue.Message), Has.Some.Contains("body"));
        }
    }
}
