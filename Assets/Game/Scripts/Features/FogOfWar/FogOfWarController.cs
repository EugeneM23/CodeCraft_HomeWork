using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>Keeps the fog centred on whatever the player currently controls.</summary>
    public class FogOfWarController : IPlayerContextInit, IPlayerContextLateTick
    {
        private readonly FogOfWarView _view;
        private IReactiveVariable<IEntity> _character;

        public FogOfWarController(FogOfWarView view) => _view = view;

        public void Init(IPlayerContext context) => _character = context.GetCharacter();

        public void LateTick(IPlayerContext context, float deltaTime)
        {
            IEntity character = _character.Value;
            if (character != null && character.TryGetTransform(out Transform t))
                _view.Rebuild(t.position);
        }
    }
}
