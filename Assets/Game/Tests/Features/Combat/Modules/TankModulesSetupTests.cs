using Atomic.Entities;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class TankModulesSetupTests
    {
        [TearDown]
        public void TearDown() => TankModulesSetup.UninstallContext(null);

        [Test]
        public void Launcher_KeptForTheTank_AcrossLeavingAndReEntering()
        {
            IEntity tank = new Entity();
            var screen = new SmokeScreen();

            SmokeLauncherModule first = TankModulesSetup.GetOrCreateLauncher(tank, null, screen, 12f, 30f);
            SmokeLauncherModule again = TankModulesSetup.GetOrCreateLauncher(tank, null, screen, 10f, 25f);

            Assert.AreSame(first, again, "the same launcher, its reload going on");
            Assert.AreEqual(10f, again.Duration, 1e-4f);
            Assert.AreEqual(25f, again.Cooldown, 1e-4f);
        }

        [Test]
        public void Launcher_OnePerTank()
        {
            var screen = new SmokeScreen();

            SmokeLauncherModule a = TankModulesSetup.GetOrCreateLauncher(new Entity(), null, screen, 12f, 30f);
            SmokeLauncherModule b = TankModulesSetup.GetOrCreateLauncher(new Entity(), null, screen, 12f, 30f);

            Assert.AreNotSame(a, b);
        }

        [Test]
        public void Launcher_NewForAnotherSmokeScreen_OrAfterUninstall()
        {
            IEntity tank = new Entity();
            var screen = new SmokeScreen();
            SmokeLauncherModule first = TankModulesSetup.GetOrCreateLauncher(tank, null, screen, 12f, 30f);

            var other = new SmokeScreen();
            SmokeLauncherModule second = TankModulesSetup.GetOrCreateLauncher(tank, null, other, 12f, 30f);
            Assert.AreNotSame(first, second);
            Assert.AreSame(other, second.Screen);

            TankModulesSetup.UninstallContext(null);
            Assert.AreNotSame(second, TankModulesSetup.GetOrCreateLauncher(tank, null, other, 12f, 30f));
        }
    }
}
