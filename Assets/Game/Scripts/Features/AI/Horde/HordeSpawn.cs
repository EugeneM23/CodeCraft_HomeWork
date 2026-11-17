using Atomic.Entities;
using Game.Horde;
using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>Builds a horde entity from code (benchmarks, tests, levels without one in the scene).</summary>
    public static class HordeSpawn
    {
        /// <summary>
        /// A new <c>[Horde]</c> GameObject in the active scene with a <see cref="MonoEntity"/> and a
        /// configured <see cref="HordeInstaller"/>, installed. The behaviour initialises on the
        /// entity's Start (the next frame) and then keeps <see cref="HordeOptions.population"/> alive.
        /// </summary>
        public static HordeInstaller CreateInScene(HordeArtConfig art, HordeOptions options)
        {
            // MonoEntity.Create constructs the entity's storage (AddComponent alone does not).
            MonoEntity entity = MonoEntity.Create(new MonoEntity.CreateArgs
            {
                name = "[Horde]",
                installOnAwake = false,
                uninstallOnDestroy = true,
                disposeValues = true,
                useUnityLifecycle = true
            });
            var installer = entity.gameObject.AddComponent<HordeInstaller>();
            installer.Configure(art, options);
            installer.Install(entity);
            return installer;
        }
    }
}
