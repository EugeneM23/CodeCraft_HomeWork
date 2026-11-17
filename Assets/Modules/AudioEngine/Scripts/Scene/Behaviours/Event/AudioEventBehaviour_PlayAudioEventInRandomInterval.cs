using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace AudioEngine
{
    [Serializable]
    public sealed class AudioEventBehaviour_PlayAudioEventInRandomInterval : AudioEventBehaviour
    {
        [SerializeField]
        private float startTime;

        [SerializeField]
        private float endTime;
        
        [SerializeField]
        private AudioEventKey eventKey;

        private float _actionTime;
        
        private bool _triggered;

        // Was a plain Reset() nobody called: the event fired once, at time 0, and never again on loop.
        public override void OnReset(in AudioSourceEvent evt)
        {
            _actionTime = Random.Range(this.startTime, this.endTime);
            _triggered = false;
        }

        public override void OnUpdate(in AudioSourceEvent evt, in float deltaTime)
        {
            if (!_triggered && evt.CurrentTime >= _actionTime)
            {
                evt.System.PlayEvent(this.eventKey, evt.Position, evt.Rotation);
                _triggered = true;
            }
        }
    }
}