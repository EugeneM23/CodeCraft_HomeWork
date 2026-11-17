using NUnit.Framework.Constraints;

namespace AIKit.Tests
{
    /// <summary>
    /// "Allocates nothing", counted by Unity's own constraint (step 90 recheck: System.GC.GetAllocatedBytesForCurrentThread
    /// reads 0 in this editor whatever is allocated, so the module's allocation tests checked nothing). Kept here so a test
    /// file does not import UnityEngine.TestTools.Constraints, whose Is clashes with NUnit's. Measure the think only: in
    /// this editor the constraint counts even a string hash (a blackboard write by name, any Dictionary lookup by a string)
    /// — give a test's stimulus outside the measured delegate.
    /// </summary>
    internal static class NoAllocation
    {
        /// <summary>The delegate given to Assert.That allocates no managed memory.</summary>
        public static IResolveConstraint Constraint =>
            UnityEngine.TestTools.Constraints.ConstraintExtensions.AllocatingGCMemory(
                UnityEngine.TestTools.Constraints.Is.Not);
    }
}
