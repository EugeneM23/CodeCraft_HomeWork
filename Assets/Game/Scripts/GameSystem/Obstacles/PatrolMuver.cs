using UnityEngine;
using DG.Tweening;
using System.Collections;

public class PatrolMover : MonoBehaviour
{
    [Header("Параметры патрулирования")]
    [SerializeField] private Transform[] waypoints;   // Передаём через инспектор
    [SerializeField] private float moveSpeed = 3f;    // Скорость движения
    [SerializeField] private float waitTime = 1f;     // Время ожидания между точками

    private Vector3[] positions;                      // Сохранённые позиции точек
    private int currentIndex = 0;
    private Tween moveTween;

    private void Start()
    {
        if (waypoints == null || waypoints.Length == 0)
        {
            Debug.LogWarning("Нет заданных точек патрулирования!");
            return;
        }

        // Сохраняем позиции всех точек
        positions = new Vector3[waypoints.Length];
        for (int i = 0; i < waypoints.Length; i++)
        {
            positions[i] = waypoints[i].position;
        }

        // Запускаем патрулирование
        StartCoroutine(PatrolRoutine());
    }

    private IEnumerator PatrolRoutine()
    {
        while (true)
        {
            Vector3 targetPos = positions[currentIndex];
            float distance = Vector3.Distance(transform.position, targetPos);
            float duration = distance / moveSpeed;

            // Запускаем движение через DOTween
            moveTween = transform.DOMove(targetPos, duration)
                .SetEase(Ease.Linear);

            // Ждём окончания движения
            yield return moveTween.WaitForCompletion();

            // Небольшая пауза
            yield return new WaitForSeconds(waitTime);

            // Переход к следующей точке
            currentIndex = (currentIndex + 1) % positions.Length;
        }
    }

    private void OnDisable()
    {
        // Безопасно убиваем твины при выключении
        moveTween?.Kill();
    }
}