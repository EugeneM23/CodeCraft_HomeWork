using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class RaycastDetector
{
    private readonly GraphicRaycaster _raycaster;
    private readonly EventSystem _eventSystem;
    private InventoryItem _ignoredItem;

    public RaycastDetector(GraphicRaycaster raycaster, EventSystem eventSystem)
    {
        _raycaster = raycaster;
        _eventSystem = eventSystem;
    }

    public void SetIgnoredItem(InventoryItem item)
    {
        _ignoredItem = item;
    }

    public bool TryGetComponent<T>(out T component) where T : Component
    {
        component = null;

        if (TryGetUIComponent(out component))
            return true;

        if (IsOverAnyUI())
            return false;

        return TryGetSceneComponent(out component);
    }

    private bool TryGetUIComponent<T>(out T component) where T : Component
    {
        component = null;
        List<RaycastResult> results = GetUIRaycastResults();

        foreach (RaycastResult result in results)
        {
            if (ShouldIgnore(result.gameObject))
                continue;

            if (result.gameObject.TryGetComponent(out component))
                return true;
        }

        return false;
    }

    private bool TryGetSceneComponent<T>(out T component) where T : Component
    {
        component = null;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit))
            return hit.collider.gameObject.TryGetComponent(out component);

        return false;
    }

    private bool IsOverAnyUI()
    {
        List<RaycastResult> results = GetUIRaycastResults();

        foreach (RaycastResult result in results)
        {
            if (!ShouldIgnore(result.gameObject))
                return true;
        }

        return false;
    }

    private List<RaycastResult> GetUIRaycastResults()
    {
        PointerEventData pointerData = new PointerEventData(_eventSystem)
        {
            position = Input.mousePosition
        };

        List<RaycastResult> results = new List<RaycastResult>();
        _raycaster.Raycast(pointerData, results);
        return results;
    }

    private bool ShouldIgnore(GameObject obj)
    {
        if (IsPartOfIgnoredItem(obj))
            return true;

        return obj.GetComponent<InventoryItem>() != null;
    }

    private bool IsPartOfIgnoredItem(GameObject obj)
    {
        if (_ignoredItem == null)
            return false;

        return obj == _ignoredItem.gameObject || obj.transform.IsChildOf(_ignoredItem.transform);
    }
}