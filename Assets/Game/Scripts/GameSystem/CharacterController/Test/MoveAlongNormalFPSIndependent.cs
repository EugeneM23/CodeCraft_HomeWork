using System;
using UnityEngine;

public class MoveAlongSurface2D_FixedVerticalDistance : MonoBehaviour
{
    [Header("Movement Settings")]
    public float speed = 5f;                  // горизонтальная скорость
    public float distanceToGround = 1f;       // вертикальная высота над поверхностью
    public LayerMask groundLayerMask;         // слой поверхности
    public float stepSize = 0.05f;            // шаг движения для точного Raycast
    public float heightSmoothSpeed = 10f;     // скорость сглаживания высоты

    private Vector2 lastNormal = Vector2.up;  // последняя нормаль поверхности

    private void Start()
    {
        Application.targetFrameRate = 60;
    }

    void Update()
    {
        float inputX = Input.GetAxis("Horizontal");
        if (Mathf.Abs(inputX) < 0.01f) return;

        // желаемое смещение на кадр
        Vector2 desiredMove = Vector2.right * inputX * speed * Time.deltaTime;

        // делим путь на маленькие шаги
        int steps = Mathf.CeilToInt(desiredMove.magnitude / stepSize);
        Vector2 stepMove = desiredMove / steps;

        for (int i = 0; i < steps; i++)
        {
            // Raycast вниз от текущей позиции
            RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, 5f, groundLayerMask);

            if (hit.collider != null)
            {
                lastNormal = hit.normal;

                // касательный вектор вдоль поверхности
                Vector2 tangent = new Vector2(lastNormal.y, -lastNormal.x).normalized;

                // горизонтальное перемещение
                Vector2 newPos = (Vector2)transform.position + tangent * stepMove.magnitude * Mathf.Sign(inputX);

                // корректировка вертикальной высоты, чтобы vertical distance = distanceToGround
                float verticalOffset = distanceToGround / lastNormal.y; // компенсируем наклон
                Vector2 offset = lastNormal * verticalOffset;
                Vector2 targetPos = hit.point + offset;

                // плавная корректировка высоты
                float smoothY = Mathf.MoveTowards(transform.position.y, targetPos.y, heightSmoothSpeed * Time.deltaTime);

                // применяем позицию
                transform.position = new Vector3(newPos.x, smoothY, transform.position.z);

                // отладка
                Debug.DrawRay(transform.position, Vector3.down * 5f, Color.red);
                Debug.DrawRay(transform.position, lastNormal, Color.green);
            }
        }
    }
}
