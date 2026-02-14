using UnityEngine;
using Zenject;

namespace Inventories
{
    public class DragItemController : ITickable
    {
        private readonly RaycastDetector _raycastDetector;

        public DragItemController(RaycastDetector raycastDetector)
        {
            _raycastDetector = raycastDetector;
        }

        public void Tick()
        {
            if (Input.GetMouseButtonDown(0))
            {
                if (_raycastDetector.TryGetUIComponent(out InventoryItem item))
                {
                    Debug.Log($"Clicked on item: {item.name}");
                }
            }
        }
    }
}