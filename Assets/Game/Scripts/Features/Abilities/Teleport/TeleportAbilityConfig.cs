using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    [CreateAssetMenu(menuName = "AbilitySystem/TeleportAbilityConfig", fileName = "TeleportAbilityConfig", order = 0)]
    public class TeleportAbilityConfig : AbilityConfig
    {
        [SerializeField] private int _manaCost = 2;
        [SerializeField] private float _radius = 5f;
        [SerializeField] private KeyCode _teleportKey = KeyCode.T;
        [SerializeField] private LayerMask _layerMask;

        protected override void Install(Ability ability, IPlayerContext context)
        {
            ability.AddPointAbilityTag();

            // No character once the player has died: PlayerCharacterDeathController clears it.
            ability.AddPointCondition(new InlineFunction<Vector3, bool>(point =>
                context.GetCharacter().Value != null &&
                // A teleport would move the whole vehicle, driver and all.
                !context.GetCharacter().Value.HasVehicleTag() &&
                ManaUseCase.Enough(context, _manaCost) &&
                VectorUseCase.LessOrEqualsDistance(context.GetCharacter().Value, point, _radius)));

            ability.AddPointAction(new InlineAction<Vector3>(point =>
            {
                ManaUseCase.Spend(context, _manaCost);

                IEntity character = context.GetCharacter().Value;

                // The character body is interpolated, so writing the transform would be
                // rubber-banded back to the physics pose on the next step. Move the body itself
                // and drop the velocity it was carrying into the teleport.
                if (character.TryGetRiggedBody(out Rigidbody body))
                {
                    body.position = point;
                    body.linearVelocity = Vector3.zero;
                }
                else
                {
                    character.GetTransform().position = point;
                }
            }));

            ability.AddPointEvent(new Atomic.Elements.Event<Vector3>());
            ability.AddManaCost(new Const<int>(_manaCost));
            ability.AddRadius(new Const<float>(_radius));

            ability.WhenTick(dt =>
            {
                if (Input.GetKey(_teleportKey) && Input.GetMouseButtonDown(0))
                {
                    if (CursorUseCase.RayCastGround(out var teleportPosition, _layerMask))
                        AbilityUseCase.Use(ability, teleportPosition);
                }
            });
        }
    }
}