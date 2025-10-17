using UnityEngine;

namespace Gameplay
{
    public class PushAbilityController : IInitializeble, IDisposable
    {
        [Inject] private readonly Player _player;
        [Inject] private readonly AnimationEventReceiver _receiver;

        IPushUpComponent _pushUpComponent;
        IPushSideComponent _pushSideComponent;

        public void Initialize()
        {
            _pushUpComponent = _player;
            _pushSideComponent = _player;
            _receiver.OnEventRaised += SendEvent;
        }

        public void Dispose() => _receiver.OnEventRaised -= SendEvent;

        private void SendEvent(EventID id)
        {
            if (id == EventID.PushAbilityUP) 
                _pushUpComponent.Push();

            if (id == EventID.PushAbilitySide)
                _pushSideComponent.Push();
        }
    }
}