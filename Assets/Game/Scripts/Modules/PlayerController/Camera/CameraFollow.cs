using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target Settings")] [SerializeField]
    private Transform target;

    [Header("Follow Settings")] [SerializeField]
    private float smoothSpeed = 5f;

    [SerializeField] private float maxDistance = 10f;

    [Header("Level Boundaries")] [SerializeField]
    private Transform boundaryTopLeft;

    [SerializeField] private Transform boundaryBottomRight;

    private Camera cam;
    private Vector3 velocity = Vector3.zero;

    void Start()
    {
        cam = GetComponent<Camera>();

        if (target != null)
        {
            Vector3 startPos = target.position;
            startPos.z = transform.position.z;
            transform.position = ClampToBoundaries(startPos);
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 targetPos = target.position;
        targetPos.z = transform.position.z;

        // Вычисляем расстояние до цели
        float distance = Vector2.Distance(transform.position, targetPos);

        // Динамическая скорость (0.1 до 1.0)
        float speedMultiplier = Mathf.Clamp01(distance / maxDistance);
        float dynamicSpeed = smoothSpeed * Mathf.Max(speedMultiplier, 0.1f);

        // SmoothDamp вместо Lerp - убирает рывки полностью
        Vector3 smoothedPosition = Vector3.SmoothDamp(
            transform.position,
            targetPos,
            ref velocity,
            1f / dynamicSpeed
        );

        transform.position = ClampToBoundaries(smoothedPosition);
    }

    private Vector3 ClampToBoundaries(Vector3 position)
    {
        if (boundaryTopLeft == null || boundaryBottomRight == null)
            return position;

        float cameraHalfHeight = cam.orthographicSize;
        float cameraHalfWidth = cameraHalfHeight * cam.aspect;

        float minX = boundaryTopLeft.position.x + cameraHalfWidth;
        float maxX = boundaryBottomRight.position.x - cameraHalfWidth;
        float minY = boundaryBottomRight.position.y + cameraHalfHeight;
        float maxY = boundaryTopLeft.position.y - cameraHalfHeight;

        // Жёсткий Clamp, но SmoothDamp компенсирует
        position.x = Mathf.Clamp(position.x, minX, maxX);
        position.y = Mathf.Clamp(position.y, minY, maxY);

        return position;
    }

    void OnDrawGizmosSelected()
    {
        if (target == null) return;

        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, maxDistance);
    }
}