using NUnit.Framework;

namespace AIKit.Tests
{
    public class UtilityTests
    {
        [Test]
        public void Curve_Linear_MapsStraightThrough()
        {
            ResponseCurve curve = ResponseCurve.Linear();

            Assert.AreEqual(0f, curve.Evaluate(0f), 1e-4f);
            Assert.AreEqual(0.5f, curve.Evaluate(0.5f), 1e-4f);
            Assert.AreEqual(1f, curve.Evaluate(1f), 1e-4f);
        }

        [Test]
        public void Curve_Inverse_IsHighWhenTheInputIsLow()
        {
            ResponseCurve curve = ResponseCurve.Inverse();

            Assert.AreEqual(1f, curve.Evaluate(0f), 1e-4f);
            Assert.AreEqual(0f, curve.Evaluate(1f), 1e-4f);
        }

        [Test]
        public void Curve_Band_PeaksAtItsCentre()
        {
            ResponseCurve curve = ResponseCurve.Band(0.5f, 1f);

            float centre = curve.Evaluate(0.5f);

            Assert.Greater(centre, curve.Evaluate(0.1f));
            Assert.Greater(centre, curve.Evaluate(0.9f));
        }

        [Test]
        public void Curve_StaysInsideZeroToOne()
        {
            ResponseCurve curve = new() { Type = CurveType.Linear, M = 10f, K = 1f, B = 0f, C = 5f };

            Assert.AreEqual(1f, curve.Evaluate(0.5f), 1e-4f);
        }

        [Test]
        public void Compensation_PullsALongProductBackUp()
        {
            float raw = 0.9f * 0.9f * 0.9f * 0.9f;

            float compensated = UtilityMath.Compensate(raw, 4);

            Assert.Greater(compensated, raw);
            Assert.Less(compensated, 1f);
        }

        [Test]
        public void Compensation_KeepsZeroAsAVeto()
        {
            Assert.AreEqual(0f, UtilityMath.Compensate(0f, 5), 1e-6f);
        }

        [Test]
        public void Score_OneZeroAxis_VetoesTheOption()
        {
            var rig = new GraphRig();
            var considerations = new[]
            {
                new Consideration { Input = ConsiderationInput.Constant, Constant = 1f, InputMin = 0f, InputMax = 1f },
                new Consideration { Input = ConsiderationInput.Constant, Constant = 0f, InputMin = 0f, InputMax = 1f }
            };

            Assert.AreEqual(0f, UtilityMath.Score(considerations, rig.Context), 1e-6f);
        }

        [Test]
        public void Consideration_DistanceToTarget_IsHighWhenClose()
        {
            var rig = new GraphRig();
            rig.CreateTargetAt(new UnityEngine.Vector3(2f, 0f, 0f));
            var consideration = new Consideration
            {
                Input = ConsiderationInput.DistanceToTarget,
                InputMin = 0f,
                InputMax = 10f,
                Curve = ResponseCurve.Inverse()
            };

            float close = consideration.Evaluate(rig.Context);
            rig.MoveTargetTo(new UnityEngine.Vector3(9f, 0f, 0f));
            float far = consideration.Evaluate(rig.Context);

            Assert.Greater(close, far);
            rig.DestroyTarget();
        }

        [Test]
        public void Consideration_Variable_ReadsTheBlackboard()
        {
            var rig = new GraphRig();
            rig.Context.Blackboard.Set("HealthFraction", 0.25f);
            var consideration = new Consideration
            {
                Input = ConsiderationInput.Variable,
                Variable = "HealthFraction",
                InputMin = 0f,
                InputMax = 1f,
                Curve = ResponseCurve.Linear()
            };

            Assert.AreEqual(0.25f, consideration.Evaluate(rig.Context), 1e-4f);
        }

        [Test]
        public void Normalize_InvertedRange_Clamps()
        {
            Assert.AreEqual(0f, UtilityMath.Normalize(-5f, 0f, 10f), 1e-6f);
            Assert.AreEqual(1f, UtilityMath.Normalize(50f, 0f, 10f), 1e-6f);
        }
    }
}
