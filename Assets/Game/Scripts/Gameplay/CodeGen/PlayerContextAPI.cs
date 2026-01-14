/**
* Code generation. Don't modify! 
**/

using Atomic.Contexts;
using System.Runtime.CompilerServices;
using System;
using UnityEngine;
using Atomic.Contexts;
using Atomic.Entities;
using Atomic.Elements;
using Modules.Common;

namespace Game
{
	public static class PlayerContextAPI
	{


		///Values
		public const int Character = 294335127; // IReactiveVariable<IEntity>
		public const int Camera = 1018227507; // IEntity
		public const int CameraOffset = -1286660539; // IValue<Vector3>
		public const int CameraRoot = 977661012; // Transform
		public const int CameraSpeed = -1615506830; // IValue<int>
		public const int MoveJoystick = -1686028204; // Joystick
		public const int RotateJoystick = 17434633; // Joystick
		public const int MaxMana = 1394248230; // IValue<int>
		public const int CurrentMana = 49250327; // IReactiveVariable<int>
		public const int Abilities = 986255111; // IReactiveDictionary<string, Ability>


		///Value Extensions

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IReactiveVariable<IEntity> GetCharacter(this IPlayerContext obj) => obj.GetValue<IReactiveVariable<IEntity>>(Character);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetCharacter(this IPlayerContext obj, out IReactiveVariable<IEntity> value) => obj.TryGetValue(Character, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddCharacter(this IPlayerContext obj, IReactiveVariable<IEntity> value) => obj.AddValue(Character, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasCharacter(this IPlayerContext obj) => obj.HasValue(Character);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelCharacter(this IPlayerContext obj) => obj.DelValue(Character);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetCharacter(this IPlayerContext obj, IReactiveVariable<IEntity> value) => obj.SetValue(Character, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEntity GetCamera(this IPlayerContext obj) => obj.GetValue<IEntity>(Camera);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetCamera(this IPlayerContext obj, out IEntity value) => obj.TryGetValue(Camera, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddCamera(this IPlayerContext obj, IEntity value) => obj.AddValue(Camera, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasCamera(this IPlayerContext obj) => obj.HasValue(Camera);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelCamera(this IPlayerContext obj) => obj.DelValue(Camera);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetCamera(this IPlayerContext obj, IEntity value) => obj.SetValue(Camera, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IValue<Vector3> GetCameraOffset(this IPlayerContext obj) => obj.GetValue<IValue<Vector3>>(CameraOffset);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetCameraOffset(this IPlayerContext obj, out IValue<Vector3> value) => obj.TryGetValue(CameraOffset, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddCameraOffset(this IPlayerContext obj, IValue<Vector3> value) => obj.AddValue(CameraOffset, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasCameraOffset(this IPlayerContext obj) => obj.HasValue(CameraOffset);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelCameraOffset(this IPlayerContext obj) => obj.DelValue(CameraOffset);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetCameraOffset(this IPlayerContext obj, IValue<Vector3> value) => obj.SetValue(CameraOffset, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Transform GetCameraRoot(this IPlayerContext obj) => obj.GetValue<Transform>(CameraRoot);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetCameraRoot(this IPlayerContext obj, out Transform value) => obj.TryGetValue(CameraRoot, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddCameraRoot(this IPlayerContext obj, Transform value) => obj.AddValue(CameraRoot, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasCameraRoot(this IPlayerContext obj) => obj.HasValue(CameraRoot);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelCameraRoot(this IPlayerContext obj) => obj.DelValue(CameraRoot);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetCameraRoot(this IPlayerContext obj, Transform value) => obj.SetValue(CameraRoot, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IValue<int> GetCameraSpeed(this IPlayerContext obj) => obj.GetValue<IValue<int>>(CameraSpeed);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetCameraSpeed(this IPlayerContext obj, out IValue<int> value) => obj.TryGetValue(CameraSpeed, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddCameraSpeed(this IPlayerContext obj, IValue<int> value) => obj.AddValue(CameraSpeed, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasCameraSpeed(this IPlayerContext obj) => obj.HasValue(CameraSpeed);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelCameraSpeed(this IPlayerContext obj) => obj.DelValue(CameraSpeed);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetCameraSpeed(this IPlayerContext obj, IValue<int> value) => obj.SetValue(CameraSpeed, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Joystick GetMoveJoystick(this IPlayerContext obj) => obj.GetValue<Joystick>(MoveJoystick);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetMoveJoystick(this IPlayerContext obj, out Joystick value) => obj.TryGetValue(MoveJoystick, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddMoveJoystick(this IPlayerContext obj, Joystick value) => obj.AddValue(MoveJoystick, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasMoveJoystick(this IPlayerContext obj) => obj.HasValue(MoveJoystick);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelMoveJoystick(this IPlayerContext obj) => obj.DelValue(MoveJoystick);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetMoveJoystick(this IPlayerContext obj, Joystick value) => obj.SetValue(MoveJoystick, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Joystick GetRotateJoystick(this IPlayerContext obj) => obj.GetValue<Joystick>(RotateJoystick);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetRotateJoystick(this IPlayerContext obj, out Joystick value) => obj.TryGetValue(RotateJoystick, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddRotateJoystick(this IPlayerContext obj, Joystick value) => obj.AddValue(RotateJoystick, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasRotateJoystick(this IPlayerContext obj) => obj.HasValue(RotateJoystick);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelRotateJoystick(this IPlayerContext obj) => obj.DelValue(RotateJoystick);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetRotateJoystick(this IPlayerContext obj, Joystick value) => obj.SetValue(RotateJoystick, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IValue<int> GetMaxMana(this IPlayerContext obj) => obj.GetValue<IValue<int>>(MaxMana);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetMaxMana(this IPlayerContext obj, out IValue<int> value) => obj.TryGetValue(MaxMana, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddMaxMana(this IPlayerContext obj, IValue<int> value) => obj.AddValue(MaxMana, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasMaxMana(this IPlayerContext obj) => obj.HasValue(MaxMana);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelMaxMana(this IPlayerContext obj) => obj.DelValue(MaxMana);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetMaxMana(this IPlayerContext obj, IValue<int> value) => obj.SetValue(MaxMana, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IReactiveVariable<int> GetCurrentMana(this IPlayerContext obj) => obj.GetValue<IReactiveVariable<int>>(CurrentMana);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetCurrentMana(this IPlayerContext obj, out IReactiveVariable<int> value) => obj.TryGetValue(CurrentMana, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddCurrentMana(this IPlayerContext obj, IReactiveVariable<int> value) => obj.AddValue(CurrentMana, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasCurrentMana(this IPlayerContext obj) => obj.HasValue(CurrentMana);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelCurrentMana(this IPlayerContext obj) => obj.DelValue(CurrentMana);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetCurrentMana(this IPlayerContext obj, IReactiveVariable<int> value) => obj.SetValue(CurrentMana, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IReactiveDictionary<string, Ability> GetAbilities(this IPlayerContext obj) => obj.GetValue<IReactiveDictionary<string, Ability>>(Abilities);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetAbilities(this IPlayerContext obj, out IReactiveDictionary<string, Ability> value) => obj.TryGetValue(Abilities, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddAbilities(this IPlayerContext obj, IReactiveDictionary<string, Ability> value) => obj.AddValue(Abilities, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasAbilities(this IPlayerContext obj) => obj.HasValue(Abilities);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelAbilities(this IPlayerContext obj) => obj.DelValue(Abilities);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetAbilities(this IPlayerContext obj, IReactiveDictionary<string, Ability> value) => obj.SetValue(Abilities, value);
    }
}
