using System;
using System.Collections.Generic;
using Inventories;
using UnityEngine;
using UnityEngine.UI;

public class InventoryView : MonoBehaviour
{
    public event Action<Vector2Int, Item> OnMoveItem;

    [SerializeField] private GridToMatrix _gridToMatrix;
    private CellView[,] _cellViews;
    private Dictionary<Item, GridItem> _items = new();

    public Item _selectedItem;
    public Vector2Int _selectedItemRelativePosition;

    private void Start()
    {
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_gridToMatrix.transform as RectTransform);
        _cellViews = _gridToMatrix.BuildMatrix();
        InitializeCells();
    }

    private void InitializeCells()
    {
        for (int i = 0; i < _cellViews.GetLength(0); i++)
        for (int j = 0; j < _cellViews.GetLength(1); j++)
        {
            _cellViews[i, j].position = new Vector2Int(i, j);
            _cellViews[i, j].inventoryView = this;
            _cellViews[i, j].OnCellClickedDown += SelectItem;
            _cellViews[i, j].OnCellClickedUp += MoveItem;
        }
    }

    private void SelectItem(Item item, Vector2Int itemPosition)
    {
        if (item != null)
        {
            _selectedItem = item;
            _selectedItemRelativePosition = itemPosition;
        }
    }

    private void MoveItem(Vector2Int clickedPosition, Vector2Int itemRelativePosition)
    {
        if (_selectedItem == null) return;

        Vector2Int targetPosition = new Vector2Int(
            clickedPosition.x - _selectedItemRelativePosition.x,
            clickedPosition.y - _selectedItemRelativePosition.y
        );

        OnMoveItem?.Invoke(targetPosition, _selectedItem);
        _selectedItem = null;
    }

    public void Clear()
    {
        for (int i = 0; i < _cellViews.GetLength(0); i++)
        for (int j = 0; j < _cellViews.GetLength(1); j++)
        {
            _cellViews[i, j].Text.text = "";
            _cellViews[i, j].Item = null;
            _cellViews[i, j].itemPosition = Vector2Int.zero;
        }

        foreach (Item item in _items.Keys)
            Destroy(_items[item].gameObject);

        _items.Clear();
    }

    public void AddItem(Item item, Vector2Int[] positions)
    {
        Vector2Int minPosition = positions[0];
        foreach (Vector2Int position in positions)
        {
            if (position.x < minPosition.x) minPosition.x = position.x;
            if (position.y < minPosition.y) minPosition.y = position.y;
        }

        foreach (Vector2Int position in positions)
        {
            _cellViews[position.x, position.y].Text.text = item.Name;
            _cellViews[position.x, position.y].Item = item;
            _cellViews[position.x, position.y].itemPosition = new Vector2Int(
                position.x - minPosition.x,
                position.y - minPosition.y
            );
        }
    }
}

public class GridItem : MonoBehaviour { }