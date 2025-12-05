using Inventories;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image _background;
    [SerializeField] private Image _itemImage;
    [SerializeField] private RectTransform _rectTransform;

    public RectTransform RectTransform => _rectTransform;
    public Item Item => _item;
    public Sprite Icon => _itemImage.sprite;

    private Item _item;
    private bool _isDragEnable;
    private Vector3 _dragOffset;
    private DragState _savedState;

    private void Update()
    {
        if (_isDragEnable)
            transform.position = Input.mousePosition + _dragOffset;
    }

    public void SetItem(Item item) => _item = item;

    public void SetIcon(Sprite sprite) => _itemImage.sprite = sprite;

    public void OnPointerEnter(PointerEventData eventData)
    {
        Color color = _background.color;
        color.a = 1f;
        _background.color = color;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Color color = _background.color;
        color.a = 0f;
        _background.color = color;
    }

    public void EnableDrag(bool isEnable, Vector2 offset = default)
    {
        _background.enabled = !isEnable;
        transform.SetAsLastSibling();
        _isDragEnable = isEnable;
        _dragOffset = transform.position - Input.mousePosition;
    }

    public void SaveState(Transform parent, Vector3 position, Vector2Int gridPosition, Vector2Int matrixPosition)
    {
        _savedState = new DragState
        {
            Parent = parent,
            Position = position,
            GridPosition = gridPosition,
            MatrixPosition = matrixPosition
        };
    }

    public void RestoreState()
    {
        if (_savedState == null)
        {
            Debug.LogWarning("Попытка восстановить состояние, но оно не было сохранено!");
            return;
        }

        transform.SetParent(_savedState.Parent);
        transform.position = _savedState.Position;
    }

    public Vector2Int GetSavedGridPosition()
    {
        return _savedState?.GridPosition ?? Vector2Int.zero;
    }

    public Vector2Int GetSavedMatrixPosition()
    {
        return _savedState?.MatrixPosition ?? Vector2Int.zero;
    }

    public Transform GetSavedParent()
    {
        return _savedState?.Parent;
    }

    public void ClearSavedState()
    {
        _savedState = null;
    }

    private class DragState
    {
        public Transform Parent;
        public Vector3 Position;
        public Vector2Int GridPosition;
        public Vector2Int MatrixPosition;
    }
}