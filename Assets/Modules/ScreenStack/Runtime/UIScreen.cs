using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ScreenNavigation
{
    /// <summary>
    /// A uGUI screen for ScreenStack, stored switched off: only the stack shows it. Show turns the GameObject on,
    /// draws it over its siblings and plays the optional transition; Hide plays it back and turns the object off.
    /// Input goes through the CanvasGroup: buttons work only while the screen is focused and fully shown. On focus
    /// the screen selects the button remembered at its last Blur, or the first one; it forgets the button when it
    /// leaves the stack. Game screens derive from it and wire their buttons in OnEnable/OnDisable.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class UIScreen : MonoBehaviour, IScreen
    {
        public event Action OnOpened;
        public event Action OnClosed;

        [Header("Stack")]
        [Tooltip("Screens below stay visible (a dialog over a menu)")]
        [SerializeField] private bool _isOverlay;
        [Tooltip("Off: Back keeps the screen (the main menu root)")]
        [SerializeField] private bool _canGoBack = true;

        [Header("Focus")]
        [Tooltip("Selected when the screen opens; empty for a mouse-only screen")]
        [SerializeField] private Selectable _firstSelected;

        [Header("View")]
        [Tooltip("Optional: without it the screen appears and disappears at once")]
        [SerializeField] private ScreenTransition _transition;

        private CanvasGroup _canvasGroup;
        private GameObject _remembered;
        private bool _shown;
        private bool _showFinished;
        private bool _focused;

        public bool IsOverlay => _isOverlay;
        public bool CanGoBack => _canGoBack;

        /// <summary>Selected when the screen opens; a screen may change it, for one whose first button is hidden.</summary>
        public Selectable FirstSelected
        {
            get => _firstSelected;
            set => _firstSelected = value;
        }

        /// <summary>Between Show and Hide, the show transition included.</summary>
        public bool IsShown => _shown;

        /// <summary>Focused and fully shown: its buttons react.</summary>
        public bool IsInteractable => _shown && _showFinished && _focused;

        // Works before Awake: the stack opens and shows screens that have never been active
        private CanvasGroup Group => _canvasGroup != null ? _canvasGroup : _canvasGroup = GetComponent<CanvasGroup>();

        /// <summary>Setup for screens built in code (tests); prefabs set the same fields in the Inspector.</summary>
        public void Configure(Selectable firstSelected, bool isOverlay = false, bool canGoBack = true,
            ScreenTransition transition = null)
        {
            _firstSelected = firstSelected;
            _isOverlay = isOverlay;
            _canGoBack = canGoBack;
            _transition = transition;
        }

        /// <summary>Gives the selection back when it is lost (a mouse click on empty space, a button switched off).</summary>
        protected virtual void Update()
        {
            if (!IsInteractable)
                return;

            EventSystem events = EventSystem.current;
            if (events != null && events.currentSelectedGameObject == null)
                Select();
        }

        public void Open()
        {
            _remembered = null;
            OnOpened?.Invoke();
        }

        public void Close()
        {
            _remembered = null;
            OnClosed?.Invoke();
        }

        public void Show()
        {
            _shown = true;
            _showFinished = false;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Group.blocksRaycasts = true;
            Refresh();

            if (_transition != null)
                _transition.Play(true, FinishShow);
            else
                FinishShow();
        }

        public void Hide()
        {
            _shown = false;
            _showFinished = false;
            Group.blocksRaycasts = false;
            Refresh();
            DropSelection();

            if (_transition != null && gameObject.activeInHierarchy)
                _transition.Play(false, FinishHide);
            else
                FinishHide();
        }

        public void Focus()
        {
            _focused = true;
            Refresh();
            Select();
        }

        public void Blur()
        {
            GameObject selected = CurrentSelection();
            if (IsMine(selected))
                _remembered = selected;

            _focused = false;
            Refresh();
        }

        private void FinishShow()
        {
            _showFinished = true;
            Refresh();
        }

        private void FinishHide() => gameObject.SetActive(false);

        private void Refresh() => Group.interactable = IsInteractable;

        private void Select()
        {
            EventSystem events = EventSystem.current;
            if (events == null)
                return;

            GameObject target = _remembered != null && _remembered.activeInHierarchy ? _remembered
                : _firstSelected != null ? _firstSelected.gameObject : null;
            events.SetSelectedGameObject(target);
        }

        // A hidden screen keeps no selection: Submit in the game would land on a switched-off button
        private void DropSelection()
        {
            if (IsMine(CurrentSelection()))
                EventSystem.current.SetSelectedGameObject(null);
        }

        private static GameObject CurrentSelection()
        {
            EventSystem events = EventSystem.current;
            return events != null ? events.currentSelectedGameObject : null;
        }

        private bool IsMine(GameObject selected) => selected != null && selected.transform.IsChildOf(transform);
    }
}
