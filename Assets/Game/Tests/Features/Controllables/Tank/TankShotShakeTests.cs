using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>Three cannon shots in a row shake the tank clean: a third, half the rest, all of it.</summary>
    public sealed class TankShotShakeTests
    {
        [Test]
        public void ThreeShotsInARow_TakeAThirdHalfAndTheRest()
        {
            Assert.AreEqual(1f / 3f, VoxelSandboxStart.ShotShare(1, 3), 1e-6f);
            Assert.AreEqual(0.5f, VoxelSandboxStart.ShotShare(2, 3), 1e-6f);
            Assert.AreEqual(1f, VoxelSandboxStart.ShotShare(3, 3), 1e-6f);
        }

        [Test]
        public void EachShotOfARow_TakesAnEqualPartOfWhatWasOnTheTank()
        {
            float left = 1f;
            for (int shot = 1; shot <= 3; shot++)
            {
                float off = left * VoxelSandboxStart.ShotShare(shot, 3);
                Assert.AreEqual(1f / 3f, off, 1e-6f, $"shot {shot}");
                left -= off;
            }

            Assert.AreEqual(0f, left, 1e-6f, "clean after the third");
        }

        [Test]
        public void PastTheRow_EveryShotCleansWhatIsLeft()
        {
            Assert.AreEqual(1f, VoxelSandboxStart.ShotShare(5, 3), 1e-6f);
            Assert.AreEqual(1f / 3f, VoxelSandboxStart.ShotShare(0, 3), 1e-6f);
        }
    }
}
