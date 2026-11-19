using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// Voxel fire at a gun's muzzle: the flame cools white-yellow, orange, red, dark; the cannon's burst is far bigger
    /// than a machine-gun round's; constant fire stays in a fixed pool, shares the hot points' light ring and
    /// allocates nothing per shot.
    /// </summary>
    public sealed class VoxelMuzzleFireTests
    {
        private GameObject _muzzle;

        [SetUp]
        public void SetUp()
        {
            _muzzle = new GameObject("TestMuzzle");
            _muzzle.transform.SetPositionAndRotation(new Vector3(0f, 2f, 0f), Quaternion.LookRotation(Vector3.forward));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_muzzle);
            foreach (string name in new[] { "<VoxelMuzzleFire>", "<BulletHotSpots>" })
            {
                GameObject go = GameObject.Find(name);
                if (go != null)
                    Object.DestroyImmediate(go);
            }
            VoxelMuzzleFire.Enabled = true;
        }

        [Test]
        public void FlameColor_CoolsFromWhiteYellowToDark()
        {
            Color hot = VoxelMuzzleFire.FlameColor(0f);
            Color orange = VoxelMuzzleFire.FlameColor(0.3f);
            Color cold = VoxelMuzzleFire.FlameColor(1f);

            Assert.That(hot.a, Is.EqualTo(1f));
            Assert.That(cold.a, Is.EqualTo(0f));
            Assert.That(hot.g, Is.GreaterThan(0.8f), "white-yellow at the muzzle");
            Assert.That(orange.g, Is.LessThan(hot.g).And.GreaterThan(cold.g));
            Assert.That(cold.r + cold.g + cold.b, Is.LessThan(0.4f), "dark at the end");
        }

        [Test]
        public void Cannon_ThrowsAFarBiggerBurstThanAMachineGunRound()
        {
            VoxelMuzzleFire.MachineGun(_muzzle.transform, Vector3.zero);
            int round = VoxelMuzzleFire.Alive;
            VoxelMuzzleFire.Cannon(_muzzle.transform, Vector3.zero);
            int shot = VoxelMuzzleFire.Alive - round;

            Assert.That(round, Is.InRange(7, 12), "a few flame cubes and 2-4 sparks a round");
            Assert.That(shot, Is.GreaterThan(round * 6));
            Assert.That(VoxelMuzzleFire.ActiveLights, Is.EqualTo(1));
            // The round's flicker comes from the hot points' light ring.
            Assert.That(BulletHotSpots.ActiveLights, Is.EqualTo(1));
        }

        [Test]
        public void ConstantFire_StaysInThePoolAndUnderTheLightCaps()
        {
            for (int i = 0; i < 40; i++)
                VoxelMuzzleFire.Cannon(_muzzle.transform, Vector3.zero);
            for (int i = 0; i < 400; i++)
                VoxelMuzzleFire.MachineGun(_muzzle.transform, Vector3.zero);

            Assert.That(VoxelMuzzleFire.Alive, Is.EqualTo(VoxelMuzzleFire.MAX));
            Assert.That(VoxelMuzzleFire.ActiveLights, Is.EqualTo(VoxelMuzzleFire.LIGHTS));
            Assert.That(BulletHotSpots.ActiveLights, Is.EqualTo(BulletHotSpots.LIGHTS));
        }

        [Test]
        public void Shots_AfterTheFirst_DoNotAllocate()
        {
            VoxelMuzzleFire.Cannon(_muzzle.transform, Vector3.zero);
            VoxelMuzzleFire.MachineGun(_muzzle.transform, Vector3.zero);

            Assert.That(() =>
            {
                for (int i = 0; i < 8; i++)
                    VoxelMuzzleFire.Cannon(_muzzle.transform, Vector3.one);
                for (int i = 0; i < 64; i++)
                    VoxelMuzzleFire.MachineGun(_muzzle.transform, Vector3.one);
            }, UnityEngine.TestTools.Constraints.ConstraintExtensions.AllocatingGCMemory(
                UnityEngine.TestTools.Constraints.Is.Not));
        }

        [Test]
        public void Disabled_ShotsLeaveNoFire()
        {
            VoxelMuzzleFire.Enabled = false;
            VoxelMuzzleFire.Cannon(_muzzle.transform, Vector3.zero);
            VoxelMuzzleFire.MachineGun(_muzzle.transform, Vector3.zero);

            Assert.That(VoxelMuzzleFire.Alive, Is.EqualTo(0));
            Assert.That(BulletHotSpots.ActiveLights, Is.EqualTo(0));
        }
    }
}
