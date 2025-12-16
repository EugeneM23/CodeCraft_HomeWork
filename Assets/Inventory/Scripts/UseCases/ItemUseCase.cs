using UnityEngine;

public abstract class ItemUseCase : ScriptableObject
{
    public abstract void Invoke(IItemConsumer itemConsumer);
}