using System;

namespace Gameplay
{
    public class AnimationEventReceiver
    {
        public event Action<EventID> OnEventRaised;

        public void SendEvent(EventID id) => OnEventRaised?.Invoke(id);
    }
}