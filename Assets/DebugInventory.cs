using Inventories;
using UnityEngine;

public class DebugInventory : MonoBehaviour
{
    [SerializeField] private Ceil empty;
    [SerializeField] private Ceil free;
    [SerializeField] private Ceil occupied;

    private Inventory inv;

    private void Start()
    {
        inv = new Inventory(5, 5);

         inv.AddItem(new Item("X", 1, 1), 1, 1);
         inv.AddItem(new Item("A", 3, 3));
        // inv.AddItem(new Item("Y", 5, 5));
        //inv.AddItem(new Item("Z", 1, 1), 4, 0);
        //inv.AddItem(new Item("Q", 2, 2), 3, 3);

        DrawGrid();
        DrawState();
    }

    void DrawGrid()
    {
        ForEachCell((x, y) =>
        {
            Ceil c = Spawn(empty, x, y);
            c._ceilText.text = $"{x}/{y}";
        });
    }

    void DrawState()
    {
        ForEachCell((x, y) =>
        {
            if (inv.IsFree(x, y))
            {
                var c = Spawn(free, x, y);
                c._ceilText.text = $"{x}/{y}";
            }
            else
            {
                inv.TryGetItem(x, y, out var item);
                var c = Spawn(occupied, x, y);
                c._ceilText.text = item.Name;
            }
        });
    }

    void ForEachCell(System.Action<int, int> act)
    {
        for (int x = 0; x < inv.Width; x++)
        for (int y = 0; y < inv.Height; y++)
            act(x, y);
    }

    Ceil Spawn(Ceil prefab, int x, int y)
    {
        return Instantiate(prefab, new Vector3(x, y, 0), Quaternion.identity)
            .GetComponent<Ceil>();
    }
}