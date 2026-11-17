using Atomic.Entities;
using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// The level's AI entity (<see cref="LevelAIInstaller"/>): the one the horde and the elites share. A level that has
    /// none in its scene gets one from <see cref="Ensure"/> — a <c>[LevelAI]</c> object made in the active scene, so a
    /// scene need not be edited to have AI. Looked up at load (Init), never in a tick.
    /// </summary>
    public static class LevelAI
    {
        private static MonoEntity s_current;

        /// <summary>The level's AI entity, or null when there is none yet.</summary>
        public static IEntity Current => s_current != null ? s_current : null;

        /// <summary>
        /// The level's AI entity: the registered one, one in the scene, or a new <c>[LevelAI]</c> made with
        /// <paramref name="navigation"/> (null — the defaults). Its grid is read on first use.
        /// </summary>
        public static IEntity Ensure(LevelNavOptions navigation = null)
        {
            if (s_current != null)
                return s_current;

            LevelAIInstaller existing = Object.FindFirstObjectByType<LevelAIInstaller>();
            if (existing != null && existing.TryGetComponent(out MonoEntity found))
            {
                if (!found.HasAILevelTag())
                    existing.Install(found);
                s_current = found;
                return found;
            }

            // MonoEntity.Create constructs the entity's storage (AddComponent alone does not).
            MonoEntity entity = MonoEntity.Create(new MonoEntity.CreateArgs
            {
                name = "[LevelAI]",
                installOnAwake = false,
                uninstallOnDestroy = true,
                disposeValues = true,
                useUnityLifecycle = true
            });
            var installer = entity.gameObject.AddComponent<LevelAIInstaller>();
            installer.Configure(navigation);
            installer.Install(entity);
            return entity;
        }

        internal static void Register(MonoEntity entity) => s_current = entity;

        internal static void Unregister(MonoEntity entity)
        {
            if (s_current == entity)
                s_current = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_current = null;
    }
}
