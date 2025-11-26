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
		public const int HitEffectPool = 667905863; // IEntityPool
		public const int ShellPool = 2051358104; // IEntityPool
		public const int AudioPool = -1361603774; // IEntityPool
		public const int WeaponCatalog = -1557559158; // WeaponCatalog
		public const int PlayerCharacter = -1319565175; // IReactiveVariable<IEntity>
		public const int PlayerCamera = 298559249; // IEntity
		public const int GamePools = -361051865; // ReactiveDictionary<string, SceneEntityPool>
		public const int GameFactory = 907141881; // GameFactory


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
		public static IEntityPool GetHitEffectPool(this IContext obj) => obj.GetValue<IEntityPool>(HitEffectPool);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetHitEffectPool(this IContext obj, out IEntityPool value) => obj.TryGetValue(HitEffectPool, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddHitEffectPool(this IContext obj, IEntityPool value) => obj.AddValue(HitEffectPool, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasHitEffectPool(this IContext obj) => obj.HasValue(HitEffectPool);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelHitEffectPool(this IContext obj) => obj.DelValue(HitEffectPool);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetHitEffectPool(this IContext obj, IEntityPool value) => obj.SetValue(HitEffectPool, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEntityPool GetShellPool(this IContext obj) => obj.GetValue<IEntityPool>(ShellPool);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetShellPool(this IContext obj, out IEntityPool value) => obj.TryGetValue(ShellPool, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddShellPool(this IContext obj, IEntityPool value) => obj.AddValue(ShellPool, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasShellPool(this IContext obj) => obj.HasValue(ShellPool);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelShellPool(this IContext obj) => obj.DelValue(ShellPool);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetShellPool(this IContext obj, IEntityPool value) => obj.SetValue(ShellPool, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEntityPool GetAudioPool(this IContext obj) => obj.GetValue<IEntityPool>(AudioPool);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetAudioPool(this IContext obj, out IEntityPool value) => obj.TryGetValue(AudioPool, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddAudioPool(this IContext obj, IEntityPool value) => obj.AddValue(AudioPool, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasAudioPool(this IContext obj) => obj.HasValue(AudioPool);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelAudioPool(this IContext obj) => obj.DelValue(AudioPool);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetAudioPool(this IContext obj, IEntityPool value) => obj.SetValue(AudioPool, value);

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
		public static IReactiveVariable<IEntity> GetPlayerCharacter(this IContext obj) => obj.GetValue<IReactiveVariable<IEntity>>(PlayerCharacter);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetPlayerCharacter(this IContext obj, out IReactiveVariable<IEntity> value) => obj.TryGetValue(PlayerCharacter, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddPlayerCharacter(this IContext obj, IReactiveVariable<IEntity> value) => obj.AddValue(PlayerCharacter, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasPlayerCharacter(this IContext obj) => obj.HasValue(PlayerCharacter);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelPlayerCharacter(this IContext obj) => obj.DelValue(PlayerCharacter);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetPlayerCharacter(this IContext obj, IReactiveVariable<IEntity> value) => obj.SetValue(PlayerCharacter, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEntity GetPlayerCamera(this IContext obj) => obj.GetValue<IEntity>(PlayerCamera);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetPlayerCamera(this IContext obj, out IEntity value) => obj.TryGetValue(PlayerCamera, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddPlayerCamera(this IContext obj, IEntity value) => obj.AddValue(PlayerCamera, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasPlayerCamera(this IContext obj) => obj.HasValue(PlayerCamera);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelPlayerCamera(this IContext obj) => obj.DelValue(PlayerCamera);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetPlayerCamera(this IContext obj, IEntity value) => obj.SetValue(PlayerCamera, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ReactiveDictionary<string, SceneEntityPool> GetGamePools(this IContext obj) => obj.GetValue<ReactiveDictionary<string, SceneEntityPool>>(GamePools);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetGamePools(this IContext obj, out ReactiveDictionary<string, SceneEntityPool> value) => obj.TryGetValue(GamePools, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AddGamePools(this IContext obj, ReactiveDictionary<string, SceneEntityPool> value) => obj.AddValue(GamePools, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasGamePools(this IContext obj) => obj.HasValue(GamePools);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool DelGamePools(this IContext obj) => obj.DelValue(GamePools);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetGamePools(this IContext obj, ReactiveDictionary<string, SceneEntityPool> value) => obj.SetValue(GamePools, value);

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
