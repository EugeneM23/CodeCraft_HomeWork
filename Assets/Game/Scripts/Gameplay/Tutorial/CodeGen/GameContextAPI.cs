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
	public static class GameContextAPI
	{


		///Values
		public const int Character = 294335127; // IEntity
		public const int CharacterTransform = -557574193; // Transform
		public const int CameraOffset = -1286660539; // IValue<Vector3>
		public const int Camera = 1018227507; // Camera
		public const int CameraSpeed = -1615506830; // IValue<int>
		public const int MoveJoystick = -1686028204; // Joystick
		public const int BulletPool = 1915726678; // IEntityPool


		///Value Extensions

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEntity GetCharacter(this IContext obj) => obj.GetValue<IEntity>(Character);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetCharacter(this IContext obj, out IEntity value) => obj.TryGetValue(Character, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddCharacter(this IContext obj, IEntity value) => obj.AddValue(Character, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasCharacter(this IContext obj) => obj.HasValue(Character);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelCharacter(this IContext obj) => obj.DelValue(Character);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetCharacter(this IContext obj, IEntity value) => obj.SetValue(Character, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Transform GetCharacterTransform(this IContext obj) => obj.GetValue<Transform>(CharacterTransform);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetCharacterTransform(this IContext obj, out Transform value) => obj.TryGetValue(CharacterTransform, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddCharacterTransform(this IContext obj, Transform value) => obj.AddValue(CharacterTransform, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasCharacterTransform(this IContext obj) => obj.HasValue(CharacterTransform);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelCharacterTransform(this IContext obj) => obj.DelValue(CharacterTransform);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetCharacterTransform(this IContext obj, Transform value) => obj.SetValue(CharacterTransform, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IValue<Vector3> GetCameraOffset(this IContext obj) => obj.GetValue<IValue<Vector3>>(CameraOffset);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetCameraOffset(this IContext obj, out IValue<Vector3> value) => obj.TryGetValue(CameraOffset, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddCameraOffset(this IContext obj, IValue<Vector3> value) => obj.AddValue(CameraOffset, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasCameraOffset(this IContext obj) => obj.HasValue(CameraOffset);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelCameraOffset(this IContext obj) => obj.DelValue(CameraOffset);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetCameraOffset(this IContext obj, IValue<Vector3> value) => obj.SetValue(CameraOffset, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Camera GetCamera(this IContext obj) => obj.GetValue<Camera>(Camera);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetCamera(this IContext obj, out Camera value) => obj.TryGetValue(Camera, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddCamera(this IContext obj, Camera value) => obj.AddValue(Camera, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasCamera(this IContext obj) => obj.HasValue(Camera);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelCamera(this IContext obj) => obj.DelValue(Camera);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetCamera(this IContext obj, Camera value) => obj.SetValue(Camera, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IValue<int> GetCameraSpeed(this IContext obj) => obj.GetValue<IValue<int>>(CameraSpeed);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetCameraSpeed(this IContext obj, out IValue<int> value) => obj.TryGetValue(CameraSpeed, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddCameraSpeed(this IContext obj, IValue<int> value) => obj.AddValue(CameraSpeed, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasCameraSpeed(this IContext obj) => obj.HasValue(CameraSpeed);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelCameraSpeed(this IContext obj) => obj.DelValue(CameraSpeed);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetCameraSpeed(this IContext obj, IValue<int> value) => obj.SetValue(CameraSpeed, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Joystick GetMoveJoystick(this IContext obj) => obj.GetValue<Joystick>(MoveJoystick);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetMoveJoystick(this IContext obj, out Joystick value) => obj.TryGetValue(MoveJoystick, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddMoveJoystick(this IContext obj, Joystick value) => obj.AddValue(MoveJoystick, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasMoveJoystick(this IContext obj) => obj.HasValue(MoveJoystick);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelMoveJoystick(this IContext obj) => obj.DelValue(MoveJoystick);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetMoveJoystick(this IContext obj, Joystick value) => obj.SetValue(MoveJoystick, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEntityPool GetBulletPool(this IContext obj) => obj.GetValue<IEntityPool>(BulletPool);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetBulletPool(this IContext obj, out IEntityPool value) => obj.TryGetValue(BulletPool, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddBulletPool(this IContext obj, IEntityPool value) => obj.AddValue(BulletPool, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasBulletPool(this IContext obj) => obj.HasValue(BulletPool);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelBulletPool(this IContext obj) => obj.DelValue(BulletPool);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetBulletPool(this IContext obj, IEntityPool value) => obj.SetValue(BulletPool, value);
    }
}
