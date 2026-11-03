using Atomic.Elements;
using Atomic.Entities;
using HighlightPlus;
using Modules.Gameplay;
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
        public static readonly TagKey<IEntity> Interactable = new(nameof(Interactable));
        public static readonly TagKey<IEntity> Jumpable = new(nameof(Jumpable));
        public static readonly TagKey<IEntity> Player = new(nameof(Player));
        public static readonly TagKey<IEntity> Enemy = new(nameof(Enemy));
        public static readonly TagKey<IEntity> MeleeWeapon = new(nameof(MeleeWeapon));
        public static readonly TagKey<IEntity> RangeWeapon = new(nameof(RangeWeapon));

        #endregion

        #region Core

        public static readonly ValueKey<IEntity, string> EntityID = new(nameof(EntityID));
        public static readonly ValueKey<IEntity, UnityEngine.GameObject> GameObject = new(nameof(GameObject));
        public static readonly ValueKey<IEntity, UnityEngine.Transform> Transform = new(nameof(Transform));

        // Modules.Gameplay.Cooldown, NOT the new Atomic.Elements.Cooldown.
        public static readonly ValueKey<IEntity, Modules.Gameplay.Cooldown> LifeTime = new(nameof(LifeTime));
        public static readonly ValueKey<IEntity, IAction> DestroyAction = new(nameof(DestroyAction));

        #endregion

        #region Movement

        public static readonly ValueKey<IEntity, IReactiveVariable<float>> MoveSpeed = new(nameof(MoveSpeed));
        public static readonly ValueKey<IEntity, IReactiveVariable<Vector3>> MoveDirection = new(nameof(MoveDirection));
        public static readonly ValueKey<IEntity, IReactiveVariable<Vector3>> RotateDirection = new(nameof(RotateDirection));
        public static readonly ValueKey<IEntity, IExpression<bool>> MoveCondition = new(nameof(MoveCondition));
        public static readonly ValueKey<IEntity, IAction<Vector3, float>> MoveAction = new(nameof(MoveAction));
        public static readonly ValueKey<IEntity, IValue<float>> RotationSpeed = new(nameof(RotationSpeed));
        public static readonly ValueKey<IEntity, IReactiveVariable<float>> Velocity = new(nameof(Velocity));

        #endregion

        #region Jump

        // was BaseFunction<bool>, which is gone; IFunction<bool> is the surviving abstraction.
        public static readonly ValueKey<IEntity, IFunction<bool>> JumpCondition = new(nameof(JumpCondition));

        /// <summary>
        /// How many sources currently grant jumping (effect areas, buffs). The jump values are
        /// added when it reaches one and removed when it falls back to zero, so two overlapping
        /// grants no longer let the first release take jumping away from the second.
        /// </summary>
        public static readonly ValueKey<IEntity, int> JumpSourceCount = new(nameof(JumpSourceCount));
        public static readonly ValueKey<IEntity, float> JumpForce = new(nameof(JumpForce));
        public static readonly ValueKey<IEntity, IEvent> JumpEvent = new(nameof(JumpEvent));

        #endregion

        #region Weapon

        public static readonly ValueKey<IEntity, MonoEntity> BulletPrefab = new(nameof(BulletPrefab));
        public static readonly ValueKey<IEntity, MonoEntity> ShellPrefab = new(nameof(ShellPrefab));
        public static readonly ValueKey<IEntity, MonoEntity> BulletHitPrefab = new(nameof(BulletHitPrefab));
        public static readonly ValueKey<IEntity, Modules.Gameplay.Ammo> Ammo = new(nameof(Ammo));
        public static readonly ValueKey<IEntity, WeaponID> WeaponId = new(nameof(WeaponId));
        public static readonly ValueKey<IEntity, Modules.Gameplay.Cooldown> WeaponCooldown = new(nameof(WeaponCooldown));
        public static readonly ValueKey<IEntity, IReactiveVariable<IEntity>> Weapon = new(nameof(Weapon));
        public static readonly ValueKey<IEntity, IReactiveVariable<IEntity>> HandWeapon = new(nameof(HandWeapon));
        public static readonly ValueKey<IEntity, UnityEngine.Transform> WeaponRoot = new(nameof(WeaponRoot));
        public static readonly ValueKey<IEntity, UnityEngine.Transform> FirePoint = new(nameof(FirePoint));
        public static readonly ValueKey<IEntity, UnityEngine.Transform> ShellPoint = new(nameof(ShellPoint));
        public static readonly ValueKey<IEntity, IReactiveVariable<int>> Damage = new(nameof(Damage));
        public static readonly ValueKey<IEntity, IExpression<int>> ExtraDamage = new(nameof(ExtraDamage));
        public static readonly ValueKey<IEntity, IValue<float>> DamageRadius = new(nameof(DamageRadius));
        public static readonly ValueKey<IEntity, LayerMask> DamageLayer = new(nameof(DamageLayer));
        public static readonly ValueKey<IEntity, IReactiveVariable<bool>> DamageCastEnabled = new(nameof(DamageCastEnabled));
        public static readonly ValueKey<IEntity, IAction> FireAction = new(nameof(FireAction));
        public static readonly ValueKey<IEntity, IEvent> FireEvent = new(nameof(FireEvent));
        public static readonly ValueKey<IEntity, IExpression<bool>> FireCondition = new(nameof(FireCondition));

        #endregion

        #region Health

        public static readonly ValueKey<IEntity, Modules.Gameplay.Health> Health = new(nameof(Health));
        public static readonly ValueKey<IEntity, IEvent> DeathEvent = new(nameof(DeathEvent));
        public static readonly ValueKey<IEntity, IAction> DeathAction = new(nameof(DeathAction));
        public static readonly ValueKey<IEntity, IEvent<TakeDamageArgs>> DamageTakenEvent = new(nameof(DamageTakenEvent));
        public static readonly ValueKey<IEntity, IEvent<TakeDamageArgs>> DeathTakenEvent = new(nameof(DeathTakenEvent));

        #endregion

        #region Physics

        public static readonly ValueKey<IEntity, CollisionEventReceiver> CollisionReceiver = new(nameof(CollisionReceiver));
        public static readonly ValueKey<IEntity, Game.TriggerEventReceiver> TriggerEventReceiver = new(nameof(TriggerEventReceiver));
        public static readonly ValueKey<IEntity, Rigidbody> RiggedBody = new(nameof(RiggedBody));

        #endregion

        #region Animation

        public static readonly ValueKey<IEntity, UnityEngine.Animator> Animator = new(nameof(Animator));
        public static readonly ValueKey<IEntity, RuntimeAnimatorController> AnimationController = new(nameof(AnimationController));
        public static readonly ValueKey<IEntity, Modules.Gameplay.AnimationEventReceiver> AnimationEventReceiver = new(nameof(AnimationEventReceiver));

        #endregion

        #region Interact

        public static readonly ValueKey<IEntity, MonoEntity> PickUpPrefab = new(nameof(PickUpPrefab));
        public static readonly ValueKey<IEntity, IAction<bool>> ShowUIAction = new(nameof(ShowUIAction));
        public static readonly ValueKey<IEntity, IAction<IEntity>> InteractAction = new(nameof(InteractAction));
        public static readonly ValueKey<IEntity, IReactiveVariable<bool>> IsInteract = new(nameof(IsInteract));
        public static readonly ValueKey<IEntity, UnityEngine.Transform> UITransform = new(nameof(UITransform));
        public static readonly ValueKey<IEntity, IReactiveVariable<IEntity>> TargetInteractable = new(nameof(TargetInteractable));
        public static readonly ValueKey<IEntity, IEvent> PickUpEvent = new(nameof(PickUpEvent));
        public static readonly ValueKey<IEntity, IEvent> DropEvent = new(nameof(DropEvent));
        public static readonly ValueKey<IEntity, Vector3> PositionOffset = new(nameof(PositionOffset));
        public static readonly ValueKey<IEntity, HighlightEffect> Highlight = new(nameof(Highlight));

        #endregion

        #region AI

        public static readonly ValueKey<IEntity, UnityEngine.Transform[]> PatrolPoints = new(nameof(PatrolPoints));
        public static readonly ValueKey<IEntity, IReactiveVariable<IEntity>> Target = new(nameof(Target));

        #endregion

        #region Sound

        public static readonly ValueKey<IEntity, UnityEngine.AudioSource> AudioSource = new(nameof(AudioSource));

        #endregion

        #region Camera

        public static readonly ValueKey<IEntity, Game.CameraShakeArgs> CameraShakeArgs = new(nameof(CameraShakeArgs));
        public static readonly ValueKey<IEntity, IEvent<Game.CameraShakeArgs>> CameraShakeEvent = new(nameof(CameraShakeEvent));
        public static readonly ValueKey<IEntity, UnityEngine.Transform> CameraPoint = new(nameof(CameraPoint));

        #endregion

        #region Buffs

        public static readonly ValueKey<IEntity, IReactiveList<BuffBase>> BuffsEffects = new(nameof(BuffsEffects));

        #endregion
    }
}
