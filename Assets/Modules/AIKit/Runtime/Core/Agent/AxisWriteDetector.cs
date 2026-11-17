using System;
using System.Collections.Generic;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace AIKit
{
    /// <summary>
    /// Editor-only alarm for invariant 4 ("one body axis — one owner"): when two different tasks write the same axis of
    /// the body within one step, the last one silently wins. Until the MovementArbiter of stage 8 exists, the actuator
    /// reports each such pair once, naming both tasks — this is how we learn which graphs need the arbiter.
    /// Actuators call Write in their axis methods and EndStep in Apply; in a build every call compiles away.
    /// </summary>
    public sealed class AxisWriteDetector
    {
        public enum Axis
        {
            Move,
            Face
        }

#if UNITY_EDITOR
        /// <summary>The task whose tick is running now (AgentTask.Tick sets it); null — outside the graph.</summary>
        internal static AgentNode CurrentWriter;

        private static readonly HashSet<(Type, Type, Axis)> Reported = new();

        private readonly AgentNode[] _writers = new AgentNode[2];
#endif

        /// <summary>The actuator received a press on the axis.</summary>
        [Conditional("UNITY_EDITOR")]
        public void Write(Axis axis, object owner)
        {
#if UNITY_EDITOR
            AgentNode writer = CurrentWriter;
            if (writer == null)
                return;

            AgentNode previous = _writers[(int)axis];
            _writers[(int)axis] = writer;
            if (previous == null || previous == writer)
                return;

            if (Reported.Add((previous.GetType(), writer.GetType(), axis)))
            {
                Debug.LogWarning($"AI Kit: {previous.DisplayName} and {writer.DisplayName} both wrote the {axis} axis " +
                                 "in one step; the last one wins. Two writers of one axis need the MovementArbiter " +
                                 "(stage 8) or one of the tasks moved out of the state.", owner as UnityEngine.Object);
            }
#endif
        }

        /// <summary>Forgets which pairs were reported (tests).</summary>
        internal static void ForgetReports()
        {
#if UNITY_EDITOR
            Reported.Clear();
#endif
        }

        /// <summary>The step's presses went to the body: the next step starts with no writers.</summary>
        [Conditional("UNITY_EDITOR")]
        public void EndStep()
        {
#if UNITY_EDITOR
            _writers[0] = null;
            _writers[1] = null;
#endif
        }
    }
}
