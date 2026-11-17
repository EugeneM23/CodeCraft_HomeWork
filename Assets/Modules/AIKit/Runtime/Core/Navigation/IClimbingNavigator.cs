using UnityEngine;

namespace AIKit
{
    /// <summary>How a point can be reached by a body that climbs.</summary>
    public enum ClimbWay
    {
        /// <summary>There is a way on foot: no climbing is needed.</summary>
        OnFoot,

        /// <summary>No way on foot, a way over the walls.</summary>
        OnlyByClimbing,

        /// <summary>No way at all, climbing or not.</summary>
        NoWay
    }

    /// <summary>
    /// A navigator whose body can climb walls (handbook §13.2: what an edge asks of the body is the body's; §13.3: the
    /// edge is passed by executing it, not by sliding to a point). The walls are links of their own on the level
    /// (a NavMeshLink of the "Climb" area); a path runs over them only while ClimbAllowed is on — the graph decides that
    /// the character climbs (the Climb To node), the body decides how. Optional next to INavigator: a navigator
    /// without it never climbs.
    /// </summary>
    public interface IClimbingNavigator
    {
        /// <summary>The body can climb at all (its settings say so).</summary>
        bool CanClimb { get; }

        /// <summary>On — the paths it plans may run over the climbing walls. A change drops the path walked now.</summary>
        bool ClimbAllowed { get; set; }

        /// <summary>The body is on a wall now, going up (1) or down (−1); 0 — it is not climbing.</summary>
        int ClimbDirection { get; }

        /// <summary>How many walls the body has climbed (up or down) since it was made.</summary>
        int Climbs { get; }

        /// <summary>How the point can be reached from where the body stands (asked anew no more often than the body allows).</summary>
        ClimbWay WayTo(Vector3 point);
    }
}
