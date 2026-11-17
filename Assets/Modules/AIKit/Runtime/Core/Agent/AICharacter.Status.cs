using System.Collections.Generic;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// The names the parts of a body give their numbers under (IAgentNumbers): the character's status reads them, and
    /// the Set Parameter block changes them (step 90). A body that has no such number says nothing about it.
    /// </summary>
    public static class BodyNumbers
    {
        /// <summary>The full speed: how fast the body runs (whatever pace it walks at now).</summary>
        public const string RUN_SPEED = "Speed";

        /// <summary>Degrees per second the body turns.</summary>
        public const string TURN_SPEED = "Turn Speed";

        /// <summary>Metres per second² the body speeds up and slows down with.</summary>
        public const string ACCELERATION = "Acceleration";

        /// <summary>Metres the body takes to get up to its speed and to stop (its acceleration is worked out from it).</summary>
        public const string RAMP_DISTANCE = "Ramp Distance";
    }

    /// <summary>
    /// The character's own say about itself (ICharacterStatus, step 90; spec 4.9) — what another graph's Get / Is reads
    /// of it, and its own: one owner for each value, read where it lives, never copied onto the blackboard (handbook §8.2;
    /// the numbers mirror of step 34 is gone). Health from the game's IHealth on the character; how fast it moves —
    /// measured from the body; its run speed, turn speed and acceleration — the numbers its parts keep (IAgentNumbers);
    /// height and radius — the body's; the phase of a blow — the attack runner's and an ability asset's
    /// (AgentContext.AttackPhase), else the phase a game's own ability runner says (IAbilityRunner.Phase); an ability
    /// — the abilities'; guarding and staggering — the context's (the Block block, the game); what it holds — the hands
    /// and the weapon rack; in combat — the Brain's facts of its last think; occupying — the objects layer
    /// (AgentContext.Occupancy). What the character cannot know of itself (swimming, flying, aiming, healing on a NavMesh
    /// body) is no data — false from TryGet, never a "no" passed off as an answer.
    /// </summary>
    public sealed partial class AICharacter : ICharacterStatus
    {
        /// <summary>Faster than this, metres per second, the character is Moving.</summary>
        public const float MOVING_SPEED = 0.2f;

        /// <summary>Faster than this part of its run speed the character is Running (a walk is slower).</summary>
        public const float RUNNING_SHARE = 0.7f;

        // The parts that keep numbers (the body's mover and pathing, the game's components), found once
        private readonly List<IAgentNumbers> _numberParts = new();
        private bool _numberPartsFound;
        private static readonly List<string> NumberNames = new();

        private IHealth _health;
        private bool _healthSearched;
        private float _healthSearchedAt;

        /// <summary>Seconds between two looks for the game's health while the character has none.</summary>
        private const float HEALTH_SEARCH_EVERY = 1f;

        public bool TryGetStat(CharacterStat stat, out float value)
        {
            value = 0f;
            if (!_initialized || Context == null)
                return false;

            switch (stat)
            {
                case CharacterStat.Health:
                    return TryHealth(out value, out _);
                case CharacterStat.MaxHealth:
                    return TryHealth(out _, out value);
                case CharacterStat.HealthPercent:
                    if (!TryHealth(out float current, out float max) || max <= 0f)
                        return false;

                    value = current / max * 100f;
                    return true;
                case CharacterStat.Speed:
                {
                    if (Context.Actuator == null)
                        return false;

                    Vector3 velocity = Context.Actuator.Body.Velocity;
                    velocity.y = 0f;
                    value = velocity.magnitude;
                    return true;
                }
                case CharacterStat.RunSpeed:
                    return TryRunSpeed(out value);
                case CharacterStat.TurnSpeed:
                    return TryNumber(BodyNumbers.TURN_SPEED, out value);
                case CharacterStat.Acceleration:
                    return TryNumber(BodyNumbers.ACCELERATION, out value);
                case CharacterStat.Height:
                    if (Context.Actuator == null)
                        return false;

                    value = Context.Actuator.Body.Height;
                    return true;
                case CharacterStat.Radius:
                    if (Context.Actuator == null)
                        return false;

                    value = Context.Actuator.Body.Radius;
                    return true;
                case CharacterStat.UsesLeft:
                    if (UsesLeft < 0)
                        return false;                   // a weapon with no end (or none): nothing to count

                    value = UsesLeft;
                    return true;
                default:
                    return false;
            }
        }

        public bool TryGetState(CharacterState state, out bool value)
        {
            value = false;
            if (!_initialized || Context == null)
                return false;

            switch (state)
            {
                case CharacterState.Moving:
                    if (!TryGetStat(CharacterStat.Speed, out float speed))
                        return false;

                    value = speed > MOVING_SPEED;
                    return true;
                case CharacterState.Running:
                    if (!TryGetStat(CharacterStat.Speed, out float now) || !TryRunSpeed(out float run) || run <= 0f)
                        return false;

                    value = now > run * RUNNING_SHARE;
                    return true;
                case CharacterState.InAir:
                    if (Context.Actuator == null)
                        return false;

                    // Off the ground and not on a wall (a climber is held by the wall), or in a jump
                    value = (_body?.Jump ?? JumpPhase.None) != JumpPhase.None ||
                            !Context.Actuator.Body.IsGrounded && (_body?.ClimbDirection ?? 0) == 0;
                    return true;
                case CharacterState.Climbing:
                    value = (_body?.ClimbDirection ?? 0) != 0;
                    return true;
                case CharacterState.Dashing:
                    value = Context.Actuator is IDashActuator { IsDashing: true };
                    return true;
                case CharacterState.Attacking:
                    value = BlowPhase != AbilityPhase.None;
                    return true;
                case CharacterState.WindingUp:
                    value = BlowPhase == AbilityPhase.WindUp;
                    return true;
                case CharacterState.Recovering:
                    value = BlowPhase == AbilityPhase.Recovery;
                    return true;
                case CharacterState.Blocking:
                    value = Context.IsBlocking;
                    return true;
                case CharacterState.Staggered:
                    value = Context.IsStaggered;
                    return true;
                case CharacterState.UsingAbility:
                    value = Abilities.IsAnyRunning;
                    return true;
                case CharacterState.Dead:
                    if (!TryHealth(out float health, out _))
                        return false;

                    value = health <= 0f;
                    return true;
                case CharacterState.Carrying:
                    value = Item != null;
                    return true;
                case CharacterState.Inside:
                    value = IsInside;
                    return true;
                case CharacterState.InCombat:
                {
                    AgentState brain = _instance?.Graph.Brain;
                    value = brain != null && CombatBrainTask.LastFactsOf(brain, Context).Sees;
                    return true;
                }
                case CharacterState.Armed:
                    value = Context.Weapon != null;
                    return true;
                case CharacterState.WeaponShoots:
                    value = Context.Weapon != null && Context.Weapon.Ranged;
                    return true;
                case CharacterState.HoldsPickedWeapon:
                    value = FromWorld != null;
                    return true;
                case CharacterState.Occupying:
                    if (Context.Occupancy == null)
                        return false;

                    value = Context.Occupancy.Occupied != null;
                    return true;
                case CharacterState.InCover:
                    if (Context.Occupancy == null)
                        return false;

                    value = Context.Occupancy.InCover;
                    return true;
                default:
                    // Swimming, Flying, Aiming, Healing: the body and the module do not know them — no data
                    return false;
            }
        }

        /// <summary>A number a part of the character keeps under this name (a game's Stamina, Morale); false — none does.</summary>
        public bool TryGetCustom(string name, out float value) => TryNumber(name, out value);

        // The phase of the blow the character strikes now: a weapon's move or an ability asset writes it into the agent's
        // status (AgentContext.AttackPhase); an ability a game's own runner plays says it itself (IAbilityRunner.Phase)
        private AbilityPhase BlowPhase =>
            Context.AttackPhase != AbilityPhase.None ? Context.AttackPhase : Abilities.RunningPhase;

        /// <summary>The part of the character that keeps the number of this name, and its index there; false — none.</summary>
        internal bool TryNumberPart(string name, out IAgentNumbers part, out int index)
        {
            part = null;
            index = -1;
            if (string.IsNullOrEmpty(name) || !_initialized)
                return false;

            FindNumberParts();
            for (int p = 0; p < _numberParts.Count; p++)
            {
                NumberNames.Clear();
                _numberParts[p].NamesOf(NumberNames);
                int at = NumberNames.IndexOf(name);
                if (at < 0)
                    continue;

                NumberNames.Clear();
                part = _numberParts[p];
                index = at;
                return true;
            }

            NumberNames.Clear();
            return false;
        }

        private bool TryNumber(string name, out float value)
        {
            value = 0f;
            if (!TryNumberPart(name, out IAgentNumbers part, out int index))
                return false;

            value = part.GetNumber(index);
            return true;
        }

        // The run speed: the body's own number for it, else what the body says its top speed is
        private bool TryRunSpeed(out float value)
        {
            if (TryNumber(BodyNumbers.RUN_SPEED, out value))
                return true;

            value = Context.Actuator != null ? Context.Actuator.Body.MaxSpeed : 0f;
            return value > 0f;
        }

        // The game's health on the character: looked for once — again if it was destroyed, and, while there is none, at
        // most once a second (a game may add it after the character woke up)
        private bool TryHealth(out float current, out float max)
        {
            current = max = 0f;
            bool gone = _health is Object destroyed && destroyed == null;
            if (!_healthSearched || gone || _health == null && Context.LocalTime - _healthSearchedAt >= HEALTH_SEARCH_EVERY)
            {
                _healthSearched = true;
                _healthSearchedAt = Context.LocalTime;
                _health = CharacterStatuses.HealthOf(EntityRoot != null ? EntityRoot : transform);
            }

            if (_health == null || _health is Object lost && lost == null)
                return false;

            current = _health.Current;
            max = _health.Max;
            return true;
        }

        // The parts that keep numbers: the body's (its mover, its pathing) and the components of the character
        private void FindNumberParts()
        {
            if (_numberPartsFound)
                return;

            _numberPartsFound = true;
            _numberParts.Clear();
            _body?.CollectNumbers(_numberParts);
            foreach (IAgentNumbers part in (EntityRoot != null ? EntityRoot : transform).GetComponentsInChildren<IAgentNumbers>(true))
            {
                if (!_numberParts.Contains(part))
                    _numberParts.Add(part);
            }
        }
    }
}
