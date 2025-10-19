using UnityEngine;

public class BulletFall : MonoBehaviour
{
    public float gravity = 9.8f;
    public float fallSpeed = 0f; // внутренняя скорость падения
    public LayerMask groundLayer;
    public float radius = 0.1f;

    private bool hasHitGround = false;

    // Скорость, которую MoveController использует по оси Y:
    public float VelocityY { get; private set; }

    // Вызываем до движения (чтобы падение учитывало весь сдвиг)
    public void SimulateFall(float moveSpeed)
    {
        // Если лежит на земле — проверяем, не унесли ли её в воздух (перемещением вручную или движением игрока)
        if (hasHitGround)
        {
            if (!IsGrounded())
            {
                hasHitGround = false;
                fallSpeed = 0f;
                VelocityY = 0f;
            }
            else
            {
                VelocityY = 0f;
                return;
            }
        }

        // Увеличиваем скорость падения
        fallSpeed += gravity * Time.deltaTime;

        // Какая дистанция будет пройдена с учётом MoveController
        float distance = fallSpeed * Time.deltaTime * moveSpeed;

        // CircleCast на эту дистанцию
        RaycastHit2D hit =
            Physics2D.CircleCast(transform.position, radius, Vector2.down, Mathf.Abs(distance), groundLayer);

        if (hit.collider != null)
        {
            float groundY = hit.point.y + radius;
            transform.position = new Vector3(transform.position.x, groundY, transform.position.z);

            hasHitGround = true;
            VelocityY = 0f;
            fallSpeed = 0f;
        }
        else
        {
            VelocityY = -fallSpeed;
        }
    }

    bool IsGrounded()
    {
        float checkDistance = 0.02f;
        return Physics2D.CircleCast(transform.position, radius, Vector2.down, checkDistance, groundLayer);
    }
}