using UnityEngine;

public class BackGroundAnimator : MonoBehaviour
{
    [SerializeField] private float speedY = 0.5f;
    
    private Material material;
    private Vector2 offset;

    void Start() => material = GetComponent<SpriteRenderer>().material;

    void Update()
    {
        offset.y += speedY * Time.deltaTime;
        material.mainTextureOffset = offset;
    }
}