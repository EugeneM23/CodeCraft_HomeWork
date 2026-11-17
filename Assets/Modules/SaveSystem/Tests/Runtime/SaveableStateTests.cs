using NUnit.Framework;
using Saving.Objects;
using UnityEngine;

namespace Saving.Tests
{
    /// <summary>The snapshot of one object, without a scene or a save system around it.</summary>
    public class SaveableStateTests
    {
        private GameObject _object;

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_object);

        [Test]
        public void Saveable_CaptureThenRestore_ActiveAndData()
        {
            _object = new GameObject("Snapshot");
            var saveable = _object.AddComponent<Saveable>();
            var counter = _object.AddComponent<SaveTestCounter>();
            var child = new GameObject("Child");
            child.transform.SetParent(_object.transform, false);
            counter.Value = 5;
            _object.transform.position = new Vector3(2f, 3f, 0f);
            child.SetActive(false);
            var state = saveable.CaptureState();

            counter.Value = 1;
            _object.transform.position = Vector3.zero;
            child.SetActive(true);
            saveable.RestoreState(state);

            Assert.AreEqual(5, counter.Value);
            Assert.AreEqual(new Vector3(2f, 3f, 0f), _object.transform.position);
            Assert.IsFalse(child.activeSelf);
        }
    }
}
