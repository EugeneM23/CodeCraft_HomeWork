using UnityEngine;

[CreateAssetMenu(fileName = "HealPotionUseCase", menuName = "InventoryItem/UseCases/HealPotionUseCase")]
public class HealPotionUseCase : ItemUseCase
{
    public override void Invoke(IItemConsumer itemConsumer)
    {
        Debug.Log(itemConsumer == null);
        TestCharacter testCharacter = itemConsumer.GetComponent<TestCharacter>();
        Debug.Log(testCharacter.Health);
    }
}