namespace Game
{
    /// <summary>
    /// A target source whose enemies have colliders (entity enemies): bullets hit them through physics and shell
    /// blasts through their own overlap (ExplosionUseCase), so <see cref="EnemyTargets.RaycastNonPhysical"/> and
    /// <see cref="EnemyTargets.DamageAreaNonPhysical"/> leave it out.
    /// </summary>
    public interface IPhysicalEnemyTargetSource : IEnemyTargetSource
    {
    }
}
