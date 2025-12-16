using UnityEngine;
using Inventories;

public abstract class ItemUseCase : ScriptableObject
{
    // UseCase теперь получает Inventory, из которого сам достанет Consumer
    public abstract void Invoke(Inventory inventory, ItemInstance itemInstance);
}