using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// Who stands at an object of the world (step 90; handbook §24.1 — the object keeps its own state, the agents only ask
    /// it; §18а.2 — a cover that no longer hides is no cover). An occupant is there while it is about and near its place:
    /// a fallen character (switched off, its body still lying at the place — the lab's Lab Life) occupies nothing, and its
    /// cover, perch, place or turret is free for the next one (a fallen gunner leaves the gun on its mount, the rounds in
    /// it come back). An object switched off has nobody at it: its occupants are in cover no more. The numbers of a cover
    /// (how far off it is still occupied, how far round it still hides) and of a perch (where its top is) are the
    /// object's own.
    /// </summary>
    public sealed class WorldOccupancyTests
    {
        private const string OBJECT = "Object";
        private const string THREAT = "Threat";
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

        // ---------- a fallen occupant ----------

        [Test]
        public void Cover_ItsShooterFalls_TheCoverIsFreeForAnother_NobodyIsInCover()
        {
            Transform threat = Make<Transform>(THREAT, new Vector3(0f, 0f, 12f));
            var cover = Make<Cover>("Crate", new Vector3(0f, 0f, 3f));
            GraphRig shooter = Rig(Vector3.zero);
            shooter.WakeUpAt(cover.PlaceFor(Offer.Occupy, shooter.Context, threat));
            Assert.That(Flat(shooter.Context.Position - new Vector3(0f, 0f, 3f - cover.Behind)).magnitude, Is.LessThan(1e-4f),
                "Set-up: Behind metres on the far side of the crate");
            Assert.AreEqual(TaskStatus.Success, Occupy(shooter, cover, threat), "Set-up: taken");
            Assert.IsTrue(shooter.Context.InCover, "Set-up: in cover");
            GraphRig other = Rig(new Vector3(3f, 0f, 0f));
            Assert.IsFalse(cover.Offers(Offer.Occupy, other.Context), "Set-up: taken, nothing for another");

            Fall(shooter);

            Assert.IsTrue(cover.Offers(Offer.Occupy, other.Context), "Its shooter fell: free for another");
            Assert.AreEqual(0, cover.OccupantCount, "Nobody stands behind it");
            Assert.IsNull(shooter.Context.Occupied, "The fallen one occupies nothing");
            Assert.IsFalse(shooter.Context.InCover, "…and is in cover no more");
            Assert.IsTrue(shooter.Agent.TryGetState(CharacterState.Occupying, out bool occupying) && !occupying,
                "Its status says it occupies nothing");
        }

        [Test]
        public void PerchAndAIObject_TheirOccupantFalls_FreeForAnother()
        {
            var perch = Make<Perch>("Perch", Vector3.zero);
            perch.Height = 0f;                                              // its top at the feet: no climbing here
            var seat = Make<AIObject>("Seat", new Vector3(10f, 0f, 0f));
            seat.Capacity = 1;
            GraphRig first = Rig(Vector3.zero);
            GraphRig second = Rig(new Vector3(10f, 0f, 0f));
            GraphRig other = Rig(new Vector3(5f, 0f, 0f));
            Assert.AreEqual(TaskStatus.Success, Occupy(first, perch, null), "Set-up: on the perch");
            Assert.AreEqual(TaskStatus.Success, Occupy(second, seat, null), "Set-up: at the seat");
            Assert.IsFalse(perch.Offers(Offer.Occupy, other.Context) || seat.Offers(Offer.Occupy, other.Context),
                "Set-up: both taken");

            Fall(first);
            Fall(second);

            Assert.IsTrue(perch.Offers(Offer.Occupy, other.Context), "The perch is free again");
            Assert.IsTrue(seat.Offers(Offer.Occupy, other.Context), "The seat of one is free again");
            Assert.AreEqual(0, seat.OccupantCount);
            Assert.IsNull(first.Context.Occupied);
            Assert.IsNull(second.Context.Occupied);
        }

        [Test]
        public void Turret_ItsGunnerFalls_TheGunIsLeftOnItsMount_ItsRoundsComeBack()
        {
            Turret turret = NewTurret(rounds: 7, capacity: 10);
            GraphRig gunner = Rig(turret.SeatPosition, worldRack: false);       // the character's own rack
            WeaponAsset sword = Weapon("Sword", 2f);
            gunner.GiveWeapon(sword);
            Assert.That(Flat(gunner.Context.Position - new Vector3(10f - turret.SeatDistance, 0f, 0f)).magnitude,
                Is.LessThan(1e-4f), "Set-up: at the seat, Seat Distance behind the turret");
            gunner.Context.Blackboard.Set(OBJECT, turret.transform);
            Assert.AreEqual(TaskStatus.Success,
                Run(gunner, new InteractTask { Object = OBJECT, Interaction = Offer.Use }, 1f), "Set-up: manned");
            Assert.AreSame(turret.GunModel, gunner.Agent.FromWorld, "Set-up: the gun is the weapon in the hand");
            gunner.Agent.SpendUse();                                         // a round fired
            Assert.AreEqual(6, turret.Ammo, "Set-up: one round fired");
            GraphRig other = Rig(new Vector3(3f, 0f, 0f), worldRack: false);
            Assert.IsFalse(turret.Offers(Offer.Use, other.Context), "Set-up: manned by another");

            Fall(gunner);

            Assert.IsNull(turret.Gunner, "A fallen gunner mans it no more");
            Assert.AreEqual(6, turret.Ammo, "The rounds left in the gun are the turret's again");
            Assert.IsTrue(turret.Offers(Offer.Use, other.Context), "Free for the next gunner");
            Assert.IsNull(gunner.Agent.FromWorld, "The gun is left on its mount");
            Assert.AreSame(sword, gunner.Context.Weapon, "…the fallen one's own weapon is back in its hand");
            Assert.IsNull(gunner.Context.Occupied);
        }

        [Test]
        public void Turret_ItsGunnerFallsLettingGoOfTheGun_ItsLastRoundComesBack_NotFired()
        {
            Turret turret = NewTurret(rounds: 1, capacity: 10);
            int fired = 0;
            turret.Fired += (_, _, _) => fired++;
            GraphRig gunner = Rig(turret.SeatPosition, worldRack: false);
            WeaponAsset sword = Weapon("Sword", 2f);
            gunner.GiveWeapon(sword);
            gunner.Context.Blackboard.Set(OBJECT, turret.transform);
            Assert.AreEqual(TaskStatus.Success,
                Run(gunner, new InteractTask { Object = OBJECT, Interaction = Offer.Use }, 1f), "Set-up: manned, one round");

            Fall(gunner);
            gunner.Agent.LetGoFromWorld();                                   // a fall that lets go of what the hand held

            Assert.IsNull(turret.Gunner, "A fallen gunner mans it no more");
            Assert.AreEqual(1, turret.Ammo, "Its one round left the hand unfired: the turret's again");
            Assert.AreEqual(0, fired, "…never counted as fired");
            Assert.AreSame(sword, gunner.Context.Weapon);
        }

        // ---------- an object switched off ----------

        [Test]
        public void Cover_SwitchedOff_ItsShooterOccupiesNothing_IsInCoverNoMore()
        {
            Transform threat = Make<Transform>(THREAT, new Vector3(0f, 0f, 12f));
            var cover = Make<Cover>("Crate", new Vector3(0f, 0f, 3f));
            GraphRig shooter = Rig(Vector3.zero);
            shooter.WakeUpAt(cover.PlaceFor(Offer.Occupy, shooter.Context, threat));
            Assert.AreEqual(TaskStatus.Success, Occupy(shooter, cover, threat), "Set-up: taken");
            Assert.IsTrue(shooter.Context.InCover, "Set-up: in cover");

            cover.gameObject.SetActive(false);                              // the crate is gone (pooled, broken)

            Assert.IsNull(shooter.Context.Occupied, "It stands at nothing");
            Assert.IsFalse(shooter.Context.InCover, "…and is in cover no more");
            Assert.IsTrue(shooter.Agent.TryGetState(CharacterState.Occupying, out bool occupying) && !occupying,
                "Status: not Occupying");
            Assert.IsTrue(shooter.Agent.TryGetState(CharacterState.InCover, out bool inCover) && !inCover,
                "Status: not In Cover");
            Assert.AreEqual(0, cover.OccupantCount);
        }

        [Test]
        public void PerchAndAIObject_SwitchedOff_TheirOccupantsOccupyNothing()
        {
            var perch = Make<Perch>("Perch", Vector3.zero);
            perch.Height = 0f;
            var well = Make<AIObject>("Well", new Vector3(10f, 0f, 0f));
            GraphRig first = Rig(Vector3.zero);
            GraphRig second = Rig(new Vector3(10f, 0f, 0f));
            Assert.AreEqual(TaskStatus.Success, Occupy(first, perch, null), "Set-up: on the perch");
            Assert.AreEqual(TaskStatus.Success, Occupy(second, well, null), "Set-up: at the well");

            perch.enabled = false;
            well.gameObject.SetActive(false);

            Assert.IsNull(first.Context.Occupied, "A perch switched off: nobody is on it");
            Assert.IsNull(second.Context.Occupied, "A place gone: nobody stands at it");
            Assert.AreEqual(0, well.OccupantCount);
        }

        [Test]
        public void Turret_SwitchedOff_ItsGunnerLetsGoOfTheGun_TheRoundsStayTheTurrets()
        {
            Turret turret = NewTurret(rounds: 5, capacity: 10);
            GraphRig gunner = Rig(turret.SeatPosition, worldRack: false);
            WeaponAsset sword = Weapon("Sword", 2f);
            gunner.GiveWeapon(sword);
            gunner.Context.Blackboard.Set(OBJECT, turret.transform);
            Assert.AreEqual(TaskStatus.Success,
                Run(gunner, new InteractTask { Object = OBJECT, Interaction = Offer.Use }, 1f), "Set-up: manned");

            turret.gameObject.SetActive(false);

            Assert.AreEqual(5, turret.Ammo, "Its rounds stay its own");
            Assert.IsNull(turret.Gunner, "Nobody mans a turret that is gone");
            Assert.IsNull(gunner.Agent.FromWorld, "The gunner holds its gun no more");
            Assert.AreSame(sword, gunner.Context.Weapon, "…its own weapon is back in its hand");
            Assert.IsNull(gunner.Context.Occupied);
        }

        // ---------- the moment of taking ----------

        [Test]
        public void Interact_ACoverTakenInTheLastMoment_Failed_NothingOccupied()
        {
            Transform threat = Make<Transform>(THREAT, new Vector3(0f, 0f, 12f));
            var cover = Make<Cover>("Crate", new Vector3(0f, 0f, 3f));
            GraphRig first = Rig(Vector3.zero);
            first.WakeUpAt(cover.PlaceFor(Offer.Occupy, first.Context, threat));
            Assert.AreEqual(TaskStatus.Success, Occupy(first, cover, threat), "Set-up: the first one took it");
            GraphRig late = Rig(first.Context.Position);
            var watch = new CharacterActionsWatch(late.Context);
            late.Context.Blackboard.Set(OBJECT, cover.transform);
            late.Context.Blackboard.Set(THREAT, threat);
            var occupy = new InteractTask { Object = OBJECT, Interaction = Offer.Occupy, Target = THREAT };

            Assert.AreEqual(TaskStatus.Failure, occupy.Tick(late.Context, DT), "Taken in the last moment: Failed at once");
            Assert.IsNull(late.Context.Occupied, "It occupies nothing");
            Assert.AreEqual(1, cover.OccupantCount, "The first one still stands behind it");
            Assert.IsTrue(cover.IsOccupiedBy(first.Context));
            Assert.IsEmpty(watch.Played, "No action was asked for");
            Assert.AreEqual(ActionState.Kind.None, late.Context.Actions.Current);
        }

        // ---------- the numbers of a cover and a perch ----------

        [Test]
        public void Cover_StaysOccupiedWithin2_7mOfItsMiddle_ShieldsWithin40Degrees_NoThreat_PlaceBehindItsBack()
        {
            var cover = Make<Cover>("Crate", Vector3.zero);
            Assert.AreEqual(1.2f, cover.Behind, "Set-up: the defaults this test counts with");
            Assert.AreEqual(0.5f, cover.Arrive);
            Assert.AreEqual(40f, cover.ShieldAngle);
            Transform threat = Make<Transform>(THREAT, new Vector3(0f, 0f, 10f));
            GraphRig shooter = Rig(new Vector3(0f, 0f, -cover.Behind));
            Assert.AreEqual(TaskStatus.Success, Occupy(shooter, cover, threat), "Set-up: taken against the threat");
            float near = cover.Behind + cover.Arrive + WorldObject.LEAVE_SLACK;

            shooter.Actuator.Position = new Vector3(0f, 0f, -(near - 0.1f));
            Assert.IsTrue(cover.IsOccupiedBy(shooter.Context), $"Within {near} m of its middle: still there");
            shooter.Actuator.Position = new Vector3(0f, 0f, -(near + 0.1f));
            Assert.IsFalse(cover.IsOccupiedBy(shooter.Context), $"Beyond {near} m: walked off");

            Assert.AreEqual(TaskStatus.Success, Occupy(shooter, cover, threat, at: Around(cover.Behind, 35f)),
                "Taken again 35° off the line");
            Assert.IsTrue(cover.Shields(shooter.Context), "35° off the line from the threat: hidden");
            shooter.Actuator.Position = Around(cover.Behind, cover.ShieldAngle + 5f);
            Assert.IsFalse(cover.Shields(shooter.Context), "45°: seen round the side");
            Assert.IsTrue(cover.IsOccupiedBy(shooter.Context), "…though it still stands at the cover");

            Assert.That(Flat(cover.PlaceFor(Offer.Occupy, shooter.Context, null) - new Vector3(0f, 0f, -cover.Behind)).magnitude,
                Is.LessThan(1e-4f), "No threat: behind its own back");
            cover.transform.rotation = Quaternion.LookRotation(Vector3.right);
            Assert.That(Flat(cover.PlaceFor(Offer.Occupy, shooter.Context, null) - new Vector3(-cover.Behind, 0f, 0f)).magnitude,
                Is.LessThan(1e-4f), "Turned to face +x: behind it is −x");
        }

        [Test]
        public void Perch_IsOn_WithinHalfSizePlus0_15_NoLowerThan0_6UnderTheTop_TurnedWithIt()
        {
            var perch = Make<Perch>("Perch", Vector3.zero);
            perch.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            perch.Height = 2.5f;
            perch.HalfSize = 1.5f;
            Quaternion turn = Quaternion.Euler(0f, 45f, 0f);
            Vector3 top = new(0f, perch.Height, 0f);

            Assert.IsTrue(perch.IsOn(top + turn * new Vector3(perch.HalfSize + 0.1f, 0f, 0f)),
                "Half Size + 0.1 along its own x (turned 45°): on");
            Assert.IsFalse(perch.IsOn(top + turn * new Vector3(perch.HalfSize + 0.2f, 0f, 0f)), "Half Size + 0.2: off");
            Assert.IsTrue(perch.IsOn(top + turn * new Vector3(0f, 0f, -(perch.HalfSize + 0.1f))), "…and along its z");
            // Turned 45°, its corner lies along the world's x: 2.2 m out is still on it (1.556 along each of its sides),
            // 2.4 m is past the corner (1.697)
            Assert.IsTrue(perch.IsOn(top + new Vector3(2.2f, 0f, 0f)), "Towards its corner: on farther out");
            Assert.IsFalse(perch.IsOn(top + new Vector3(2.4f, 0f, 0f)), "…past its corner: off");
            Assert.IsTrue(perch.IsOn(new Vector3(0f, perch.Height - 0.55f, 0f)), "0.55 below the top: still on");
            Assert.IsFalse(perch.IsOn(new Vector3(0f, perch.Height - 0.65f, 0f)), "0.65 below the top: under it");
            Assert.AreEqual(top, perch.TopCenter);
            Assert.AreEqual(top, perch.PlaceFor(Offer.Occupy, null, null));

            Transform marker = Make<Transform>("Top", new Vector3(0f, 3f, 0.5f));
            perch.Top = marker;
            Assert.AreEqual(marker.position, perch.TopCenter, "A Top object: its middle is there");
            Assert.AreEqual(marker.position, perch.PlaceFor(Offer.Occupy, null, null));
            Assert.IsTrue(perch.IsOn(marker.position + turn * new Vector3(perch.HalfSize, 0f, 0f)));
        }

        [Test]
        public void AIObject_ABoxZoneTurnedWithIt_RandomPointsAndContains_ABoxWinsOverARadius_ADiscSpreadsEvenly()
        {
            var place = Make<AIObject>("Field", Vector3.zero);
            place.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            place.ZoneSize = new Vector2(4f, 2f);
            place.ZoneRadius = 5f;

            Assert.IsTrue(place.HasZone);
            // Local (−2, 0, −1) — the corner of the box — turned 90° about y is world (−1, 0, 2): the box wins over the radius
            Assert.That((place.RandomPoint(0f, 0f) - new Vector3(-1f, 0f, 2f)).magnitude, Is.LessThan(1e-4f));
            Assert.IsTrue(place.Contains(new Vector3(0f, 0f, 1.9f), 0f), "Within its 4 m (turned: along z)");
            Assert.IsFalse(place.Contains(new Vector3(0f, 0f, 2.1f), 0f), "Out of its 4 m");
            Assert.IsTrue(place.Contains(new Vector3(0f, 0f, 2.1f), 0.2f), "…within the margin");
            Assert.IsFalse(place.Contains(new Vector3(1.1f, 0f, 0f), 0f), "Out of its 2 m (turned: along x)");

            var disc = Make<AIObject>("Yard", Vector3.zero);
            disc.ZoneRadius = 3f;
            Assert.IsTrue(disc.HasZone);
            // A quarter turn (0.25) at the edge (the root of 1): world forward
            Assert.That((disc.RandomPoint(0.25f, 1f) - new Vector3(0f, 0f, 3f)).magnitude, Is.LessThan(1e-4f));
            Assert.That(disc.RandomPoint(0.6f, 0.25f).magnitude, Is.EqualTo(disc.ZoneRadius * 0.5f).Within(1e-4f),
                "A quarter of the area is within half the radius: the points do not crowd the middle");
            Assert.IsFalse(disc.Contains(new Vector3(0f, 0f, 3.2f), 0f));
            Assert.IsTrue(disc.Contains(new Vector3(0f, 0f, 3.2f), 0.3f));

            var bare = Make<AIObject>("Post", new Vector3(7f, 0f, 0f));
            Assert.IsFalse(bare.HasZone);
            Assert.AreEqual(bare.transform.position, bare.RandomPoint(0.3f, 0.8f), "No zone: the object itself");
            Assert.IsFalse(bare.Contains(bare.transform.position, 1f), "No zone: contains nothing");
        }

        // ---------- helpers ----------

        private GraphRig Rig(Vector3 at, bool worldRack = true)
        {
            var rig = new GraphRig(trace: false);
            rig.WakeUpAt(at);
            rig.UseObjects(_registry);
            if (worldRack)
                rig.UseWorldRack();
            return rig;
        }

        // It falls as the lab's soldiers do: its brain stops, its character is switched off, the body lies where it stood
        private static void Fall(GraphRig rig)
        {
            rig.Instance?.Stop(rig.Context);
            rig.Agent.enabled = false;
        }

        // Interact Occupy run to its end (at the point first, if one is given)
        private TaskStatus Occupy(GraphRig rig, WorldObject target, Transform threat, Vector3? at = null)
        {
            if (at.HasValue)
                rig.Actuator.Position = at.Value;
            rig.Context.Blackboard.Set(OBJECT, target.transform);
            rig.Context.Blackboard.Set(THREAT, threat);
            return Run(rig, new InteractTask { Object = OBJECT, Interaction = Offer.Occupy, Target = THREAT }, 1f);
        }

        // A point this far behind a cover at the origin (the threat at +z), this many degrees round from straight behind
        private static Vector3 Around(float distance, float degrees) =>
            Quaternion.Euler(0f, degrees, 0f) * new Vector3(0f, 0f, -distance);

        private Turret NewTurret(int rounds, int capacity)
        {
            var turret = Make<Turret>("Turret", new Vector3(10f, 0f, 0f));
            turret.transform.rotation = Quaternion.LookRotation(Vector3.right);
            turret.Weapon = Weapon("Gun", 20f);
            turret.Capacity = capacity;
            turret.Ammo = rounds;
            return turret;
        }

        private WeaponAsset Weapon(string name, float reach)
        {
            WeaponAsset weapon = WeaponAsset.Create(new WeaponMove { Name = "Attack 1", Reach = reach });
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

        private static TaskStatus Run(GraphRig rig, AgentTask task, float seconds)
        {
            TaskStatus answer = TaskStatus.Running;
            int steps = Mathf.CeilToInt(seconds / DT);
            for (int i = 0; i < steps && answer == TaskStatus.Running; i++)
            {
                rig.Context.Advance(DT);
                answer = task.Tick(rig.Context, DT);
                rig.Actuator.Apply(DT);
            }

            return answer;
        }

        private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
