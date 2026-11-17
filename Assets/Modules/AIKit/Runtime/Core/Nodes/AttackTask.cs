using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// Attacks an object (a Transform variable — bound on the character or wired from a sensor; empty — the Brain's
    /// target) with the weapon in the character's hand, by the move picked in Move — a slot every weapon of the
    /// character has ("Attack 1", "Heavy"; empty — the weapon's first move): the graph does not care which weapon it is,
    /// the weapon's numbers and clips make the difference. It strikes for as long as the target is within the move's
    /// reach: move after move, and the agent stays in the node in between. The node only commands and waits (handbook
    /// §20.1: the graph asks, the body acts and answers): the character carries the move out (AttackRunner) — a blade
    /// swings and strikes the target in reach, a weapon that shoots asks for a place to shoot from, draws and lets a
    /// projectile go. One run of the task is one move. The task repeats (AgentTask.Repeats): after a move its node
    /// runs it again (its Done pin, wired, leads on after each move), and the Brain takes another decision only between
    /// the moves, never in the middle of one — a move that has started runs to its end.
    /// Failure ("Out Of Reach") — when a move should start, or after one, it cannot be made from here: the target is out
    /// of the move's reach, behind a cover (a move that needs a clear line), there is none, or no place to shoot from:
    /// come closer, or do something else. With no weapon in the hand, or no move of that name on it, the node never
    /// starts a move (the character's inspector and Validate All say so).
    /// With no arrow from this pin the agent stands and attacks again once it can.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Actions/Combat/Attack", "Task", "aikit.task.attack", Success = "Done", Failure = "Out Of Reach", Simple = true)]
    public sealed class AttackTask : AgentTask
    {
        private sealed class Mem
        {
            public readonly WeaponAttack Attack = new();
        }

        /// <summary>The Transform variable (of the agent) to strike; empty — the Brain's target.</summary>
        [VariableName(VariableType.Transform)] public string Target;

        /// <summary>The move (slot) of the weapon in the hand to strike with; empty — the weapon's first move.</summary>
        [WeaponMoveName] public string Move = "";

        // Shared by every agent of the graph: caches the name's id only, never a value
        [NonSerialized] private BlackboardVariable<Transform> _target;

        /// <summary>Move after move: the node runs the task again, and the Brain waits for the end of a move to take another decision.</summary>
        public override bool Repeats => true;

        /// <summary>The blow of this agent's current (or last) run has been delivered and landed.</summary>
        public bool LandedFor(AgentContext context) => Memory<Mem>(context).Attack.Landed;

        /// <summary>The move this node strikes with needs a clear line to the target (a shot) — with this weapon.</summary>
        public bool NeedsSightWith(WeaponAsset weapon) => weapon != null && weapon.Find(Move) is { NeedsSight: true };

        /// <summary>The move this node strikes with needs a clear line to the target — with the agent's weapon.</summary>
        public bool NeedsSightFor(AgentContext context) => NeedsSightWith(context?.Weapon);

        private Transform TargetOf(AgentContext context) =>
            string.IsNullOrEmpty(Target) ? context.BrainTarget : _target.Get(context.Blackboard, Target);

        protected override void OnEnter(AgentContext context)
        {
            Mem m = Memory<Mem>(context);
            WeaponAsset weapon = context?.Weapon;
            m.Attack.Reset(weapon, weapon?.Find(Move), context != null ? TargetOf(context) : null,
                context?.Position ?? Vector3.zero);
            if (weapon == null || m.Attack.Move == null)
                return;                                 // nothing to strike with: the task fails on its first tick

            AttackRunner.Begin(context, m.Attack);
        }

        protected override TaskStatus OnTick(AgentContext context, float dt)
        {
            if (context == null)
                return TaskStatus.Failure;

            WeaponAttack attack = Memory<Mem>(context).Attack;
            if (attack.Weapon == null || attack.Move == null)
                return TaskStatus.Failure;

            attack.Target = TargetOf(context);
            attack.MayFace = !attack.Struck && attack.Target != null && Claim(context, BodyAxis.Face, MoveAuthority.Frozen);
            return AttackRunner.Tick(context, attack, dt);
        }

        // Left — at the end of a move or taken away: what the attack holds is put down (a move cut short drops its action)
        protected override void OnExit(AgentContext context, ExitReason reason) =>
            AttackRunner.End(context, Memory<Mem>(context).Attack);
    }
}
