using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target Settings")]
    [SerializeField] private Transform target; // Объект за которым следует камера
    
    [Header("Follow Settings")]
    [SerializeField] private float deadZoneWidth = 4f; // Ширина мертвой зоны
    [SerializeField] private float deadZoneHeight = 3f; // Высота мертвой зоны
    [SerializeField] private float followSpeed = 5f; // Скорость следования
    
    [Header("Level Boundaries")]
    [SerializeField] private Transform boundaryTopLeft;
    [SerializeField] private Transform boundaryTopRight;
    [SerializeField] private Transform boundaryBottomLeft;
    [SerializeField] private Transform boundaryBottomRight;
    
    [Header("Debug Visualization")]
    [SerializeField] private bool showDeadZone = true; // Показывать мертвую зону в игре
    [SerializeField] private Color deadZoneColor = Color.yellow;
    
    private float minX, maxX, minY, maxY;
    private Vector3 initialOffset;
    private Camera cam;
    private float cameraHalfWidth;
    private float cameraHalfHeight;

    void Start()
    {
        cam = GetComponent<Camera>();
        
        // Вычисляем размеры поля зрения камеры
        CalculateCameraSize();
        
        // Устанавливаем камеру в позицию целевого объекта на старте
        if (target != null)
        {
            Vector3 startPosition = target.position;
            startPosition.z = transform.position.z; // Сохраняем Z координату камеры
            transform.position = startPosition;
            
            // Запоминаем начальное смещение
            initialOffset = transform.position - target.position;
        }
        
        // Вычисляем границы уровня с учетом размера камеры
        CalculateBoundaries();
    }

    void LateUpdate()
    {
        if (target == null) return;
        
        // Вычисляем относительную позицию игрока от центра камеры
        Vector2 cameraPos2D = new Vector2(transform.position.x, transform.position.y);
        Vector2 targetPos2D = new Vector2(target.position.x, target.position.y);
        Vector2 offset = targetPos2D - cameraPos2D;
        
        // Половины размеров мертвой зоны
        float halfWidth = deadZoneWidth / 2f;
        float halfHeight = deadZoneHeight / 2f;
        
        // Проверяем, вышел ли игрок за пределы квадратной мертвой зоны
        bool isOutsideDeadZone = Mathf.Abs(offset.x) > halfWidth || Mathf.Abs(offset.y) > halfHeight;
        
        if (isOutsideDeadZone)
        {
            // Целевая позиция камеры
            Vector3 targetPosition = target.position + initialOffset;
            targetPosition.z = transform.position.z; // Сохраняем Z координату
            
            // Плавно двигаемся к цели
            Vector3 newPosition = Vector3.Lerp(transform.position, targetPosition, followSpeed * Time.deltaTime);
            
            // Применяем границы уровня с учетом размера камеры
            newPosition.x = Mathf.Clamp(newPosition.x, minX, maxX);
            newPosition.y = Mathf.Clamp(newPosition.y, minY, maxY);
            
            transform.position = newPosition;
        }
        
        // Рисуем мертвую зону в игре
        if (showDeadZone)
        {
            DrawDeadZone();
        }
    }

    // Вычисляем размер поля зрения камеры
    private void CalculateCameraSize()
    {
        if (cam.orthographic)
        {
            // Для ортографической камеры
            cameraHalfHeight = cam.orthographicSize;
            cameraHalfWidth = cameraHalfHeight * cam.aspect;
        }
        else
        {
            // Для перспективной камеры
            float distance = Mathf.Abs(transform.position.z - (target != null ? target.position.z : 0));
            cameraHalfHeight = distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            cameraHalfWidth = cameraHalfHeight * cam.aspect;
        }
    }

    // Вычисляем границы на основе 4 объектов с учетом размера камеры
    private void CalculateBoundaries()
    {
        if (boundaryTopLeft != null && boundaryTopRight != null && 
            boundaryBottomLeft != null && boundaryBottomRight != null)
        {
            float levelMinX = Mathf.Min(boundaryBottomLeft.position.x, boundaryTopLeft.position.x);
            float levelMaxX = Mathf.Max(boundaryBottomRight.position.x, boundaryTopRight.position.x);
            float levelMinY = Mathf.Min(boundaryBottomLeft.position.y, boundaryBottomRight.position.y);
            float levelMaxY = Mathf.Max(boundaryTopLeft.position.y, boundaryTopRight.position.y);
            
            // Учитываем размер камеры, чтобы края не выходили за границы
            minX = levelMinX + cameraHalfWidth;
            maxX = levelMaxX - cameraHalfWidth;
            minY = levelMinY + cameraHalfHeight;
            maxY = levelMaxY - cameraHalfHeight;
            
            // Если уровень меньше чем поле зрения камеры, центрируем камеру
            if (minX > maxX)
            {
                float center = (levelMinX + levelMaxX) / 2f;
                minX = maxX = center;
            }
            if (minY > maxY)
            {
                float center = (levelMinY + levelMaxY) / 2f;
                minY = maxY = center;
            }
        }
        else
        {
            Debug.LogWarning("Не все граничные объекты установлены!");
        }
    }

    // Рисуем мертвую зону во время игры
    private void DrawDeadZone()
    {
        float halfWidth = deadZoneWidth / 2f;
        float halfHeight = deadZoneHeight / 2f;
        
        Vector3 center = transform.position;
        
        // Углы квадрата
        Vector3 topLeft = center + new Vector3(-halfWidth, halfHeight, 0);
        Vector3 topRight = center + new Vector3(halfWidth, halfHeight, 0);
        Vector3 bottomRight = center + new Vector3(halfWidth, -halfHeight, 0);
        Vector3 bottomLeft = center + new Vector3(-halfWidth, -halfHeight, 0);
        
        // Рисуем линии квадрата
        Debug.DrawLine(topLeft, topRight, deadZoneColor);
        Debug.DrawLine(topRight, bottomRight, deadZoneColor);
        Debug.DrawLine(bottomRight, bottomLeft, deadZoneColor);
        Debug.DrawLine(bottomLeft, topLeft, deadZoneColor);
    }

    // Визуализация квадратной мертвой зоны и границ в редакторе
    private void OnDrawGizmosSelected()
    {
        // Рисуем квадратную мертвую зону
        Gizmos.color = Color.yellow;
        Vector3 center = transform.position;
        Vector3 size = new Vector3(deadZoneWidth, deadZoneHeight, 0.1f);
        Gizmos.DrawWireCube(center, size);
        
        // Рисуем границы уровня
        if (boundaryTopLeft != null && boundaryTopRight != null && 
            boundaryBottomLeft != null && boundaryBottomRight != null)
        {
            Gizmos.color = Color.red;
            
            // Рисуем линии границ
            Gizmos.DrawLine(boundaryTopLeft.position, boundaryTopRight.position);
            Gizmos.DrawLine(boundaryTopRight.position, boundaryBottomRight.position);
            Gizmos.DrawLine(boundaryBottomRight.position, boundaryBottomLeft.position);
            Gizmos.DrawLine(boundaryBottomLeft.position, boundaryTopLeft.position);
            
            // Рисуем реальные границы движения камеры (с учетом поля зрения)
            if (Application.isPlaying)
            {
                Gizmos.color = Color.green;
                Vector3 topLeft = new Vector3(minX, maxY, transform.position.z);
                Vector3 topRight = new Vector3(maxX, maxY, transform.position.z);
                Vector3 bottomRight = new Vector3(maxX, minY, transform.position.z);
                Vector3 bottomLeft = new Vector3(minX, minY, transform.position.z);
                
                Gizmos.DrawLine(topLeft, topRight);
                Gizmos.DrawLine(topRight, bottomRight);
                Gizmos.DrawLine(bottomRight, bottomLeft);
                Gizmos.DrawLine(bottomLeft, topLeft);
            }
        }
    }
}