using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Inventories
{
    public class RaycastDetector
    {
        private readonly GraphicRaycaster _raycaster;
        private readonly EventSystem _eventSystem;

        public RaycastDetector(GraphicRaycaster raycaster, EventSystem eventSystem)
        {
            _raycaster = raycaster;
            _eventSystem = eventSystem;
        }

        public bool TryGetUIComponent<T>(out T component) where T : Component
        {
            component = null;

            var pointerData = new PointerEventData(_eventSystem)
            {
                position = Input.mousePosition
            };

            var results = new List<RaycastResult>();
            _raycaster.Raycast(pointerData, results);

            foreach (var result in results)
            {
                if (result.gameObject.TryGetComponent(out component))
                    return true;
            }

            return false;
        }
    }
}