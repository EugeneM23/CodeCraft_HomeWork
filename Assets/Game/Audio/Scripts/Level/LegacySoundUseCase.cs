using Atomic.Entities;
using UnityEngine;

namespace Game.AudioEngine.Adapters
{
    /// <summary>
    /// TEMPORARY. The old prefab sounds still on Tank_01 and Regax (WeaponFireSoundInstaller,
    /// CharacterAudioInstaller). While they are there, the new sounds of the same kind are not attached, or
    /// everything would sound twice. Delete this class and its calls once the old installers are removed
    /// (spec 2026-11-17-game-audio, 3.8).
    /// </summary>
    public static class LegacySoundUseCase
    {
        /// <summary>The weapon plays its own shot sound.</summary>
        public static bool HasLegacyShotSound(IEntity weapon) =>
            weapon.HasBehaviour<WeaponFireSoundBehaviour>() || HasComponent<WeaponFireSoundInstaller>(weapon);

        /// <summary>
        /// The character plays this kind of sound itself: <typeparamref name="T"/> is DamageSoundBehaviour,
        /// DeathSoundBehaviour, AttackSoundBehaviour or MoveStepSoundBehaviour.
        /// </summary>
        public static bool HasLegacyCharacterSound<T>(IEntity character) where T : IEntityBehaviour =>
            character.HasBehaviour<T>();

        private static bool HasComponent<T>(IEntity entity) where T : Component =>
            entity is Component component && component != null && component.TryGetComponent(out T _);
    }
}
