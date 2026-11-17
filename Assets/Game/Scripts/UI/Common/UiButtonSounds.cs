using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// The place for interface sounds on a control: reports hover and click to IUiSounds. The sound
    /// team plugs the clips into IUiSounds; nothing here changes.
    /// </summary>
    public sealed class UiButtonSounds : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        [Tooltip("Sound of a click on this control (Back for back buttons; buy buttons keep Click, the purchase result plays Purchase)")]
        [SerializeField] private UiSound _click = UiSound.Click;

        private Selectable _selectable;

        public UiSound Click
        {
            get => _click;
            set => _click = value;
        }

        private void Awake() => _selectable = GetComponent<Selectable>();

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (IsActive())
                AppServices.UiSounds.Play(UiSound.Hover);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                AppServices.UiSounds.Play(IsActive() ? _click : UiSound.Error);
        }

        private bool IsActive() => _selectable == null || _selectable.IsInteractable();
    }
}
