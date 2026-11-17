using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ScreenNavigation.Tests
{
    /// <summary>UIScreen with a real EventSystem: visibility, input, selection memory, transitions.</summary>
    public class UIScreenTests
    {
        private EventSystem _events;
        private GameObject _canvas;

        [SetUp]
        public void SetUp()
        {
            _events = new GameObject("EventSystem").AddComponent<EventSystem>();
            _canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_canvas);
            Object.DestroyImmediate(_events.gameObject);
        }

        private GameObject Selected => _events.currentSelectedGameObject;

        /// <summary>A switched-off screen with buttons First (selected on open) and Second.</summary>
        private UIScreen CreateScreen(string name, bool overlay = false, float fade = -1f)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.SetActive(false);
            go.transform.SetParent(_canvas.transform, false);
            var screen = go.AddComponent<UIScreen>();
            Button first = CreateButton(go.transform, "First");
            CreateButton(go.transform, "Second");

            FadeTransition transition = null;
            if (fade >= 0f)
            {
                transition = go.AddComponent<FadeTransition>();
                transition.Configure(fade);
            }

            screen.Configure(first, overlay, transition: transition);
            return screen;
        }

        private static Button CreateButton(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            return go.GetComponent<Button>();
        }

        private static GameObject Child(UIScreen screen, string name) => screen.transform.Find(name).gameObject;

        private static void Enter(UIScreen screen)
        {
            screen.Open();
            screen.Show();
            screen.Focus();
        }

        [Test]
        public void UIScreen_Focus_SelectsFirstSelected()
        {
            UIScreen screen = CreateScreen("A");

            Enter(screen);

            Assert.AreSame(Child(screen, "First"), Selected);
            Assert.IsTrue(screen.IsShown);
            Assert.IsTrue(screen.IsInteractable);
            Assert.IsTrue(screen.GetComponent<CanvasGroup>().interactable);
        }

        [Test]
        public void UIScreen_FirstSelectedChanged_SelectedOnFocus()
        {
            UIScreen screen = CreateScreen("A");
            Assert.AreSame(Child(screen, "First"), screen.FirstSelected.gameObject);

            screen.FirstSelected = Child(screen, "Second").GetComponent<Button>();
            Enter(screen);

            Assert.AreSame(Child(screen, "Second"), Selected, "a screen may open on another button (the first one is hidden)");
        }

        [Test]
        public void UIScreen_FocusAfterBlur_SelectsRememberedButton()
        {
            UIScreen screen = CreateScreen("A");
            Enter(screen);
            _events.SetSelectedGameObject(Child(screen, "Second"));

            screen.Blur();
            _events.SetSelectedGameObject(null);
            screen.Focus();

            Assert.AreSame(Child(screen, "Second"), Selected);
        }

        [Test]
        public void UIScreen_ReopenedAfterClose_SelectsFirstAgain()
        {
            UIScreen screen = CreateScreen("A");
            Enter(screen);
            _events.SetSelectedGameObject(Child(screen, "Second"));
            screen.Blur();
            screen.Hide();
            screen.Close();

            Enter(screen);

            Assert.AreSame(Child(screen, "First"), Selected);
        }

        [Test]
        public void UIScreen_Blur_StaysVisibleButNotInteractable()
        {
            UIScreen screen = CreateScreen("A");
            Enter(screen);

            screen.Blur();

            Assert.IsTrue(screen.gameObject.activeSelf);
            Assert.IsTrue(screen.IsShown);
            Assert.IsFalse(screen.IsInteractable);
            Assert.IsFalse(screen.GetComponent<CanvasGroup>().interactable);
        }

        [Test]
        public void UIScreen_Hide_SwitchesOffAndDropsItsSelection()
        {
            UIScreen screen = CreateScreen("A");
            Enter(screen);
            screen.Blur();
            Assert.IsTrue(screen.gameObject.activeSelf, "precondition: the screen is shown");
            Assert.IsNotNull(Selected, "precondition: the screen holds the selection");

            screen.Hide();

            Assert.IsFalse(screen.gameObject.activeSelf);
            Assert.IsFalse(screen.IsShown);
            Assert.IsNull(Selected, "Submit in the game must not reach a switched-off button");
        }

        [Test]
        public void UIScreen_ShowWithFade_InteractableOnlyAfterFade()
        {
            UIScreen screen = CreateScreen("A", fade: 0.2f);
            var fade = screen.GetComponent<FadeTransition>();

            Enter(screen);
            Assert.IsFalse(screen.IsInteractable, "buttons wait for the fade");

            fade.Tick(0.1f);
            Assert.IsFalse(screen.IsInteractable);

            fade.Tick(0.1f);
            Assert.IsTrue(screen.IsInteractable);
            Assert.IsTrue(screen.GetComponent<CanvasGroup>().interactable);
        }

        [Test]
        public void UIScreen_HideWithFade_SwitchesOffAfterFade()
        {
            UIScreen screen = CreateScreen("A", fade: 0.2f);
            var fade = screen.GetComponent<FadeTransition>();
            Enter(screen);
            fade.Tick(1f);

            screen.Blur();
            screen.Hide();
            Assert.IsTrue(screen.gameObject.activeSelf, "still fading out");

            fade.Tick(1f);
            Assert.IsFalse(screen.gameObject.activeSelf);
        }

        [Test]
        public void UIScreen_Show_DrawsOverSiblings()
        {
            UIScreen first = CreateScreen("A");
            UIScreen second = CreateScreen("B");
            second.Show();

            first.Show();

            Assert.AreEqual(_canvas.transform.childCount - 1, first.transform.GetSiblingIndex());
        }

        [Test]
        public void UIScreen_OpenAndClose_RaiseEvents()
        {
            UIScreen screen = CreateScreen("A");
            int opened = 0;
            int closed = 0;
            screen.OnOpened += () => opened++;
            screen.OnClosed += () => closed++;

            screen.Open();
            screen.Close();

            Assert.AreEqual(1, opened);
            Assert.AreEqual(1, closed);
        }

        [UnityTest]
        public IEnumerator UIScreen_SelectionLost_SelectsAgain()
        {
            UIScreen screen = CreateScreen("A");
            Enter(screen);

            _events.SetSelectedGameObject(null);
            yield return null;

            Assert.AreSame(Child(screen, "First"), Selected);
        }

        [Test]
        public void ScreenStack_DialogOverScreen_ReturnsSelectionToOpener()
        {
            var stack = new ScreenStack();
            UIScreen menu = CreateScreen("Menu");
            UIScreen dialog = CreateScreen("Dialog", overlay: true);
            stack.Push(menu);
            _events.SetSelectedGameObject(Child(menu, "Second"));

            stack.Push(dialog);

            Assert.IsTrue(menu.IsShown, "an overlay keeps the menu visible");
            Assert.IsFalse(menu.IsInteractable);
            Assert.AreSame(Child(dialog, "First"), Selected);

            stack.Pop();

            Assert.AreSame(Child(menu, "Second"), Selected);
            Assert.IsTrue(menu.IsInteractable);
        }
    }
}
