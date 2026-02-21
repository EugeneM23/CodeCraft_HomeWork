using System;
using UnityEngine;

namespace AudioEngine
{
    [Serializable]
    public sealed class AudioEventAction_InvokeAudioCallback : IAudioEventAction
    {
        [SerializeField]
        private AudioCallbackKey callback;
        
        public void Invoke(in AudioSourceEvent evt) => evt.System.InvokeCallback(this.callback);
    }
}