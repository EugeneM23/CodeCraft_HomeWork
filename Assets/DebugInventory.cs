using System.Collections.Generic;
using Inventories;
using Sirenix.OdinInspector;
using UnityEngine;

public class DebugInventory : MonoBehaviour
{
    [SerializeField] private Ceil empty;
    [SerializeField] private Ceil free;
    [SerializeField] private Ceil occupied;

    private Inventory inv;
    private List<Ceil> _ceilsView = new List<Ceil>();
    private Item item;

    private void Start()
    {
        inv = new Inventory(5, 5);

        // inv.AddItem(new Item("X", 1, 1), 1, 1);
        // inv.AddItem(new Item("Z", 1, 1));
        // inv.AddItem(new Item("F", 1, 1));
        // inv.AddItem(new Item("N", 1, 1));
        // item = new Item("M", 1, 1);
        // inv.AddItem(item);

        inv.AddItem(new Item("A", 3, 3));
        //inv.AddItem(new Item("Q", 2, 2));

        Redraw();
    }

    // -------------------- ПЕРЕРИСОВКА --------------------

    void Redraw()
    {
        ClearView();

        ForEachCell((x, y) =>
        {
            // 1. Рисуем EMPTY (сетку)
            var gridCeil = Spawn(empty, x, y);
            gridCeil._ceilText.text = $"{x}/{y}";

            // 2. Рисуем содержимое (free/occupied)
            if (inv.IsFree(x, y))
            {
                var c = Spawn(free, x, y);
                c._ceilText.text = $"{x}/{y}";
            }
            else
            {
                inv.TryGetItem(x, y, out var itemHere);
                var c = Spawn(occupied, x, y);
                c._ceilText.text = itemHere.Name;
            }
        });
    }

    void ClearView()
    {
        foreach (var ceil in _ceilsView)
        {
            if (ceil != null)
                Destroy(ceil.gameObject);
        }
        _ceilsView.Clear();
    }

    // -------------------- BUTTONS --------------------

    [Button]
    public void MoveItem(Vector2Int newPosition, Vector2Int itemPosition)
    {
        Item item1 = inv.GetItem(itemPosition);
        inv.MoveItem(item1, newPosition);
        Redraw();
    }

    [Button]
    public void ReorganizeSpace()
    {
        inv.ReorganizeSpace();
        Redraw();
    }

    // -------------------- UTILS --------------------

    void ForEachCell(System.Action<int, int> act)
    {
        for (int y = inv.Height - 1; y >= 0; y--)
            for (int x = 0; x < inv.Width; x++)
                act(x, y);
    }

    Ceil Spawn(Ceil prefab, int x, int y)
    {
        Ceil ceil = Instantiate(
            prefab,
            new Vector3(x, inv.Height - 1 - y, 0),
            Quaternion.identity,
            this.transform
        );

        _ceilsView.Add(ceil);
        return ceil;
    }
}
