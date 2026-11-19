using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public class ArmourMathTests
    {
        private static readonly ArmourRules Rules = new() { ricochetAngle = 70f, overmatch = 3f, normalisation = 5f, ricochetKeep = 0.6f };

        [Test]
        public void ImpactAngle_IsZeroHeadOn_AndGrowsTowardTheGraze()
        {
            Assert.That(ArmourMath.ImpactAngle(Vector3.forward, Vector3.back), Is.EqualTo(0f).Within(0.01f));
            Vector3 at60 = Quaternion.Euler(0f, 60f, 0f) * Vector3.forward;
            Assert.That(ArmourMath.ImpactAngle(at60, Vector3.back), Is.EqualTo(60f).Within(0.01f));
        }

        [TestCase(100f, 0f, 100f)]
        [TestCase(100f, 60f, 200f)]
        [TestCase(80f, 45f, 113.137f)]
        public void EffectiveThickness_IsNominalOverCosine(float nominal, float angle, float expected) =>
            Assert.That(ArmourMath.EffectiveThickness(nominal, angle), Is.EqualTo(expected).Within(0.01f));

        [Test]
        public void EffectiveThickness_StaysFiniteAlongThePlate() =>
            Assert.That(float.IsFinite(ArmourMath.EffectiveThickness(50f, 90f)), Is.True);

        [Test]
        public void Normalise_TurnsTowardTheNormal_NeverPastIt()
        {
            Assert.That(ArmourMath.Normalise(30f, 5f), Is.EqualTo(25f));
            Assert.That(ArmourMath.Normalise(3f, 5f), Is.EqualTo(0f));
        }

        [Test]
        public void SteepHit_OnAThickPlate_Ricochets()
        {
            ArmourResult r = ArmourMath.Resolve(150f, 88f, 80f, 75f, ArmourKind.Plate, Rules);
            Assert.That(r.Outcome, Is.EqualTo(ArmourOutcome.Ricochet));
            Assert.That(r.Remaining, Is.EqualTo(90f).Within(0.01f));
        }

        [Test]
        public void SteepHit_OnAnOvermatchedPlate_DoesNotRicochet()
        {
            // 88 mm over 25 mm: three times over, the shell bites in whatever the angle.
            ArmourResult r = ArmourMath.Resolve(150f, 88f, 25f, 75f, ArmourKind.Plate, Rules);
            Assert.That(r.Overmatched, Is.True);
            Assert.That(r.Outcome, Is.EqualTo(ArmourOutcome.Penetration));
        }

        [Test]
        public void ThickerThanPenetration_AlongThePath_DoesNotPenetrate()
        {
            // 100 mm at 50° (45° after normalisation) is 141 mm: a 125 mm shell sticks.
            ArmourResult r = ArmourMath.Resolve(125f, 88f, 100f, 50f, ArmourKind.Plate, Rules);
            Assert.That(r.Outcome, Is.EqualTo(ArmourOutcome.NoPenetration));
            Assert.That(r.Effective, Is.EqualTo(141.42f).Within(0.05f));
            Assert.That(r.Remaining, Is.EqualTo(0f));
        }

        [Test]
        public void ThinnerThanPenetration_Penetrates_AndKeepsTheRest()
        {
            ArmourResult r = ArmourMath.Resolve(125f, 88f, 80f, 5f, ArmourKind.Plate, Rules);
            Assert.That(r.Outcome, Is.EqualTo(ArmourOutcome.Penetration));
            Assert.That(r.Effective, Is.EqualTo(80f).Within(0.01f));
            Assert.That(r.Remaining, Is.EqualTo(45f).Within(0.01f));
        }

        [Test]
        public void SpacedArmour_EatsPenetration_AndLetsTheShellOn()
        {
            ArmourResult r = ArmourMath.Resolve(125f, 88f, 20f, 0f, ArmourKind.Spaced, Rules);
            Assert.That(r.Outcome, Is.EqualTo(ArmourOutcome.SpacedPassed));
            Assert.That(r.Remaining, Is.EqualTo(105f).Within(0.01f));
        }

        [Test]
        public void Module_RegistersTheHit_WithoutArmour()
        {
            ArmourResult r = ArmourMath.Resolve(60f, 88f, 0f, 30f, ArmourKind.Module, Rules);
            Assert.That(r.Outcome, Is.EqualTo(ArmourOutcome.ModuleHit));
            Assert.That(r.Remaining, Is.EqualTo(60f));
        }

        [Test]
        public void RollPenetration_StraysWithinTheSpread()
        {
            Assert.That(ArmourMath.RollPenetration(100f, 0.08f, -1f), Is.EqualTo(92f).Within(0.001f));
            Assert.That(ArmourMath.RollPenetration(100f, 0.08f, 1f), Is.EqualTo(108f).Within(0.001f));
            Assert.That(ArmourMath.RollPenetration(100f, 0.08f, 5f), Is.EqualTo(108f).Within(0.001f));
        }

        [Test]
        public void NormalisedDirection_TurnsAFewDegreesTowardTheInwardNormal()
        {
            Vector3 dir = Quaternion.Euler(0f, 40f, 0f) * Vector3.forward;
            Vector3 turned = ArmourMath.NormalisedDirection(dir, Vector3.back, 5f);
            Assert.That(Vector3.Angle(turned, Vector3.forward), Is.EqualTo(35f).Within(0.01f));
            Vector3 nearly = Quaternion.Euler(0f, 2f, 0f) * Vector3.forward;
            Assert.That(Vector3.Angle(ArmourMath.NormalisedDirection(nearly, Vector3.back, 5f), Vector3.forward),
                Is.EqualTo(0f).Within(0.01f));
        }

        [TestCase(0f, 0.2f, 1f, ArmourFacing.Front)]
        [TestCase(0f, 0f, -1f, ArmourFacing.Rear)]
        [TestCase(0.9f, 0.1f, 0.3f, ArmourFacing.Right)]
        [TestCase(-0.9f, 0.1f, 0.3f, ArmourFacing.Left)]
        [TestCase(0.2f, 0.95f, 0.3f, ArmourFacing.Top)]
        [TestCase(0f, -1f, 0f, ArmourFacing.Bottom)]
        public void Facing_IsTheNormalsMainAxis(float x, float y, float z, ArmourFacing expected) =>
            Assert.That(ArmourMath.Facing(new Vector3(x, y, z)), Is.EqualTo(expected));
    }
}
