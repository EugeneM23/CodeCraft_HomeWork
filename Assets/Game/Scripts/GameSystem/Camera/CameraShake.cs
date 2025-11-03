using System.Collections;
using Gameplay;
using Modules.PlayerController;
using UnityEngine;

public class CameraShake : MonoBehaviour
{
    [Inject] private readonly PlayerCharacterProvider _provider;

    [Header("Shake Settings")]
    [SerializeField] private float smashMagnitude = 0.3f;
    [SerializeField] private float smashDuration = 0.25f;
    [SerializeField] private float smashVelocityThreshold = -50f;

    private Vector3 originalPosition;
    private Coroutine currentShake;

    private void Awake()
    {
        originalPosition = transform.localPosition;
    }

    private void OnEnable()
    {
        if (_provider != null)
        {
            _provider.OnPlayerChanged += OnPlayerChanged;
            SubscribeToPlayer();
        }
    }

    private void OnDisable()
    {
        if (_provider != null)
        {
            _provider.OnPlayerChanged -= OnPlayerChanged;
            UnsubscribeFromPlayer();
        }
    }

    private void OnPlayerChanged()
    {
        UnsubscribeFromPlayer();
        SubscribeToPlayer();
    }

    private void SubscribeToPlayer()
    {
        if (_provider.Player != null)
        {
            _provider.Player.OnGrounded += OnPlayerGrounded;
        }
    }

    private void UnsubscribeFromPlayer()
    {
        if (_provider.Player != null)
        {
            _provider.Player.OnGrounded -= OnPlayerGrounded;
        }
    }

    private void OnPlayerGrounded(Vector2 velocity)
    {
        if (velocity.y < smashVelocityThreshold)
        {
            TriggerShake(smashMagnitude, smashDuration);
        }
    }

    private void TriggerShake(float magnitude, float duration)
    {
        if (currentShake != null)
        {
            StopCoroutine(currentShake);
        }

        currentShake = StartCoroutine(ShakeCoroutine(magnitude, duration));
    }

    private IEnumerator ShakeCoroutine(float magnitude, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float strength = magnitude * (1f - elapsed / duration);
            Vector3 offset = Random.insideUnitCircle * strength;
            transform.localPosition = originalPosition + offset;

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localPosition = originalPosition;
        currentShake = null;
    }

    public void ShakeCustom(float magnitude, float duration)
    {
        TriggerShake(magnitude, duration);
    }
}