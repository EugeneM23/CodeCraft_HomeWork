/**
* Code generation. Don't modify! 
**/

using Atomic.Entities;
using System.Runtime.CompilerServices;
using UnityEngine;
using Atomic.Entities;

namespace Game
{
	public static class WeaponEntityAPI
	{


		///Values
		public const int BulletPrefab = -918778767; // SceneEntity
		public const int FirePoint = 397255013; // Transform


		///Value Extensions

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static SceneEntity GetBulletPrefab(this IWeaponEntity obj) => obj.GetValue<SceneEntity>(BulletPrefab);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetBulletPrefab(this IWeaponEntity obj, out SceneEntity value) => obj.TryGetValue(BulletPrefab, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddBulletPrefab(this IWeaponEntity obj, SceneEntity value) => obj.AddValue(BulletPrefab, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasBulletPrefab(this IWeaponEntity obj) => obj.HasValue(BulletPrefab);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelBulletPrefab(this IWeaponEntity obj) => obj.DelValue(BulletPrefab);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetBulletPrefab(this IWeaponEntity obj, SceneEntity value) => obj.SetValue(BulletPrefab, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Transform GetFirePoint(this IWeaponEntity obj) => obj.GetValue<Transform>(FirePoint);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetFirePoint(this IWeaponEntity obj, out Transform value) => obj.TryGetValue(FirePoint, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddFirePoint(this IWeaponEntity obj, Transform value) => obj.AddValue(FirePoint, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasFirePoint(this IWeaponEntity obj) => obj.HasValue(FirePoint);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelFirePoint(this IWeaponEntity obj) => obj.DelValue(FirePoint);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetFirePoint(this IWeaponEntity obj, Transform value) => obj.SetValue(FirePoint, value);
    }
}
