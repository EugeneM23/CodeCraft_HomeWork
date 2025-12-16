using Inventories;
using UnityEngine;

public class TestCharacter : MonoBehaviour, IItemConsumer
{
    public int Health = 50;
    public Inventory Inventory { get; set; }

    public new T GetComponent<T>()
    {
        if (typeof(T) == typeof(IItemConsumer))
        {
            return (T)(IItemConsumer)this;
        }
        
        return base.GetComponent<T>();
    }
}