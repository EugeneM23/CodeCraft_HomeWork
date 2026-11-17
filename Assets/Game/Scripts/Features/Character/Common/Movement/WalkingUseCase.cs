using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public static class WalkingUseCase
    {
        /// <summary>
        /// The values every walking body carries — the player's character, an enemy, the ridden
        /// Regax: speeds, where it wants to go and to face, how fast it is going, and the rule that
        /// it walks only while alive and while the weapon in hand lets it (a melee swing roots it).
        /// </summary>
        /// <remarks>
        /// Behaviours stay with the installer: it picks how the body moves (RigidbodyMove/Rotate or
        /// TransformMove/Rotate), and the order they are added in is the order they tick.
        /// Needs Health on the entity already, and Weapon by the time the condition is first asked.
        /// </remarks>
        public static void AddWalking(IEntity entity, float moveSpeed, float rotationSpeed, Vector3 facing = default)
        {
            entity.AddMoveSpeed(new ReactiveFloat(moveSpeed));
            entity.AddRotationSpeed(new Variable<float>(rotationSpeed));
            entity.AddMoveCondition(new AndExpression(
                new ExpressionMember<bool>(entity.GetHealth().Exists),
                new ExpressionMember<bool>(() => entity.GetWeapon().Value.GetMoveCondition().Invoke())
            ));
            entity.AddMoveDirection(new ReactiveVariable<Vector3>());
            entity.AddRotateDirection(new ReactiveVariable<Vector3>(facing));
            entity.AddVelocity(new ReactiveFloat());
        }
    }
}
