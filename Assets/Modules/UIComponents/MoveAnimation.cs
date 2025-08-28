using System;
using DG.Tweening;
using UnityEngine;
using Zenject;

namespace Game.Views
{
    public class MoveAnimation : MonoBehaviour
    {
        public void MoveToTarget(Vector3 position, float duration, Action onComplete = null)
        {
            gameObject.transform.DOMove(position, duration).SetEase(Ease.InOutSine)
                .OnComplete(() => onComplete?.Invoke());
        }
    }
}