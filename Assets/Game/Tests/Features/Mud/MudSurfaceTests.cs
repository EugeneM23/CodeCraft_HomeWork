using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    /// <summary>
    /// Where the stamps land on the track map. The mud is set up the way Awake does it, minus the
    /// render texture and the stamp material, so nothing here needs a graphics device.
    /// </summary>
    public sealed class MudSurfaceTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        private GameObject _floor;
        private MudSurface _mud;

        [SetUp]
        public void SetUp()
        {
            // A 10 x 10 m floor with its corner at the origin.
            _floor = new GameObject("Mud");
            _mud = _floor.AddComponent<MudSurface>();
            Prepare(new Vector2(0f, 0f), new Vector2(10f, 10f));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (string batch in new[] { "_digs", "_presses", "_dents" })
                Object.DestroyImmediate(BatchMesh(batch));
            Object.DestroyImmediate(_floor);
        }

        [Test]
        public void Dent_IsAQuadOverTheOval_InMapSpace()
        {
            _mud.Dent(new Vector3(5f, 2f, 5f), Vector3.right, 1f, 0.5f, 0.3f, 2f, 0f);

            Assert.That(Vertices("_dents"), Is.EqualTo(new[]
            {
                new Vector3(0.4f, 0.45f, 0f), new Vector3(0.6f, 0.45f, 0f),
                new Vector3(0.6f, 0.55f, 0f), new Vector3(0.4f, 0.55f, 0f)
            }).Using(VectorComparer));
            Assert.That(Indices("_dents"), Is.EqualTo(new[] { 0, 1, 2, 0, 2, 3 }));
            Assert.That(Params("_dents")[0], Is.EqualTo(new Vector4(1f, 0.5f, 0.3f, 0f)));
        }

        [Test]
        public void Dent_TurnsWithItsForward()
        {
            _mud.Dent(new Vector3(5f, 0f, 5f), Vector3.forward, 1f, 0.5f, 0.3f, 2f, 0f);

            // Long along Z now, narrow along X.
            Assert.That(Vertices("_dents")[2], Is.EqualTo(new Vector3(0.45f, 0.6f, 0f)).Using(VectorComparer));
        }

        [Test]
        public void Dent_WithARimAndARoughEdge_CoversMoreGround()
        {
            // The rim reaches half the radius past the edge on each side, a rough edge a third more.
            _mud.Dent(new Vector3(5f, 0f, 5f), Vector3.right, 1f, 0.5f, 0.3f, 2f, 0.5f, 1f);

            Assert.That(Vertices("_dents")[2], Is.EqualTo(new Vector3(0.77f, 0.635f, 0f)).Using(VectorComparer));
        }

        [Test]
        public void Dent_WithoutAHeading_FacesAlongZ()
        {
            _mud.Dent(new Vector3(5f, 0f, 5f), Vector3.up, 1f, 0.5f, 0.3f, 2f, 0f);

            Assert.That(Vertices("_dents")[2], Is.EqualTo(new Vector3(0.45f, 0.6f, 0f)).Using(VectorComparer));
        }

        [Test]
        public void Dent_OfNoDepthOrSize_LeavesNoMark()
        {
            _mud.Dent(new Vector3(5f, 0f, 5f), Vector3.right, 1f, 0.5f, 0f, 2f, 0f);
            _mud.Dent(new Vector3(5f, 0f, 5f), Vector3.right, 0f, 0.5f, 0.3f, 2f, 0f);
            _mud.Dent(new Vector3(5f, 0f, 5f), Vector3.right, 1f, 0f, 0.3f, 2f, 0f);

            Assert.That(Vertices("_dents"), Is.Empty);
        }

        [Test]
        public void Dig_ThrowsUpBermsBesideTheStrip_PressDoesNot()
        {
            // A metre-wide rut 6 m long: the berms take it out to 0.8 m either side of its middle.
            _mud.Dig(new Vector3(2f, 0f, 5f), new Vector3(8f, 0f, 5f), 1f, 0.2f);
            _mud.Press(new Vector3(2f, 0f, 5f), new Vector3(8f, 0f, 5f), 1f, 0.2f);

            Assert.That(Vertices("_digs")[2], Is.EqualTo(new Vector3(0.8f, 0.58f, 0f)).Using(VectorComparer));
            Assert.That(Vertices("_presses")[2], Is.EqualTo(new Vector3(0.8f, 0.55f, 0f)).Using(VectorComparer));
        }

        [Test]
        public void Dig_CarriesHowFarAlongTheRutEachEndLies()
        {
            // So consecutive strips of one rut line up their pattern along it.
            _mud.Dig(new Vector3(2f, 0f, 5f), new Vector3(8f, 0f, 5f), 1f, 0.2f);

            List<Vector4> parameters = Params("_digs");
            Assert.That(parameters[0].w, Is.EqualTo(2f).Within(1e-5f));
            Assert.That(parameters[1].w, Is.EqualTo(8f).Within(1e-5f));
            Assert.That(parameters[0].x, Is.EqualTo(0.5f), "half the width");
            Assert.That(parameters[0].y, Is.EqualTo(0.8f).Within(1e-5f), "out to the edge of the berm");
        }

        [Test]
        public void Dig_OnTheSpot_LeavesNoMark()
        {
            _mud.Dig(new Vector3(5f, 0f, 5f), new Vector3(5f, 1f, 5f), 1f, 0.2f);

            Assert.That(Vertices("_digs"), Is.Empty);
        }

        [Test]
        public void Map_CoversTheFloorFromItsCornerAcross()
        {
            // An oblong floor 10 m along X and 20 m along Z centred on the origin.
            Prepare(new Vector2(-5f, -10f), new Vector2(10f, 20f));

            _mud.Dent(new Vector3(0f, 0f, 5f), Vector3.right, 1f, 1f, 0.3f, 2f, 0f);

            Assert.That(Centre(Vertices("_dents")), Is.EqualTo(new Vector3(0.5f, 0.75f, 0f)).Using(VectorComparer));
        }

        private static readonly IEqualityComparer<Vector3> VectorComparer = new Vector3Comparer();

        private sealed class Vector3Comparer : IEqualityComparer<Vector3>
        {
            public bool Equals(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < 1e-8f;
            public int GetHashCode(Vector3 v) => 0;
        }

        private static Vector3 Centre(List<Vector3> quad) => (quad[0] + quad[1] + quad[2] + quad[3]) / 4f;

        private void Prepare(Vector2 min, Vector2 size)
        {
            Set("_min", min);
            Set("_size", size);
            Type batch = typeof(MudSurface).GetNestedType("Batch", BindingFlags.NonPublic);
            foreach (string name in new[] { "_digs", "_presses", "_dents" })
            {
                if (Batch(name) != null)
                    Object.DestroyImmediate(BatchMesh(name));
                Set(name, Activator.CreateInstance(batch, true));
            }
        }

        private void Set(string field, object value) =>
            typeof(MudSurface).GetField(field, Private).SetValue(_mud, value);

        private object Batch(string name) => typeof(MudSurface).GetField(name, Private).GetValue(_mud);

        private T Part<T>(string batch, string part)
        {
            object instance = Batch(batch);
            return (T)instance.GetType().GetField(part).GetValue(instance);
        }

        private Mesh BatchMesh(string batch) => Part<Mesh>(batch, "Mesh");
        private List<Vector3> Vertices(string batch) => Part<List<Vector3>>(batch, "Vertices");
        private List<Vector4> Params(string batch) => Part<List<Vector4>>(batch, "Params");
        private List<int> Indices(string batch) => Part<List<int>>(batch, "Indices");
    }
}
