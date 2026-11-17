using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// Keep Formation: the member goes to its slot in the squad's formation and stands there, facing where the ranks face
    /// (round a surrounded foe — the foe), and keeps it while the formation marches, turns or closes round a foe: far off,
    /// it runs there by the path; near, it steps with its slot (the slot's own speed and the way back to it), so the ranks
    /// move as one and nobody overruns the line. A foe in front within the weapon's reach turns the member's head to it.
    /// Success ("In Reach") — the member stands on its slot (in the front rank — the inner ring — with Front Only), its
    /// own target (its Brain's: what it sees) is within the reach of its weapon's first move, and it has a turn to strike
    /// that foe (no more than the Squad Brain's Strikers Per Foe at once): lead it to Strike, and from Strike back here —
    /// the turn goes to the next one when it comes back. Failure ("No Squad") — the character marches in no squad. Pace —
    /// how fast it walks to its slot from afar (a part of its full speed).
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Actions/Squad/Keep Formation", "Task", "aikit.task.keep-formation", Success = "In Reach", Order = 10,
        Failure = "No Squad", Simple = true)]
    public sealed class KeepFormationTask : AgentTask
    {
        /// <summary>Farther than this off its slot the member takes the path there (round walls and others).</summary>
        private const float FAR = 4f;

        /// <summary>Metres short of its slot the straight way may end and still count as clear (the body's own room).</summary>
        private const float BLOCKED_SLACK = 0.3f;

        /// <summary>Within this of a slot that stands the member stops; it walks again only when it is SETTLE_OFF away.</summary>
        private const float SETTLE = 0.25f;
        private const float SETTLE_OFF = 0.6f;

        /// <summary>How hard the member makes up its distance to the slot: metres per second per metre off it.</summary>
        private const float GAIN = 2f;

        /// <summary>The slowest step that is still a step (a part of full speed): slower than this the feet only shuffle.</summary>
        private const float SLOWEST = 0.15f;

        /// <summary>A slot moving slower than this (m/s) stands.</summary>
        private const float STANDING_SLOT = 0.05f;

        /// <summary>Metres off its slot within which a member of the front rank may go and strike.</summary>
        private const float STRIKE_FROM = 1.5f;

        /// <summary>A foe nearer than this many reaches, in front (within FACE_ANGLE of the ranks' facing), is looked at.</summary>
        private const float FACE_FOE = 1.6f;
        private const float FACE_ANGLE = 75f;

        /// <summary>
        /// On — only the front rank (the inner ring) goes to strike, the ranks behind wait for a place in front (swords,
        /// spears). Off — every rank strikes from its slot (archers shoot over the front rank).
        /// </summary>
        public bool FrontOnly = true;

        /// <summary>How fast it goes to its slot from afar, as a part of the body's full speed (villagers walk, soldiers run).</summary>
        public BlackboardValue<float> Pace = 1f;

        /// <summary>
        /// What decides "In Reach" besides the reach (spec 10.2: a check the node makes is said on the node): the squad
        /// gives the turns to strike — the Squad Brain's Strikers Per Foe at one foe at once, a rest after the member's own
        /// blow, a turn held a few seconds at most.
        /// </summary>
        public override string Requirement =>
            "strikes only on a turn its squad gives: Strikers Per Foe at one foe (its Squad Brain's), a rest after its own blow";

        private sealed class Mem
        {
            public bool Settled;
            public bool Round;                   // going round something to its slot by the path: kept till it is there
        }

        protected override void OnEnter(AgentContext context)
        {
            Memory<Mem>(context).Settled = false;
            Memory<Mem>(context).Round = false;
            context?.Squad?.EndStrike(context);         // back from a blow: the turn goes to the next one
        }

        protected override TaskStatus OnTick(AgentContext context, float dt)
        {
            Squad squad = context?.Squad;
            if (squad == null || !squad.Has(context))
                return TaskStatus.Failure;

            IAgentActuator actuator = context.Actuator;
            if (actuator == null)
                return TaskStatus.Failure;

            if (!squad.SlotOf(context, out Vector3 slot, out Vector3 look, out Vector3 velocity, out bool front))
            {
                actuator.Stop();                        // the ranks are not dealt yet: it stands for a moment
                return TaskStatus.Running;
            }

            Vector3 position = context.Position;
            Vector3 offset = slot - position;
            offset.y = 0f;
            float distance = offset.magnitude;

            Transform foe = context.BrainTarget;
            float reach = ReachOf(context);
            float toFoe = foe != null ? Flat(foe.position - position).magnitude : float.MaxValue;
            if ((front || !FrontOnly) && distance <= STRIKE_FROM && toFoe <= reach && squad.MayStrike(context, foe, FrontOnly))
                return TaskStatus.Success;

            Mem m = Memory<Mem>(context);
            INavigator navigation = context.Navigation;

            // Near its slot, but something stands between (a well, a wall): straight steps would push into it — the way
            // round it is the path, as from afar, and once it goes round it keeps to the path till it is at its slot (a
            // straight look from every step along the way would turn it back into the obstacle)
            bool round = m.Round && distance > SETTLE_OFF;
            bool blocked = !round && distance > SETTLE_OFF && context.Walk != null &&
                           context.Walk.FreeDistance(position, offset / distance, distance) < distance - BLOCKED_SLACK;
            if ((distance > FAR || round || blocked) && navigation != null)
            {
                // Far off its slot (or cut off from it): the way there round whatever stands between (a path)
                m.Settled = false;
                if (navigation is INavigationPace pace)
                    pace.Pace = Pace.Get(context);
                if (navigation.SetDestination(slot).CanGo() && navigation.Status.IsMoving())
                {
                    m.Round = distance <= FAR;
                    if (Claim(context, BodyAxis.Move, MoveAuthority.Path))
                        navigation.Step(dt);
                    return TaskStatus.Running;
                }
            }

            m.Round = false;
            navigation?.Clear();
            bool slotStands = velocity.magnitude < STANDING_SLOT;
            if (slotStands && distance <= (m.Settled ? SETTLE_OFF : SETTLE))
            {
                m.Settled = true;
                if (Claim(context, BodyAxis.Move, MoveAuthority.Strafe))
                    actuator.Stop();
            }
            else
            {
                // Near: step with the slot, and make up the distance to it
                m.Settled = false;
                Vector3 want = velocity + offset * GAIN;
                float most = Mathf.Max(0.1f, actuator.Body.MaxSpeed);
                float scale = Mathf.Clamp(want.magnitude / most, SLOWEST, 1f);
                if (Claim(context, BodyAxis.Move, MoveAuthority.Strafe))
                    actuator.Move(want, scale);
            }

            Vector3 face = look;
            if (foe != null && toFoe <= reach * FACE_FOE &&
                Vector3.Angle(look, Flat(foe.position - position)) <= FACE_ANGLE)
                face = Flat(foe.position - position);
            if (Claim(context, BodyAxis.Face, MoveAuthority.Strafe))
                actuator.Face(face);
            return TaskStatus.Running;
        }

        protected override void OnExit(AgentContext context, ExitReason reason)
        {
            if (context?.Navigation is INavigationPace pace)
                pace.Pace = 1f;
            context?.Navigation?.Clear();
            if (reason == ExitReason.Finished)
                return;                                 // In Reach: the blow goes on from here, it keeps its turn

            context?.Actuator?.Stop();
            context?.Squad?.EndStrike(context);
        }

        // How far the member's weapon reaches with its first move (the move a plain Strike makes); none — not at all
        private static float ReachOf(AgentContext context)
        {
            WeaponAsset weapon = context.Weapon;
            WeaponMove move = weapon != null ? weapon.Find("") : null;
            return move != null ? move.Reach : 0f;
        }

        private static Vector3 Flat(Vector3 vector) => new(vector.x, 0f, vector.z);
    }
}
