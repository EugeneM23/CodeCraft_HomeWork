namespace Modules.UI
{
    using UnityEngine;
    using DG.Tweening;

    public sealed class FloatingAnimation : MonoBehaviour
    {
        [SerializeField] private float floatDistance = 0.5f; // Расстояние покачивания
        [SerializeField] private float duration = 2f; // Длительность подъема и опускания

        private Tween _anim;

        private void Start() => StartFloating();
        private void OnEnable() => _anim.Play();
        private void OnDisable() => _anim.Pause();

        private void StartFloating()
        {
            // Начальная позиция объекта
            Vector3 originalPosition = transform.localPosition;
            Vector3 floatingPosition = originalPosition + new Vector3(0f, floatDistance, 0f);

            // Анимация подъема
            _anim = transform
                .DOLocalMove(floatingPosition, duration)
                .SetEase(Ease.InOutSine) // Плавный переход
                .SetLoops(-1, LoopType.Yoyo); // Бесконечный цикл вверх-вниз
        }
    }
}