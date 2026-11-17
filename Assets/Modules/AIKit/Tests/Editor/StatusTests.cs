using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The AI character's own say about itself (ICharacterStatus, step 90; spec 4.9): every number and state, each from
    /// its one owner — the body, the attack runner's phase, the abilities, the hands, the weapon, the Brain's facts, the
    /// game's health — read where it lives, never copied onto the blackboard; what the character cannot know is no data.
    /// </summary>
    public sealed class StatusTests
    {
        private const float DT = 0.02f;
        private readonly List<Object> _created = new();
        private GraphRig _rig;

        [SetUp]
        public void SetUp() => _rig = new GraphRig(trace: false);

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

        private AICharacter Me => _rig.Agent;

        private bool State(CharacterState state)
        {
            Assert.IsTrue(Me.TryGetState(state, out bool value), $"{state}: the character says");
            return value;
        }

        private void NoState(CharacterState state) =>
            Assert.IsFalse(Me.TryGetState(state, out _), $"{state}: the character does not know it — no data");

        private float Stat(CharacterStat stat)
        {
            Assert.IsTrue(Me.TryGetStat(stat, out float value), $"{stat}: the character says");
            return value;
        }

        // ---------- the body ----------

        [Test]
        public void Moving_FasterThanMovingSpeed_Running_FasterThanItsShareOfTheRunSpeed()
        {
            Assert.IsFalse(State(CharacterState.Moving), "It stands");
            Assert.AreEqual(0f, Stat(CharacterStat.Speed));

            _rig.Actuator.Move(Vector3.right, 1f);
            Assert.AreEqual(_rig.Actuator.Speed, Stat(CharacterStat.Speed), 1e-4f, "Speed is measured from the body");
            Assert.IsTrue(State(CharacterState.Moving));
            Assert.IsTrue(State(CharacterState.Running), "At full speed it runs");

            float walk = AICharacter.RUNNING_SHARE * 0.9f;
            _rig.Actuator.Move(Vector3.right, walk);
            Assert.IsTrue(State(CharacterState.Moving));
            Assert.IsFalse(State(CharacterState.Running), "Below its share of the run speed it walks");

            _rig.Actuator.Move(Vector3.right, AICharacter.MOVING_SPEED * 0.5f / _rig.Actuator.Speed);
            Assert.IsFalse(State(CharacterState.Moving), "Slower than the moving speed: it stands");
        }

        [Test]
        public void RunSpeedHeightRadius_FromTheBody_TurnSpeedAndAcceleration_OnlyFromAPartThatKeepsThem()
        {
            Assert.AreEqual(_rig.Actuator.Speed, Stat(CharacterStat.RunSpeed), "No number of its own: the body's top speed");
            Assert.AreEqual(_rig.Actuator.Body.Height, Stat(CharacterStat.Height));
            Assert.AreEqual(_rig.Actuator.Body.Radius, Stat(CharacterStat.Radius));
            Assert.IsFalse(Me.TryGetStat(CharacterStat.TurnSpeed, out _), "Nobody keeps it: no data, not 0");
            Assert.IsFalse(Me.TryGetStat(CharacterStat.Acceleration, out _));

            AICharacter numbered = Numbered(out Numbers numbers);
            Assert.IsTrue(numbered.TryGetStat(CharacterStat.RunSpeed, out float run));
            Assert.AreEqual(numbers.Values[0], run, "The part's own number wins over the body's guess");
            Assert.IsTrue(numbered.TryGetStat(CharacterStat.TurnSpeed, out float turn));
            Assert.AreEqual(numbers.Values[1], turn);
            Assert.IsTrue(numbered.TryGetStat(CharacterStat.Acceleration, out float acceleration));
            Assert.AreEqual(numbers.Values[2], acceleration);
            Assert.IsTrue(numbered.TryGetCustom(Numbers.MORALE, out float morale), "A game's own number by its name");
            Assert.AreEqual(numbers.Values[3], morale);
            Assert.IsFalse(numbered.TryGetCustom("Stamina", out _));
        }

        [Test]
        public void InAir_OffTheGround_Dashing_WhileADashLasts()
        {
            Assert.IsFalse(State(CharacterState.InAir));
            _rig.Actuator.IsGrounded = false;
            Assert.IsTrue(State(CharacterState.InAir));

            Assert.IsFalse(State(CharacterState.Dashing));
            _rig.Actuator.Dash(Vector3.right, 0.5f);
            Assert.IsTrue(State(CharacterState.Dashing));
        }

        [Test]
        public void Climbing_TheBodyIsOnAWall_IsNotInTheAir()
        {
            var body = new WallBody();
            AICharacter climber = Character(body);

            Assert.IsTrue(climber.TryGetState(CharacterState.Climbing, out bool climbing) && !climbing);
            body.Climb = 1;
            body.Mover.IsGrounded = false;                      // the wall holds it, the ground does not
            Assert.IsTrue(climber.TryGetState(CharacterState.Climbing, out climbing) && climbing);
            Assert.IsTrue(climber.TryGetState(CharacterState.InAir, out bool inAir) && !inAir, "On a wall, not in the air");
        }

        // ---------- the blow, the guard, the ability ----------

        [Test]
        public void AttackingWindingUpRecovering_FromThePhaseOfTheBlow()
        {
            Assert.IsFalse(State(CharacterState.Attacking));
            _rig.Context.AttackPhase = AbilityPhase.WindUp;
            Assert.IsTrue(State(CharacterState.Attacking));
            Assert.IsTrue(State(CharacterState.WindingUp));
            Assert.IsFalse(State(CharacterState.Recovering));

            _rig.Context.AttackPhase = AbilityPhase.Recovery;
            Assert.IsTrue(State(CharacterState.Attacking));
            Assert.IsFalse(State(CharacterState.WindingUp));
            Assert.IsTrue(State(CharacterState.Recovering), "After the blow: the window to punish it");
        }

        [Test]
        public void BlockingStaggeredUsingAbility_FromWhoSetsThem()
        {
            Assert.IsFalse(State(CharacterState.Blocking));
            _rig.Context.IsBlocking = true;
            Assert.IsTrue(State(CharacterState.Blocking));

            Assert.IsFalse(State(CharacterState.Staggered));
            _rig.Context.IsStaggered = true;
            Assert.IsTrue(State(CharacterState.Staggered));

            TestAbilityRunner throwing = _rig.AddAbility("Throw", duration: 1f);
            Assert.IsFalse(State(CharacterState.UsingAbility));
            throwing.Begin(_rig.Context);
            Assert.IsTrue(State(CharacterState.UsingAbility));
        }

        // ---------- the hands, the weapon, the place ----------

        [Test]
        public void CarryingInsideArmedWeaponShoots_FromTheHandsTheHouseAndTheWeapon()
        {
            Assert.IsFalse(State(CharacterState.Carrying));
            var box = new GameObject("Box") { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(box);
            Me.Hold(box.transform);
            Assert.IsTrue(State(CharacterState.Carrying));

            Assert.IsFalse(State(CharacterState.Inside));
            Me.IsInside = true;
            Assert.IsTrue(State(CharacterState.Inside));
            Me.IsInside = false;

            Assert.IsFalse(State(CharacterState.Armed), "Nothing in the hand");
            Assert.IsFalse(State(CharacterState.WeaponShoots));
            _rig.GiveWeapon(2f, 0.3f, 0.3f);
            Assert.IsTrue(State(CharacterState.Armed));
            Assert.IsFalse(State(CharacterState.WeaponShoots), "A blade");
            Me.Weapon.Shoots = true;
            Assert.IsTrue(State(CharacterState.WeaponShoots), "A bow");
            Assert.IsFalse(State(CharacterState.HoldsPickedWeapon), "Its own weapon, not one taken from the world");
        }

        [Test]
        public void OccupyingInCover_TheObjectsLayerSays_NothingTaken_No_AnOccupancyGiven_ItsWord()
        {
            // The context answers it itself (step 90: the objects layer — what its last Interact took): nothing taken, no
            Assert.IsFalse(State(CharacterState.Occupying), "Nothing taken: not occupying");
            Assert.IsFalse(State(CharacterState.InCover), "Nothing taken: not in cover");

            var cover = new GameObject("Cover") { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(cover);
            var occupancy = new Occupancy();
            _rig.Context.Occupancy = occupancy;
            Assert.IsFalse(State(CharacterState.Occupying));
            occupancy.Occupied = cover.transform;
            occupancy.InCover = true;
            Assert.IsTrue(State(CharacterState.Occupying));
            Assert.IsTrue(State(CharacterState.InCover));
        }

        [Test]
        public void SwimmingFlyingAimingHealing_ACharacterOnTheGroundDoesNotKnowThem_NoData()
        {
            NoState(CharacterState.Swimming);
            NoState(CharacterState.Flying);
            NoState(CharacterState.Aiming);
            NoState(CharacterState.Healing);
            Assert.IsFalse(Me.TryGetStat(CharacterStat.UsesLeft, out _), "A weapon with no end: nothing to count");
        }

        // ---------- the game's health ----------

        [Test]
        public void Health_FromTheGamesHealth_PercentAndDead_NoHealth_NoData()
        {
            AICharacter healthless = Numbered(out _);
            Assert.IsFalse(healthless.TryGetStat(CharacterStat.Health, out _), "No IHealth on the character: no data, not 0");
            Assert.IsFalse(healthless.TryGetState(CharacterState.Dead, out _));

            Health health = Me.gameObject.AddComponent<Health>();
            health.Current = 3f;
            health.Max = 4f;
            Assert.AreEqual(3f, Stat(CharacterStat.Health));
            Assert.AreEqual(4f, Stat(CharacterStat.MaxHealth));
            Assert.AreEqual(75f, Stat(CharacterStat.HealthPercent), 1e-4f);
            Assert.IsFalse(State(CharacterState.Dead));

            health.Current = 0f;
            Assert.IsTrue(State(CharacterState.Dead));
        }

        // ---------- the Brain ----------

        [Test]
        public void InCombat_TheBrainHadATargetAtItsLastThink_AskingCountsNothingAnew()
        {
            const string TARGET = "Target";
            var g = new BrainGraphBuilder();
            g.BrainTask.Target = TARGET;
            g.Variable(TARGET, VariableType.Transform);
            g.Do(g.Decision(new WatchReasoner(), 1), g.Block(new WaitTask(100f)));
            _rig.SetGraph(g.Build());
            _rig.Step(DT);
            Assert.IsFalse(State(CharacterState.InCombat), "No target: not in combat");

            var foe = new GameObject("Foe") { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(foe);
            Me.Blackboard.Set(TARGET, foe.transform);
            Assert.IsFalse(State(CharacterState.InCombat), "Asked between thinks: the Brain's last facts, not counted anew");
            _rig.StepSeconds(_rig.ThinkInterval + DT);
            Assert.IsTrue(State(CharacterState.InCombat));
        }

        [Test]
        public void Status_IsFoundOnTheCharacter_ByAnotherAgentsQuestion()
        {
            Assert.AreSame(Me, CharacterStatuses.Of(Me.transform), "The AI character is its own status");
        }

        [Test]
        public void Status_NotInitialized_SaysNothing()
        {
            var go = new GameObject("Sleeping") { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            go.SetActive(false);
            var sleeping = go.AddComponent<AICharacter>();

            Assert.IsFalse(sleeping.TryGetStat(CharacterStat.Speed, out _));
            Assert.IsFalse(sleeping.TryGetState(CharacterState.Moving, out _));
        }

        // ---------- helpers ----------

        // A character whose parts keep numbers: Speed, Turn Speed, Acceleration (the body's names) and the game's Morale
        private AICharacter Numbered(out Numbers numbers)
        {
            var go = new GameObject("Numbered") { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            go.SetActive(false);
            numbers = go.AddComponent<Numbers>();
            var character = go.AddComponent<AICharacter>();
            var actuator = new TestActuator();
            character.SetParts(actuator, new DirectNavigator(actuator));
            character.Initialize();
            return character;
        }

        private AICharacter Character(AgentBody body)
        {
            var go = new GameObject("Bodied") { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            go.SetActive(false);
            var character = go.AddComponent<AICharacter>();
            character.Body = body;
            character.Initialize();
            return character;
        }

        /// <summary>The game's health of a character.</summary>
        internal sealed class Health : MonoBehaviour, IHealth
        {
            public float Current { get; set; } = 1f;

            public float Max { get; set; } = 1f;
        }

        /// <summary>A part of a character that keeps numbers under the body's names and one of the game's own.</summary>
        internal sealed class Numbers : MonoBehaviour, IAgentNumbers
        {
            public const string MORALE = "Morale";

            public readonly float[] Values = { 6f, 360f, 12f, 0.5f };

            private static readonly string[] Names =
                { BodyNumbers.RUN_SPEED, BodyNumbers.TURN_SPEED, BodyNumbers.ACCELERATION, MORALE };

            public void NamesOf(List<string> names) => names.AddRange(Names);

            public float GetNumber(int index) => Values[index];

            public void SetNumber(int index, float value) => Values[index] = value;
        }

        private sealed class Occupancy : IOccupancy
        {
            public Transform Occupied { get; set; }

            public bool InCover { get; set; }
        }

        // A body that may be on a wall (the test says when)
        private sealed class WallBody : AgentBody
        {
            public readonly TestActuator Mover = new();
            public int Climb;

            public override void Attach(Transform owner)
            {
            }

            public override IAgentActuator Actuator => Mover;

            public override int ClimbDirection => Climb;
        }
    }
}
