using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// Stage 3c, the utility core (handbook §4.2–4.6, table §36): every curve at three points, every knob doing
    /// something, no NaN anywhere, compensation by the geometric mean, a veto that the trace tells from a zero, and
    /// selection strategies owned by the set of decisions.
    /// </summary>
    public class UtilityCoreTests
    {
        private static ResponseCurve Curve(CurveType type, float m = 1f, float k = 1f, float b = 0f, float c = 0f) =>
            new() { Type = type, M = m, K = k, B = b, C = c };

        private static void AssertThreePoints(ResponseCurve curve, float at0, float atHalf, float at1)
        {
            Assert.AreEqual(at0, curve.Evaluate(0f), 1e-3f, $"{curve} at 0");
            Assert.AreEqual(atHalf, curve.Evaluate(0.5f), 1e-3f, $"{curve} at 0.5");
            Assert.AreEqual(at1, curve.Evaluate(1f), 1e-3f, $"{curve} at 1");
        }

        // --- every curve, three points ---

        [Test]
        public void Linear_ThreePoints() => AssertThreePoints(Curve(CurveType.Linear), 0f, 0.5f, 1f);

        [Test]
        public void Polynomial_ThreePoints() => AssertThreePoints(Curve(CurveType.Polynomial, k: 2f), 0f, 0.25f, 1f);

        [Test]
        public void Logistic_ThreePoints()
        {
            float low = 1f / (1f + Mathf.Exp(5f));
            AssertThreePoints(Curve(CurveType.Logistic), low, 0.5f, 1f - low);
        }

        [Test]
        public void Logit_ThreePoints()
        {
            // 0.5 + ln(p / (1 − p)) / 10 with p clamped to (1e-4, 1 − 1e-4) at the edges
            float edge = Mathf.Log(1e-4f / (1f - 1e-4f)) / 10f + 0.5f;
            AssertThreePoints(Curve(CurveType.Logit), Mathf.Clamp01(edge), 0.5f, Mathf.Clamp01(1f - edge));
        }

        [Test]
        public void Normal_ThreePoints()
        {
            float side = Mathf.Exp(-10f * 0.25f);
            AssertThreePoints(Curve(CurveType.Normal, b: 0.5f), side, 1f, side);
        }

        [Test]
        public void Sine_ThreePoints() => AssertThreePoints(Curve(CurveType.Sine), 0f, 1f, 0f);

        // --- every knob does something ---

        [Test]
        public void K_ChangesTheSteepnessOfLogit()
        {
            float soft = Curve(CurveType.Logit, k: 2f).Evaluate(0.8f);
            float steep = Curve(CurveType.Logit, k: 0.5f).Evaluate(0.8f);

            Assert.Greater(steep, soft, "A smaller K bends the logit harder away from the middle");
        }

        [Test]
        public void K_ChangesTheWidthOfNormal()
        {
            float narrow = Curve(CurveType.Normal, k: 4f, b: 0.5f).Evaluate(0.7f);
            float wide = Curve(CurveType.Normal, k: 0.5f, b: 0.5f).Evaluate(0.7f);

            Assert.Greater(wide, narrow);
        }

        [Test]
        public void K_ChangesThePeriodOfSine()
        {
            float one = Curve(CurveType.Sine, k: 1f).Evaluate(0.25f);
            float two = Curve(CurveType.Sine, k: 2f).Evaluate(0.25f);

            Assert.That(Mathf.Abs(one - two), Is.GreaterThan(1e-3f));
        }

        [Test]
        public void Polynomial_NeverNaN_OnAGridOfInputs()
        {
            float[] ks = { -2f, -0.5f, 0f, 0.3f, 0.5f, 1f, 2.5f, 3f };
            float[] bs = { -1f, 0f, 0.3f, 0.7f, 1.5f };
            foreach (float k in ks)
            foreach (float b in bs)
            {
                for (int i = 0; i <= 20; i++)
                {
                    float y = Curve(CurveType.Polynomial, k: k, b: b).Evaluate(i / 20f);
                    Assert.IsFalse(float.IsNaN(y), $"k {k}, b {b}, x {i / 20f}");
                    Assert.That(y, Is.InRange(0f, 1f));
                }
            }
        }

        // --- combining axes ---

        [Test]
        public void Compensate_TenAxesOfPointNine_GiveBackPointNine()
        {
            float raw = Mathf.Pow(0.9f, 10);

            Assert.AreEqual(0.349f, raw, 1e-3f);
            Assert.AreEqual(0.9f, UtilityMath.Compensate(raw, 10), 1e-3f);
        }

        [Test]
        public void Score_IsNeverNegativeOrNaN()
        {
            Assert.AreEqual(0f, (float)new UtilityScore(-3f));
            Assert.AreEqual(0f, (float)new UtilityScore(float.NaN));
        }

        private static Consideration VariableAxis(string variable, bool required = true) =>
            new()
            {
                Name = variable,
                Input = ConsiderationInput.Variable,
                Variable = variable,
                InputMin = 0f,
                InputMax = 1f,
                Required = required
            };

        [Test]
        public void RequiredAxisWithoutData_Vetoes_OptionalIsSkipped()
        {
            var rig = new GraphRig();
            rig.Context.Blackboard.Set("Health", 0.5f);

            Assert.IsFalse(UtilityMath.Evaluate(new[] { VariableAxis("Health"), VariableAxis("Missing") }, rig.Context,
                ScoreReduction.Product, out _, out Consideration veto));
            Assert.AreEqual("Missing", veto.Name);

            Assert.IsTrue(UtilityMath.Evaluate(new[] { VariableAxis("Health"), VariableAxis("Missing", false) },
                rig.Context, ScoreReduction.Product, out UtilityScore score, out _));
            Assert.AreEqual(0.5f, (float)score, 1e-4f, "The optional axis without data has no say");
        }

        [Test]
        public void DistanceAxis_WithoutATarget_IsNoData_NotFarAway()
        {
            var rig = new GraphRig();
            var axis = new Consideration { Input = ConsiderationInput.DistanceToTarget, InputMax = 10f };

            Assert.IsFalse(axis.Evaluate(rig.Context, out _));

            rig.CreateTargetAt(new Vector3(5f, 0f, 0f));

            Assert.IsTrue(axis.Evaluate(rig.Context, out UtilityScore score));
            Assert.AreEqual(0.5f, (float)score, 1e-4f);
        }

        [Test]
        public void Average_AddsArgumentsUp()
        {
            var rig = new GraphRig();
            rig.Context.Blackboard.Set("A", 0.2f);
            rig.Context.Blackboard.Set("B", 0.8f);

            UtilityMath.Evaluate(new[] { VariableAxis("A"), VariableAxis("B") }, rig.Context, ScoreReduction.Average,
                out UtilityScore score, out _);

            Assert.AreEqual(0.5f, (float)score, 1e-4f);
        }

        [Test]
        public void Trace_TellsAVetoFromAZero()
        {
            var rig = new GraphRig();
            rig.CreateTargetAt(new Vector3(1f, 0f, 0f));
            rig.Context.Blackboard.Set("Zero", 0f);
            rig.Abilities.Add(new AbilityDefinition
            {
                Id = "NoData", MaxRange = 5f, RequiresGround = false, Considerations = new[] { VariableAxis("Missing") }
            });
            rig.Abilities.Add(new AbilityDefinition
            {
                Id = "Zero", MaxRange = 5f, RequiresGround = false, Considerations = new[] { VariableAxis("Zero") }
            });

            Assert.IsNull(rig.Abilities.Select(rig.Context), "Neither can be chosen");

            var kinds = new Dictionary<string, DecisionRecord>();
            for (int i = 0; i < rig.Context.Trace.Count; i++)
                kinds[rig.Context.Trace[i].AbilityId] = rig.Context.Trace[i];

            Assert.AreEqual(DecisionKind.AbilityVetoed, kinds["NoData"].Kind);
            Assert.AreEqual("Missing", kinds["NoData"].Detail);
            Assert.AreEqual(DecisionKind.AbilityCandidate, kinds["Zero"].Kind);
            Assert.AreEqual(0f, kinds["Zero"].Score);
        }

        // --- selection strategies ---

        private static readonly float[] OneTwoThree = { 1f, 2f, 3f };

        [Test]
        public void TopRandom_OfTwo_NeverPicksTheWorstOfThree()
        {
            var rig = new GraphRig(seed: 17);
            var strategy = new TopRandomSelection(2);
            var picked = new HashSet<int>();

            for (int i = 0; i < 200; i++)
                picked.Add(strategy.Select(OneTwoThree, rig.Context));

            CollectionAssert.DoesNotContain(picked, 0, "Score 1 is outside the top two");
            CollectionAssert.IsSupersetOf(picked, new[] { 1, 2 });
        }

        [Test]
        public void Max_AlwaysTheBest_Threshold_KeepsLowScoresOut()
        {
            Assert.AreEqual(2, new MaxSelection().Select(OneTwoThree, null));
            Assert.AreEqual(-1, new MaxSelection(threshold: 5f).Select(OneTwoThree, null));
        }

        [Test]
        public void Random_OnlyAboveTheThreshold()
        {
            var rig = new GraphRig(seed: 3);
            var strategy = new RandomSelection(threshold: 1.5f);
            var picked = new HashSet<int>();
            for (int i = 0; i < 100; i++)
                picked.Add(strategy.Select(OneTwoThree, rig.Context));

            CollectionAssert.AreEquivalent(new[] { 1, 2 }, picked);
        }

        [Test]
        public void RandomWeighted_PrefersTheHigherScore()
        {
            var rig = new GraphRig(seed: 5);
            var strategy = new RandomWeightedSelection();
            int best = 0;
            float[] scores = { 1f, 9f };
            for (int i = 0; i < 200; i++)
            {
                if (strategy.Select(scores, rig.Context) == 1)
                    best++;
            }

            Assert.Greater(best, 150);
        }

        [Test]
        public void Abilities_OwnTheStrategy()
        {
            var rig = new GraphRig(seed: 9);
            rig.CreateTargetAt(new Vector3(1f, 0f, 0f));
            rig.AddAbility("Light", cooldown: 0f, maxRange: 5f, weight: 1f);
            rig.AddAbility("Heavy", cooldown: 0f, maxRange: 5f, weight: 1.1f);
            rig.Abilities.Strategy = new MaxSelection();

            for (int i = 0; i < 20; i++)
                Assert.AreEqual("Heavy", rig.Abilities.Select(rig.Context));
        }

        // --- the final shape of an ability ---

        private sealed class BlockedSight : ISightProbe
        {
            public bool HasLineOfSight(Vector3 from, Vector3 to) => false;
        }

        [Test]
        public void Ability_RequiresLineOfSight_AndAwareness()
        {
            var rig = new GraphRig();
            rig.CreateTargetAt(new Vector3(1f, 0f, 0f));
            rig.Abilities.Add(new AbilityDefinition
            {
                Id = "Shot", Cooldown = 0f, RequiresGround = false, Range = new RangeBand(0f, 10f),
                RequiresLineOfSight = true, MinAwareness = 0.5f
            });

            Assert.IsTrue(rig.Abilities.IsReady("Shot", rig.Context));

            rig.Context.Track.Awareness = 0.2f;
            Assert.IsFalse(rig.Abilities.IsReady("Shot", rig.Context), "Not aware enough");

            rig.Context.Track.Awareness = 1f;
            rig.Context.Sight = new BlockedSight();
            Assert.IsFalse(rig.Abilities.IsReady("Shot", rig.Context), "No clear line");
        }
    }
}
