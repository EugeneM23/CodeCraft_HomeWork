namespace Modules.UI
{
    using UnityEngine;
    using DG.Tweening;

    public sealed class FloatingAnimation : MonoBehaviour
    {
        public float radius = 100f;
        public float rotationSpeed = 90f;
        private Tweener _tween;

        private void Start()
        {
            Vector3 center = transform.parent.position;
            transform.position = center + Vector3.right * radius;

            float duration = 360f / rotationSpeed;
            bool clockwise = Random.Range(0, 2) == 0;
            float endAngle = clockwise ? 360f : -360f;

            _tween = DOVirtual.Float(0f, endAngle, duration, angle =>
                {
                    float radians = angle * Mathf.Deg2Rad;
                    Vector3 offset = new Vector3(Mathf.Cos(radians) * radius, Mathf.Sin(radians) * radius, 0f);
                    transform.position = center + offset;
                })
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Restart)
                .SetAutoKill(false)
                .Pause();
        }

        private void OnEnable()
        {
            if (_tween != null)
            {
                _tween.Play();
            }
            else
            {
                StartCoroutine(DelayedStart());
            }
        }

        private void OnDisable()
        {
            _tween?.Pause();
        }

        private System.Collections.IEnumerator DelayedStart()
        {
            yield return null;
            if (_tween != null)
            {
                _tween.Play();
            }
        }
    }
}