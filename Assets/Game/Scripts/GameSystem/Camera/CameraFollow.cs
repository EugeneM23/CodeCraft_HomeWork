using System.Collections;
using Game.Scripts.GameObject.Player;
using Gameplay;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Inject] private readonly PlayerCharacterProvider _provider;

    [Header("Follow Settings")]
    [SerializeField] private float smoothSpeed = 5f;
    [SerializeField] private float maxDistance = 10f;
    [SerializeField] private float transitionDelay = 2f;
    [SerializeField] private float transitionDuration = 1f;

    [Header("Level Boundaries")]
    [SerializeField] private Transform boundaryTopLeft;
    [SerializeField] private Transform boundaryBottomRight;

    private Camera cam;
    private Vector3 velocity = Vector3.zero;
    private Coroutine transitionCoroutine;
    private bool isTransitioning = false;

    void Start()
    {
        cam = GetComponent<Camera>();
        
        if (_provider.Player != null)
        {
            SnapToPlayer();
        }
    }

    void OnEnable()
    {
        if (_provider != null)
        {
            _provider.OnPlayerChanged += OnPlayerChanged;
        }
    }

    void OnDisable()
    {
        if (_provider != null)
        {
            _provider.OnPlayerChanged -= OnPlayerChanged;
        }
    }

    void LateUpdate()
    {
        if (_provider.Player == null || isTransitioning) return;

        Vector3 targetPos = _provider.Player.transform.position;
        targetPos.z = transform.position.z;

        float distance = Vector2.Distance(transform.position, targetPos);
        float speedMultiplier = Mathf.Clamp01(distance / maxDistance);
        float dynamicSpeed = smoothSpeed * Mathf.Max(speedMultiplier, 0.1f);

        Vector3 smoothedPosition = Vector3.SmoothDamp(
            transform.position,
            targetPos,
            ref velocity,
            1f / dynamicSpeed
        );

        transform.position = ClampToBoundaries(smoothedPosition);
    }

    private void OnPlayerChanged()
    {
        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
        }

        transitionCoroutine = StartCoroutine(TransitionToNewPlayer());
    }

    private IEnumerator TransitionToNewPlayer()
    {
        isTransitioning = true;
        velocity = Vector3.zero;

        // Ждем 2 секунды
        yield return new WaitForSeconds(transitionDelay);

        if (_provider.Player == null)
        {
            isTransitioning = false;
            yield break;
        }

        // Плавный переход к новому персонажу
        Vector3 startPos = transform.position;
        Vector3 targetPos = _provider.Player.transform.position;
        targetPos.z = transform.position.z;
        targetPos = ClampToBoundaries(targetPos);

        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / transitionDuration;
            t = Mathf.SmoothStep(0f, 1f, t); // Плавная интерполяция

            transform.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        transform.position = targetPos;
        isTransitioning = false;
    }

    private void SnapToPlayer()
    {
        Vector3 targetPos = _provider.Player.transform.position;
        targetPos.z = transform.position.z;
        transform.position = ClampToBoundaries(targetPos);
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

        position.x = Mathf.Clamp(position.x, minX, maxX);
        position.y = Mathf.Clamp(position.y, minY, maxY);

        return position;
    }
}