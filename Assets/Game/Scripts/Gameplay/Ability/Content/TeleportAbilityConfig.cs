using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.Content
{
    [CreateAssetMenu(menuName = "AbilitySystem/TeleportAbilityConfig", fileName = "TeleportAbilityConfig", order = 0)]
    public class TeleportAbilityConfig : AbilityConfig
    {
        [SerializeField] private int _initialCharges;
        [SerializeField] private int _manaCost = 2;
        [SerializeField] private float _radius = 5f;
        [SerializeField] private KeyCode _teleportKey = KeyCode.T;
        [SerializeField] private LayerMask _layerMask;

        protected override void Install(Ability ability, IPlayerContext context)
        {
            ability.AddPointAbilityTag();

            ability.AddPointCondition(new BaseFunction<Vector3, bool>(point =>
                ManaUseCase.Enough(context, _manaCost) &&
                VectorUseCase.LessOrEqualsDistance(context.GetCharacter().Value, point, _radius)));

            ability.AddPointAction(new BaseAction<Vector3>(point =>
            {
                ManaUseCase.Spend(context, _manaCost);
                context.GetCharacter().Value.GetTransform().position = point;
            }));

            ability.AddManaCost(new Const<int>(_manaCost));
            ability.AddRadius(new Const<float>(_radius));

            ability.WhenUpdate(dt =>
            {
                if (Input.GetKey(_teleportKey) && Input.GetMouseButtonDown(0))
                {
                    if (TryGetMouseWorldPosition(out var teleportPosition)) 
                        ability.GetPointAction().Invoke(teleportPosition);
                }
            });
        }

        private bool TryGetMouseWorldPosition(out Vector3 worldPosition)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, _layerMask))
            {
                worldPosition = hit.point;
                return true;
            }

            worldPosition = Vector3.zero;
            return false;
        }
    }
}