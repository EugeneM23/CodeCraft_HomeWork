using System;
using UnityEngine;

namespace AudioEngine
{
    [Serializable]
    public sealed class AudioLowPassFilter : AudioEventBehaviour
    {
        [SerializeReference] private IFloatProvider cutoffFrequency = new FloatConst(5007.7f);
        [SerializeReference] private IFloatProvider lowpassResonanceQ = new FloatConst(1);

        private UnityEngine.AudioLowPassFilter _filter;

        public override void OnStart(in AudioSourceEvent evt)
        {
            _filter = evt.Source.gameObject.AddComponent<UnityEngine.AudioLowPassFilter>();
            _filter.cutoffFrequency = this.cutoffFrequency.Value;
            _filter.lowpassResonanceQ = this.lowpassResonanceQ.Value;
        }

        public override void OnStop(in AudioSourceEvent evt)
        {
            if (_filter != null) 
                GameObject.Destroy(_filter);
        }
    }
}