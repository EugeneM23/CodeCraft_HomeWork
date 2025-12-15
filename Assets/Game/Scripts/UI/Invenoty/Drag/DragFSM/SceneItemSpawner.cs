using Inventories;
using UnityEngine;

public class SceneItemSpawner : MonoBehaviour
{
    public void SpawnItem(ItemData itemItemData, Vector3 raycastHitPoint)
    {
        Instantiate(itemItemData.SpawnPrefab, raycastHitPoint, Quaternion.identity);
    }
}