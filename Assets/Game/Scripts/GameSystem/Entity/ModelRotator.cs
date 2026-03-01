using UnityEngine;
using UnityEngine.EventSystems;

public class ModelRotator : MonoBehaviour, IDragHandler, IEndDragHandler, IPointerDownHandler
{
    public float rotationSpeed = 0.5f;
    public float acceleration = 2f;
    public float damping = 5f;

    private float currentVelocity;
    private bool isDragging;

    public void OnPointerDown(PointerEventData eventData)
    {
        currentVelocity = 0f;
        isDragging = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        isDragging = true;
        float dragSpeed = eventData.delta.x * rotationSpeed;
        currentVelocity = Mathf.Lerp(currentVelocity, dragSpeed, acceleration * Time.deltaTime);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;
    }

    void Update()
    {
        if (Mathf.Abs(currentVelocity) > 0.01f)
        {
            transform.Rotate(Vector3.up, currentVelocity);

            if (!isDragging)
            {
                currentVelocity = Mathf.Lerp(currentVelocity, 0f, damping * Time.deltaTime);
            }
        }
    }
}