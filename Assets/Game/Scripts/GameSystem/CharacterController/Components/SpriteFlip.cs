using UnityEngine;

public class SpriteFlip
{
    private readonly Transform _transform;

    public SpriteFlip(PlayerController controller)
    {
        _transform = controller.transform;
    }

    public void Flip(Vector2 direction)
    {
        if (direction == Vector2.zero) return;

        Vector3 scale = _transform.localScale;
        scale.x = Mathf.Abs(scale.x) * Mathf.Sign(direction.x);
        _transform.localScale = scale;
    }
}