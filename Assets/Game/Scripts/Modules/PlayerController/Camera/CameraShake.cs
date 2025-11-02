using System.Collections;
using Gameplay;
using Modules.PlayerController;
using UnityEngine;

public class CameraShake : MonoBehaviour
{
    [Header("References")] [SerializeField]
    private CharacterController2D _character;

    [SerializeField] private Transform _cameraTransform;

    [Header("Shake Settings")] [SerializeField]
    private ShakeSettings _jumpShake = new ShakeSettings(0.1f, 0.15f, 10f);

    [SerializeField] private ShakeSettings _dashShake = new ShakeSettings(0.15f, 0.2f, 15f);
    [SerializeField] private ShakeSettings _smashShake = new ShakeSettings(0.3f, 0.25f, 20f);
    [SerializeField] private ShakeSettings _collisionShake = new ShakeSettings(0.2f, 0.15f, 12f);

    private Vector3 _originalPosition;
    private Coroutine _currentShake;

    private void Awake()
    {
        if (_cameraTransform == null)
            _cameraTransform = transform;

        _originalPosition = _cameraTransform.localPosition;
    }

    private void OnEnable()
    {
        _character.OnGrounded += OnSmashShake;
    }

    private void OnDisable()
    {
        _character.OnGrounded -= OnSmashShake;
    }

    private void OnJumpShake() => TriggerShake(_jumpShake);
    private void OnDashShake() => TriggerShake(_dashShake);

    private void OnSmashShake(Vector2 characterVelocity)
    {
        characterVelocity.Log(Color.green);
        if (characterVelocity.y < -50f)
        {
            TriggerShake(_smashShake);
        }
    }

    private void OnCollisionShake() => TriggerShake(_collisionShake);

    private void TriggerShake(ShakeSettings settings)
    {
        if (_currentShake != null)
            StopCoroutine(_currentShake);

        _currentShake = StartCoroutine(ShakeCoroutine(settings));
    }

    private IEnumerator ShakeCoroutine(ShakeSettings settings)
    {
        float elapsed = 0f;

        while (elapsed < settings.Duration)
        {
            float strength = settings.Magnitude * (1f - elapsed / settings.Duration);

            Vector3 offset = Random.insideUnitCircle * strength;
            _cameraTransform.localPosition = _originalPosition + offset;

            elapsed += Time.deltaTime;
            yield return null;
        }

        _cameraTransform.localPosition = _originalPosition;
        _currentShake = null;
    }

    public void ShakeCustom(float magnitude, float duration, float frequency = 10f)
    {
        TriggerShake(new ShakeSettings(magnitude, duration, frequency));
    }

    [System.Serializable]
    private class ShakeSettings
    {
        [Tooltip("Сила тряски")] public float Magnitude;

        [Tooltip("Длительность тряски в секундах")]
        public float Duration;

        [Tooltip("Частота колебаний (не используется в базовой реализации)")]
        public float Frequency;

        public ShakeSettings(float magnitude, float duration, float frequency)
        {
            Magnitude = magnitude;
            Duration = duration;
            Frequency = frequency;
        }
    }
}