using Inventories;
using UnityEngine;

public class SceneItem : MonoBehaviour
{
    [SerializeField] private ItemData _itemData;

    public ItemData ItemData => _itemData;
}