using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public class TankArmourConfigTests
    {
        private TankArmourConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<TankArmourConfig>();
            TankArmourConfig.Zone Z(string name, string part, ArmourFacing facing, float mm, float yMax = 1f) => new()
            {
                name = name, part = part, facing = facing, thickness = mm, boxMin = Vector3.zero,
                boxMax = new Vector3(1f, yMax, 1f)
            };
            _config.Set(ArmourRules.Default, new[]
            {
                Z("Маска", "Gun", ArmourFacing.Any, 150f),
                Z("Гусеница", "Track?_*", ArmourFacing.Any, 20f),
                Z("НЛД", "HullVisual", ArmourFacing.Front, 70f, 0.45f),
                Z("ВЛД", "HullVisual", ArmourFacing.Front, 110f),
                Z("Борт", "HullVisual", ArmourFacing.Left | ArmourFacing.Right, 80f),
                Z("Экипаж", "Crewman", ArmourFacing.Any, 0f)
            }, 40f);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_config);

        [Test]
        public void TheFrontPlate_SplitsByHeight()
        {
            Assert.That(_config.Find("Voxels", "HullVisual", ArmourFacing.Front, new Vector3(0.5f, 0.3f, 0.95f)).name, Is.EqualTo("НЛД"));
            Assert.That(_config.Find("Voxels", "HullVisual", ArmourFacing.Front, new Vector3(0.5f, 0.7f, 0.95f)).name, Is.EqualTo("ВЛД"));
        }

        [Test]
        public void TheFacing_PicksTheSide()
        {
            Assert.That(_config.Find("Voxels", "HullVisual", ArmourFacing.Left, new Vector3(0f, 0.5f, 0.5f)).name, Is.EqualTo("Борт"));
            Assert.That(_config.Find("Voxels", "HullVisual", ArmourFacing.Rear, new Vector3(0.5f, 0.5f, 0f)), Is.Null);
        }

        [Test]
        public void PartsMatchByWildcard_AndTheBodysOwnName()
        {
            Assert.That(_config.Find("Voxels", "TrackL_Front", ArmourFacing.Top, Vector3.one * 0.5f).name, Is.EqualTo("Гусеница"));
            Assert.That(_config.Find("Voxels", "Gun", ArmourFacing.Front, Vector3.one * 0.5f).name, Is.EqualTo("Маска"));
            Assert.That(_config.Find("Crewman", "HullVisual", ArmourFacing.Rear, Vector3.one * 0.5f).name, Is.EqualTo("Экипаж"));
        }

        [TestCase("Track?_*", "TrackR_Rear", true)]
        [TestCase("Track?_*", "TrackLR_Rear", false)]
        [TestCase("Wheel_*", "Wheel_", true)]
        [TestCase("HullVisual", "HullVisual2", false)]
        public void Wildcard_MatchesTheWholeName(string pattern, string text, bool expected) =>
            Assert.That(TankArmourConfig.Wildcard.Match(pattern, text), Is.EqualTo(expected));
    }
}
