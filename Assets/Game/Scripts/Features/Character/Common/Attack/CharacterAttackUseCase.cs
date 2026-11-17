using System;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public static class CharacterAttackUseCase
    {
        /// <summary>
        /// A weapon in hand and the attack that fires it: the slot, where the weapon sits, the
        /// condition (alive, plus <paramref name="extraCondition"/> — the ridden Regax attacks only
        /// with a rider on it), the fire action and the melee hit cast.
        /// </summary>
        public static void AddHeldWeapon(IEntity entity, MonoEntity weapon, Transform weaponRoot,
            Func<bool> extraCondition = null)
        {
            entity.AddWeapon(new ReactiveVariable<IEntity>(weapon));
            entity.AddWeaponRoot(weaponRoot);

            AndExpression condition = new AndExpression(new ExpressionMember<bool>(entity.GetHealth().Exists));
            if (extraCondition != null)
                condition.Add(extraCondition);

            entity.AddFireCondition(condition);
            entity.AddFireAction(new CharacterFireAction(entity));
            entity.AddBehaviour(new DamageCastBehaviour());
        }
    }
}
