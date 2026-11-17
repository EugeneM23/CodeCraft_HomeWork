using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// The body of a character, picked in its inspector: makes the parts the brain drives (the "gamepad", the
    /// navigator, the eyes) as plain objects and hands them over. A body kind lives in any assembly (NavMesh, 2D).
    /// </summary>
    [Serializable]
    public abstract class AgentBody
    {
        /// <summary>Unity components this body needs on the character (added by the inspector).</summary>
        public virtual Type[] RequiredComponents => Type.EmptyTypes;

        /// <summary>Called once, before the brain asks for any part.</summary>
        public abstract void Attach(Transform owner);

        public abstract IAgentActuator Actuator { get; }

        public virtual INavigator Navigator => null;

        public virtual ISightProbe Sight => null;

        public virtual IGroundProbe Ground => null;

        /// <summary>What the body's eyes find around it for a Senses sensor of the graph (null — it sees nothing).</summary>
        public virtual ITargetScanner Scanner => null;

        /// <summary>How far the body can go straight from a point (walls, edges); null — every way is free.</summary>
        public virtual IWalkProbe Walk => null;

        /// <summary>How the body lets a projectile fly (an arrow, a thrown rock, a lob); null — it lands at once.</summary>
        public virtual IProjectileLauncher Launcher => null;

        /// <summary>The body's numbers for the blackboard ("Speed", "Turn Speed"…).</summary>
        public virtual void CollectNumbers(List<IAgentNumbers> owners)
        {
        }

        /// <summary>
        /// The same numbers before the body is attached (the editor's Blackboard panel): names and the values the parts
        /// would publish, read from the settings and the owner's components without touching them.
        /// </summary>
        public virtual void DescribeNumbers(Transform owner, List<(string Name, float Value)> numbers)
        {
        }

        /// <summary>
        /// The clips move and turn this body — root motion, handbook §20.2: of actions (states tagged Action: blows,
        /// turns, a hit) and, with WalksByAnimation, of the walk too. The animator hands every frame's motion of the root
        /// to MoveByAnimation.
        /// </summary>
        public virtual bool UsesRootMotion => false;

        /// <summary>
        /// The clips carry the walk too (full root motion): the body works out where to go and how fast, the animator is
        /// shown that (ShownVelocity) instead of the speed the body had, and the walking clip moves the body.
        /// </summary>
        public virtual bool WalksByAnimation => false;

        /// <summary>
        /// With WalksByAnimation: the speed the body is asked to move at now (the path's, a step's, the crowd's word on
        /// it included) — what the animator shows, so the clips carry the body that way. Metres per second, flat.
        /// </summary>
        public virtual Vector3 ShownVelocity => Vector3.zero;

        /// <summary>
        /// The animator moved the root this frame (dt seconds) by deltaPosition / deltaRotation; inAction — an action
        /// plays (or is being blended into), not the walk. A body that takes it puts itself where that leads, on the
        /// ground it may stand on, and returns how far it really went (flat); one that does not — zero.
        /// </summary>
        public virtual Vector3 MoveByAnimation(Vector3 deltaPosition, Quaternion deltaRotation, bool inAction, float dt) =>
            Vector3.zero;

        /// <summary>
        /// The body is on a wall now: 1 — climbing up, −1 — down, 0 — not climbing. A fact of the body for the picture
        /// (handbook §20.1: the animator reads the body, not the AI). A body that does not climb is never on a wall.
        /// </summary>
        public virtual int ClimbDirection => 0;

        /// <summary>
        /// The climbing clip plays now: 1 — up, −1 — played back down, 0 — not. On a climb the body also steps to the
        /// wall and off it (from and to the walkable ground): those are steps of a walk and shown as a walk.
        /// </summary>
        public virtual int ClimbClipDirection => ClimbDirection;

        /// <summary>
        /// The body jumps now (down off a wall, over a jump link): taking off, in the air, landing — a fact of the body
        /// for the picture (handbook §20.1). A body that does not jump is never in a jump.
        /// </summary>
        public virtual JumpPhase Jump => JumpPhase.None;

        /// <summary>
        /// The body executes edges it needs clips' numbers for (it climbs walls or jumps over links): the editor measures
        /// the character's climbing and jumping clips into its Traversal (step 90, C20).
        /// </summary>
        public virtual bool Traverses => false;

        /// <summary>
        /// The climbing and jumping clips of the character, as the editor measured them (the body keeps them: they are
        /// its data, handbook §13.3); null — the body takes none. Set in the editor (Measure) or by a test.
        /// </summary>
        public virtual MeasuredTraversal Traversal
        {
            get => null;
            set { }
        }

        /// <summary>Once per rendered frame (Update), for what follows the frame and not the brain's step.</summary>
        public virtual void FrameTick(float dt)
        {
        }

        /// <summary>
        /// The character has gone inside a building (Go Inside) or come out: while inside it is in nobody's way — the body
        /// steps aside however it can (a NavMesh Body is the least important to give way to). Nothing by default.
        /// </summary>
        public virtual void SetInside(bool inside)
        {
        }

        /// <summary>The character is switched on again (after Attach): the body may pick up what it dropped.</summary>
        public virtual void OnEnable()
        {
        }

        /// <summary>
        /// The character is switched off (after Attach): the body ends what is in progress (a jump in the air) so that
        /// switching it on again does not resume a stale motion.
        /// </summary>
        public virtual void OnDisable()
        {
        }
    }

    /// <summary>
    /// Where a body is in a jump (AgentBody.Jump: down off a wall, over a jump link): an animator shows start → in the
    /// air → landing.
    /// </summary>
    public enum JumpPhase
    {
        None = 0,
        TakeOff = 1,
        InAir = 2,
        Landing = 3
    }
}
