using UnityEngine;

namespace AIKit.Tests
{
    // The rig and the objects of the world (step 90): a registry of the agent's own, a rack that takes weapons from the
    // world, where the agent wakes up
    internal sealed partial class GraphRig
    {
        /// <summary>The agent searches this registry (a fresh one when none is given) — no object of the scene is in it.</summary>
        public WorldObjects UseObjects(WorldObjects registry = null)
        {
            registry ??= new WorldObjects();
            Context.WorldObjects = registry;
            return registry;
        }

        /// <summary>The agent's rack takes weapons from the world (the stand-in for the moves lane's rack).</summary>
        public WorldRack UseWorldRack()
        {
            var rack = new WorldRack(Agent);
            Context.Rack = rack;
            return rack;
        }

        /// <summary>The agent's body stands here and this is its home (where it woke up).</summary>
        public void WakeUpAt(Vector3 at)
        {
            Actuator.Position = at;
            Agent.transform.position = at;
            Context.Home = at;
        }
    }
}
