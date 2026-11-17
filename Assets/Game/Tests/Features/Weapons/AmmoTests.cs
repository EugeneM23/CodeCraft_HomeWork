using NUnit.Framework;

namespace Game.Tests
{
    public sealed class AmmoTests
    {
        [Test]
        public void Spend_TakesOneRound_AndReportsIt()
        {
            var ammo = new Ammo(3);
            int changes = 0;
            ammo.OnStateChanged += () => changes++;

            ammo.Spend();

            Assert.That(ammo.GetCount(), Is.EqualTo(2));
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void Spend_WhenEmpty_StaysAtZero_AndIsSilent()
        {
            var ammo = new Ammo(0);
            int changes = 0;
            ammo.OnStateChanged += () => changes++;

            ammo.Spend();

            Assert.That(ammo.GetCount(), Is.Zero);
            Assert.That(ammo.Exists(), Is.False);
            Assert.That(changes, Is.Zero);
        }

        [Test]
        public void SpendAll_EmptiesTheMagazine_AndReportsIt()
        {
            var ammo = new Ammo(30);
            int changes = 0;
            ammo.OnStateChanged += () => changes++;

            ammo.SpendAll();

            Assert.That(ammo.GetCount(), Is.Zero);
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void SpendAll_WhenAlreadyEmpty_IsSilent()
        {
            var ammo = new Ammo(0);
            int changes = 0;
            ammo.OnStateChanged += () => changes++;

            ammo.SpendAll();

            Assert.That(changes, Is.Zero);
        }

        [Test]
        public void SpendAllThenAdd_ReplacesTheCount_TheWayAPickUpLoadsAWeapon()
        {
            var ammo = new Ammo(7);

            ammo.SpendAll();
            ammo.Add(12);

            Assert.That(ammo.GetCount(), Is.EqualTo(12));
            Assert.That(ammo.Exists(), Is.True);
        }
    }
}
