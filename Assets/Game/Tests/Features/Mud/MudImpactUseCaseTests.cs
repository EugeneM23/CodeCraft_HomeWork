using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    /// <summary>
    /// A 10 x 10 m slab of mud far from the scene origin, its top at <see cref="Top"/>. The mud is
    /// set up the way Awake does it, minus the render texture and the stamp material.
    /// </summary>
    public sealed class MudImpactUseCaseTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly Vector3 Far = new(5000f, 5000f, 5000f);
        private static readonly float Top = Far.y + 0.5f;

        private readonly List<GameObject> _created = new();
        private readonly List<MudSurface> _muds = new();

        [TearDown]
        public void TearDown()
        {
            foreach (MudSurface mud in _muds)
                foreach (string batch in new[] { "_digs", "_presses", "_dents" })
                    Object.DestroyImmediate(Part<Mesh>(mud, batch, "Mesh"));
            _muds.Clear();

            foreach (GameObject gameObject in _created)
                if (gameObject != null)
                    Object.DestroyImmediate(gameObject);
            _created.Clear();
        }

        [Test]
        public void ColliderWithMudOnIt_IsMud()
        {
            Assert.That(MudImpactUseCase.IsMud(Mud().GetComponent<Collider>()), Is.True);
        }

        [Test]
        public void PlainFloor_IsNotMud()
        {
            Assert.That(MudImpactUseCase.IsMud(Floor()), Is.False);
            Assert.That(MudImpactUseCase.IsMud(null), Is.False);
        }

        [Test]
        public void TryFindBelow_FindsTheMudAndWhereItsTopIs()
        {
            MudSurface mud = Mud();

            bool found = MudImpactUseCase.TryFindBelow(Far + new Vector3(1f, 1.5f, 2f), 1.5f, out MudSurface surface,
                out Vector3 ground);

            Assert.That(found, Is.True);
            Assert.That(surface, Is.SameAs(mud));
            Assert.That(ground.y, Is.EqualTo(Top).Within(1e-3f));
            Assert.That(ground.x, Is.EqualTo(Far.x + 1f).Within(1e-3f));
        }

        [Test]
        public void TryFindBelow_OverPlainFloor_FindsNoMudButStillTheGround()
        {
            Floor();

            bool found = MudImpactUseCase.TryFindBelow(Far + new Vector3(0f, 1f, 0f), 1.5f, out MudSurface surface,
                out Vector3 ground);

            Assert.That(found, Is.False);
            Assert.That(surface, Is.Null);
            Assert.That(ground.y, Is.EqualTo(Top).Within(1e-3f));
        }

        [Test]
        public void TryFindBelow_MudOutOfReach_IsNotFound()
        {
            Mud();
            Vector3 point = Far + new Vector3(0f, 3f, 0f);

            bool found = MudImpactUseCase.TryFindBelow(point, 1.5f, out _, out Vector3 ground);

            Assert.That(found, Is.False);
            Assert.That(ground, Is.EqualTo(point));
        }

        [Test]
        public void TryFindBelow_LooksThroughCharacters()
        {
            // Someone standing between the blast and the mud does not hide it.
            MudSurface mud = Mud();
            GameObject character = Track(new GameObject("Character") { layer = PhysicsLayers.Character });
            character.transform.position = Far + new Vector3(0f, 1f, 0f);
            character.AddComponent<BoxCollider>().size = new Vector3(1f, 0.5f, 1f);
            Physics.SyncTransforms();

            bool found = MudImpactUseCase.TryFindBelow(Far + new Vector3(0f, 1.5f, 0f), 1.5f, out MudSurface surface, out _);

            Assert.That(found, Is.True);
            Assert.That(surface, Is.SameAs(mud));
        }

        [Test]
        public void Bullet_DentsTheMudWhereItHit()
        {
            MudSurface mud = Mud();
            Vector3 hit = new(Far.x + 2f, Top, Far.z - 3f);

            MudImpactUseCase.Bullet(mud.GetComponent<Collider>(), hit, new Vector3(1f, -1f, 0f));

            List<Vector3> dent = Dents(mud);
            Assert.That(dent, Has.Count.EqualTo(4));
            Vector3 centre = (dent[0] + dent[1] + dent[2] + dent[3]) / 4f;
            Assert.That(centre.x, Is.EqualTo(0.7f).Within(1e-4f));
            Assert.That(centre.y, Is.EqualTo(0.2f).Within(1e-4f));
        }

        [Test]
        public void Bullet_FlatShot_GougesLongerThanWide()
        {
            MudSurface mud = Mud();

            MudImpactUseCase.Bullet(mud.GetComponent<Collider>(), new Vector3(Far.x, Top, Far.z), Vector3.right);

            Vector4 shape = Part<List<Vector4>>(mud, "_dents", "Params")[0];
            Assert.That(shape.x, Is.GreaterThan(shape.y * 1.5f));
        }

        [Test]
        public void Bullet_IntoPlainFloor_LeavesTheMudAlone()
        {
            MudSurface mud = Mud();

            MudImpactUseCase.Bullet(Floor(), new Vector3(Far.x, Top, Far.z), Vector3.down);

            Assert.That(Dents(mud), Is.Empty);
        }

        [Test]
        public void Blast_LeavesACraterUnderItWithPitsAround()
        {
            MudSurface mud = Mud();

            MudImpactUseCase.Blast(new Vector3(Far.x, Top + 0.1f, Far.z), 3f);

            List<Vector3> dents = Dents(mud);
            Assert.That(dents.Count / 4, Is.InRange(1 + 3, 1 + 6), "a crater and three to six pits");
            Vector3 crater = (dents[0] + dents[1] + dents[2] + dents[3]) / 4f;
            Assert.That(crater.x, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(crater.y, Is.EqualTo(0.5f).Within(1e-4f));
        }

        [Test]
        public void Blast_HigherUp_DigsShallower()
        {
            // Half the reach up: half as deep as the 1.2-1.6 a blast on the ground digs.
            MudSurface mud = Mud();

            MudImpactUseCase.Blast(new Vector3(Far.x, Top + 0.75f, Far.z), 3f);

            float depth = Part<List<Vector4>>(mud, "_dents", "Params")[0].z;
            Assert.That(depth, Is.InRange(0.6f - 1e-4f, 0.8f + 1e-4f));
        }

        [Test]
        public void Blast_HighAboveTheMud_LeavesNoMark()
        {
            MudSurface mud = Mud();

            MudImpactUseCase.Blast(new Vector3(Far.x, Top + 3f, Far.z), 3f);

            Assert.That(Dents(mud), Is.Empty);
        }

        private GameObject Track(GameObject gameObject)
        {
            _created.Add(gameObject);
            return gameObject;
        }

        private Collider Floor()
        {
            GameObject floor = Track(new GameObject("Floor"));
            floor.transform.position = Far;
            BoxCollider collider = floor.AddComponent<BoxCollider>();
            collider.size = new Vector3(10f, 1f, 10f);
            Physics.SyncTransforms();
            return collider;
        }

        private MudSurface Mud()
        {
            GameObject floor = Track(new GameObject("Mud"));
            floor.transform.position = Far;
            MudSurface mud = floor.AddComponent<MudSurface>();
            floor.GetComponent<BoxCollider>().size = new Vector3(10f, 1f, 10f);
            Physics.SyncTransforms();

            Set(mud, "_min", new Vector2(Far.x - 5f, Far.z - 5f));
            Set(mud, "_size", new Vector2(10f, 10f));
            Type batch = typeof(MudSurface).GetNestedType("Batch", BindingFlags.NonPublic);
            foreach (string name in new[] { "_digs", "_presses", "_dents" })
                Set(mud, name, Activator.CreateInstance(batch, true));

            _muds.Add(mud);
            return mud;
        }

        private static void Set(MudSurface mud, string field, object value) =>
            typeof(MudSurface).GetField(field, Private).SetValue(mud, value);

        private static T Part<T>(MudSurface mud, string batch, string part)
        {
            object instance = typeof(MudSurface).GetField(batch, Private).GetValue(mud);
            return (T)instance.GetType().GetField(part).GetValue(instance);
        }

        private static List<Vector3> Dents(MudSurface mud) => Part<List<Vector3>>(mud, "_dents", "Vertices");
    }
}
