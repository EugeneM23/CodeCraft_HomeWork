using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI; // Добавлено для Image

public class UITurntable : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    [Header("Настройки вращения")]
    [SerializeField] private RectTransform target3DObject; // 3D объект с RectTransform
    [SerializeField] private float rotationSpeed = 0.5f; // Скорость вращения
    [SerializeField] private bool invertRotation = false; // Инвертировать направление
    [SerializeField] private Vector3 rotationAxis = Vector3.up; // Ось вращения (Y по умолчанию)
    
    [Header("Дополнительные настройки")]
    [SerializeField] private bool smoothRotation = true; // Плавное вращение
    [SerializeField] private float smoothTime = 0.1f; // Время сглаживания
    
    private bool isDragging = false;
    private Vector2 lastMousePosition;
    private float currentVelocity = 0f;
    private Vector3 targetRotation;

    private void Start()
    {
        // Проверяем наличие любого Graphic компонента (Image или RawImage)
        Graphic graphic = GetComponent<Graphic>();
        
        if (graphic == null)
        {
            // Проверяем RawImage
            RawImage rawImg = GetComponent<RawImage>();
            if (rawImg == null)
            {
                Debug.LogWarning("UITurntable: Отсутствует Image или RawImage компонент! Добавляю Image...");
                Image img = gameObject.AddComponent<Image>();
                img.color = new Color(1, 1, 1, 0.01f); // Почти прозрачный
                graphic = img;
            }
            else
            {
                graphic = rawImg;
            }
        }
        
        // Проверяем Raycast Target
        if (graphic != null && !graphic.raycastTarget)
        {
            Debug.LogWarning("UITurntable: Raycast Target выключен! Включаю...");
            graphic.raycastTarget = true;
        }
        
        if (target3DObject != null)
        {
            targetRotation = target3DObject.localEulerAngles;
        }
        
        Debug.Log("UITurntable инициализирован на " + gameObject.name);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        Debug.Log("OnPointerDown сработал!");
        isDragging = true;
        lastMousePosition = eventData.position;
        
        if (smoothRotation && target3DObject != null)
        {
            targetRotation = target3DObject.localEulerAngles;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Debug.Log("OnPointerUp сработал!");
        isDragging = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        Debug.Log("OnDrag сработал!");
        if (target3DObject == null) return;

        // Вычисляем разницу по горизонтали
        float deltaX = eventData.position.x - lastMousePosition.x;
        
        // Применяем инверсию если нужно
        if (invertRotation)
            deltaX = -deltaX;
        
        // Вычисляем угол поворота
        float rotationDelta = deltaX * rotationSpeed;
        
        if (smoothRotation)
        {
            // Плавное вращение
            targetRotation += rotationAxis * rotationDelta;
            
            Vector3 currentRotation = target3DObject.localEulerAngles;
            Vector3 smoothedRotation = new Vector3(
                Mathf.LerpAngle(currentRotation.x, targetRotation.x, 1f - Mathf.Exp(-smoothTime * 10f)),
                Mathf.LerpAngle(currentRotation.y, targetRotation.y, 1f - Mathf.Exp(-smoothTime * 10f)),
                Mathf.LerpAngle(currentRotation.z, targetRotation.z, 1f - Mathf.Exp(-smoothTime * 10f))
            );
            
            target3DObject.localEulerAngles = smoothedRotation;
        }
        else
        {
            // Прямое вращение
            target3DObject.Rotate(rotationAxis, rotationDelta, Space.Self);
        }
        
        // Сохраняем текущую позицию для следующего кадра
        lastMousePosition = eventData.position;
    }

    // Метод для установки объекта из кода
    public void SetTarget3DObject(RectTransform rectTransform)
    {
        target3DObject = rectTransform;
        if (target3DObject != null)
        {
            targetRotation = target3DObject.localEulerAngles;
        }
    }
}