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
		public const int Character = 294335127; // IEntity
		public const int CharacterTransform = -557574193; // Transform
		public const int CameraOffset = -1286660539; // IValue<Vector3>
		public const int CameraRoot = 977661012; // Transform
		public const int Camera = 1018227507; // Camera
		public const int CameraSpeed = -1615506830; // IValue<int>
		public const int MoveJoystick = -1686028204; // Joystick
		public const int RotateJoystick = 17434633; // Joystick


		///Value Extensions

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEntity GetCharacter(this IPlayerContext obj) => obj.GetValue<IEntity>(Character);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetCharacter(this IPlayerContext obj, out IEntity value) => obj.TryGetValue(Character, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddCharacter(this IPlayerContext obj, IEntity value) => obj.AddValue(Character, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasCharacter(this IPlayerContext obj) => obj.HasValue(Character);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelCharacter(this IPlayerContext obj) => obj.DelValue(Character);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetCharacter(this IPlayerContext obj, IEntity value) => obj.SetValue(Character, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Transform GetCharacterTransform(this IPlayerContext obj) => obj.GetValue<Transform>(CharacterTransform);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetCharacterTransform(this IPlayerContext obj, out Transform value) => obj.TryGetValue(CharacterTransform, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddCharacterTransform(this IPlayerContext obj, Transform value) => obj.AddValue(CharacterTransform, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasCharacterTransform(this IPlayerContext obj) => obj.HasValue(CharacterTransform);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelCharacterTransform(this IPlayerContext obj) => obj.DelValue(CharacterTransform);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetCharacterTransform(this IPlayerContext obj, Transform value) => obj.SetValue(CharacterTransform, value);

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
		public static Camera GetCamera(this IPlayerContext obj) => obj.GetValue<Camera>(Camera);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetCamera(this IPlayerContext obj, out Camera value) => obj.TryGetValue(Camera, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddCamera(this IPlayerContext obj, Camera value) => obj.AddValue(Camera, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasCamera(this IPlayerContext obj) => obj.HasValue(Camera);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelCamera(this IPlayerContext obj) => obj.DelValue(Camera);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetCamera(this IPlayerContext obj, Camera value) => obj.SetValue(Camera, value);

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
    }
}
