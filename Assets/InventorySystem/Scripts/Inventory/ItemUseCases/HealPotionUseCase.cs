using UnityEngine;
using Inventories;

[CreateAssetMenu(fileName = "HealPotionUseCase", menuName = "InventoryItem/UseCases/HealPotionUseCase")]
public class HealPotionUseCase : ItemUseCase
{
    [SerializeField] private int healAmount = 20;

    public override void Invoke(Inventory inventory, Item item)
    {
        IItemConsumer consumer = inventory.Owner;
        if (consumer == null) return;

        ItemConsumer character = consumer.GetComponent<ItemConsumer>();

        character.Health += healAmount;
        Debug.Log($"Healed! New health: {character.Health}");

        bool stackIsEmpty = item.UseOne();

        if (stackIsEmpty)
        {
            inventory.RemoveItem(item.ID);
            Debug.Log("Item stack depleted and removed");
        }
        else
        {
            Debug.Log($"Items remaining: {item.StackQuantity}");
        }
    }
}