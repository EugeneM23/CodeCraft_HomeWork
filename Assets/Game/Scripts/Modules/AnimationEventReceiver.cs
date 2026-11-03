using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

namespace Modules.Gameplay
{
    public sealed class AnimationEventReceiver : MonoBehaviour
    {
        public event Action<string> OnEvent;

        private readonly Dictionary<string, Action> _handlers = new();

        [UsedImplicitly]
        private void ReceiveEvent(string message)
        {
            if (_handlers.TryGetValue(message, out Action handler)) 
                handler.Invoke();

            this.OnEvent?.Invoke(message);
        }

        public void Subscribe(string evt, Action action)
        {
            if (_handlers.TryGetValue(evt, out Action handler))
                handler += action;
            else
                handler = action;

            _handlers[evt] = handler;
        }

        public void Unsubscribe(string evt, Action action)
        {
            if (_handlers.TryGetValue(evt, out Action handler))
            {
                handler -= action;
                _handlers[evt] = handler;
            }
        }
    }
}