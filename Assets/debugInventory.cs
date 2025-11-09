using System;
using Inventories;
using UnityEngine;

public class DebugInventory : MonoBehaviour
{
    [SerializeField] private Ceil _empty;
    [SerializeField] private Ceil _red;
    [SerializeField] private Ceil _green;

    private Inventory _inventory;

    private void Start()
    {
        _inventory = new Inventory(5, 5);

        Item itemX = new Item("X", 3, 3);
        Item itemY = new Item("Y", 1, 1);
        Item itemZ = new Item("Z", 1, 1);
        Item itemQ = new Item("Q", 2, 2);

        //_inventory.AddItem(itemX, 1, 1);
        _inventory.AddItem(itemX);
        _inventory.AddItem(itemY);
        _inventory.AddItem(itemZ, 4, 0);
        _inventory.AddItem(itemQ, 3, 3);

        for (int widht = 0; widht < _inventory.Width; widht++)
        {
            for (int height = 0; height < _inventory.Height; height++)
            {
                Ceil instant = Instant(_empty, widht, height);
                instant._ceilText.text = $"{widht}/{height}";
            }
        }

        for (int widht = 0; widht < _inventory.Width; widht++)
        {
            for (int height = 0; height < _inventory.Height; height++)
            {
                if (_inventory.IsFree(widht, height))
                {
                    Ceil ceil = Instant(_green, widht, height);
                    ceil._ceilText.text = $"{widht}/{height}";
                }
                else
                {
                    Item item = _inventory._ceils[widht, height];
                    Ceil ceil = Instant(_red, widht, height);
                    ceil._ceilText.text = $"{item.Name}";
                }
            }
        }
    }

    public Ceil Instant(Ceil prefab, int width, int height)
    {
        Vector3 p = new Vector3(width, height, 0);
        Ceil ceil = Instantiate(prefab, p, Quaternion.identity).GetComponent<Ceil>();
        return ceil;
    }
}