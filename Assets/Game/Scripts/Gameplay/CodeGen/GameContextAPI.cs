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
		public const int WeaponCatalog = -1557559158; // WeaponCatalog
		public const int PlayerContext = -122845622; // PlayerContext
		public const int GameFactory = 907141881; // GameFactory


		///Value Extensions

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
		public static PlayerContext GetPlayerContext(this IContext obj) => obj.GetValue<PlayerContext>(PlayerContext);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetPlayerContext(this IContext obj, out PlayerContext value) => obj.TryGetValue(PlayerContext, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddPlayerContext(this IContext obj, PlayerContext value) => obj.AddValue(PlayerContext, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasPlayerContext(this IContext obj) => obj.HasValue(PlayerContext);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelPlayerContext(this IContext obj) => obj.DelValue(PlayerContext);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetPlayerContext(this IContext obj, PlayerContext value) => obj.SetValue(PlayerContext, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static GameFactory GetGameFactory(this IContext obj) => obj.GetValue<GameFactory>(GameFactory);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetGameFactory(this IContext obj, out GameFactory value) => obj.TryGetValue(GameFactory, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddGameFactory(this IContext obj, GameFactory value) => obj.AddValue(GameFactory, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasGameFactory(this IContext obj) => obj.HasValue(GameFactory);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelGameFactory(this IContext obj) => obj.DelValue(GameFactory);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetGameFactory(this IContext obj, GameFactory value) => obj.SetValue(GameFactory, value);
    }
}
