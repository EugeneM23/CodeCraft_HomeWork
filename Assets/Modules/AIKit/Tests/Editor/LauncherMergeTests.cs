using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// One projectile flight for every move (step 90: the throw launcher merged into IProjectileLauncher): a body with no
    /// launcher lands a projectile at once — the target is hit, a model from the hand is used up, the landing is told
    /// once where; a body with one gets the launch, with the shot's arc.
    /// </summary>
    public sealed class LauncherMergeTests
    {
        private readonly List<Object> _created = new();

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

        private Transform Thing(string name, Vector3 at)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            go.transform.position = at;
            _created.Add(go);
            return go.transform;
        }

        [Test]
        public void NoLauncher_LandsAtOnce_HitsTheTarget_UsesTheModelUp_TellsTheLanding()
        {
            Transform shooter = Thing("Shooter", Vector3.zero);
            Transform target = Thing("Target", new Vector3(0f, 0f, 6f));
            HitProbe hits = target.gameObject.AddComponent<HitProbe>();
            Transform rock = Thing("Rock", Vector3.zero);
            var landing = new Told();

            Projectiles.Launch(null, new ProjectileLaunch(shooter, target, target.position, 3f, new WeaponShot(), null,
                0.15f, 0f, rock, null, landing, 0));

            Assert.AreEqual(1, hits.Hits);
            Assert.AreEqual(3f, hits.LastDamage);
            Assert.IsTrue(rock == null, "A model that flew is gone");
            Assert.AreEqual(1, landing.Count);
            Assert.AreEqual(target.position, landing.Point);
            Assert.AreSame(target, landing.Struck);
        }

        [Test]
        public void NoLauncher_AtAPoint_NoDamageOfItsOwn_TheLandingIsToldThePoint()
        {
            Transform shooter = Thing("Shooter", Vector3.zero);
            var landing = new Told();
            var point = new Vector3(2f, 0f, 3f);

            Projectiles.Launch(null, new ProjectileLaunch(shooter, null, point, 0f, new WeaponShot(), null, 0.3f, 1f, null,
                null, landing, 0));

            Assert.AreEqual(1, landing.Count);
            Assert.AreEqual(point, landing.Point);
            Assert.IsNull(landing.Struck);
        }

        [Test]
        public void ALauncher_GetsTheLaunch_NothingLandsAtOnce()
        {
            Transform shooter = Thing("Shooter", Vector3.zero);
            Transform target = Thing("Target", new Vector3(0f, 0f, 6f));
            HitProbe hits = target.gameObject.AddComponent<HitProbe>();
            var launcher = new Capture();

            Projectiles.Launch(launcher, new ProjectileLaunch(shooter, target, 2f, new WeaponShot { Arc = 0.2f }, null));

            Assert.AreEqual(1, launcher.Launches.Count);
            Assert.AreEqual(0.2f, launcher.Launches[0].Arc, "A weapon's shot: straight or a lob by its own Arc");
            Assert.AreEqual(0, hits.Hits, "The body's flight hits, later");
        }

        [Test]
        public void Bow_NoLauncherOnTheBody_TheShotLandsAtOnce_ThroughTheSameFallback()
        {
            var rig = new GraphRig();
            WeaponAsset bow = WeaponAsset.Create(new WeaponMove { Name = "Shot", Reach = 14f, WindUp = 0.2f, Damage = 2f });
            bow.Shoots = true;
            bow.Shot.Pool = "";
            rig.GiveWeapon(bow);
            rig.Context.Launcher = null;
            Transform victim = rig.CreateTargetAt(new Vector3(0f, 0f, 8f));
            HitProbe hits = victim.gameObject.AddComponent<HitProbe>();
            rig.Context.Blackboard.Set("Victim", victim);
            rig.SetGraph(GraphRig.OneState(new AttackTask { Target = "Victim" }));
            rig.Instance.Start(rig.Context);

            rig.StepSeconds(0.25f);

            Assert.AreEqual(1, hits.Hits);
            Assert.AreEqual(2f, hits.LastDamage);
        }

        private sealed class Capture : IProjectileLauncher
        {
            public readonly List<ProjectileLaunch> Launches = new();

            public void Launch(in ProjectileLaunch launch) => Launches.Add(launch);
        }

        private sealed class Told : IProjectileLanding
        {
            public int Count;
            public Vector3 Point;
            public Transform Struck;

            public void Landed(in ProjectileLaunch launch, Vector3 point, Transform struck)
            {
                Count++;
                Point = point;
                Struck = struck;
            }
        }
    }
}
