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
using HighlightPlus;

namespace Game
{
	public static class AbilityAPI
	{
		///Tags
		public const int BaseAbility = -306370659;
		public const int PointAbility = 1926569125;
		public const int TargetAbility = -1187668890;


		///Values
		public const int BaseAction = 942602042; // IAction
		public const int BaseCondition = -395139187; // IFunction<bool>
		public const int BaseEvent = -1277683221; // IEvent
		public const int Charges = -179148410; // IReactiveVariable<int>
		public const int ManaCost = -175662323; // IValue<int>
		public const int Radius = 1020291948; // IValue<float>
		public const int PointAction = -269391236; // IAction<Vector3>
		public const int PointCondition = 1323724090; // IFunction<Vector3,bool>
		public const int PointEvent = 1637331210; // IEvent<Vector3>
		public const int TargetAction = 1858217420; // IAction<IEntity>
		public const int TargetCondition = 1133093239; // IFunction<IEntity,bool>
		public const int TargetEvent = -321991458; // IEvent<IEntity>


		///Tag Extensions

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasBaseAbilityTag(this Ability obj) => obj.HasTag(BaseAbility);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddBaseAbilityTag(this Ability obj) => obj.AddTag(BaseAbility);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelBaseAbilityTag(this Ability obj) => obj.DelTag(BaseAbility);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasPointAbilityTag(this Ability obj) => obj.HasTag(PointAbility);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddPointAbilityTag(this Ability obj) => obj.AddTag(PointAbility);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelPointAbilityTag(this Ability obj) => obj.DelTag(PointAbility);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasTargetAbilityTag(this Ability obj) => obj.HasTag(TargetAbility);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddTargetAbilityTag(this Ability obj) => obj.AddTag(TargetAbility);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelTargetAbilityTag(this Ability obj) => obj.DelTag(TargetAbility);


		///Value Extensions

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IAction GetBaseAction(this Ability obj) => obj.GetValue<IAction>(BaseAction);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetBaseAction(this Ability obj, out IAction value) => obj.TryGetValue(BaseAction, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddBaseAction(this Ability obj, IAction value) => obj.AddValue(BaseAction, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasBaseAction(this Ability obj) => obj.HasValue(BaseAction);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelBaseAction(this Ability obj) => obj.DelValue(BaseAction);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetBaseAction(this Ability obj, IAction value) => obj.SetValue(BaseAction, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IFunction<bool> GetBaseCondition(this Ability obj) => obj.GetValue<IFunction<bool>>(BaseCondition);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetBaseCondition(this Ability obj, out IFunction<bool> value) => obj.TryGetValue(BaseCondition, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddBaseCondition(this Ability obj, IFunction<bool> value) => obj.AddValue(BaseCondition, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasBaseCondition(this Ability obj) => obj.HasValue(BaseCondition);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelBaseCondition(this Ability obj) => obj.DelValue(BaseCondition);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetBaseCondition(this Ability obj, IFunction<bool> value) => obj.SetValue(BaseCondition, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEvent GetBaseEvent(this Ability obj) => obj.GetValue<IEvent>(BaseEvent);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetBaseEvent(this Ability obj, out IEvent value) => obj.TryGetValue(BaseEvent, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddBaseEvent(this Ability obj, IEvent value) => obj.AddValue(BaseEvent, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasBaseEvent(this Ability obj) => obj.HasValue(BaseEvent);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelBaseEvent(this Ability obj) => obj.DelValue(BaseEvent);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetBaseEvent(this Ability obj, IEvent value) => obj.SetValue(BaseEvent, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IReactiveVariable<int> GetCharges(this Ability obj) => obj.GetValue<IReactiveVariable<int>>(Charges);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetCharges(this Ability obj, out IReactiveVariable<int> value) => obj.TryGetValue(Charges, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddCharges(this Ability obj, IReactiveVariable<int> value) => obj.AddValue(Charges, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasCharges(this Ability obj) => obj.HasValue(Charges);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelCharges(this Ability obj) => obj.DelValue(Charges);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetCharges(this Ability obj, IReactiveVariable<int> value) => obj.SetValue(Charges, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IValue<int> GetManaCost(this Ability obj) => obj.GetValue<IValue<int>>(ManaCost);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetManaCost(this Ability obj, out IValue<int> value) => obj.TryGetValue(ManaCost, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddManaCost(this Ability obj, IValue<int> value) => obj.AddValue(ManaCost, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasManaCost(this Ability obj) => obj.HasValue(ManaCost);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelManaCost(this Ability obj) => obj.DelValue(ManaCost);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetManaCost(this Ability obj, IValue<int> value) => obj.SetValue(ManaCost, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IValue<float> GetRadius(this Ability obj) => obj.GetValue<IValue<float>>(Radius);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetRadius(this Ability obj, out IValue<float> value) => obj.TryGetValue(Radius, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddRadius(this Ability obj, IValue<float> value) => obj.AddValue(Radius, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasRadius(this Ability obj) => obj.HasValue(Radius);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelRadius(this Ability obj) => obj.DelValue(Radius);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetRadius(this Ability obj, IValue<float> value) => obj.SetValue(Radius, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IAction<Vector3> GetPointAction(this Ability obj) => obj.GetValue<IAction<Vector3>>(PointAction);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetPointAction(this Ability obj, out IAction<Vector3> value) => obj.TryGetValue(PointAction, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddPointAction(this Ability obj, IAction<Vector3> value) => obj.AddValue(PointAction, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasPointAction(this Ability obj) => obj.HasValue(PointAction);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelPointAction(this Ability obj) => obj.DelValue(PointAction);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetPointAction(this Ability obj, IAction<Vector3> value) => obj.SetValue(PointAction, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IFunction<Vector3,bool> GetPointCondition(this Ability obj) => obj.GetValue<IFunction<Vector3,bool>>(PointCondition);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetPointCondition(this Ability obj, out IFunction<Vector3,bool> value) => obj.TryGetValue(PointCondition, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddPointCondition(this Ability obj, IFunction<Vector3,bool> value) => obj.AddValue(PointCondition, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasPointCondition(this Ability obj) => obj.HasValue(PointCondition);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelPointCondition(this Ability obj) => obj.DelValue(PointCondition);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetPointCondition(this Ability obj, IFunction<Vector3,bool> value) => obj.SetValue(PointCondition, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEvent<Vector3> GetPointEvent(this Ability obj) => obj.GetValue<IEvent<Vector3>>(PointEvent);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetPointEvent(this Ability obj, out IEvent<Vector3> value) => obj.TryGetValue(PointEvent, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddPointEvent(this Ability obj, IEvent<Vector3> value) => obj.AddValue(PointEvent, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasPointEvent(this Ability obj) => obj.HasValue(PointEvent);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelPointEvent(this Ability obj) => obj.DelValue(PointEvent);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetPointEvent(this Ability obj, IEvent<Vector3> value) => obj.SetValue(PointEvent, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IAction<IEntity> GetTargetAction(this Ability obj) => obj.GetValue<IAction<IEntity>>(TargetAction);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetTargetAction(this Ability obj, out IAction<IEntity> value) => obj.TryGetValue(TargetAction, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddTargetAction(this Ability obj, IAction<IEntity> value) => obj.AddValue(TargetAction, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasTargetAction(this Ability obj) => obj.HasValue(TargetAction);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelTargetAction(this Ability obj) => obj.DelValue(TargetAction);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetTargetAction(this Ability obj, IAction<IEntity> value) => obj.SetValue(TargetAction, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IFunction<IEntity,bool> GetTargetCondition(this Ability obj) => obj.GetValue<IFunction<IEntity,bool>>(TargetCondition);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetTargetCondition(this Ability obj, out IFunction<IEntity,bool> value) => obj.TryGetValue(TargetCondition, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddTargetCondition(this Ability obj, IFunction<IEntity,bool> value) => obj.AddValue(TargetCondition, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasTargetCondition(this Ability obj) => obj.HasValue(TargetCondition);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelTargetCondition(this Ability obj) => obj.DelValue(TargetCondition);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetTargetCondition(this Ability obj, IFunction<IEntity,bool> value) => obj.SetValue(TargetCondition, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEvent<IEntity> GetTargetEvent(this Ability obj) => obj.GetValue<IEvent<IEntity>>(TargetEvent);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetTargetEvent(this Ability obj, out IEvent<IEntity> value) => obj.TryGetValue(TargetEvent, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddTargetEvent(this Ability obj, IEvent<IEntity> value) => obj.AddValue(TargetEvent, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasTargetEvent(this Ability obj) => obj.HasValue(TargetEvent);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelTargetEvent(this Ability obj) => obj.DelValue(TargetEvent);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetTargetEvent(this Ability obj, IEvent<IEntity> value) => obj.SetValue(TargetEvent, value);
    }
}
