using UnityEngine;
using Inventories;

[CreateAssetMenu(fileName = "HealPotionUseCase", menuName = "InventoryItem/UseCases/HealPotionUseCase")]
public class HealPotionUseCase : ItemUseCase
{
    [SerializeField] private int healAmount = 20;

    public override void Invoke(InventoryPresenter presenter, Item item)
    {
        IItemConsumer consumer = presenter.Owner;
        if (consumer == null) return;

        ItemConsumer character = consumer.GetComponent<ItemConsumer>();

        character.Health += healAmount;
        Debug.Log($"Healed! New health: {character.Health}");

        bool stackIsEmpty = item.UseOne();

        if (stackIsEmpty)
        {
            presenter.RemoveItem(item.ID);
            Debug.Log("Item stack depleted and removed");
        }
        else
        {
            Debug.Log($"Items remaining: {item.StackQuantity}");
        }
    }
}