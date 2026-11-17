using NUnit.Framework.Constraints;

namespace Game.Horde.Tests
{
    /// <summary>
    /// "Allocates nothing", counted by Unity's own constraint: in this editor
    /// System.GC.GetAllocatedBytesForCurrentThread reads 0 whatever is allocated (see AIKit.Tests.NoAllocation).
    /// Kept here so a test file does not import UnityEngine.TestTools.Constraints, whose Is clashes with NUnit's.
    /// </summary>
    internal static class NoAllocation
    {
        public static IResolveConstraint Constraint =>
            UnityEngine.TestTools.Constraints.ConstraintExtensions.AllocatingGCMemory(
                UnityEngine.TestTools.Constraints.Is.Not);
    }
}
