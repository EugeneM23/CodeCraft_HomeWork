using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ScreenNavigation
{
    /// <summary>
    /// Mouse hover selects the button, so mouse, keyboard and gamepad share one selection and one look.
    /// </summary>
    public sealed class SelectOnPointerEnter : MonoBehaviour, IPointerEnterHandler
    {
        private Selectable _selectable;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_selectable == null)
                _selectable = GetComponent<Selectable>();

            EventSystem events = EventSystem.current;
            if (_selectable != null && _selectable.IsInteractable() && events != null)
                events.SetSelectedGameObject(gameObject);
        }
    }
}
