using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ScreenNavigation.Tests
{
    /// <summary>Mouse hover selects an interactable button — mouse and gamepad share one selection.</summary>
    public class SelectOnPointerEnterTests
    {
        private EventSystem _events;
        private Button _button;

        [SetUp]
        public void SetUp()
        {
            _events = new GameObject("EventSystem").AddComponent<EventSystem>();
            var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            go.AddComponent<SelectOnPointerEnter>();
            _button = go.GetComponent<Button>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_button.gameObject);
            Object.DestroyImmediate(_events.gameObject);
        }

        private void Hover() =>
            ExecuteEvents.Execute(_button.gameObject, new PointerEventData(_events), ExecuteEvents.pointerEnterHandler);

        [Test]
        public void SelectOnPointerEnter_Hover_SelectsButton()
        {
            Hover();

            Assert.AreSame(_button.gameObject, _events.currentSelectedGameObject);
        }

        [Test]
        public void SelectOnPointerEnter_NotInteractable_KeepsSelection()
        {
            _button.interactable = false;

            Hover();

            Assert.IsNull(_events.currentSelectedGameObject);
        }
    }
}
