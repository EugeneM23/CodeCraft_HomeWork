using Gameplay;
using UnityEngine;

public class JumpComponent
{
    private readonly PlayerController _controller;
    private float normalInfluence = 0.5f;

    public JumpComponent(PlayerController controller)
    {
        _controller = controller;
    }

    public void Jump(Vector2 diraction, float jumpForce)
    {
        Vector3 normal = _controller.SurfaceNormal.normalized;

        Vector3 jumpDir = Vector3.Lerp(diraction, normal, normalInfluence);

        jumpDir.y = 1f;

        jumpDir = jumpDir.normalized;

        _controller.AddImpulse(jumpForce, jumpDir);
    }
}