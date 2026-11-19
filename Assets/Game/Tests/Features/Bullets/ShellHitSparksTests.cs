using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// An HE shell's hit: several times a bullet's sparks and a hot point whose light is brighter and reaches further,
    /// from the bullets' own pools and under their light cap.
    /// </summary>
    public sealed class ShellHitSparksTests
    {
        [TearDown]
        public void TearDown()
        {
            foreach (string name in new[] { "<VoxelSparks>", "<BulletHotSpots>" })
            {
                GameObject go = GameObject.Find(name);
                if (go != null)
                    Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void HeHit_ThrowsSeveralTimesABulletsSparks_AndABrighterWiderLight()
        {
            VoxelSparks.Emit(Vector3.zero, Vector3.up, Vector3.down);
            int bullet = VoxelSparks.Alive;
            BulletHotSpots.Emit(Vector3.zero, Vector3.up);
            Light bulletLight = Lit();

            ShellHitSparksUseCase.Emit(new Vector3(5f, 0f, 0f), Vector3.up);
            int shell = VoxelSparks.Alive - bullet;
            Light shellLight = Lit(bulletLight);

            Assert.That(shell, Is.GreaterThanOrEqualTo(bullet * 4));
            Assert.That(shellLight, Is.Not.Null);
            Assert.That(shellLight.intensity, Is.GreaterThan(bulletLight.intensity));
            Assert.That(shellLight.range, Is.GreaterThan(bulletLight.range));
            Assert.That(BulletHotSpots.ActiveLights, Is.EqualTo(2));
        }

        [Test]
        public void ManyHeHits_StayUnderTheHotPointLightCap()
        {
            for (int i = 0; i < 40; i++)
                ShellHitSparksUseCase.Emit(new Vector3(i, 0f, 0f), Vector3.zero);

            Assert.That(BulletHotSpots.ActiveLights, Is.EqualTo(BulletHotSpots.LIGHTS));
        }

        /// <summary>A burning hot-point light other than <paramref name="except"/>.</summary>
        private static Light Lit(Light except = null)
        {
            GameObject root = GameObject.Find("<BulletHotSpots>");
            foreach (Light l in root.GetComponentsInChildren<Light>(true))
                if (l.enabled && l != except)
                    return l;
            return null;
        }
    }
}
