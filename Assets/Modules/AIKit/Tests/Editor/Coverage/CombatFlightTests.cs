using System.Collections.Generic;
using AIKit.NavMeshNavigation;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The one flight of every move, at its corners (the step 90 recheck of the fight): a lob whose target is gone comes
    /// down where it was aimed, hits nobody and tells its landing once; a straight flight that deals nothing still hits
    /// and says whom; a launch with nothing to fly at lands at once and uses up the model it carried; the ground mark of
    /// a lob is a flat disc with no collider, as wide as the blast, gone at its end.
    /// </summary>
    public sealed class CombatFlightTests
    {
        private const float DT = 0.02f;

        private readonly List<Object> _created = new();

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
        }

        private Transform Thing(string name, Vector3 at)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            go.transform.position = at;
            _created.Add(go);
            return go.transform;
        }

        [Test]
        public void ArcFlight_TargetDestroyedInTheAir_LandsAtTheAimPoint_StruckNobody_TellsItsLanding()
        {
            Transform shooter = Thing("Thrower", Vector3.zero);
            Vector3 stood = new(0f, 0f, 8f);
            Transform target = Thing("Target", stood);
            var shot = new WeaponShot { Arc = 0.15f, Speed = 14f, HitRadius = 0.7f, Pool = "" };
            var landing = new CombatLandingProbe();
            ArcFlight flight = Thing("Rock", new Vector3(0f, 1.4f, 0f)).gameObject.AddComponent<ArcFlight>();
            flight.Launch(new ProjectileLaunch(shooter, target, stood, 1f, shot, null, shot.Arc, 0f, null, null, landing, 7),
                flight.transform.position);

            Object.DestroyImmediate(target.gameObject);                 // the target is gone while it flies
            flight.Fly(flight.Duration);

            Assert.IsTrue(flight.Ended);
            Assert.IsFalse(flight.Hit, "Nobody to hit");
            Assert.AreEqual(1, landing.Count, "Its landing is told once");
            Assert.AreEqual(stood, landing.Points[0], "Where it was aimed: where the target stood when it was let go");
            Assert.IsNull(landing.Struck[0], "It struck nobody");
        }

        [Test]
        public void ArrowFlight_DamageZero_PassesNear_HitButNoBlow_LandingToldItStruck()
        {
            Transform shooter = Thing("Shooter", Vector3.zero);
            Transform target = Thing("Target", new Vector3(0f, 0f, 8f));
            HitProbe hits = target.gameObject.AddComponent<HitProbe>();
            var shot = new WeaponShot { Speed = 20f, HitRadius = 0.7f, AimHeight = 1.2f, Pool = "" };
            var landing = new CombatLandingProbe();
            ArrowFlight arrow = Thing("Bolt", new Vector3(0f, 1.2f, 0f)).gameObject.AddComponent<ArrowFlight>();
            arrow.Launch(new ProjectileLaunch(shooter, target, target.position, 0f, shot, null, 0f, 0f, null, null, landing, 3),
                11f);

            for (int i = 0; i < 200 && !arrow.Ended; i++)
                arrow.Fly(DT);

            Assert.IsTrue(arrow.Hit, "It passed near the target: a hit");
            Assert.AreEqual(0, hits.Hits, "It deals nothing itself — the ability's effects do, where it lands");
            Assert.AreEqual(1, landing.Count);
            Assert.AreSame(target, landing.Struck[0], "The landing is told whom it struck");
        }

        [Test]
        public void ArrowLauncher_NoTargetNoArc_LandsAtOnce_CarriedModelUsedUp()
        {
            Transform shooter = Thing("Shooter", Vector3.zero);
            Transform model = Thing("Rock", new Vector3(0f, 1.4f, 0.3f));
            Vector3 point = new(0f, 0f, 6f);
            var shot = new WeaponShot { Pool = "" };
            var landing = new CombatLandingProbe();

            ArrowLauncher.Shared.Launch(new ProjectileLaunch(shooter, null, point, 1f, shot, null, 0f, 0f, model, null,
                landing, 0));

            Assert.IsTrue(model == null, "Nothing to fly at: the model it carried is used up at once");
            Assert.AreEqual(1, landing.Count, "The landing is told at once");
            Assert.AreEqual(point, landing.Points[0], "…at the point");
            Assert.IsNull(landing.Struck[0], "…hitting nobody");
            Assert.IsEmpty(Object.FindObjectsByType<ArcFlight>(FindObjectsInactive.Include, FindObjectsSortMode.None), "No lob");
            Assert.IsEmpty(Object.FindObjectsByType<ArrowFlight>(FindObjectsInactive.Include, FindObjectsSortMode.None), "No arrow");
        }

        [Test]
        public void GroundMarkCue_DiscHasNoCollider_TwiceItsRadiusWide()
        {
            const float RADIUS = 2f;
            var cue = new GroundMarkCue { Radius = RADIUS };
            Vector3 point = new(1f, 0f, 3f);

            var mark = cue.Begin(new AbilityImpact(null, null, null, point, null)) as GameObject;
            Assert.IsNotNull(mark, "A mark is made");
            _created.Add(mark);

            Assert.IsNull(mark.GetComponent<Collider>(), "A flat disc that nothing bumps into");
            Assert.AreEqual(new Vector3(RADIUS * 2f, 0.01f, RADIUS * 2f), mark.transform.localScale, "As wide as the blast");
            Assert.AreEqual(point + Vector3.up * 0.03f, mark.transform.position, "On the ground where it will come down");

            cue.End(mark);
            Assert.IsTrue(mark == null, "Gone at its end");
        }
    }
}
