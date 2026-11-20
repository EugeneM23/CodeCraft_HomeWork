using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The tank knocks this body down before it rolls onto it (<see cref="KnockdownBehaviour"/>, TankCrushBehaviour). Listed
    /// after the core installer (its Animator, transform, walking and health come first). Zombie.prefab: Zombie Falling
    /// Back Death.
    /// </summary>
    public sealed class KnockdownInstaller : MonoEntityInstaller
    {
        [Tooltip("The fall it plays when the tank drives into it (humanoid, played once).")]
        [SerializeField] private AnimationClip _fall;

        [Tooltip("Seconds it lies if the hull never rolls onto it; then it gets up and goes on.")]
        [SerializeField] private float _lie = 3f;

        public override void Install(IEntity entity) => entity.AddBehaviour(new KnockdownBehaviour(_fall, _lie));
    }
}
