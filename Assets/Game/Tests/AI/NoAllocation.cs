using NUnit.Framework.Constraints;

namespace Game.AIKit.Adapters.Tests
{
    /// <summary>
    /// "Allocates nothing", counted by Unity's own constraint (see Game.Horde.Tests.NoAllocation): kept here so a test
    /// file does not import UnityEngine.TestTools.Constraints, whose Is clashes with NUnit's.
    /// </summary>
    internal static class NoAllocation
    {
        public static IResolveConstraint Constraint =>
            UnityEngine.TestTools.Constraints.ConstraintExtensions.AllocatingGCMemory(
                UnityEngine.TestTools.Constraints.Is.Not);
    }
}
