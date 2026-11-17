using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Tests
{
    public sealed class GrenadeHudBehaviourTests
    {
        private GameObject _hudObject;
        private Text _hud;
        private Ammo _frags;
        private Ammo _flashes;
        private IEntity _character;

        [SetUp]
        public void SetUp()
        {
            _hudObject = new GameObject("GrenadeHud", typeof(RectTransform));
            _hud = _hudObject.AddComponent<Text>();

            _frags = new Ammo(3);
            _flashes = new Ammo(1);
            _character = new Entity();
            _character.AddGrenadeAmmo(new Dictionary<GrenadeType, Ammo>
            {
                [GrenadeType.Frag] = _frags,
                [GrenadeType.Flash] = _flashes
            });
            _character.AddGrenadeSelected(new ReactiveVariable<GrenadeType>(GrenadeType.Frag));
            _character.AddBehaviour(new GrenadeHudBehaviour(_hud));
            _character.Init();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_hudObject);

        [Test]
        public void AtStart_ShowsTheSelectedTypeAndItsStock()
        {
            Assert.That(_hud.text, Is.EqualTo("Граната: Осколочная ×3  [Tab]"));
        }

        [Test]
        public void GrenadeSpent_CountGoesDown()
        {
            _frags.Spend();

            Assert.That(_hud.text, Is.EqualTo("Граната: Осколочная ×2  [Tab]"));
        }

        [Test]
        public void TypeSwitched_ShowsTheNewTypeAndItsStock()
        {
            _character.GetGrenadeSelected().Value = GrenadeType.Flash;

            Assert.That(_hud.text, Is.EqualTo("Граната: Флеш ×1  [Tab]"));
        }

        [Test]
        public void OtherTypesStockChanges_LineStillShowsTheSelectedOne()
        {
            _flashes.Add(4);

            Assert.That(_hud.text, Is.EqualTo("Граната: Осколочная ×3  [Tab]"));
        }

        [Test]
        public void AfterDispose_LineIsNoLongerRedrawn()
        {
            _character.Dispose();

            _frags.Spend();

            Assert.That(_hud.text, Is.EqualTo("Граната: Осколочная ×3  [Tab]"));
        }
    }
}
