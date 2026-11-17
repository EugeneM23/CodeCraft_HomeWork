using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Entity API definition. The Get/TryGet/Add/Has/Del/Set extension methods are emitted
    /// into this partial class at compile time by EntityAPIGenerator.dll.
    /// Replaces the former EntityAPI.yaml + generated EntityAPI.cs pair.
    /// </summary>
    [EntityExtensionsAPI]
    public static partial class EntityAPI
    {
        #region Tags

        public static readonly TagKey<IEntity> Damageable = new(nameof(Damageable));
        public static readonly TagKey<IEntity> Player = new(nameof(Player));
        public static readonly TagKey<IEntity> Enemy = new(nameof(Enemy));
        public static readonly TagKey<IEntity> MeleeWeapon = new(nameof(MeleeWeapon));
        public static readonly TagKey<IEntity> RangeWeapon = new(nameof(RangeWeapon));
        // A tank or another machine. The player's one is PlayerContext.Character.
        public static readonly TagKey<IEntity> Vehicle = new(nameof(Vehicle));

        #endregion

        #region Core

        public static readonly ValueKey<IEntity, string> EntityID = new(nameof(EntityID));
        public static readonly ValueKey<IEntity, UnityEngine.GameObject> GameObject = new(nameof(GameObject));
        public static readonly ValueKey<IEntity, UnityEngine.Transform> Transform = new(nameof(Transform));

        // Game.CooldownTimer, NOT the new Atomic.Elements.Cooldown.
        public static readonly ValueKey<IEntity, Game.CooldownTimer> LifeTime = new(nameof(LifeTime));
        public static readonly ValueKey<IEntity, IAction> DestroyAction = new(nameof(DestroyAction));

        #endregion

        #region Movement

        public static readonly ValueKey<IEntity, IReactiveVariable<float>> MoveSpeed = new(nameof(MoveSpeed));
        public static readonly ValueKey<IEntity, IReactiveVariable<Vector3>> MoveDirection = new(nameof(MoveDirection));
        public static readonly ValueKey<IEntity, IReactiveVariable<Vector3>> RotateDirection = new(nameof(RotateDirection));
        public static readonly ValueKey<IEntity, IExpression<bool>> MoveCondition = new(nameof(MoveCondition));
        public static readonly ValueKey<IEntity, IValue<float>> RotationSpeed = new(nameof(RotationSpeed));
        public static readonly ValueKey<IEntity, IReactiveVariable<float>> Velocity = new(nameof(Velocity));

        #endregion

        #region Weapon

        public static readonly ValueKey<IEntity, MonoEntity> BulletPrefab = new(nameof(BulletPrefab));
        public static readonly ValueKey<IEntity, MonoEntity> ShellPrefab = new(nameof(ShellPrefab));
        // On a spent casing: how fast (rad/s) it tumbles as it is thrown out. Without it, it does not.
        public static readonly ValueKey<IEntity, float> ShellSpin = new(nameof(ShellSpin));
        public static readonly ValueKey<IEntity, MonoEntity> BulletHitPrefab = new(nameof(BulletHitPrefab));
        public static readonly ValueKey<IEntity, Game.Ammo> Ammo = new(nameof(Ammo));
        public static readonly ValueKey<IEntity, Game.CooldownTimer> WeaponCooldown = new(nameof(WeaponCooldown));
        public static readonly ValueKey<IEntity, IReactiveVariable<IEntity>> Weapon = new(nameof(Weapon));
        public static readonly ValueKey<IEntity, UnityEngine.Transform> FirePoint = new(nameof(FirePoint));
        public static readonly ValueKey<IEntity, UnityEngine.Transform> ShellPoint = new(nameof(ShellPoint));
        public static readonly ValueKey<IEntity, IValue<float>> Spread = new(nameof(Spread));
        public static readonly ValueKey<IEntity, IReactiveVariable<int>> Damage = new(nameof(Damage));
        public static readonly ValueKey<IEntity, IValue<float>> DamageRadius = new(nameof(DamageRadius));
        public static readonly ValueKey<IEntity, LayerMask> DamageLayer = new(nameof(DamageLayer));
        public static readonly ValueKey<IEntity, IAction> FireAction = new(nameof(FireAction));
        public static readonly ValueKey<IEntity, IEvent> FireEvent = new(nameof(FireEvent));
        public static readonly ValueKey<IEntity, IExpression<bool>> FireCondition = new(nameof(FireCondition));
        // On a weapon mounted on something with a body: the colliders its bullets fly through
        // instead of hitting. Without it, a bullet hits whatever it meets. No gun sets it now.
        public static readonly ValueKey<IEntity, UnityEngine.Collider[]> ShooterColliders = new(nameof(ShooterColliders));
        // On a bullet: the shooter colliders its collisions are switched off with, put back when a
        // pooled bullet is fired by somebody else.
        public static readonly ValueKey<IEntity, UnityEngine.Collider[]> IgnoredColliders = new(nameof(IgnoredColliders));

        #endregion

        #region Health

        public static readonly ValueKey<IEntity, Game.Health> Health = new(nameof(Health));
        public static readonly ValueKey<IEntity, IEvent<TakeDamageArgs>> DamageTakenEvent = new(nameof(DamageTakenEvent));
        public static readonly ValueKey<IEntity, IEvent<TakeDamageArgs>> DeathTakenEvent = new(nameof(DeathTakenEvent));

        #endregion

        #region Physics

        public static readonly ValueKey<IEntity, CollisionEventReceiver> CollisionReceiver = new(nameof(CollisionReceiver));
        public static readonly ValueKey<IEntity, Rigidbody> RiggedBody = new(nameof(RiggedBody));

        // Explosive projectiles (tank shells): on any contact they deal falloff damage in a radius
        // and dig a voxel crater instead of a single-target hit.
        public static readonly ValueKey<IEntity, float> ExplosionRadius = new(nameof(ExplosionRadius));
        public static readonly ValueKey<IEntity, float> ExplosionMinDamageFactor = new(nameof(ExplosionMinDamageFactor));
        public static readonly ValueKey<IEntity, MonoEntity> ExplosionVfxPrefab = new(nameof(ExplosionVfxPrefab));
        // Pooled spray of small crumbs, tinted with the material of what the blast hit.
        public static readonly ValueKey<IEntity, MonoEntity> ExplosionCrumbsPrefab = new(nameof(ExplosionCrumbsPrefab));

        // Ricochet: a bullet that grazes a surface bounces off it and flies on instead of stopping.
        // A bullet without these values (or with RicochetMaxCount 0) never ricochets.
        public static readonly ValueKey<IEntity, int> RicochetMaxCount = new(nameof(RicochetMaxCount));
        // Largest angle (degrees) between the flight direction and the surface that still bounces.
        public static readonly ValueKey<IEntity, float> RicochetMaxAngle = new(nameof(RicochetMaxAngle));
        // How many times the bullet has bounced since it left the barrel.
        public static readonly ValueKey<IEntity, int> RicochetCount = new(nameof(RicochetCount));

        #endregion

        #region Animation

        public static readonly ValueKey<IEntity, UnityEngine.Animator> Animator = new(nameof(Animator));
        public static readonly ValueKey<IEntity, RuntimeAnimatorController> AnimationController = new(nameof(AnimationController));
        public static readonly ValueKey<IEntity, Game.AnimationEventReceiver> AnimationEventReceiver = new(nameof(AnimationEventReceiver));

        #endregion

        #region AI

        public static readonly ValueKey<IEntity, IReactiveVariable<IEntity>> Target = new(nameof(Target));

        #endregion

        #region Sound


        #endregion

        #region Camera

        public static readonly ValueKey<IEntity, Game.CameraShakeArgs> CameraShakeArgs = new(nameof(CameraShakeArgs));
        public static readonly ValueKey<IEntity, IEvent<Game.CameraShakeArgs>> CameraShakeEvent = new(nameof(CameraShakeEvent));
        public static readonly ValueKey<IEntity, UnityEngine.Transform> CameraPoint = new(nameof(CameraPoint));
        // How far the camera sits from this entity, as a multiple of the player's camera offset.
        // Without it, 1. A tank sets 1.25: it is bigger than a person and shoots further.
        public static readonly ValueKey<IEntity, float> CameraDistance = new(nameof(CameraDistance));

        #endregion

        #region Vehicle

        // Drive stick: x turns the hull (-1 left .. 1 right), y is the throttle (-1 reverse .. 1 forward).
        public static readonly ValueKey<IEntity, IReactiveVariable<Vector2>> DriveInput = new(nameof(DriveInput));
        // Boost held (Shift): the drive runs faster and picks up speed faster while true.
        public static readonly ValueKey<IEntity, IReactiveVariable<bool>> DriveBoost = new(nameof(DriveBoost));
        // Who drives it: the player context (PlayerVehicleController) or, later, an AI; null while nobody does.
        public static readonly ValueKey<IEntity, IReactiveVariable<IEntity>> Driver = new(nameof(Driver));
        // Yaw-only mount the cannon and its fire point hang from. Does not sway with the hull.
        public static readonly ValueKey<IEntity, UnityEngine.Transform> TurretPivot = new(nameof(TurretPivot));
        // The visible turret, under the swaying hull, copying the pivot's yaw.
        public static readonly ValueKey<IEntity, UnityEngine.Transform> TurretVisual = new(nameof(TurretVisual));
        // Degrees per second.
        public static readonly ValueKey<IEntity, IValue<float>> TurretRotationSpeed = new(nameof(TurretRotationSpeed));
        // The visible hull and running gear. Carries no colliders.
        public static readonly ValueKey<IEntity, UnityEngine.Transform> HullVisual = new(nameof(HullVisual));
        // Road wheels on springs: what TankSuspensionBehaviour found under each of them this step.
        public static readonly ValueKey<IEntity, Game.TankSuspension> Suspension = new(nameof(Suspension));
        // A second gun next to Weapon — the tank's turret machine gun, on the right mouse button.
        public static readonly ValueKey<IEntity, IReactiveVariable<IEntity>> SecondaryWeapon = new(nameof(SecondaryWeapon));
        public static readonly ValueKey<IEntity, IExpression<bool>> SecondaryFireCondition = new(nameof(SecondaryFireCondition));
        public static readonly ValueKey<IEntity, IAction> SecondaryFireAction = new(nameof(SecondaryFireAction));

        #endregion
    }
}
