using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

[RequireComponent(typeof(RectTransform))]
public class DragItemUIRaycastFull : MonoBehaviour
{
    [SerializeField] private Image _itemIcon;
    [SerializeField] private GraphicRaycaster _raycaster;
    [SerializeField] private int _gridResolution = 3; // сетка точек по X и Y

    private RectTransform _rectTransform;
    private EventSystem _eventSystem;

    // пул для результатов NonAlloc
    private RaycastResult[] _raycastResults;

    void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _eventSystem = EventSystem.current;

        if (_eventSystem == null)
            Debug.LogError("EventSystem отсутствует в сцене!");

        if (_raycaster == null)
        {
            _raycaster = GetComponentInParent<GraphicRaycaster>();
            if (_raycaster == null)
                Debug.LogError("GraphicRaycaster не найден на Canvas!");
        }

        // создаём пул для результатов
        _raycastResults = new RaycastResult[50]; // увеличь, если много элементов
    }

    void Update()
    {
        CheckUIUnderItem();
    }

    private void CheckUIUnderItem()
    {
        if (_raycaster == null || _eventSystem == null) return;

        Vector3[] corners = new Vector3[4];
        _rectTransform.GetWorldCorners(corners);

        HashSet<GameObject> hitsSet = new HashSet<GameObject>();

        // создаём сетку точек
        for (int x = 0; x < _gridResolution; x++)
        {
            float tX = x / (float)(_gridResolution - 1);
            Vector3 left = Vector3.Lerp(corners[0], corners[1], tX);
            Vector3 right = Vector3.Lerp(corners[3], corners[2], tX);

            for (int y = 0; y < _gridResolution; y++)
            {
                float tY = y / (float)(_gridResolution - 1);
                Vector3 worldPoint = Vector3.Lerp(left, right, tY);
                Vector2 screenPoint =
                    RectTransformUtility.WorldToScreenPoint(_raycaster.GetComponent<Camera>() ?? Camera.main,
                        worldPoint);

                PointerEventData pd = new PointerEventData(_eventSystem)
                {
                    position = screenPoint
                };

                List<RaycastResult> results = new List<RaycastResult>();
                _raycaster.Raycast(pd, results);

                foreach (var r in results)
                {
                    if (r.gameObject != gameObject)
                        hitsSet.Add(r.gameObject);
                }
            }
        }

        // Выводим все найденные UI элементы
        foreach (GameObject obj in hitsSet)
        {
            if (obj.TryGetComponent(out CellView cell))
                cell.Highlight(true);
        }
    }

    // Отображаем прямоугольник в сцене
    private void OnDrawGizmos()
    {
        if (_rectTransform == null) return;

        Vector3[] corners = new Vector3[4];
        _rectTransform.GetWorldCorners(corners);

        Gizmos.color = Color.green;
        Gizmos.DrawLine(corners[0], corners[1]);
        Gizmos.DrawLine(corners[1], corners[2]);
        Gizmos.DrawLine(corners[2], corners[3]);
        Gizmos.DrawLine(corners[3], corners[0]);
    }
}