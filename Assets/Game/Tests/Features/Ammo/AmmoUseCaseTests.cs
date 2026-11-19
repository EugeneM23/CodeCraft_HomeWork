using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class AmmoUseCaseTests
    {
        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
                Object.DestroyImmediate(_go);
        }

        private static IEntity Cannon(int damage = 150, float radius = 5f)
        {
            IEntity weapon = new Entity();
            weapon.AddDamage(new ReactiveInt(damage));
            weapon.AddShellRadius(new Variable<float>(radius));
            return weapon;
        }

        private IEntity Shell(Vector3 direction)
        {
            _go = new GameObject("Shell");
            _go.AddComponent<Rigidbody>().useGravity = false;
            IEntity shell = new Entity();
            shell.AddGameObject(_go);
            shell.AddTransform(_go.transform);
            shell.AddMoveDirection(new ReactiveVector3(direction));
            shell.AddMoveSpeed(new ReactiveFloat(60f));
            shell.AddRiggedBody(_go.GetComponent<Rigidbody>());
            shell.AddRicochetMaxCount(2);
            shell.AddRicochetMaxAngle(25f);
            shell.AddRicochetCount(0);
            shell.AddExplosionRadius(5f);
            return shell;
        }

        /// <summary>Flies along +x, down at <paramref name="below"/> degrees to a floor.</summary>
        private static Vector3 Down(float below) => Quaternion.AngleAxis(-below, Vector3.forward) * Vector3.right;

        [Test]
        public void Switch_TogglesHighExplosiveAndArmourPiercing()
        {
            IEntity cannon = Cannon();
            Assert.That(AmmoUseCase.Loaded(cannon), Is.EqualTo(TankAmmoConfig.Load().Start));
            ShellKind first = AmmoUseCase.Switch(cannon);
            Assert.That(AmmoUseCase.Switch(cannon), Is.Not.EqualTo(first));
            Assert.That(AmmoUseCase.Loaded(cannon), Is.EqualTo(AmmoUseCase.Next(first)));
        }

        [Test]
        public void Prepare_GivesTheShellItsTypesNumbers_WithoutTouchingTheCannon()
        {
            IEntity cannon = Cannon(150, 4f);
            AmmoUseCase.GetOrAdd(cannon).Value = ShellKind.ArmourPiercing;
            IEntity shell = Shell(Vector3.forward);
            shell.AddDamage(cannon.GetDamage());
            AmmoUseCase.Prepare(shell, cannon);

            ShellAmmo ap = TankAmmoConfig.Load().Get(ShellKind.ArmourPiercing);
            Assert.That(AmmoUseCase.KindOf(shell), Is.EqualTo(ShellKind.ArmourPiercing));
            Assert.That(shell.GetMoveSpeed().Value, Is.EqualTo(ap.speed));
            Assert.That(shell.GetDamage().Value, Is.EqualTo(Mathf.RoundToInt(150 * ap.damageFactor)));
            Assert.That(cannon.GetDamage().Value, Is.EqualTo(150), "the cannon's own damage is left");
            Assert.That(shell.GetExplosionRadius(), Is.EqualTo(4f * ap.radiusFactor).Within(1e-4f));

            AmmoUseCase.GetOrAdd(cannon).Value = ShellKind.HighExplosive;
            AmmoUseCase.Prepare(shell, cannon);
            ShellAmmo he = TankAmmoConfig.Load().Get(ShellKind.HighExplosive);
            Assert.That(shell.GetMoveSpeed().Value, Is.EqualTo(he.speed));
            Assert.That(shell.GetExplosionRadius(), Is.EqualTo(4f * he.radiusFactor).Within(1e-4f));
        }

        [Test]
        public void HighExplosive_NeverRicochets()
        {
            IEntity cannon = Cannon();
            AmmoUseCase.GetOrAdd(cannon).Value = ShellKind.HighExplosive;
            IEntity shell = Shell(Down(5f));
            AmmoUseCase.Prepare(shell, cannon);
            Assert.That(AmmoUseCase.ShouldRicochet(shell, Down(5f), Vector3.up), Is.False);
        }

        [Test]
        public void ArmourPiercing_GlancesOffAGraze_NotASteepHit()
        {
            IEntity cannon = Cannon();
            AmmoUseCase.GetOrAdd(cannon).Value = ShellKind.ArmourPiercing;
            IEntity shell = Shell(Down(10f));
            AmmoUseCase.Prepare(shell, cannon);
            // 10° under the floor's plane is 80° from its normal; 40° under it, 50°.
            Assert.That(AmmoUseCase.ShouldRicochet(shell, Down(10f), Vector3.up), Is.True);
            Assert.That(AmmoUseCase.ShouldRicochet(shell, Down(40f), Vector3.up), Is.False);
        }

        [Test]
        public void ArmourPiercing_RunsOutOfRicochets()
        {
            IEntity cannon = Cannon();
            AmmoUseCase.GetOrAdd(cannon).Value = ShellKind.ArmourPiercing;
            IEntity shell = Shell(Down(10f));
            AmmoUseCase.Prepare(shell, cannon);
            shell.SetRicochetCount(TankAmmoConfig.Load().Get(ShellKind.ArmourPiercing).maxRicochets);
            Assert.That(AmmoUseCase.ShouldRicochet(shell, Down(10f), Vector3.up), Is.False);
        }

        [Test]
        public void Ricochet_ReflectsTheShell_AndSlowsIt()
        {
            IEntity cannon = Cannon();
            AmmoUseCase.GetOrAdd(cannon).Value = ShellKind.ArmourPiercing;
            IEntity shell = Shell(Down(10f));
            AmmoUseCase.Prepare(shell, cannon);
            float before = shell.GetMoveSpeed().Value;
            AmmoUseCase.Ricochet(shell, Vector3.zero, Vector3.up, null);

            Vector3 after = shell.GetMoveDirection().Value;
            Assert.That(after.y, Is.GreaterThan(0f), "it flies up off the floor");
            Assert.That(Vector3.Angle(after, Vector3.right), Is.EqualTo(10f).Within(0.01f));
            Assert.That(shell.GetRicochetCount(), Is.EqualTo(1));
            float keep = TankAmmoConfig.Load().Get(ShellKind.ArmourPiercing).ricochetSpeedKeep;
            Assert.That(shell.GetMoveSpeed().Value, Is.EqualTo(before * keep).Within(1e-3f));
        }

        [Test]
        public void MachineGunBullets_TakeNoType()
        {
            IEntity cannon = Cannon();
            IEntity bullet = new Entity();
            bullet.AddMoveSpeed(new ReactiveFloat(120f));
            AmmoUseCase.Prepare(bullet, cannon);
            Assert.That(AmmoUseCase.IsShell(bullet), Is.False);
            Assert.That(bullet.GetMoveSpeed().Value, Is.EqualTo(120f));
        }

        [Test]
        public void ArmourPiercing_GoesThroughHouseWalls_HighExplosiveDoesNot()
        {
            ShellAmmo ap = TankAmmoConfig.Load().Get(ShellKind.ArmourPiercing);
            ShellAmmo he = TankAmmoConfig.Load().Get(ShellKind.HighExplosive);
            Assert.That(ap.CanPassWalls, Is.True);
            Assert.That(he.CanPassWalls, Is.False);

            // A 20 cm outer wall: a fresh shell goes through both outer walls and a couple of inner ones.
            var outer = new ShellHouseUseCase.Wall { Thickness = 0.2f, Depth = 0.2f, Through = true, Solid = true };
            float wall = ShellHouseUseCase.Cost(outer, ap);
            Assert.That(ap.penetration / wall, Is.InRange(4f, 6f), "walls a fresh shell goes through");
            // A 20 cm pier stops it.
            var pier = new ShellHouseUseCase.Wall { Thickness = 0.2f, Hard = 0.2f, Depth = 0.2f, Through = true, Solid = true };
            Assert.That(ShellHouseUseCase.Cost(pier, ap), Is.GreaterThan(ap.penetration * 1.08f));
            // A little wider hole than the 0.4 m the small burst dug in a house (5 m × 0.25 × 0.32), but not a crater.
            Assert.That(ap.houseHole, Is.InRange(0.5f, 0.6f));
        }

        [Test]
        public void SurfaceNormal_OfATiltedSlab_IsItsSlope()
        {
            _go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _go.transform.position = new Vector3(-500f, 500f, 500f);
            _go.transform.rotation = Quaternion.Euler(0f, 0f, 20f);
            _go.transform.localScale = new Vector3(4f, 0.2f, 4f);
            Physics.SyncTransforms();
            Collider collider = _go.GetComponent<Collider>();
            Vector3 dir = Down(15f);
            Assert.That(
                Physics.Raycast(_go.transform.position - dir * 6f, dir, out RaycastHit hit, 20f), Is.True);
            Vector3 normal = AmmoUseCase.SurfaceNormal(collider, hit.point, dir, Vector3.up);
            Assert.That(Vector3.Angle(normal, _go.transform.up), Is.LessThan(1f));
        }
    }
}
