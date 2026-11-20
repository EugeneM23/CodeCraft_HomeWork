using Atomic.Entities;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// Gives a skinned zombie its brain (<see cref="ZombieChaseBehaviour"/>): it goes for the tank, takes a place round
    /// the hull and bites. Listed after the zombie's core installer (its walking values, health and weapon come first).
    /// Put on <c>Zombie.prefab</c> by Tools/AI/Give Zombies Chase.
    /// </summary>
    public sealed class ZombieChaseInstaller : MonoEntityInstaller
    {
        /// <summary>The behaviour made by the last <see cref="Install"/>.</summary>
        public ZombieChaseBehaviour Behaviour { get; private set; }

        public override void Install(IEntity entity)
        {
            Behaviour = new ZombieChaseBehaviour();
            entity.AddBehaviour(Behaviour);
        }
    }
}
