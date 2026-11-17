using Atomic.Entities;
using RayFire;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Makes a projectile explosive. Put it next to BulletInstaller: BulletCollisionBehaviour then
    /// detonates the projectile on any contact instead of hitting a single target.
    /// </summary>
    public class ExplosionInstaller : MonoEntityInstaller
    {
        [SerializeField] private float _radius = 4f;
        [Tooltip("Share of the damage dealt at the very edge of the radius; full damage at the centre.")]
        [Range(0f, 1f)]
        [SerializeField] private float _minDamageFactor = 0.25f;
        [Tooltip("Bricks / shards of a prefractured wall closer than this to the blast are knocked out whole. The rest of the wall is left untouched.")]
        [SerializeField] private float _breachRadius = 0.6f;
        [Tooltip("Speed (m/s) a knocked-out piece leaves the wall with at the very centre, in a random direction.")]
        [SerializeField] private float _breachSpeed = 5f;
        [Tooltip("Share of knocked-out pieces that break into 2-3 parts in the air instead of flying whole, 0..1.")]
        [Range(0f, 1f)]
        [SerializeField] private float _splitChance = 0.4f;
        [Tooltip("Gives loose pieces already lying around (debris, fallen bricks) a shove. Its range is overwritten by Radius; it deals no damage.")]
        [SerializeField] private RayfireBomb _bomb;
        [Tooltip("Pooled effect entity (HitEffectInstaller) spawned at the explosion point.")]
        [SerializeField] private MonoEntity _vfxPrefab;
        [Tooltip("Pooled spray of small crumbs (HitEffectInstaller + particle systems), aimed along the shot and tinted with the material of what was hit. Not spawned on characters.")]
        [SerializeField] private MonoEntity _crumbsPrefab;
        [SerializeField] private CameraShakeArgs _shake;

        public override void Install(IEntity entity)
        {
            entity.AddExplosionRadius(_radius);
            entity.AddExplosionMinDamageFactor(_minDamageFactor);
            entity.AddExplosionBreachRadius(_breachRadius);
            entity.AddExplosionBreachSpeed(_breachSpeed);
            entity.AddExplosionSplitChance(_splitChance);

            if (_bomb != null)
                entity.AddExplosionBomb(_bomb);
            if (_vfxPrefab != null)
                entity.AddExplosionVfxPrefab(_vfxPrefab);
            if (_crumbsPrefab != null)
                entity.AddExplosionCrumbsPrefab(_crumbsPrefab);
            if (_shake != null && _shake.ShakeDuration > 0f)
                entity.AddCameraShakeArgs(_shake);
        }
    }
}
