using System.Collections.Generic;
using AIKit.NavMeshNavigation;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// A character's run of an ability made of parts, at its corners (the step 90 recheck of the fight): every throw in
    /// the air comes down with its own effects and its own mark, however many fly at once; a straight projectile's own
    /// flight decides whether it hit — the damage does not hit a target the bolt missed by its own radius; a lob always
    /// has its time in the air, even a flat one.
    /// </summary>
    public sealed class CombatAbilityRunnerTests
    {
        private const string FOE = "Foe";

        private GraphRig _rig;
        private Transform _target;
        private HitProbe _hits;
        private readonly List<Object> _created = new();

        [SetUp]
        public void SetUp()
        {
            _rig = new GraphRig();
            _target = _rig.CreateTargetAt(new Vector3(0f, 0f, 1.5f));
            _hits = _target.gameObject.AddComponent<HitProbe>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (ArcFlight flight in Object.FindObjectsByType<ArcFlight>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(flight.gameObject);
            foreach (ArrowFlight arrow in Object.FindObjectsByType<ArrowFlight>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(arrow.gameObject);
            foreach (Object created in _created)
            {
                if (created != null)
                    Object.DestroyImmediate(created);
            }

            _created.Clear();
            GraphRig.DestroyAll();
        }

        private AbilityRunner Runner(AbilityAsset ability)
        {
            _created.Add(ability);
            var runner = new AbilityRunner(ability, _rig.Agent.transform);
            _rig.Abilities.Add(runner.Definition);
            _rig.Abilities.Register(runner);
            return runner;
        }

        private static AbilityAsset Ability(string name, float windUp, float recovery, params AbilityPart[] parts)
        {
            AbilityAsset ability = AbilityAsset.Create(name, parts);
            ability.Timing = new AbilityTiming { WindUp = windUp, Recovery = recovery };
            ability.Queue.AtOnce = 0;
            return ability;
        }

        // ---------- many throws in the air ----------

        [Test]
        public void AbilityRunner_FiveThrowsInTheAir_EachLandingDoesItsOwnEffects()
        {
            var launcher = new CombatCaptureLauncher();
            _rig.Context.Launcher = launcher;
            var cue = new CombatTokenCue();
            Runner(Ability("Bolt", 0.05f, 0.05f, new ProjectileDelivery { Arc = 0f }, new DamageEffect { Damage = 1f }, cue));

            const int THROWS = 5;
            for (int i = 0; i < THROWS; i++)
            {
                Assert.IsTrue(_rig.Abilities.TryUse("Bolt", _rig.Context), $"Set-up: throw {i + 1} (no pause)");
                _rig.Tick(0.11f);
            }

            Assert.AreEqual(THROWS, launcher.Launches.Count, "Set-up: five in the air");
            Assert.AreEqual(THROWS, cue.Made.Count, "A mark for each");
            CollectionAssert.IsEmpty(cue.Ended, "Nothing came down yet: every throw's mark is still shown");
            for (int i = 0; i < THROWS; i++)
            {
                ProjectileLaunch launch = launcher.Launches[i];
                launch.Landing.Landed(launch, _target.position, _target);
                Assert.AreEqual(i + 1, _hits.Hits, $"Landing {i + 1} does its own damage");
                Assert.AreEqual(i + 1, cue.Ended.Count, $"Landing {i + 1} ends one mark");
                Assert.AreSame(cue.Made[i], cue.Ended[i], $"…its own throw's mark");
            }
        }

        // ---------- who decides a hit ----------

        [Test]
        public void DamageEffect_AStraightBoltThatMissed_HurtsNobody_EvenStoppedNearTheTarget()
        {
            var launcher = new CombatCaptureLauncher();
            _rig.Context.Launcher = launcher;
            Runner(Ability("Bolt", 0.1f, 0.1f, new ProjectileDelivery { Arc = 0f, HitRadius = 0.7f },
                new DamageEffect { Damage = 1f, HitRadius = 1f }));
            _rig.Abilities.TryUse("Bolt", _rig.Context);
            _rig.Tick(0.15f);
            ProjectileLaunch launch = launcher.Launches[0];

            // It passed 0.8 m aside (more than its 0.7 m) and a wall 0.3 m behind the target stopped it
            launch.Landing.Landed(launch, _target.position + new Vector3(0.8f, 0f, 0.3f), null);

            Assert.AreEqual(0, _hits.Hits, "The bolt's own flight said it missed: the damage does not hit by its own radius");
        }

        [Test]
        public void DamageEffect_ALobComingDownWithinTheEffectsRadius_Hits()
        {
            var launcher = new CombatCaptureLauncher();
            _rig.Context.Launcher = launcher;
            Runner(Ability("Grenade", 0.1f, 0.1f, new LobDelivery(), new DamageEffect { Damage = 1f, HitRadius = 1f }));
            _rig.Abilities.TryUse("Grenade", _rig.Context);
            _rig.Tick(0.15f);
            ProjectileLaunch launch = launcher.Launches[0];

            launch.Landing.Landed(launch, _target.position + new Vector3(0.85f, 0f, 0f), null);

            Assert.AreEqual(1, _hits.Hits, "A lob hits by the effect's radius where it comes down (0.85 ≤ 1)");
        }

        // ---------- a flat lob ----------

        [Test]
        public void LobDelivery_ArcZero_StillLandsAfterItsFlight()
        {
            const float FLIGHT = 1f;
            _rig.Context.Launcher = ArrowLauncher.Shared;
            var finder = new ListFinder();
            finder.Add(FOE, _target);
            _rig.Context.Objects = finder;
            Runner(Ability("Flat Throw", 0.1f, 0.1f, new LobDelivery { Flight = FLIGHT, Arc = 0f },
                new AreaDamageEffect { Radius = 2f, Damage = 1f, Tag = FOE }));

            _rig.Abilities.TryUse("Flat Throw", _rig.Context);
            _rig.Tick(0.15f);

            Assert.AreEqual(0, _hits.Hits, "Not at the release: the target has its second to step out");
            ArcFlight[] flights = Object.FindObjectsByType<ArcFlight>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.AreEqual(1, flights.Length, "It flies");
            Assert.AreEqual(FLIGHT, flights[0].Duration, 1e-4f, "For its Flight seconds");
            flights[0].Fly(FLIGHT);
            Assert.AreEqual(1, _hits.Hits, "The blast where it came down");
        }
    }
}
