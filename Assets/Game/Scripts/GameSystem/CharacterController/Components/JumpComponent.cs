using Gameplay;
using UnityEngine;

public class JumpComponent
{
    private readonly PlayerController _controller;
    private float normalInfluence = 0.3f;

    public JumpComponent(PlayerController controller)
    {
        _controller = controller;   
    }

    public void Jump(Vector2 diraction, float jumpForce)
    {
        // Берём нормаль поверхности, по которой стоим
        Vector3 normal = _controller.SurfaceNormal.normalized;

        // Смешиваем с направлением строго вверх
        // НО ВАЖНО: не нормализуем здесь, чтобы потом управлять осью Y
        Vector3 jumpDir = Vector3.Lerp(diraction, normal, normalInfluence);

        // Делаем вертикальную силу прыжка фиксированной (высота одинаковая даже на склонах)
        jumpDir.y = 1f;

        // Теперь нормализуем — чтобы было ровно 1 по длине
        jumpDir = jumpDir.normalized;

        // Применяем импульс
        _controller.AddImpulse(jumpForce, jumpDir);
    }
}