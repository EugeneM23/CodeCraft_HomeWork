using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace AIKit.NavMeshNavigation
{
    /// <summary>
    /// A body on Unity's NavMesh: the "gamepad" over a NavMeshAgent, paths with local avoidance, the eyes of 3D physics
    /// and, when switched on, flights over the links of the Jump area. Needs a NavMeshAgent on the character.
    /// </summary>
    [Serializable]
    public sealed class NavMeshBody : AgentBody, ILegacyTraversal
    {
        [Tooltip("Degrees per second the body turns to where the AI looks")]
        [Min(1f)]
        public float TurnSpeed = 720f;

        [Tooltip("Metres per second each second the body speeds up and brakes with — on a path, on a hand-held step and " +
                 "when it stops. High enough to look eager (8 brings a walk up in a fifth of a second), not so high " +
                 "that the body jumps to speed in one frame and the feet slide (the agent's own setting is replaced). " +
                 "Ignored where Ramp Distance is set")]
        [Min(0.5f)]
        public float Acceleration = 8f;

        [Tooltip("Metres the body takes to get up to the speed it is given, and to stop again — the distance the feet " +
                 "of the start and stop clips cover on the ground (handbook §20.2: the body moves the way the clips " +
                 "show, or they slide). The acceleration is worked out from it for whatever speed the body walks at, " +
                 "so a slow step starts gently and a run bursts off. 0 — Acceleration is used as it stands")]
        [Min(0f)]
        public float RampDistance;

        [Tooltip("Metres the body stands below the baked NavMesh (a negative number lowers it). A NavMesh is baked a few " +
                 "centimetres above the ground it was built from, and a character put straight onto it walks in the air")]
        public float GroundOffset;

        [Tooltip("On — the body takes an avoidance priority of its own, the same one every run. Bodies of one priority " +
                 "give way to each other at the same moment, step back into each other and shiver; with a priority " +
                 "each, one of the two goes first. Off — the priority set on the NavMeshAgent is kept")]
        public bool OwnAvoidancePriority = true;

        [Tooltip("How far from the asked point the navigator may snap onto the mesh")]
        [Min(0.1f)]
        public float SampleRadius = 3f;

        [Tooltip("A new destination closer than this to the current one does not trigger a new path — chasing a " +
                 "moving target would otherwise recalculate the path every single frame")]
        [Min(0f)]
        public float RepathDistance = 0.75f;

        [Header("Root motion")]
        [Tooltip("On — the clips of actions (states tagged Action: blows, turns in place, a hit) move and turn the body " +
                 "as they go, kept on the NavMesh: a blow steps forward as far as its clip does, a turn comes round with " +
                 "the feet (handbook §20.2). Walking stays the agent's. Needs an animated character whose action clips " +
                 "carry root motion (the root's position and turn not baked into the pose)")]
        public bool ActionRootMotion;

        [Tooltip("On — the clips move the body all the time, the walk too (handbook §20.2): the agent works out the path " +
                 "and the way round the others, the animator is shown that speed, and the body goes where the root of the " +
                 "clip goes, kept on the NavMesh; the feet never slide. Actions as with Action Root Motion. Needs an " +
                 "animated character whose clips (walk, start, stop) carry root motion. Off — Action Root Motion decides")]
        public bool RootMotion;

        [Tooltip("With Root Motion: how much the walk may be stretched or shortened to keep to the path's pace (0.25 — up " +
                 "to a quarter faster or slower than the clip goes; 0 — exactly as the clip goes). Its direction always " +
                 "keeps to the path: the crowd's way round is kept. Actions (blows, dodges) are never stretched")]
        [Range(0f, 1f)]
        public float PaceGive = 0.25f;

        [Tooltip("With Root Motion: metres per second two bodies standing inside each other are pushed apart at (the " +
                 "agent's own push no longer moves a body the animation carries). 0 — never")]
        [Min(0f)]
        public float PushApart = 1.5f;

        [Header("Eyes")]
        [Tooltip("What blocks sight (usually the level geometry)")]
        public LayerMask SightBlockers = 1;

        [Tooltip("What counts as ground")]
        public LayerMask GroundLayers = 1;

        [Tooltip("Where the eyes look for targets — read only by a Senses sensor of the graph; only entities with an " +
                 "IPerceivable count (empty — the character perceives nothing)")]
        public LayerMask TargetLayers;

        [Header("Edges")]
        [Tooltip("On — paths may run over the links of the Jump area: the body takes off, flies over the link under " +
                 "gravity and lands, the way its jumping clips go (measured, see below)")]
        public bool JumpsOverLinks;

        [Tooltip("On — the body can climb walls: the links of the Climb area. Its paths run over them only while the " +
                 "graph allows it (the Climb To node); then the body climbs up or down the wall the way its clip goes " +
                 "(measured, see below)")]
        public bool ClimbsWalls;

        [Tooltip("On — down a wall the body jumps: steps to its edge, takes off, falls under gravity and lands (Jump " +
                 "Phase for the animator); off — it climbs down, the climb played back")]
        public bool ClimbsDownByJumping = true;

        /// <summary>Metres from the edge of the top a body jumping down takes off from (its feet at the edge).</summary>
        public const float JUMP_FROM_EDGE = 0.15f;

        /// <summary>
        /// Metres per second squared a jumping or falling body is pulled down with: Physics.gravity's (the user's word, C20:
        /// the fall is gravity's — nothing speeds it up).
        /// </summary>
        public static float Gravity => Mathf.Max(0.1f, Physics.gravity.magnitude);

        /// <summary>
        /// Metres apart along a wall bodies climb (or jump down) at the least: a body that comes to the wall nearer than
        /// this to one on it waits at the foot until that one is off (handbook §13.4: two capsules on one narrow edge read
        /// as a fault; the user's number, 14.11.2026).
        /// </summary>
        public const float CLIMB_SPACING = 1.5f;

        /// <summary>
        /// Seconds a climber's answer to "can I get there on foot, or only by climbing?" is kept for a target that has
        /// not moved: the question plans two paths.
        /// </summary>
        public const float CLIMB_CHECK_INTERVAL = 0.5f;

        // The character's climbing and jumping clips as the editor measured them (step 90, C20): the body's own data,
        // shown by the inspector as one line — never typed
        [SerializeField, HideInInspector] private MeasuredTraversal _traversal = new();

        // ---------- the numbers bodies had before step 90, read only by the migration (Measure); delete after it ----------
        // (the old names: the scenes saved before keep them under these)

        [SerializeField, HideInInspector] private float ClimbTime;
        [SerializeField, HideInInspector] private AnimationCurve ClimbRise = new();
        [SerializeField, HideInInspector] private AnimationCurve ClimbAdvance = new();
        [SerializeField, HideInInspector] private float ClimbReach;
        [SerializeField, HideInInspector] private float ClimbFromWall = 0.15f;
        [SerializeField, HideInInspector] private float JumpTakeOff = 0.2f;
        [SerializeField, HideInInspector] private float JumpLanding = 0.4f;

        /// <summary>
        /// The climbing numbers this body had before step 90 (measured by the lab then, kept in the scene), as measured
        /// data — for the migration when there is no clip to measure; false — it had none.
        /// </summary>
        public bool TryTakeLegacyTraversal(out MeasuredTraversal legacy)
        {
            legacy = null;
            if (ClimbTime <= 0.01f && ClimbReach <= 0.01f)
                return false;

            legacy = new MeasuredTraversal
            {
                MeasuredFrom = "the body's numbers before step 90",
                HasClimb = ClimbTime > 0.01f,
                Climb = new ClimbMotion
                {
                    Time = ClimbTime,
                    Reach = ClimbReach,
                    FromWall = ClimbFromWall,
                    Rise = ClimbRise != null ? new AnimationCurve(ClimbRise.keys) : null,
                    Advance = ClimbAdvance != null ? new AnimationCurve(ClimbAdvance.keys) : null
                },
                HasJump = true,
                Jump = new JumpMotion { TakeOff = JumpTakeOff, Landing = JumpLanding }
            };
            return true;
        }

        /// <summary>The old numbers are moved: they are emptied, so the migration never takes them twice.</summary>
        public void ClearLegacyTraversal()
        {
            ClimbTime = 0f;
            ClimbReach = 0f;
            ClimbRise = new AnimationCurve();
            ClimbAdvance = new AnimationCurve();
        }

        public override bool Traverses => ClimbsWalls || JumpsOverLinks;

        /// <summary>
        /// The character's climbing and jumping clips, measured in the editor (the inspector's Measure, the AI
        /// Animator's): the climb and the jump go the way they do. Never null; unmeasured — the plain way (a straight
        /// climb at the body's speed, a jump with no take-off or landing time).
        /// </summary>
        public override MeasuredTraversal Traversal
        {
            get => _traversal ??= new MeasuredTraversal();
            set => _traversal = value ?? new MeasuredTraversal();
        }

        /// <summary>The way over edges the climber and the jumper read (the measured clips).</summary>
        public ITraversalMotion Motion => Traversal;

        public NavMeshMover Mover { get; private set; }

        public NavMeshPathing Pathing { get; private set; }

        public PhysicsEyes3D Eyes { get; private set; }

        /// <summary>Null when the body does not jump.</summary>
        public NavMeshLinkJumper Jumper { get; private set; }

        /// <summary>Null when the body does not climb.</summary>
        public NavMeshLinkClimber Climber { get; private set; }

        /// <summary>The body is passing a link now (in the air or on a wall): the hand-held steps and turns wait.</summary>
        public bool IsTraversing => Jumper is { IsJumping: true } || Climber is { IsClimbing: true };

        public override int ClimbDirection => Climber?.Direction ?? 0;

        public override int ClimbClipDirection => Climber?.ClipDirection ?? 0;

        public override JumpPhase Jump =>
            Climber is { IsDropping: true } ? Climber.JumpPhase : Jumper?.Phase ?? JumpPhase.None;

        public override Type[] RequiredComponents => new[] { typeof(NavMeshAgent) };

        public override void Attach(Transform owner)
        {
            NavMeshAgent agent = owner.GetComponent<NavMeshAgent>();
            if (agent == null)
            {
                Debug.LogError($"{owner.name}: NavMesh Body needs a NavMeshAgent on the character.", owner);
                return;
            }

            _agent = agent;

            agent.updateRotation = false;
            agent.updateUpAxis = false;
            agent.updatePosition = !RootMotion;     // with root motion the animation puts the body, the agent follows it
            agent.baseOffset = GroundOffset;
            ApplyAcceleration(agent);
            if (OwnAvoidancePriority)
                agent.avoidancePriority = PriorityFor(owner.name);
            Mover = new NavMeshMover(owner, agent, this);
            Mover.Join();
            Pathing = new NavMeshPathing(owner, agent, this);
            Eyes = new PhysicsEyes3D(owner, this);
            if (JumpsOverLinks)
            {
                Jumper = new NavMeshLinkJumper(owner, agent, this);     // takes the flight over and opens the jump area
            }
            else
            {
                agent.areaMask &= ~(1 << NavMeshLinkJumper.JUMP_AREA);
            }

            // A climber takes the walls over (closed until the graph allows them); any other body never walks a wall
            if (ClimbsWalls)
                Climber = new NavMeshLinkClimber(owner, agent, this);
            else
                agent.areaMask &= ~(1 << NavMeshLinkClimber.CLIMB_AREA);
        }

        /// <summary>
        /// Gives the agent the acceleration it should have at the speed it walks now: Ramp Distance says how far the
        /// body takes to get up to that speed (v² / 2s), and with no Ramp Distance the Acceleration as it stands.
        /// Asked again whenever the speed changes — a pace, a number set from the graph.
        /// </summary>
        public void ApplyAcceleration(NavMeshAgent agent)
        {
            if (agent == null)
                return;

            agent.acceleration = RampDistance > 0.01f && agent.speed > 0.01f
                ? agent.speed * agent.speed / (2f * RampDistance)
                : Acceleration;
        }

        /// <summary>
        /// An avoidance priority of this body's own, from its name — the same one every run, spread over the middle of
        /// the range (30…69) so that no two bodies of a crowd are of one mind about who gives way (0 is the most
        /// important, 99 the least; Unity's own default is 50 for everybody).
        /// </summary>
        public static int PriorityFor(string name)
        {
            uint hash = 2166136261;                     // FNV-1a: the same number for a name on any machine and run
            for (int i = 0; i < (name?.Length ?? 0); i++)
            {
                hash ^= name[i];
                hash *= 16777619;
            }

            hash ^= hash >> 13;                         // and a last mix, or "Skeleton 1" and "Skeleton 2" land together
            hash *= 2654435761;
            hash ^= hash >> 16;
            return 30 + (int)(hash % 40);
        }

        public override IAgentActuator Actuator => Mover;

        public override INavigator Navigator => Pathing;

        public override ISightProbe Sight => Eyes;

        public override IGroundProbe Ground => Eyes;

        public override ITargetScanner Scanner => Eyes;

        public override IProjectileLauncher Launcher => ArrowLauncher.Shared;

        public override IWalkProbe Walk => Mover;

        public override void CollectNumbers(List<IAgentNumbers> owners)
        {
            if (Mover != null) owners.Add(Mover);
            if (Pathing != null) owners.Add(Pathing);
        }

        public override void DescribeNumbers(Transform owner, List<(string Name, float Value)> numbers)
        {
            NavMeshAgent agent = owner != null ? owner.GetComponent<NavMeshAgent>() : null;
            numbers.Add((NavMeshMover.TURN_SPEED, TurnSpeed));                         // what the mover publishes
            numbers.Add((NavMeshMover.ACCELERATION, agent != null ? agent.acceleration : 0f));
            numbers.Add((NavMeshPathing.SPEED, agent != null ? agent.speed : 0f));     // what the pathing publishes
        }

        public override bool UsesRootMotion => ActionRootMotion || RootMotion;

        public override bool WalksByAnimation => RootMotion;

        public override Vector3 ShownVelocity => Mover != null ? Mover.ShownVelocity : Vector3.zero;

        public override Vector3 MoveByAnimation(Vector3 deltaPosition, Quaternion deltaRotation, bool inAction, float dt)
        {
            if (Mover == null || IsTraversing)
                return Vector3.zero;                    // a flight over a link or a wall is the jumper's or the climber's

            return Mover.MoveByAnimation(deltaPosition, deltaRotation, inAction, dt);
        }

        public override void OnEnable() => Mover?.Join();

        public override void FrameTick(float dt)
        {
            Climber?.FrameTick(dt);
            if (Climber is not { IsClimbing: true })
                Jumper?.FrameTick(dt);
            Pathing?.AfterLinks();                  // off a link now: the way asked meanwhile is planned
        }

        // The agent and the priority it gives way with while it is out (Go Inside sets the least important meanwhile)
        [System.NonSerialized] private NavMeshAgent _agent;
        [System.NonSerialized] private int _outsidePriority = -1;

        /// <summary>
        /// Inside a building (Go Inside) the body stays at the door, and a neighbour walking along the wall must not be
        /// pinned between it and the edge of the ground: the least important body is not walked around at all — the others
        /// pass as if the porch were empty. Out again, it gives way as before.
        /// </summary>
        public override void SetInside(bool inside)
        {
            if (_agent == null)
                return;

            if (inside)
            {
                if (_outsidePriority < 0)
                    _outsidePriority = _agent.avoidancePriority;
                _agent.avoidancePriority = 99;
            }
            else if (_outsidePriority >= 0)
            {
                _agent.avoidancePriority = _outsidePriority;
                _outsidePriority = -1;
            }
        }

        public override void OnDisable()
        {
            Jumper?.Abort();
            Climber?.Abort();
            Mover?.Halt();                              // switched on again later, it starts from a standstill
            Mover?.Leave();
        }
    }
}
