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
using System.Collections.Generic;

namespace Game
{
	public static class GameContextAPI
	{


		///Values
		public const int BulletPool = 1915726678; // IEntityPool
		public const int WeaponCatalog = -1557559158; // WeaponCatalog
		public const int Players = -369919430; // IDictionary<PlayerID, IPlayerContext>


		///Value Extensions

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

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static WeaponCatalog GetWeaponCatalog(this IContext obj) => obj.GetValue<WeaponCatalog>(WeaponCatalog);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetWeaponCatalog(this IContext obj, out WeaponCatalog value) => obj.TryGetValue(WeaponCatalog, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddWeaponCatalog(this IContext obj, WeaponCatalog value) => obj.AddValue(WeaponCatalog, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasWeaponCatalog(this IContext obj) => obj.HasValue(WeaponCatalog);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelWeaponCatalog(this IContext obj) => obj.DelValue(WeaponCatalog);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetWeaponCatalog(this IContext obj, WeaponCatalog value) => obj.SetValue(WeaponCatalog, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IDictionary<PlayerID, IPlayerContext> GetPlayers(this IContext obj) => obj.GetValue<IDictionary<PlayerID, IPlayerContext>>(Players);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetPlayers(this IContext obj, out IDictionary<PlayerID, IPlayerContext> value) => obj.TryGetValue(Players, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddPlayers(this IContext obj, IDictionary<PlayerID, IPlayerContext> value) => obj.AddValue(Players, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasPlayers(this IContext obj) => obj.HasValue(Players);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelPlayers(this IContext obj) => obj.DelValue(Players);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetPlayers(this IContext obj, IDictionary<PlayerID, IPlayerContext> value) => obj.SetValue(Players, value);
    }
}
