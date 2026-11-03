using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Gameplay;
using UnityEngine;

namespace Modules.Gameplay
{
    public sealed class TrailView : MonoBehaviour
    {
        [SerializeField]
        private TrailRenderer trailPrefab;

        [SerializeField, Space]
        private Transform trailParent;

        [SerializeField]
        private Vector3 trailScale = new(5, 5, 2.5f);

        [SerializeField]
        private Vector3 trailOffset = new(0, 0, -1.5f);

        private TrailRenderer _trail;

        public void Show()
        {
            _trail = Instantiate(this.trailPrefab, this.trailParent);

            Transform tranform = _trail.transform;
            tranform.localScale = this.trailScale;
            tranform.localPosition = this.trailOffset;
            tranform.localEulerAngles = Vector3.zero;

            _trail.SetPositions(Array.Empty<Vector3>());
            _trail.Clear();
            _trail.emitting = true;
        }

        public void Hide()
        {
            if (_trail != null)
            {
                this.UnspawnTrail(_trail).Forget();
                _trail = null;
            }
        }

        private async UniTaskVoid UnspawnTrail(TrailRenderer trail)
        {
            trail.transform.parent = null;
            await UniTask.Delay(TimeSpan.FromSeconds(trail.time), DelayType.DeltaTime);
            Destroy(trail.gameObject);
        }
    }
}