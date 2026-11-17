using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// A search by a kind of object of the world finds that kind itself (step 90 integration: a scene migration put an AI
    /// Object marker, owned by the gunner, on the turret; the search for a turret found the marker — "mine" before
    /// "public" — and the marker offers none of the turret's uses, so the gunner never manned the gun).
    /// </summary>
    public sealed class RegistryKindTests
    {
        private readonly List<Object> _created = new();
        private readonly List<WorldObject> _found = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created)
            {
                if (created != null)
                    Object.DestroyImmediate(created);
            }

            _created.Clear();
        }

        [Test]
        public void Registry_ATurretWithAMarkerBesideIt_TheSearchForTurretsFindsTheTurret_NotTheMarker()
        {
            var registry = new WorldObjects();
            var go = new GameObject("Turret") { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            var turret = go.AddComponent<Turret>();
            var marker = go.AddComponent<AIObject>();
            marker.Kind = "Turret";
            registry.Register(turret);
            registry.Register(marker);

            _found.Clear();
            registry.Find(new ObjectQuery { Component = typeof(Turret) }, _found);

            CollectionAssert.AreEqual(new WorldObject[] { turret }, _found,
                "A kind of object of the world is found as itself, never as another object on the same game object");
        }

        [Test]
        public void Registry_AGameComponentThatIsNoObjectOfTheWorld_IsFoundThroughTheMarkerThatCarriesIt()
        {
            var registry = new WorldObjects();
            var go = new GameObject("Potion") { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            var marker = go.AddComponent<AIObject>();
            go.AddComponent<BoxCollider>();                 // stands in for a game's own component on the prefab
            registry.Register(marker);

            _found.Clear();
            registry.Find(new ObjectQuery { Component = typeof(BoxCollider) }, _found);

            CollectionAssert.AreEqual(new WorldObject[] { marker }, _found,
                "Any component of the game is searched through the AI Object that carries it — no code needed");
        }
    }
}
