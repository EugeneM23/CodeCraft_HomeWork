using UnityEngine;

public class SpriteFlip
{
    private readonly Transform transform;

    public SpriteFlip(Transform transform)
    {
        this.transform = transform;
    }

    public void Flip(Vector2 direction)
    {
        if (direction == Vector2.zero) return;

        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * Mathf.Sign(direction.x);
        transform.localScale = scale;
    }
}