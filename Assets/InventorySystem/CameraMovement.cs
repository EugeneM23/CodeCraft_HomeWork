using System.Collections;
using UnityEngine;

public class CameraMovement : MonoBehaviour
{
    [Header("Positions")] [SerializeField] private Transform _gameplayPosition;
    [SerializeField] private Transform _inventoryPosition;

    [Header("Settings")] [SerializeField] private float _moveDuration = 0.5f;
    [SerializeField] private AnimationCurve _moveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private Camera _camera;
    private Coroutine _moveCoroutine;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
    }

    public void MoveToInventoryPosition()
    {
        MoveTo(_inventoryPosition);
    }

    public void MoveToGameplayPosition()
    {
        MoveTo(_gameplayPosition);
    }

    private void MoveTo(Transform targetTransform)
    {
        if (_moveCoroutine != null)
            StopCoroutine(_moveCoroutine);

        _moveCoroutine = StartCoroutine(MoveCoroutine(targetTransform.position, targetTransform.rotation));
    }

    private IEnumerator MoveCoroutine(Vector3 targetPosition, Quaternion targetRotation)
    {
        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;
        float elapsed = 0f;

        while (elapsed < _moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / _moveDuration;
            float curveValue = _moveCurve.Evaluate(t);

            transform.position = Vector3.Lerp(startPosition, targetPosition, curveValue);
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, curveValue);

            yield return null;
        }

        transform.position = targetPosition;
        transform.rotation = targetRotation;
        _moveCoroutine = null;
    }
}