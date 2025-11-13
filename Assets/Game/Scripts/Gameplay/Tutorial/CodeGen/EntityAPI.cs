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


		///Values
		public const int GameObject = 1482111001; // GameObject
		public const int Transform = -180157682; // Transform
		public const int Target = 1103309514; // Transform
		public const int LifeTime = 1688468960; // IValue<float>
		public const int MoveSpeed = 526065662; // IValue<float>
		public const int MoveDirection = -721923052; // IReactiveVariable<Vector3>
		public const int MoveCondition = 1466174948; // IExpression<bool>
		public const int MoveAction = 1225226561; // IAction<Vector3, float>
		public const int RotationSpeed = 1771316350; // IValue<float>
		public const int Weapon = 1855955664; // IEntity
		public const int FirePoint = 397255013; // Transform
		public const int Damage = 375673178; // IValue<int>
		public const int FireAction = 1186461126; // IAction
		public const int FireEvent = -1683597082; // BaseEvent
		public const int FireCondition = -280402907; // IFunction<bool>
		public const int Health = -915003867; // ReactiveInt
		public const int CollisionReceiver = 905037854; // CollisionEventReceiver
		public const int Animator = -1714818978; // Animator


		///Tag Extensions

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasDamageableTag(this IEntity obj) => obj.HasTag(Damageable);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddDamageableTag(this IEntity obj) => obj.AddTag(Damageable);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelDamageableTag(this IEntity obj) => obj.DelTag(Damageable);


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
		public static Transform GetTarget(this IEntity obj) => obj.GetValue<Transform>(Target);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetTarget(this IEntity obj, out Transform value) => obj.TryGetValue(Target, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddTarget(this IEntity obj, Transform value) => obj.AddValue(Target, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasTarget(this IEntity obj) => obj.HasValue(Target);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelTarget(this IEntity obj) => obj.DelValue(Target);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetTarget(this IEntity obj, Transform value) => obj.SetValue(Target, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IValue<float> GetLifeTime(this IEntity obj) => obj.GetValue<IValue<float>>(LifeTime);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetLifeTime(this IEntity obj, out IValue<float> value) => obj.TryGetValue(LifeTime, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddLifeTime(this IEntity obj, IValue<float> value) => obj.AddValue(LifeTime, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasLifeTime(this IEntity obj) => obj.HasValue(LifeTime);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelLifeTime(this IEntity obj) => obj.DelValue(LifeTime);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetLifeTime(this IEntity obj, IValue<float> value) => obj.SetValue(LifeTime, value);

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
		public static IEntity GetWeapon(this IEntity obj) => obj.GetValue<IEntity>(Weapon);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetWeapon(this IEntity obj, out IEntity value) => obj.TryGetValue(Weapon, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddWeapon(this IEntity obj, IEntity value) => obj.AddValue(Weapon, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasWeapon(this IEntity obj) => obj.HasValue(Weapon);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelWeapon(this IEntity obj) => obj.DelValue(Weapon);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetWeapon(this IEntity obj, IEntity value) => obj.SetValue(Weapon, value);

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
		public static IFunction<bool> GetFireCondition(this IEntity obj) => obj.GetValue<IFunction<bool>>(FireCondition);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetFireCondition(this IEntity obj, out IFunction<bool> value) => obj.TryGetValue(FireCondition, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddFireCondition(this IEntity obj, IFunction<bool> value) => obj.AddValue(FireCondition, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasFireCondition(this IEntity obj) => obj.HasValue(FireCondition);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelFireCondition(this IEntity obj) => obj.DelValue(FireCondition);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetFireCondition(this IEntity obj, IFunction<bool> value) => obj.SetValue(FireCondition, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ReactiveInt GetHealth(this IEntity obj) => obj.GetValue<ReactiveInt>(Health);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetHealth(this IEntity obj, out ReactiveInt value) => obj.TryGetValue(Health, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddHealth(this IEntity obj, ReactiveInt value) => obj.AddValue(Health, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasHealth(this IEntity obj) => obj.HasValue(Health);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelHealth(this IEntity obj) => obj.DelValue(Health);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetHealth(this IEntity obj, ReactiveInt value) => obj.SetValue(Health, value);

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
    }
}
