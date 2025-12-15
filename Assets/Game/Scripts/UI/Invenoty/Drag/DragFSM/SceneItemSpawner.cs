using Inventories;
using UnityEngine;

public class SceneItemSpawner : MonoBehaviour
{
    [SerializeField] private GameObject _spawnPrefab;

    public void SpawnItem(ItemData itemItemData, Vector3 raycastHitPoint)
    {
        Instantiate(_spawnPrefab, raycastHitPoint, Quaternion.identity);
    }
}