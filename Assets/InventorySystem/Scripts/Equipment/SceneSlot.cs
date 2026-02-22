using UnityEngine;
using UnityEngine.EventSystems;

public class SceneSlot : MonoBehaviour
{
    private Camera _camera;
    private bool _isDragging;
    private Vector3 _offset;

    private void Start()
    {
        _camera = Camera.main;
    }

    private void OnMouseDown()
    {
        Debug.Log("Begin Drag");
        _isDragging = true;

        // Вычисляем смещение между позицией мыши и объектом
        Vector3 mousePos = GetMouseWorldPosition();
        _offset = transform.position - mousePos;
    }

    private void OnMouseDrag()
    {
        if (_isDragging)
        {
        }
    }

    private void OnMouseUp()
    {
        Debug.Log("End Drag");
        _isDragging = false;
    }

    private Vector3 GetMouseWorldPosition()
    {
        Vector3 mousePoint = Input.mousePosition;
        mousePoint.z = _camera.WorldToScreenPoint(transform.position).z;
        return _camera.ScreenToWorldPoint(mousePoint);
    }
}