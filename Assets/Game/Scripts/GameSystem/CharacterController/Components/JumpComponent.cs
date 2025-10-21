using Gameplay;
using UnityEngine;

public class JumpComponent
{
    private readonly GravityComponent gravityComponent;
    private float jumpForce;
    private bool jumpRequested = false;
    private float normalInfluence = 0.5f;

    public JumpComponent(float jumpForce, GravityComponent gravityComponent)
    {
        this.jumpForce = jumpForce;
        this.gravityComponent = gravityComponent;
    }

    public void RequestJump()
    {
        // Берём нормаль поверхности, по которой стоим
        Vector3 normal = gravityComponent.SurfaceNormal.normalized;

        // Смешиваем с направлением строго вверх
        // НО ВАЖНО: не нормализуем здесь, чтобы потом управлять осью Y
        Vector3 jumpDir = Vector3.Lerp(Vector3.up, normal, normalInfluence);

        // Делаем вертикальную силу прыжка фиксированной (высота одинаковая даже на склонах)
        jumpDir.y = 1f;

        // Теперь нормализуем — чтобы было ровно 1 по длине
        jumpDir = jumpDir.normalized;

        // Применяем импульс
        gravityComponent.AddImpulse(jumpForce, jumpDir);
    }

    public bool ConsumeJumpRequest()
    {
        if (!jumpRequested) return false;
        jumpRequested = false;
        return true;
    }

    public float GetJumpForce() => jumpForce;
}