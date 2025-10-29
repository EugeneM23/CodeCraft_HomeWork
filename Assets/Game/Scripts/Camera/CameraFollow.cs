using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target Settings")]
    [SerializeField] private Transform target;

    [Header("Follow Settings")]
    [SerializeField] private float followRadius = 5f; // Радиус 100% преследования
    [SerializeField] private float followSpeed = 10f; // Максимальная скорость преследования

    [Header("Level Boundaries")]
    [SerializeField] private Transform boundaryTopLeft;
    [SerializeField] private Transform boundaryTopRight;
    [SerializeField] private Transform boundaryBottomLeft;
    [SerializeField] private Transform boundaryBottomRight;

    [Header("Debug Visualization")]
    [SerializeField] private bool showFollowRadius = true;
    [SerializeField] private Color radiusColor = new Color(0.3f, 0.8f, 1f, 0.4f);

    private float minX, maxX, minY, maxY;
    private Camera cam;
    private float cameraHalfWidth, cameraHalfHeight;

    void Start()
    {
        cam = GetComponent<Camera>();
        CalculateCameraSize();
        CalculateBoundaries();

        if (target != null)
        {
            Vector3 startPos = target.position;
            startPos.z = transform.position.z;
            transform.position = startPos;
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 camPos = transform.position;
        Vector3 targetPos = target.position;
        targetPos.z = camPos.z;

        // Вектор до цели
        Vector3 toTarget = targetPos - camPos;
        float distance = toTarget.magnitude;

        // Определяем коэффициент слежения (0..1)
        // На расстоянии 0 — камера стоит, на расстоянии followRadius — 100% скорости
        float t = Mathf.Clamp01(distance / followRadius);

        // Плавное движение камеры с учётом градиента скорости
        Vector3 desiredPos = camPos + toTarget * t;

        // Плавно приближаем камеру к новой позиции
        transform.position = Vector3.Lerp(camPos, desiredPos, followSpeed * Time.deltaTime);

        // Ограничиваем камеру в пределах уровня
        Vector3 clamped = transform.position;
        clamped.x = Mathf.Clamp(clamped.x, minX, maxX);
        clamped.y = Mathf.Clamp(clamped.y, minY, maxY);
        transform.position = clamped;

        if (showFollowRadius)
            DrawFollowRadius();
    }

    private void CalculateCameraSize()
    {
        if (cam.orthographic)
        {
            cameraHalfHeight = cam.orthographicSize;
            cameraHalfWidth = cameraHalfHeight * cam.aspect;
        }
        else
        {
            float distance = Mathf.Abs(transform.position.z - (target ? target.position.z : 0));
            cameraHalfHeight = distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            cameraHalfWidth = cameraHalfHeight * cam.aspect;
        }
    }

    private void CalculateBoundaries()
    {
        if (boundaryTopLeft == null || boundaryTopRight == null || boundaryBottomLeft == null || boundaryBottomRight == null)
            return;

        float levelMinX = Mathf.Min(boundaryBottomLeft.position.x, boundaryTopLeft.position.x);
        float levelMaxX = Mathf.Max(boundaryBottomRight.position.x, boundaryTopRight.position.x);
        float levelMinY = Mathf.Min(boundaryBottomLeft.position.y, boundaryBottomRight.position.y);
        float levelMaxY = Mathf.Max(boundaryTopLeft.position.y, boundaryTopRight.position.y);

        minX = levelMinX + cameraHalfWidth;
        maxX = levelMaxX - cameraHalfWidth;
        minY = levelMinY + cameraHalfHeight;
        maxY = levelMaxY - cameraHalfHeight;
    }

    private void DrawFollowRadius()
    {
        // Рисуем круг радиуса слежения вокруг камеры
        int segments = 32;
        Vector3 prev = transform.position + new Vector3(followRadius, 0, 0);
        for (int i = 1; i <= segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2;
            Vector3 next = transform.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * followRadius;
            Debug.DrawLine(prev, next, radiusColor);
            prev = next;
        }
    }
}
