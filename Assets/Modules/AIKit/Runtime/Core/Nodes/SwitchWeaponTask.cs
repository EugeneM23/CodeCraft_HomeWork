using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// Takes up another of the character's weapons: the body stands, the weapon in the hand goes on the back (its
    /// Sheathe Action; at its Sheathe At the model is on the back), then the new one comes into the hand (its Draw
    /// Action; at its Draw At the model is in the hand and the Attack node strikes with its moves from then on). The
    /// numbers are the weapon's (read from its clips — handbook §20.3); without an animator they are the whole
    /// switch. The weapon already in the hand — Done at once, nothing is played. A switch that has started is not
    /// taken away by the Brain (CommitsFor); a switch cut short anyway is finished at once, so the model in the hand and
    /// the weapon the character strikes with never part. Not Carried — the character has no such weapon.
    /// A weapon taken from the world (a rock, a turret) is none of the character's: the node switches the character's
    /// own weapons only. With one in the hand, the own weapon on the back is changed at once, with nothing shown, and is
    /// the one that comes back when the rock is spent.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Actions/Combat/Switch Weapon", "Task", "aikit.task.switch-weapon", Success = "Done", Failure = "Not Carried",
        Simple = true)]
    public sealed class SwitchWeaponTask : AgentTask
    {
        private enum Phase
        {
            None,
            PuttingAway,
            TakingUp,
            Done
        }

        private sealed class Mem
        {
            public Phase Phase;
            public float Time;
            public bool Moved;              // the model of this phase is where it goes
            public WeaponAsset From;
            public WeaponAsset To;
            public bool Shows;              // the body shows this phase's action
        }

        /// <summary>Weapon: the one after the weapon in the hand in the character's list, round to the first.</summary>
        public const string NEXT = "Next";

        /// <summary>The weapon to take up (one the character carries, by its asset's name), or Next.</summary>
        [WeaponName] public string Weapon = "";

        // The character's own weapon in the hand (behind a weapon taken from the world while one is held)
        private static WeaponAsset OwnOf(AgentContext context) => context?.Rack?.Own ?? context?.Weapon;

        private static WeaponAsset Wanted(AgentContext context, string weapon) =>
            weapon == NEXT ? context?.Rack?.After(OwnOf(context)) : context?.Rack?.Find(weapon);

        /// <summary>One action: started once, it runs out by itself.</summary>
        public override bool IsOneShot => true;

        /// <summary>A switch that has started runs to its end: the Brain does not take the agent elsewhere meanwhile.</summary>
        public override bool CommitsFor(AgentContext context) =>
            IsRunningFor(context) && Memory<Mem>(context).Phase is Phase.PuttingAway or Phase.TakingUp;

        protected override void OnEnter(AgentContext context)
        {
            Mem m = Memory<Mem>(context);
            m.Phase = Phase.None;
            m.Time = 0f;
            m.Moved = false;
            m.Shows = false;
            m.From = OwnOf(context);
            m.To = Wanted(context, Weapon);
            if (m.To == null)
                return;                                     // not carried: the task fails on its first tick

            if (m.To == m.From)
            {
                m.Phase = Phase.Done;                       // already in the hand
                return;
            }

            if (context.Rack != null && context.Rack.HoldsFromWorld)
            {
                // The hand holds a weapon from the world: the own one on the back is changed, nothing is shown
                context.Rack.TakeUp(m.To);
                m.Phase = Phase.Done;
                return;
            }

            context.Navigation?.Clear();
            context.Actuator?.Stop();
            if (m.From != null)
                Begin(context, m, Phase.PuttingAway, m.From.SheatheAction);
            else
                TakingUp(context, m);
        }

        // The hand is empty: the animator shows the new weapon's clips from now on, and the body takes it up with them
        private static void TakingUp(AgentContext context, Mem m)
        {
            context.Rack?.ShowMotionsOf(m.To);
            Begin(context, m, Phase.TakingUp, m.To.DrawAction);
        }

        protected override TaskStatus OnTick(AgentContext context, float dt)
        {
            if (context == null)
                return TaskStatus.Failure;

            Mem m = Memory<Mem>(context);
            if (m.Phase == Phase.Done)
                return TaskStatus.Success;
            if (m.To == null)
                return TaskStatus.Failure;

            if (Claim(context, BodyAxis.Move, MoveAuthority.Frozen))
                context.Actuator?.Stop();

            m.Time += dt;
            if (m.Phase == Phase.PuttingAway)
            {
                if (!m.Moved && m.Time >= m.From.SheatheAt)
                {
                    m.Moved = true;
                    context.Rack?.PutAway(m.From);
                }

                if (m.Time < Mathf.Max(m.From.SheatheTime, m.From.SheatheAt))
                    return TaskStatus.Running;

                TakingUp(context, m);
                return TaskStatus.Running;
            }

            if (!m.Moved && m.Time >= m.To.DrawAt)
                TakeUp(context, m);

            if (m.Time < Mathf.Max(m.To.DrawTime, m.To.DrawAt))
                return TaskStatus.Running;

            m.Phase = Phase.Done;
            return TaskStatus.Success;
        }

        // Cut short in the middle: finished at once — the new weapon is in the hand if the old one was already put
        // away, else the old one stays where it is (its action is dropped)
        protected override void OnExit(AgentContext context, ExitReason reason)
        {
            Mem m = Memory<Mem>(context);
            if (m.Phase is not (Phase.PuttingAway or Phase.TakingUp) || context == null)
                return;

            if (m.Shows)
                context.Actions.End(true);

            bool handEmpty = m.Phase == Phase.PuttingAway ? m.Moved : !m.Moved;
            if (handEmpty)
                TakeUp(context, m);

            m.Phase = Phase.None;
        }

        private static void Begin(AgentContext context, Mem m, Phase phase, string action)
        {
            m.Phase = phase;
            m.Time = 0f;
            m.Moved = false;
            m.Shows = false;
            if (string.IsNullOrEmpty(action))
                return;

            context.Actions.Begin(action);
            m.Shows = context.Actions.CanShow(action);
        }

        private static void TakeUp(AgentContext context, Mem m)
        {
            m.Moved = true;
            if (context.Rack != null)
                context.Rack.TakeUp(m.To);
            else
                context.Weapon = m.To;
        }
    }
}
