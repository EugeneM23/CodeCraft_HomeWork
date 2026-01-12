using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public static class AbilityUseCase
    {
        public static void Use(Ability ability)
        {
            if (!ability.HasBaseAbilityTag())
                return;

            if (!ability.GetBaseCondition().Invoke())
                return;

            ability.GetBaseEvent().Invoke();
            ability.GetBaseAction().Invoke();
        }

        public static void Use(Ability ability, Vector3 position)
        {
            if (!ability.HasPointAbilityTag())
                return;

            if (!ability.GetPointCondition().Invoke(position))
                return;

            ability.GetPointEvent().Invoke(position);
            ability.GetPointAction().Invoke(position);
        }

        public static void Use(Ability ability, IEntity target)
        {
            if (!ability.HasTargetAbilityTag())
                return;

            if (!ability.GetTargetCondition().Invoke(target))
                return;

            ability.GetTargetEvent().Invoke(target);
            ability.GetTargetAction().Invoke(target);
        }
    }
}