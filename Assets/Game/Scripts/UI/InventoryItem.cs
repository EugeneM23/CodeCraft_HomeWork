using UnityEngine;

public class InventoryItem : MonoBehaviour
{
    public RectTransform RectTransform { get; private set; }

    private void Awake()
    {
        RectTransform = GetComponent<RectTransform>();
    }
}