/**
* Code generation. Don't modify! 
**/

using Atomic.Entities;
using System.Runtime.CompilerServices;
using UnityEngine;
using Atomic.Entities;
using Atomic.Elements;
using System;
using Modules.Gameplay;

namespace Game
{
	public static class EntityAPI
	{
		///Tags
		public const int Damageable = 563499515;
		public const int Interactable = 1077199658;
		public const int Player = -1615495341;


		///Values
		public const int GameObject = 1482111001; // GameObject
		public const int Transform = -180157682; // Transform
		public const int LifeTime = 1688468960; // Cooldown
		public const int DestroyAction = 85938956; // IAction
		public const int MoveSpeed = 526065662; // IValue<float>
		public const int MoveDirection = -721923052; // IReactiveVariable<Vector3>
		public const int MoveCondition = 1466174948; // IExpression<bool>
		public const int MoveAction = 1225226561; // IAction<Vector3, float>
		public const int RotationSpeed = 1771316350; // IValue<float>
		public const int Ammo = 1337839892; // Ammo
		public const int WeaponId = -1822610762; // WeaponID
		public const int WeaponFireRate = -1052254408; // Cooldown
		public const int Weapon = 1855955664; // IReactiveVariable<IEntity>
		public const int WeaponRoot = 381533304; // Transform
		public const int FirePoint = 397255013; // Transform
		public const int Damage = 375673178; // IValue<int>
		public const int FireAction = 1186461126; // IAction
		public const int FireEvent = -1683597082; // BaseEvent
		public const int FireCondition = -280402907; // IExpression<bool>
		public const int MeleeAttackAction = -1365836177; // IAction
		public const int MeleeAttackEvent = 41534113; // BaseEvent
		public const int MeleeAttackCondition = -1770140745; // IExpression<bool>
		public const int Health = -915003867; // Health
		public const int CollisionReceiver = 905037854; // CollisionEventReceiver
		public const int TriggerEventReceiver = -484936241; // TriggerEventReceiver
		public const int RiggedBody = 1421993665; // Rigidbody
		public const int Animator = -1714818978; // Animator
		public const int WeaponAnimSet = 1176554500; // AnimSet
		public const int AnimationController = -1518513581; // RuntimeAnimatorController
		public const int AnimationEventReceiver = 1837262450; // AnimationEventReceiver
		public const int PickUpPrefab = 1763436596; // SceneEntity
		public const int ShowUIAction = 1409166592; // IAction<bool>
		public const int InteractAction = -1026843572; // IAction<IEntity>
		public const int IsInteract = -173365543; // IReactiveVariable<bool>
		public const int UITransform = 327940928; // Transform
		public const int TargetInteractable = -990542098; // IReactiveVariable<IEntity>
		public const int PatrolPoints = 587900176; // Transform[]
		public const int Target = 1103309514; // IReactiveVariable<IEntity>
		public const int AudioSource = 907064781; // AudioSource


		///Tag Extensions

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasDamageableTag(this IEntity obj) => obj.HasTag(Damageable);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddDamageableTag(this IEntity obj) => obj.AddTag(Damageable);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelDamageableTag(this IEntity obj) => obj.DelTag(Damageable);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasInteractableTag(this IEntity obj) => obj.HasTag(Interactable);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddInteractableTag(this IEntity obj) => obj.AddTag(Interactable);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelInteractableTag(this IEntity obj) => obj.DelTag(Interactable);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasPlayerTag(this IEntity obj) => obj.HasTag(Player);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddPlayerTag(this IEntity obj) => obj.AddTag(Player);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelPlayerTag(this IEntity obj) => obj.DelTag(Player);


		///Value Extensions

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static GameObject GetGameObject(this IEntity obj) => obj.GetValue<GameObject>(GameObject);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetGameObject(this IEntity obj, out GameObject value) => obj.TryGetValue(GameObject, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddGameObject(this IEntity obj, GameObject value) => obj.AddValue(GameObject, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasGameObject(this IEntity obj) => obj.HasValue(GameObject);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelGameObject(this IEntity obj) => obj.DelValue(GameObject);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetGameObject(this IEntity obj, GameObject value) => obj.SetValue(GameObject, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Transform GetTransform(this IEntity obj) => obj.GetValue<Transform>(Transform);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetTransform(this IEntity obj, out Transform value) => obj.TryGetValue(Transform, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddTransform(this IEntity obj, Transform value) => obj.AddValue(Transform, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasTransform(this IEntity obj) => obj.HasValue(Transform);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelTransform(this IEntity obj) => obj.DelValue(Transform);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetTransform(this IEntity obj, Transform value) => obj.SetValue(Transform, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Cooldown GetLifeTime(this IEntity obj) => obj.GetValue<Cooldown>(LifeTime);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetLifeTime(this IEntity obj, out Cooldown value) => obj.TryGetValue(LifeTime, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddLifeTime(this IEntity obj, Cooldown value) => obj.AddValue(LifeTime, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasLifeTime(this IEntity obj) => obj.HasValue(LifeTime);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelLifeTime(this IEntity obj) => obj.DelValue(LifeTime);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetLifeTime(this IEntity obj, Cooldown value) => obj.SetValue(LifeTime, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IAction GetDestroyAction(this IEntity obj) => obj.GetValue<IAction>(DestroyAction);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetDestroyAction(this IEntity obj, out IAction value) => obj.TryGetValue(DestroyAction, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddDestroyAction(this IEntity obj, IAction value) => obj.AddValue(DestroyAction, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasDestroyAction(this IEntity obj) => obj.HasValue(DestroyAction);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelDestroyAction(this IEntity obj) => obj.DelValue(DestroyAction);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetDestroyAction(this IEntity obj, IAction value) => obj.SetValue(DestroyAction, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IValue<float> GetMoveSpeed(this IEntity obj) => obj.GetValue<IValue<float>>(MoveSpeed);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetMoveSpeed(this IEntity obj, out IValue<float> value) => obj.TryGetValue(MoveSpeed, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddMoveSpeed(this IEntity obj, IValue<float> value) => obj.AddValue(MoveSpeed, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasMoveSpeed(this IEntity obj) => obj.HasValue(MoveSpeed);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelMoveSpeed(this IEntity obj) => obj.DelValue(MoveSpeed);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetMoveSpeed(this IEntity obj, IValue<float> value) => obj.SetValue(MoveSpeed, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IReactiveVariable<Vector3> GetMoveDirection(this IEntity obj) => obj.GetValue<IReactiveVariable<Vector3>>(MoveDirection);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetMoveDirection(this IEntity obj, out IReactiveVariable<Vector3> value) => obj.TryGetValue(MoveDirection, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddMoveDirection(this IEntity obj, IReactiveVariable<Vector3> value) => obj.AddValue(MoveDirection, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasMoveDirection(this IEntity obj) => obj.HasValue(MoveDirection);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelMoveDirection(this IEntity obj) => obj.DelValue(MoveDirection);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetMoveDirection(this IEntity obj, IReactiveVariable<Vector3> value) => obj.SetValue(MoveDirection, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IExpression<bool> GetMoveCondition(this IEntity obj) => obj.GetValue<IExpression<bool>>(MoveCondition);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetMoveCondition(this IEntity obj, out IExpression<bool> value) => obj.TryGetValue(MoveCondition, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddMoveCondition(this IEntity obj, IExpression<bool> value) => obj.AddValue(MoveCondition, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasMoveCondition(this IEntity obj) => obj.HasValue(MoveCondition);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelMoveCondition(this IEntity obj) => obj.DelValue(MoveCondition);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetMoveCondition(this IEntity obj, IExpression<bool> value) => obj.SetValue(MoveCondition, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IAction<Vector3, float> GetMoveAction(this IEntity obj) => obj.GetValue<IAction<Vector3, float>>(MoveAction);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetMoveAction(this IEntity obj, out IAction<Vector3, float> value) => obj.TryGetValue(MoveAction, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddMoveAction(this IEntity obj, IAction<Vector3, float> value) => obj.AddValue(MoveAction, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasMoveAction(this IEntity obj) => obj.HasValue(MoveAction);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelMoveAction(this IEntity obj) => obj.DelValue(MoveAction);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetMoveAction(this IEntity obj, IAction<Vector3, float> value) => obj.SetValue(MoveAction, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IValue<float> GetRotationSpeed(this IEntity obj) => obj.GetValue<IValue<float>>(RotationSpeed);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetRotationSpeed(this IEntity obj, out IValue<float> value) => obj.TryGetValue(RotationSpeed, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddRotationSpeed(this IEntity obj, IValue<float> value) => obj.AddValue(RotationSpeed, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasRotationSpeed(this IEntity obj) => obj.HasValue(RotationSpeed);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelRotationSpeed(this IEntity obj) => obj.DelValue(RotationSpeed);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetRotationSpeed(this IEntity obj, IValue<float> value) => obj.SetValue(RotationSpeed, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Ammo GetAmmo(this IEntity obj) => obj.GetValue<Ammo>(Ammo);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetAmmo(this IEntity obj, out Ammo value) => obj.TryGetValue(Ammo, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddAmmo(this IEntity obj, Ammo value) => obj.AddValue(Ammo, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasAmmo(this IEntity obj) => obj.HasValue(Ammo);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelAmmo(this IEntity obj) => obj.DelValue(Ammo);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetAmmo(this IEntity obj, Ammo value) => obj.SetValue(Ammo, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static WeaponID GetWeaponId(this IEntity obj) => obj.GetValue<WeaponID>(WeaponId);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetWeaponId(this IEntity obj, out WeaponID value) => obj.TryGetValue(WeaponId, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddWeaponId(this IEntity obj, WeaponID value) => obj.AddValue(WeaponId, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasWeaponId(this IEntity obj) => obj.HasValue(WeaponId);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelWeaponId(this IEntity obj) => obj.DelValue(WeaponId);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetWeaponId(this IEntity obj, WeaponID value) => obj.SetValue(WeaponId, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Cooldown GetWeaponFireRate(this IEntity obj) => obj.GetValue<Cooldown>(WeaponFireRate);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetWeaponFireRate(this IEntity obj, out Cooldown value) => obj.TryGetValue(WeaponFireRate, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddWeaponFireRate(this IEntity obj, Cooldown value) => obj.AddValue(WeaponFireRate, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasWeaponFireRate(this IEntity obj) => obj.HasValue(WeaponFireRate);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelWeaponFireRate(this IEntity obj) => obj.DelValue(WeaponFireRate);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetWeaponFireRate(this IEntity obj, Cooldown value) => obj.SetValue(WeaponFireRate, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IReactiveVariable<IEntity> GetWeapon(this IEntity obj) => obj.GetValue<IReactiveVariable<IEntity>>(Weapon);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetWeapon(this IEntity obj, out IReactiveVariable<IEntity> value) => obj.TryGetValue(Weapon, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddWeapon(this IEntity obj, IReactiveVariable<IEntity> value) => obj.AddValue(Weapon, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasWeapon(this IEntity obj) => obj.HasValue(Weapon);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelWeapon(this IEntity obj) => obj.DelValue(Weapon);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetWeapon(this IEntity obj, IReactiveVariable<IEntity> value) => obj.SetValue(Weapon, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Transform GetWeaponRoot(this IEntity obj) => obj.GetValue<Transform>(WeaponRoot);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetWeaponRoot(this IEntity obj, out Transform value) => obj.TryGetValue(WeaponRoot, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddWeaponRoot(this IEntity obj, Transform value) => obj.AddValue(WeaponRoot, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasWeaponRoot(this IEntity obj) => obj.HasValue(WeaponRoot);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelWeaponRoot(this IEntity obj) => obj.DelValue(WeaponRoot);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetWeaponRoot(this IEntity obj, Transform value) => obj.SetValue(WeaponRoot, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Transform GetFirePoint(this IEntity obj) => obj.GetValue<Transform>(FirePoint);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetFirePoint(this IEntity obj, out Transform value) => obj.TryGetValue(FirePoint, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddFirePoint(this IEntity obj, Transform value) => obj.AddValue(FirePoint, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasFirePoint(this IEntity obj) => obj.HasValue(FirePoint);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelFirePoint(this IEntity obj) => obj.DelValue(FirePoint);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetFirePoint(this IEntity obj, Transform value) => obj.SetValue(FirePoint, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IValue<int> GetDamage(this IEntity obj) => obj.GetValue<IValue<int>>(Damage);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetDamage(this IEntity obj, out IValue<int> value) => obj.TryGetValue(Damage, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddDamage(this IEntity obj, IValue<int> value) => obj.AddValue(Damage, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasDamage(this IEntity obj) => obj.HasValue(Damage);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelDamage(this IEntity obj) => obj.DelValue(Damage);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetDamage(this IEntity obj, IValue<int> value) => obj.SetValue(Damage, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IAction GetFireAction(this IEntity obj) => obj.GetValue<IAction>(FireAction);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetFireAction(this IEntity obj, out IAction value) => obj.TryGetValue(FireAction, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddFireAction(this IEntity obj, IAction value) => obj.AddValue(FireAction, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasFireAction(this IEntity obj) => obj.HasValue(FireAction);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelFireAction(this IEntity obj) => obj.DelValue(FireAction);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetFireAction(this IEntity obj, IAction value) => obj.SetValue(FireAction, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static BaseEvent GetFireEvent(this IEntity obj) => obj.GetValue<BaseEvent>(FireEvent);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetFireEvent(this IEntity obj, out BaseEvent value) => obj.TryGetValue(FireEvent, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddFireEvent(this IEntity obj, BaseEvent value) => obj.AddValue(FireEvent, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasFireEvent(this IEntity obj) => obj.HasValue(FireEvent);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelFireEvent(this IEntity obj) => obj.DelValue(FireEvent);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetFireEvent(this IEntity obj, BaseEvent value) => obj.SetValue(FireEvent, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IExpression<bool> GetFireCondition(this IEntity obj) => obj.GetValue<IExpression<bool>>(FireCondition);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetFireCondition(this IEntity obj, out IExpression<bool> value) => obj.TryGetValue(FireCondition, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddFireCondition(this IEntity obj, IExpression<bool> value) => obj.AddValue(FireCondition, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasFireCondition(this IEntity obj) => obj.HasValue(FireCondition);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelFireCondition(this IEntity obj) => obj.DelValue(FireCondition);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetFireCondition(this IEntity obj, IExpression<bool> value) => obj.SetValue(FireCondition, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IAction GetMeleeAttackAction(this IEntity obj) => obj.GetValue<IAction>(MeleeAttackAction);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetMeleeAttackAction(this IEntity obj, out IAction value) => obj.TryGetValue(MeleeAttackAction, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddMeleeAttackAction(this IEntity obj, IAction value) => obj.AddValue(MeleeAttackAction, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasMeleeAttackAction(this IEntity obj) => obj.HasValue(MeleeAttackAction);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelMeleeAttackAction(this IEntity obj) => obj.DelValue(MeleeAttackAction);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetMeleeAttackAction(this IEntity obj, IAction value) => obj.SetValue(MeleeAttackAction, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static BaseEvent GetMeleeAttackEvent(this IEntity obj) => obj.GetValue<BaseEvent>(MeleeAttackEvent);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetMeleeAttackEvent(this IEntity obj, out BaseEvent value) => obj.TryGetValue(MeleeAttackEvent, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddMeleeAttackEvent(this IEntity obj, BaseEvent value) => obj.AddValue(MeleeAttackEvent, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasMeleeAttackEvent(this IEntity obj) => obj.HasValue(MeleeAttackEvent);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelMeleeAttackEvent(this IEntity obj) => obj.DelValue(MeleeAttackEvent);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetMeleeAttackEvent(this IEntity obj, BaseEvent value) => obj.SetValue(MeleeAttackEvent, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IExpression<bool> GetMeleeAttackCondition(this IEntity obj) => obj.GetValue<IExpression<bool>>(MeleeAttackCondition);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetMeleeAttackCondition(this IEntity obj, out IExpression<bool> value) => obj.TryGetValue(MeleeAttackCondition, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddMeleeAttackCondition(this IEntity obj, IExpression<bool> value) => obj.AddValue(MeleeAttackCondition, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasMeleeAttackCondition(this IEntity obj) => obj.HasValue(MeleeAttackCondition);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelMeleeAttackCondition(this IEntity obj) => obj.DelValue(MeleeAttackCondition);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetMeleeAttackCondition(this IEntity obj, IExpression<bool> value) => obj.SetValue(MeleeAttackCondition, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Health GetHealth(this IEntity obj) => obj.GetValue<Health>(Health);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetHealth(this IEntity obj, out Health value) => obj.TryGetValue(Health, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddHealth(this IEntity obj, Health value) => obj.AddValue(Health, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasHealth(this IEntity obj) => obj.HasValue(Health);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelHealth(this IEntity obj) => obj.DelValue(Health);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetHealth(this IEntity obj, Health value) => obj.SetValue(Health, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static CollisionEventReceiver GetCollisionReceiver(this IEntity obj) => obj.GetValue<CollisionEventReceiver>(CollisionReceiver);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetCollisionReceiver(this IEntity obj, out CollisionEventReceiver value) => obj.TryGetValue(CollisionReceiver, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddCollisionReceiver(this IEntity obj, CollisionEventReceiver value) => obj.AddValue(CollisionReceiver, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasCollisionReceiver(this IEntity obj) => obj.HasValue(CollisionReceiver);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelCollisionReceiver(this IEntity obj) => obj.DelValue(CollisionReceiver);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetCollisionReceiver(this IEntity obj, CollisionEventReceiver value) => obj.SetValue(CollisionReceiver, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static TriggerEventReceiver GetTriggerEventReceiver(this IEntity obj) => obj.GetValue<TriggerEventReceiver>(TriggerEventReceiver);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetTriggerEventReceiver(this IEntity obj, out TriggerEventReceiver value) => obj.TryGetValue(TriggerEventReceiver, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddTriggerEventReceiver(this IEntity obj, TriggerEventReceiver value) => obj.AddValue(TriggerEventReceiver, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasTriggerEventReceiver(this IEntity obj) => obj.HasValue(TriggerEventReceiver);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelTriggerEventReceiver(this IEntity obj) => obj.DelValue(TriggerEventReceiver);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetTriggerEventReceiver(this IEntity obj, TriggerEventReceiver value) => obj.SetValue(TriggerEventReceiver, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Rigidbody GetRiggedBody(this IEntity obj) => obj.GetValue<Rigidbody>(RiggedBody);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetRiggedBody(this IEntity obj, out Rigidbody value) => obj.TryGetValue(RiggedBody, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddRiggedBody(this IEntity obj, Rigidbody value) => obj.AddValue(RiggedBody, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasRiggedBody(this IEntity obj) => obj.HasValue(RiggedBody);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelRiggedBody(this IEntity obj) => obj.DelValue(RiggedBody);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetRiggedBody(this IEntity obj, Rigidbody value) => obj.SetValue(RiggedBody, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Animator GetAnimator(this IEntity obj) => obj.GetValue<Animator>(Animator);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetAnimator(this IEntity obj, out Animator value) => obj.TryGetValue(Animator, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddAnimator(this IEntity obj, Animator value) => obj.AddValue(Animator, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasAnimator(this IEntity obj) => obj.HasValue(Animator);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelAnimator(this IEntity obj) => obj.DelValue(Animator);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetAnimator(this IEntity obj, Animator value) => obj.SetValue(Animator, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static AnimSet GetWeaponAnimSet(this IEntity obj) => obj.GetValue<AnimSet>(WeaponAnimSet);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetWeaponAnimSet(this IEntity obj, out AnimSet value) => obj.TryGetValue(WeaponAnimSet, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddWeaponAnimSet(this IEntity obj, AnimSet value) => obj.AddValue(WeaponAnimSet, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasWeaponAnimSet(this IEntity obj) => obj.HasValue(WeaponAnimSet);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelWeaponAnimSet(this IEntity obj) => obj.DelValue(WeaponAnimSet);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetWeaponAnimSet(this IEntity obj, AnimSet value) => obj.SetValue(WeaponAnimSet, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static RuntimeAnimatorController GetAnimationController(this IEntity obj) => obj.GetValue<RuntimeAnimatorController>(AnimationController);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetAnimationController(this IEntity obj, out RuntimeAnimatorController value) => obj.TryGetValue(AnimationController, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddAnimationController(this IEntity obj, RuntimeAnimatorController value) => obj.AddValue(AnimationController, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasAnimationController(this IEntity obj) => obj.HasValue(AnimationController);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelAnimationController(this IEntity obj) => obj.DelValue(AnimationController);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetAnimationController(this IEntity obj, RuntimeAnimatorController value) => obj.SetValue(AnimationController, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static AnimationEventReceiver GetAnimationEventReceiver(this IEntity obj) => obj.GetValue<AnimationEventReceiver>(AnimationEventReceiver);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetAnimationEventReceiver(this IEntity obj, out AnimationEventReceiver value) => obj.TryGetValue(AnimationEventReceiver, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddAnimationEventReceiver(this IEntity obj, AnimationEventReceiver value) => obj.AddValue(AnimationEventReceiver, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasAnimationEventReceiver(this IEntity obj) => obj.HasValue(AnimationEventReceiver);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelAnimationEventReceiver(this IEntity obj) => obj.DelValue(AnimationEventReceiver);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetAnimationEventReceiver(this IEntity obj, AnimationEventReceiver value) => obj.SetValue(AnimationEventReceiver, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static SceneEntity GetPickUpPrefab(this IEntity obj) => obj.GetValue<SceneEntity>(PickUpPrefab);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetPickUpPrefab(this IEntity obj, out SceneEntity value) => obj.TryGetValue(PickUpPrefab, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddPickUpPrefab(this IEntity obj, SceneEntity value) => obj.AddValue(PickUpPrefab, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasPickUpPrefab(this IEntity obj) => obj.HasValue(PickUpPrefab);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelPickUpPrefab(this IEntity obj) => obj.DelValue(PickUpPrefab);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetPickUpPrefab(this IEntity obj, SceneEntity value) => obj.SetValue(PickUpPrefab, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IAction<bool> GetShowUIAction(this IEntity obj) => obj.GetValue<IAction<bool>>(ShowUIAction);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetShowUIAction(this IEntity obj, out IAction<bool> value) => obj.TryGetValue(ShowUIAction, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddShowUIAction(this IEntity obj, IAction<bool> value) => obj.AddValue(ShowUIAction, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasShowUIAction(this IEntity obj) => obj.HasValue(ShowUIAction);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelShowUIAction(this IEntity obj) => obj.DelValue(ShowUIAction);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetShowUIAction(this IEntity obj, IAction<bool> value) => obj.SetValue(ShowUIAction, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IAction<IEntity> GetInteractAction(this IEntity obj) => obj.GetValue<IAction<IEntity>>(InteractAction);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetInteractAction(this IEntity obj, out IAction<IEntity> value) => obj.TryGetValue(InteractAction, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddInteractAction(this IEntity obj, IAction<IEntity> value) => obj.AddValue(InteractAction, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasInteractAction(this IEntity obj) => obj.HasValue(InteractAction);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelInteractAction(this IEntity obj) => obj.DelValue(InteractAction);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetInteractAction(this IEntity obj, IAction<IEntity> value) => obj.SetValue(InteractAction, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IReactiveVariable<bool> GetIsInteract(this IEntity obj) => obj.GetValue<IReactiveVariable<bool>>(IsInteract);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetIsInteract(this IEntity obj, out IReactiveVariable<bool> value) => obj.TryGetValue(IsInteract, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddIsInteract(this IEntity obj, IReactiveVariable<bool> value) => obj.AddValue(IsInteract, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasIsInteract(this IEntity obj) => obj.HasValue(IsInteract);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelIsInteract(this IEntity obj) => obj.DelValue(IsInteract);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetIsInteract(this IEntity obj, IReactiveVariable<bool> value) => obj.SetValue(IsInteract, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Transform GetUITransform(this IEntity obj) => obj.GetValue<Transform>(UITransform);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetUITransform(this IEntity obj, out Transform value) => obj.TryGetValue(UITransform, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddUITransform(this IEntity obj, Transform value) => obj.AddValue(UITransform, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasUITransform(this IEntity obj) => obj.HasValue(UITransform);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelUITransform(this IEntity obj) => obj.DelValue(UITransform);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetUITransform(this IEntity obj, Transform value) => obj.SetValue(UITransform, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IReactiveVariable<IEntity> GetTargetInteractable(this IEntity obj) => obj.GetValue<IReactiveVariable<IEntity>>(TargetInteractable);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetTargetInteractable(this IEntity obj, out IReactiveVariable<IEntity> value) => obj.TryGetValue(TargetInteractable, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddTargetInteractable(this IEntity obj, IReactiveVariable<IEntity> value) => obj.AddValue(TargetInteractable, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasTargetInteractable(this IEntity obj) => obj.HasValue(TargetInteractable);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelTargetInteractable(this IEntity obj) => obj.DelValue(TargetInteractable);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetTargetInteractable(this IEntity obj, IReactiveVariable<IEntity> value) => obj.SetValue(TargetInteractable, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Transform[] GetPatrolPoints(this IEntity obj) => obj.GetValue<Transform[]>(PatrolPoints);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetPatrolPoints(this IEntity obj, out Transform[] value) => obj.TryGetValue(PatrolPoints, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddPatrolPoints(this IEntity obj, Transform[] value) => obj.AddValue(PatrolPoints, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasPatrolPoints(this IEntity obj) => obj.HasValue(PatrolPoints);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelPatrolPoints(this IEntity obj) => obj.DelValue(PatrolPoints);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetPatrolPoints(this IEntity obj, Transform[] value) => obj.SetValue(PatrolPoints, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IReactiveVariable<IEntity> GetTarget(this IEntity obj) => obj.GetValue<IReactiveVariable<IEntity>>(Target);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetTarget(this IEntity obj, out IReactiveVariable<IEntity> value) => obj.TryGetValue(Target, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddTarget(this IEntity obj, IReactiveVariable<IEntity> value) => obj.AddValue(Target, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasTarget(this IEntity obj) => obj.HasValue(Target);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelTarget(this IEntity obj) => obj.DelValue(Target);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetTarget(this IEntity obj, IReactiveVariable<IEntity> value) => obj.SetValue(Target, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static AudioSource GetAudioSource(this IEntity obj) => obj.GetValue<AudioSource>(AudioSource);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetAudioSource(this IEntity obj, out AudioSource value) => obj.TryGetValue(AudioSource, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddAudioSource(this IEntity obj, AudioSource value) => obj.AddValue(AudioSource, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasAudioSource(this IEntity obj) => obj.HasValue(AudioSource);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelAudioSource(this IEntity obj) => obj.DelValue(AudioSource);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetAudioSource(this IEntity obj, AudioSource value) => obj.SetValue(AudioSource, value);
    }
}
