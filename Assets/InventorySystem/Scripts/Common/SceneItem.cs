using Inventories;
using UnityEngine;

public class SceneItem : MonoBehaviour
{
    [SerializeField] public ItemData ItemData;
    [SerializeField] public ItemAudioData  ItemAudioData;
    [SerializeField] public int Quantity = 1; 
}