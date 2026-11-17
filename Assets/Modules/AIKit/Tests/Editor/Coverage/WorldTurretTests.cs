using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// A turret's rounds (step 90, spec 10.6 — the turret is a weapon on a mount, its rounds the gun's uses; handbook §24.1
    /// — the object keeps its own state): while it is manned its rounds are in the gun, and the turret counts them with
    /// its own — loaded while manned, it is full counting the rounds in the gun, and those come back when the gunner
    /// leaves, never counted as fired; set while manned, the rounds in the gun stay; a real character's Attack fires the
    /// gun from its mount, every round counted as fired, and with the last one the character's own weapon is back.
    /// </summary>
    public sealed class WorldTurretTests
    {
        private const string OBJECT = "Object";
        private const string VICTIM = "Victim";
        private const float DT = 0.02f;

        private readonly List<Object> _created = new();
        private WorldObjects _registry;

        [SetUp]
        public void SetUp() => _registry = new WorldObjects();

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created)
            {
                if (created != null)
                    Object.DestroyImmediate(created);
            }

            _created.Clear();
            GraphRig.DestroyAll();
        }

        [Test]
        public void Turret_LoadedWhileManned_FullCountingTheRoundsInTheGun_TheyComeBackWhenTheGunnerLeaves()
        {
            Turret turret = NewTurret(rounds: 6, capacity: 10);
            turret.LoadTime = 0.3f;
            int fired = 0;
            turret.Fired += (_, _, _) => fired++;
            GraphRig gunner = Rig(turret.SeatPosition, out WorldRack rack);
            Assert.AreEqual(TaskStatus.Success, Interact(gunner, turret, Offer.Use), "Set-up: manned");
            rack.SpendUse();
            rack.SpendUse();
            Assert.AreEqual(4, turret.Ammo, "Two shots: four left, in the gun");
            Assert.AreEqual(2, fired);

            GraphRig loader = Rig(turret.SeatPosition, out _);
            Transform box = Make<Transform>("Box", Vector3.zero);
            loader.Agent.Hold(box);
            Assert.AreEqual(TaskStatus.Success, Interact(loader, turret, Offer.Load), "Loaded");
            Assert.AreEqual(turret.Capacity, turret.Ammo, "Full: four in the gun and six in the turret");
            Assert.IsTrue(box == null, "The box is used up");
            Assert.IsNull(loader.Agent.Item);

            gunner.Actuator.Position = turret.SeatPosition + new Vector3(0f, 0f, 4f);     // off to something else

            Assert.IsNull(turret.Gunner, "Set-up: it left the turret");
            Assert.AreEqual(turret.Capacity, turret.Ammo, "The four in the gun are the turret's again: still full");
            Assert.AreEqual(2, fired, "…none of them counted as fired");
        }

        [Test]
        public void Turret_AmmoSetWhileManned_TheRoundsInTheGunStay()
        {
            Turret turret = NewTurret(rounds: 4, capacity: 10);
            GraphRig gunner = Rig(turret.SeatPosition, out WorldRack rack);
            Assert.AreEqual(TaskStatus.Success, Interact(gunner, turret, Offer.Use), "Set-up: manned, four in the gun");

            turret.Ammo = 8;
            Assert.AreEqual(8, turret.Ammo, "Eight: four in the gun, four in the turret");
            Assert.AreEqual(4, rack.UsesLeft, "The gun's rounds are as they were");

            turret.Ammo = 2;
            Assert.AreEqual(4, turret.Ammo, "Fewer than in the gun: the turret's part is empty, the gun keeps its four");
            Assert.AreEqual(4, rack.UsesLeft);
        }

        [Test]
        public void Turret_MannedByARealCharacter_ItsAttackFiresTheGun_TheTurretCountsEveryRound()
        {
            const int ROUNDS = 3;
            Turret turret = NewTurret(rounds: ROUNDS, capacity: 10);
            turret.Weapon = Weapon("Gun", new WeaponMove { Name = "Attack 1", Reach = 20f, WindUp = 0.1f, Recovery = 0.1f });
            turret.Weapon.Shoots = true;
            turret.Weapon.Shot.Pool = "";
            int fired = 0;
            turret.Fired += (_, _, _) => fired++;
            var gunner = new GraphRig(trace: false);
            gunner.WakeUpAt(turret.SeatPosition);
            gunner.UseObjects(_registry);
            WeaponAsset sword = Weapon("Sword", new WeaponMove { Name = "Attack 1", Reach = 2f });
            gunner.GiveWeapon(sword);
            var launcher = new Capture();
            gunner.Context.Launcher = launcher;
            Assert.AreEqual(TaskStatus.Success, Interact(gunner, turret, Offer.Use), "Set-up: manned");
            Transform victim = gunner.CreateTargetAt(turret.SeatPosition + new Vector3(10f, 0f, 0f));
            gunner.Context.Blackboard.Set(VICTIM, victim);
            gunner.SetGraph(GraphRig.OneState(new AttackTask { Target = VICTIM }));
            gunner.Instance.Start(gunner.Context);
            Transform gun = turret.GunModel;

            bool everParented = false;
            for (int i = 0; i < 300 && launcher.Launches.Count < ROUNDS + 1; i++)
            {
                gunner.Step(DT);
                _ = turret.Ammo;                                            // the turret looks at its gun every frame
                everParented |= gun.parent != null;
            }

            Assert.AreEqual(ROUNDS, launcher.Launches.Count, "Every round left the gun, and no more");
            Assert.IsTrue(launcher.Launches.TrueForAll(launch => launch.Origin == gun), "…from the gun on its mount");
            Assert.AreEqual(ROUNDS, fired, "The turret counted every round as fired");
            Assert.IsTrue(turret.IsEmpty);
            Assert.IsNull(turret.Gunner, "Spent: nobody mans it");
            Assert.AreSame(sword, gunner.Context.Weapon, "The gunner's own weapon is back in its hand");
            Assert.IsFalse(everParented, "The gun never left its mount");
        }

        // ---------- helpers ----------

        /// <summary>A launcher that only writes the launches down.</summary>
        private sealed class Capture : IProjectileLauncher
        {
            public readonly List<ProjectileLaunch> Launches = new();

            public void Launch(in ProjectileLaunch launch) => Launches.Add(launch);
        }

        private GraphRig Rig(Vector3 at, out WorldRack rack)
        {
            var rig = new GraphRig(trace: false);
            rig.WakeUpAt(at);
            rig.UseObjects(_registry);
            rack = rig.UseWorldRack();
            return rig;
        }

        // Interact with the object, run to its end
        private static TaskStatus Interact(GraphRig rig, WorldObject target, Offer offer)
        {
            rig.Context.Blackboard.Set(OBJECT, target.transform);
            var task = new InteractTask { Object = OBJECT, Interaction = offer };
            TaskStatus answer = TaskStatus.Running;
            for (int i = 0; i < 200 && answer == TaskStatus.Running; i++)
            {
                rig.Context.Advance(DT);
                answer = task.Tick(rig.Context, DT);
                rig.Actuator.Apply(DT);
            }

            return answer;
        }

        private Turret NewTurret(int rounds, int capacity)
        {
            var turret = Make<Turret>("Turret", new Vector3(10f, 0f, 0f));
            turret.transform.rotation = Quaternion.LookRotation(Vector3.right);
            turret.Weapon = Weapon("Gun", new WeaponMove { Name = "Attack 1", Reach = 20f });
            turret.Capacity = capacity;
            turret.Ammo = rounds;
            _registry.Register(turret);
            return turret;
        }

        private WeaponAsset Weapon(string name, WeaponMove move)
        {
            WeaponAsset weapon = WeaponAsset.Create(move);
            weapon.name = name;
            _created.Add(weapon);
            return weapon;
        }

        private T Make<T>(string name, Vector3 at) where T : Component
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            go.transform.position = at;
            return typeof(T) == typeof(Transform) ? go.transform as T : go.AddComponent<T>();
        }
    }
}
