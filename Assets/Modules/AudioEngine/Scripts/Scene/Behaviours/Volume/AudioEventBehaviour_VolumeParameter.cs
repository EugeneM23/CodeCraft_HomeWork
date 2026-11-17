using System;
using UnityEngine;

namespace AudioEngine
{
    [Serializable]
    internal sealed class AudioEventBehaviour_VolumeParameter : AudioEventBehaviour
    {
        [SerializeField]
        private AudioParameterKey volumeKey;

        public override void OnUpdate(in AudioSourceEvent evt, in float deltaTime)
        {
            if (evt.System.TryGetFloat(this.volumeKey, out float volume))
                evt.Source.volume = Mathf.Clamp01(volume);
        }

        public override void OnReset(in AudioSourceEvent evt)
        {
            if (evt.System.TryGetFloat(this.volumeKey, out float volume))
                evt.Source.volume = Mathf.Clamp01(volume);
        }
    }
}