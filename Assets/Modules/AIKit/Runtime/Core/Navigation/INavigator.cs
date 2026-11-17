using UnityEngine;

namespace AIKit
{
    /// <summary>How a piece of the path is passed — the navigator turns the edge into presses on the actuator.</summary>
    public enum NavEdgeKind
    {
        Walk,
        Jump,
        Fall,

        /// <summary>Down through a one-way platform.</summary>
        Drop,

        Ladder,
        Dash,
        Fly
    }

    /// <summary>
    /// The answer to "take me there" — not a bool: "no way" and "I am off the map" are different reasons that call
    /// for different reactions (handbook §11.1).
    /// </summary>
    public enum NavVerdict
    {
        /// <summary>There is a way (or the navigator is trying: a path is being planned).</summary>
        Reachable,

        /// <summary>There is a way, but it is a long detour.</summary>
        Detour,

        /// <summary>No way to the destination in this structure.</summary>
        Unreachable,

        /// <summary>The agent itself is outside the structure (off the mesh, off the graph).</summary>
        OffGraph,

        /// <summary>There is no structure (or no body to drive) at all.</summary>
        NoGraph
    }

    /// <summary>Where following the path stands right now.</summary>
    public enum NavStatus
    {
        /// <summary>No destination.</summary>
        Idle,

        /// <summary>The path is being calculated.</summary>
        Planning,

        /// <summary>On the way.</summary>
        Following,

        /// <summary>On the way, but holding: an ally in front (the agent waits behind it).</summary>
        Waiting,

        /// <summary>
        /// On the way, but standing: nothing passable in front right now (a wall taller than the jump, a gap wider than
        /// it). Not given up yet — that is Unreachable, after the navigator's give-up time.
        /// </summary>
        Blocked,

        /// <summary>The navigator gave up: the destination turned out unreachable, or the agent is stuck for good.</summary>
        Unreachable
    }

    public static class NavVerdictExtensions
    {
        /// <summary>The agent can go: Reachable or Detour.</summary>
        public static bool CanGo(this NavVerdict verdict) =>
            verdict == NavVerdict.Reachable || verdict == NavVerdict.Detour;

        /// <summary>Still on it: Planning, Following, Waiting or Blocked (not Idle, not given up).</summary>
        public static bool IsMoving(this NavStatus status) =>
            status == NavStatus.Planning || status == NavStatus.Following || status == NavStatus.Waiting ||
            status == NavStatus.Blocked;
    }

    /// <summary>
    /// A part that reads the agent's context (a navigator asking the roster for allies): the agent binds it at
    /// initialization, after the context is made.
    /// </summary>
    public interface IAgentBound
    {
        void Bind(AgentContext context);
    }

    /// <summary>
    /// Takes the agent to a point. Implementations differ in how they find the way — straight at it, reactively with
    /// jumps, over a platformer graph, over a grid or over a NavMesh — and the behavior above does not change.
    /// Step(dt) is called by the task that wants the movement, so a graph without a MoveTo task never pays for it.
    /// A navigator reports failure itself (Status turns Unreachable): a task does not need a time-out to give up.
    /// </summary>
    public interface INavigator
    {
        /// <summary>The verdict at the moment of asking; a planned path may still fail later — watch Status.</summary>
        NavVerdict SetDestination(Vector3 point);

        void Clear();

        NavStatus Status { get; }

        Vector3 Destination { get; }

        /// <summary>The next point to steer at (the end of the current edge).</summary>
        Vector3 SteeringTarget { get; }

        /// <summary>Along the path, not in a straight line; infinity when there is no path.</summary>
        float RemainingDistance { get; }

        NavEdgeKind CurrentEdge { get; }

        /// <summary>One step of following the path: writes presses into the actuator.</summary>
        void Step(float dt);
    }
}
