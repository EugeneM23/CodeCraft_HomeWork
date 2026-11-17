using System.Runtime.CompilerServices;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// Where the nodes an upgrade step adds stand on the canvas (step 90): next to the node they belong to — a condition
    /// beside the decision it is wired into, a block beside the decision whose On Tick runs it — never at (0, 0). A step
    /// gives a new node its place by the node it belongs to (PlaceNear); the write that saves the upgraded graph puts the
    /// node's record there (GraphSerializer.Write), and the canvas that opens it reads it the same way (GraphDocument).
    /// Only a look: nothing at runtime reads it, and a node the step did not add has no place here.
    /// </summary>
    public static class UpgradePlaces
    {
        private sealed class Place
        {
            public AgentNode Near;
            public Vector2 Offset;
        }

        private static readonly ConditionalWeakTable<AgentNode, Place> Places = new();

        /// <summary>The node an upgrade step added stands this far from the node it belongs to.</summary>
        public static void PlaceNear(AgentNode node, AgentNode near, Vector2 offset)
        {
            if (node == null || near == null)
                return;

            Places.Remove(node);
            Places.Add(node, new Place { Near = near, Offset = offset });
        }

        /// <summary>
        /// Where a node an upgrade step added stands: the place of the record of the node it belongs to (or of that one's
        /// own place, if it was added too), plus its offset. False — no step placed it, or nothing it belongs to has a
        /// place.
        /// </summary>
        public static bool TryPlaceOf(BehaviorGraphAsset asset, AgentNode node, out Vector2 position)
        {
            position = Vector2.zero;
            Vector2 offset = Vector2.zero;
            for (int depth = 0; asset != null && node != null && depth < 8; depth++)
            {
                if (!Places.TryGetValue(node, out Place place))
                    return false;

                offset += place.Offset;
                GraphRecord near = place.Near.Id != 0 ? asset.Find(place.Near.Id) : null;
                if (near != null && !Places.TryGetValue(place.Near, out _))
                {
                    position = near.Position + offset;
                    return true;
                }

                node = place.Near;                      // it belongs to a node the step added as well
            }

            return false;
        }
    }
}
