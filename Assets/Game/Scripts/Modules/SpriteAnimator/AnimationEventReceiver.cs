using System;
using UnityEngine;

namespace Gameplay
{
    public class AnimationEventReceiver : MonoBehaviour
    {
        public event Action<EventID> OnEventRaised;

        public void SendEvent(EventID id) => OnEventRaised?.Invoke(id);
    }
}