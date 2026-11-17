using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class PickUpUseCaseTests
    {
        private GameObject _gameObject;
        private int _pickUpSounds;
        private int _removals;
        private IEntity _givenTo;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("PickUp");
            _pickUpSounds = 0;
            _removals = 0;
            _givenTo = null;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_gameObject);

        private IEntity PickUp(bool accepts)
        {
            IEntity pickUp = new Entity();
            PickUpUseCase.AddPickUp(pickUp, _gameObject.transform, character =>
            {
                _givenTo = character;
                return accepts;
            }, () => _removals++);
            pickUp.GetPickUpEvent().Subscribe(() => _pickUpSounds++);
            return pickUp;
        }

        [Test]
        public void AddPickUp_MakesItSomethingToInteractWith()
        {
            IEntity pickUp = PickUp(accepts: true);

            Assert.That(pickUp.HasInteractableTag(), Is.True);
            Assert.That(pickUp.GetTransform(), Is.SameAs(_gameObject.transform));
        }

        [Test]
        public void Taken_HandsOverTheContent_SoundsAndGoesAway()
        {
            IEntity pickUp = PickUp(accepts: true);
            IEntity character = new Entity();

            pickUp.GetInteractAction().Invoke(character);

            Assert.That(_givenTo, Is.SameAs(character));
            Assert.That(_pickUpSounds, Is.EqualTo(1));
            Assert.That(_removals, Is.EqualTo(1));
        }

        [Test]
        public void Refused_StaysWhereItIs_AndIsSilent()
        {
            IEntity pickUp = PickUp(accepts: false);

            pickUp.GetInteractAction().Invoke(new Entity());

            Assert.That(_pickUpSounds, Is.Zero);
            Assert.That(_removals, Is.Zero);
        }
    }
}
