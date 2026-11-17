using System;
using UnityEngine;

namespace AudioEngine
{
    [Serializable]
    internal sealed class AudioEventBehaviour_VolumeCurve : AudioEventBehaviour
    {
        [SerializeField]
        private AnimationCurve curve;

        public override void OnUpdate(in AudioSourceEvent evt, in float deltaTime)
        {
            evt.Source.volume = this.curve.Evaluate(evt.CurrentProgress);
        }

        public override void OnReset(in AudioSourceEvent evt)
        {
            evt.Source.volume = this.curve.Evaluate(evt.CurrentProgress);
        }
    }
}