using Modules.PlayerController;
using UnityEngine;

namespace Gameplay
{
    public class WallSmashDamageComponent : ITickable
    {
        [Inject] private CharacterController2D _character;

        public void Tick()
        {
            if (_character.IsOnWall && Mathf.Abs(_character.LastFrameVelocity.x) > 40f)
            {
                var entity = _character.GetComponent<Entity>();
                entity.GetEntityComponent<HealthComponent>().TakeDamage(100);
            }
        }
    }
}