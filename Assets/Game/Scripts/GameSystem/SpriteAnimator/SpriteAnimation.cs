using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Gameplay
{
    [CreateAssetMenu(fileName = "Animation", menuName = "Game/Sprite Animation")]
    public class SpriteAnimation : SerializedScriptableObject
    {
        [field: SerializeField] public Sprite[] Sprites { get; private set; }
        [field: SerializeField] public AnimationID ID { get; private set; }
        [field: SerializeField] public float FPS { get; private set; }
        public bool CanInterrupt = true;

        [OdinSerialize] private SpriteAnimationEvent[] _events = Array.Empty<SpriteAnimationEvent>();

        private List<EventID> _tempEvents = new(24);

        public List<EventID> GetEvents(int currentFrame)
        {
            _tempEvents.Clear();

            for (int i = 0; i < _events.Length; i++)
            {
                if (_events[i].Frame == currentFrame)
                    _tempEvents.Add(_events[i].ID);
            }

            return _tempEvents;
        }
    }
}